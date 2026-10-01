using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Features.Email;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MemberAuth;

/// <summary>
/// 會員（前台帳號）的註冊、驗證、登入、密碼、LINE 綁定、個人資料與刪除帳號（主站規劃書 §3.14「前台功能」、App 規劃書 §4）。
/// 設計重點（每一條都有對應測試，見 MemberAuthTests）：
/// ① <b>不洩漏帳號是否存在</b>：登入失敗一律同一句訊息、同樣的耗時（查無此人也跑一次 Argon2id）；忘記密碼／重寄驗證信一律回成功。
///    唯一例外是註冊（Email 已註冊 → 409，使用者需要知道該去登入），靠依 IP 限流壓住枚舉。
/// ② <b>登入失敗次數限制</b>：連續 5 次失敗鎖 15 分鐘（欄位在 <c>members</c>），鎖定期間連密碼都不比對。
/// ③ <b>Email 驗證前不能用密碼登入</b>（密碼正確才告知「尚未驗證」，不給枚舉用）；重設密碼的信連結代表控制了信箱，所以順便完成驗證。
/// ④ <b>改密碼／重設密碼／登出全部裝置都撤銷全部更新權杖</b>（App 規劃書 §4.3）。
/// ⑤ 回應與日誌<b>不含密碼雜湊、LINE 識別碼、權杖原值</b>。
/// </summary>
public sealed partial class MemberAuthService(
    ClubDbContext db,
    MemberSessionService sessions,
    MemberMembershipService memberships,
    MemberNumberGenerator numbers,
    MemberSecureTokens secureTokens,
    IEmailSender email,
    ILineLoginClient line,
    IConfiguration configuration,
    ILogger<MemberAuthService> logger)
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private static readonly Lazy<string> DummyHash = new(() => PasswordHasher.Hash("tcrfc-dummy-password-for-timing-equalisation"));

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailShape();

    // ═══════════════════════════ 輸入整理 ═══════════════════════════

    public static string NormalizeEmail(string? email)
    {
        var value = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length is 0 or > 255 || !EmailShape().IsMatch(value) || !MailAddress.TryCreate(value, out var parsed) || parsed.Address != value)
        {
            throw new MemberValidationException("請輸入正確的 Email。", "invalid_email");
        }

        return value;
    }

    private static string RequireName(string? name)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length is 0 or > 64)
        {
            throw new MemberValidationException("請填寫姓名（64 字以內）。", "invalid_name");
        }

        return value;
    }

    private static string? OptionalPhone(string? phone)
    {
        var value = phone?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.Length > 32 || !value.All(c => char.IsDigit(c) || c is '+' or '-' or ' ' or '(' or ')'))
        {
            throw new MemberValidationException("電話格式不正確，只能包含數字、+、-、空白與括號（32 字以內）。", "invalid_phone");
        }

        return value;
    }

    private static DateOnly? OptionalBirthOn(DateOnly? birthOn)
    {
        if (birthOn is null)
        {
            return null;
        }

        var today = TaiwanClock.Today;
        if (birthOn > today || birthOn < today.AddYears(-120))
        {
            throw new MemberValidationException("生日不正確。", "invalid_birth_on");
        }

        return birthOn;
    }

    public static string DbLocale(string? lang) => string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh-Hant";

    public static string? ApiLocale(string? dbLocale) => dbLocale switch { "en" => "en", "zh-Hant" => "zh", _ => null };

    private static string LangOrDefault(string? lang) => string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";

    public static bool HasPassword(Member member) => !member.PasswordHash.StartsWith('!');

    public static MemberSummaryDto ToSummary(Member member) => new()
    {
        MemberNo = member.MemberNo, Name = member.Name, EmailVerified = member.EmailVerifiedAt is not null,
        HasPassword = HasPassword(member), LineBound = member.LineUserIdHash is not null,
    };

    // ═══════════════════════════ 註冊與驗證 ═══════════════════════════

    public async Task<MemberRegisteredDto> RegisterAsync(MemberRegisterRequest request, ClubScope club, CancellationToken cancellationToken)
    {
        var emailAddress = NormalizeEmail(request.Email);
        var name = RequireName(request.Name);
        var phone = OptionalPhone(request.Phone);
        var birthOn = OptionalBirthOn(request.BirthOn);
        if (MemberPasswordPolicy.Validate(request.Password, emailAddress) is { } passwordError)
        {
            throw new MemberValidationException(passwordError, "weak_password");
        }

        if (await db.Members.AsNoTracking().AnyAsync(m => m.Email == emailAddress, cancellationToken))
        {
            throw new MemberConflictException("Email 已註冊", "這個 Email 已經註冊過了，請直接登入；忘記密碼可以用「忘記密碼」重設。", "email_taken");
        }

        var member = new Member
        {
            Id = Guid.NewGuid(), Name = name, Email = emailAddress, PasswordHash = PasswordHasher.Hash(request.Password),
            Phone = phone, BirthOn = birthOn, SignupSource = "web", Status = "active", Locale = DbLocale(request.Lang),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        await SaveNewMemberAsync(member, club, cancellationToken);

        var emailSent = await SendVerificationAsync(member, club, LangOrDefault(request.Lang), cancellationToken);
        return new MemberRegisteredDto { MemberNo = member.MemberNo, EmailVerificationRequired = true, EmailSent = emailSent };
    }

    /// <summary>配發會員編號並寫入；編號撞號（並行註冊）重試，Email 撞號回 409。</summary>
    private async Task SaveNewMemberAsync(Member member, ClubScope club, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            member.MemberNo = await numbers.NextAsync(club.ClubId, cancellationToken);
            db.Members.Add(member);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException)
            {
                db.Entry(member).State = EntityState.Detached;
                if (await db.Members.AsNoTracking().AnyAsync(m => m.Email == member.Email, cancellationToken))
                {
                    throw new MemberConflictException("Email 已註冊", "這個 Email 已經註冊過了，請直接登入。", "email_taken");
                }

                if (attempt >= 5)
                {
                    throw;
                }
            }
        }
    }

    private async Task<bool> SendVerificationAsync(Member member, ClubScope club, string lang, CancellationToken cancellationToken)
    {
        var token = secureTokens.Protect(MemberSecureTokens.PurposeEmailVerify,
            new MemberSecureTokens.EmailVerifyPayload(member.Id, member.Email, club.ClubCode), MemberSecureTokens.EmailVerifyLifetime);
        var (clubName, domain) = await ClubDisplayAsync(club.ClubId, lang, cancellationToken);
        var link = BuildLink(domain, lang, "verify-email", token);
        return await email.SendAsync(MemberEmailTemplates.Verification(member.Email, member.Name, clubName, link, lang), cancellationToken);
    }

    public async Task<string> VerifyEmailAsync(string? token, CancellationToken cancellationToken)
    {
        var payload = secureTokens.TryUnprotect<MemberSecureTokens.EmailVerifyPayload>(MemberSecureTokens.PurposeEmailVerify, token)
                      ?? throw new MemberValidationException("驗證連結無效或已過期，請重新寄送驗證信。", "token_invalid");
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == payload.MemberId, cancellationToken);
        if (member is null || member.Status != "active" || !string.Equals(member.Email, payload.Email, StringComparison.Ordinal))
        {
            throw new MemberValidationException("驗證連結無效或已過期，請重新寄送驗證信。", "token_invalid");
        }

        if (member.EmailVerifiedAt is null)
        {
            member.EmailVerifiedAt = DateTime.UtcNow;
            member.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        // 免費註冊＋Email 驗證＝一般會員，有電子會員卡（規劃書 §3.14）。俱樂部取自權杖（註冊時的站台）。
        var clubId = await db.Clubs.AsNoTracking().Where(c => c.Code == payload.Club).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);
        if (clubId is Guid id)
        {
            await memberships.EnsureRegisteredAsync(member.Id, member.Name, id, cancellationToken);
        }

        return payload.Club;
    }

    /// <summary>重寄驗證信。<b>一律回成功</b>（不洩漏 Email 是否註冊過）。</summary>
    public async Task ResendVerificationAsync(MemberResendVerificationRequest request, ClubScope club, CancellationToken cancellationToken)
    {
        var emailAddress = NormalizeEmail(request.Email);
        var member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == emailAddress, cancellationToken);
        if (member is { Status: "active", EmailVerifiedAt: null })
        {
            await SendVerificationAsync(member, club, LangOrDefault(request.Lang), cancellationToken);
        }
    }

    // ═══════════════════════════ 登入 ═══════════════════════════

    public async Task<(MemberSessionTokens Tokens, MemberSummaryDto Summary)> LoginAsync(MemberLoginRequest request, CancellationToken cancellationToken)
    {
        // 輸入形狀不對（過長、不像 Email）直接當成帳密錯誤：不要為了超長字串跑 Argon2id（DoS），也不洩漏任何資訊。
        string emailAddress;
        try
        {
            emailAddress = NormalizeEmail(request.Email);
        }
        catch (MemberValidationException)
        {
            throw InvalidCredentials();
        }

        var password = request.Password ?? string.Empty;
        if (password.Length is 0 or > MemberPasswordPolicy.MaxLength)
        {
            throw InvalidCredentials();
        }

        var member = await db.Members.FirstOrDefaultAsync(m => m.Email == emailAddress, cancellationToken);
        var now = DateTime.UtcNow;

        if (member is null || member.Status == "deleted")
        {
            PasswordHasher.Verify(password, DummyHash.Value); // 耗時與真實帳號一致，不讓人從回應時間分辨帳號存不存在
            throw InvalidCredentials();
        }

        if (member.LockedUntil is DateTime lockedUntil && lockedUntil > now)
        {
            throw new MemberAccountLockedException(lockedUntil);
        }

        bool passwordOk;
        if (HasPassword(member))
        {
            passwordOk = PasswordHasher.Verify(password, member.PasswordHash);
        }
        else
        {
            PasswordHasher.Verify(password, DummyHash.Value); // 尚未設定密碼的帳號（LINE 註冊）：照樣花一次運算時間，一律失敗
            passwordOk = false;
        }

        if (!passwordOk)
        {
            member.FailedAttemptCount += 1;
            if (member.FailedAttemptCount >= MaxFailedAttempts)
            {
                member.LockedUntil = now.Add(LockDuration);
                member.FailedAttemptCount = 0;
            }

            member.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            throw InvalidCredentials();
        }

        // 密碼正確之後才揭露帳號狀態（不給枚舉用）。
        if (member.Status == "suspended")
        {
            throw new MemberForbiddenException("這個帳號已停用，請聯繫客服。", "account_suspended");
        }

        if (member.EmailVerifiedAt is null)
        {
            throw new MemberForbiddenException("請先到信箱點選驗證連結，完成 Email 驗證後再登入。", "email_not_verified");
        }

        member.FailedAttemptCount = 0;
        member.LockedUntil = null;
        member.LastLoginAt = now;
        member.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        var tokens = await sessions.IssueAsync(member.Id, request.RememberMe, cancellationToken);
        return (tokens, ToSummary(member));
    }

    private static MemberUnauthenticatedException InvalidCredentials()
        => new("Email 或密碼不正確。", "invalid_credentials");

    public async Task<(MemberSessionTokens Tokens, MemberSummaryDto Summary)?> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        var rotated = await sessions.RotateAsync(rawRefreshToken, cancellationToken);
        if (rotated is null)
        {
            return null;
        }

        var member = await db.Members.AsNoTracking().FirstAsync(m => m.Id == rotated.Value.MemberId, cancellationToken);
        return (rotated.Value.Tokens, ToSummary(member));
    }

    // ═══════════════════════════ 忘記密碼、重設、變更 ═══════════════════════════

    /// <summary>忘記密碼。<b>一律回成功</b>，帳號存在才寄信。</summary>
    public async Task ForgotPasswordAsync(MemberForgotPasswordRequest request, ClubScope club, CancellationToken cancellationToken)
    {
        var emailAddress = NormalizeEmail(request.Email);
        var member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == emailAddress, cancellationToken);
        if (member is not { Status: "active" })
        {
            return;
        }

        var lang = LangOrDefault(request.Lang);
        var token = secureTokens.Protect(MemberSecureTokens.PurposePasswordReset,
            new MemberSecureTokens.PasswordResetPayload(member.Id, MemberSecureTokens.PasswordFingerprint(member.PasswordHash)),
            MemberSecureTokens.PasswordResetLifetime);
        var (clubName, domain) = await ClubDisplayAsync(club.ClubId, lang, cancellationToken);
        await email.SendAsync(MemberEmailTemplates.PasswordReset(member.Email, member.Name, clubName, BuildLink(domain, lang, "reset-password", token), lang), cancellationToken);
    }

    public async Task ResetPasswordAsync(MemberResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var payload = secureTokens.TryUnprotect<MemberSecureTokens.PasswordResetPayload>(MemberSecureTokens.PurposePasswordReset, request.Token)
                      ?? throw new MemberValidationException("重設連結無效或已過期，請重新申請。", "token_invalid");
        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == payload.MemberId, cancellationToken);
        if (member is null || member.Status != "active"
            || !string.Equals(MemberSecureTokens.PasswordFingerprint(member.PasswordHash), payload.Fingerprint, StringComparison.Ordinal))
        {
            throw new MemberValidationException("重設連結無效或已過期，請重新申請。", "token_invalid");
        }

        if (MemberPasswordPolicy.Validate(request.NewPassword, member.Email) is { } error)
        {
            throw new MemberValidationException(error, "weak_password");
        }

        var now = DateTime.UtcNow;
        member.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        member.EmailVerifiedAt ??= now; // 能收到重設信就代表控制這個信箱
        member.FailedAttemptCount = 0;
        member.LockedUntil = null;
        member.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await sessions.RevokeAllAsync(member.Id, cancellationToken);
    }

    /// <summary>變更（或第一次設定）密碼，成功後撤銷全部裝置的登入，並替目前這個裝置發一組新的。</summary>
    public async Task<(MemberSessionTokens Tokens, MemberSummaryDto Summary)> ChangePasswordAsync(
        Guid memberId, MemberChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var member = await db.Members.FirstAsync(m => m.Id == memberId, cancellationToken);
        if (HasPassword(member))
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > MemberPasswordPolicy.MaxLength
                || !PasswordHasher.Verify(request.CurrentPassword, member.PasswordHash))
            {
                throw new MemberUnauthenticatedException("目前的密碼不正確。", "invalid_credentials");
            }
        }

        if (MemberPasswordPolicy.Validate(request.NewPassword, member.Email) is { } error)
        {
            throw new MemberValidationException(error, "weak_password");
        }

        member.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await sessions.RevokeAllAsync(member.Id, cancellationToken);
        var tokens = await sessions.IssueAsync(member.Id, persistent: false, cancellationToken);
        return (tokens, ToSummary(member));
    }

    // ═══════════════════════════ 個人資料與刪除 ═══════════════════════════

    public async Task<MemberProfileDto> GetProfileAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking().FirstAsync(m => m.Id == memberId, cancellationToken);
        return ToProfile(member);
    }

    public async Task<MemberProfileDto> UpdateProfileAsync(Guid memberId, MemberUpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var member = await db.Members.FirstAsync(m => m.Id == memberId, cancellationToken);
        var name = RequireName(request.Name);
        var phone = OptionalPhone(request.Phone);
        var birthOn = OptionalBirthOn(request.BirthOn);
        string? locale = null;
        if (!string.IsNullOrWhiteSpace(request.Locale))
        {
            if (request.Locale is not ("zh" or "en"))
            {
                throw new MemberValidationException("語系只能是 zh 或 en。", "invalid_locale");
            }

            locale = DbLocale(request.Locale);
        }

        // 姓名變更時，仍使用中的會員卡持卡人姓名一併更新？——不更新：卡上的持卡人姓名是發卡當下的值（家庭方案副卡姓名也不同於會員本人），
        // 以後台 K2 的卡片管理為準，避免會員改名牽動副卡。
        member.Name = name;
        member.Phone = phone;
        member.BirthOn = birthOn;
        member.Locale = locale;
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToProfile(member);
    }

    public static MemberProfileDto ToProfile(Member member) => new()
    {
        MemberNo = member.MemberNo, Name = member.Name, Email = member.Email, Phone = member.Phone, BirthOn = member.BirthOn,
        Locale = ApiLocale(member.Locale), EmailVerified = member.EmailVerifiedAt is not null, HasPassword = HasPassword(member),
        LineBound = member.LineUserIdHash is not null, SignupSource = member.SignupSource,
        SignupSourceLabel = MemberLabels.Of(MemberLabels.SignupSource, member.SignupSource) ?? member.SignupSource, CreatedAt = member.CreatedAt,
    };

    /// <summary>
    /// 刪除帳號（規劃書 §3.14「刪除帳號（個資刪除請求流程）」、App 規劃書 §4.6）：<b>欄位清除、不是刪列</b>。
    /// 保留會員編號與遮罩姓名；Email 換成不可寄送的佔位值（<c>deleted-編號@deleted.invalid</c>）、電話／生日／LINE／密碼清除；
    /// 會員卡全部撤銷（token 作廢）、全部更新權杖撤銷、App 裝置解除綁定、球衣收件個資清除、未完成的付款訂單取消。
    /// <b>保留</b>：會籍列與付款紀錄（稅法與會計法規，優先於刪除請求）、已鎖定的抽獎名單快照（只有會員編號與遮罩姓名，本來就不可回溯修改）。
    /// </summary>
    public async Task DeleteAccountAsync(Guid memberId, MemberDeleteAccountRequest request, CancellationToken cancellationToken)
    {
        var member = await db.Members.FirstAsync(m => m.Id == memberId, cancellationToken);
        if (HasPassword(member))
        {
            if (string.IsNullOrEmpty(request.Password) || request.Password.Length > MemberPasswordPolicy.MaxLength
                || !PasswordHasher.Verify(request.Password, member.PasswordHash))
            {
                throw new MemberUnauthenticatedException("密碼不正確，無法刪除帳號。", "invalid_credentials");
            }
        }
        else if (!string.Equals(request.Confirm, "DELETE", StringComparison.Ordinal))
        {
            throw new MemberValidationException("請輸入 DELETE 以確認刪除帳號。", "confirmation_required");
        }

        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        member.Status = "deleted";
        member.Name = PiiMasking.MaskName(member.Name) ?? "○";
        member.Email = $"deleted-{member.MemberNo.ToLowerInvariant()}@deleted.invalid";
        member.Phone = null;
        member.BirthOn = null;
        member.LineUserIdEncrypted = null;
        member.LineUserIdHash = null;
        member.PasswordHash = "!deleted-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        member.InternalNote = $"（{now:yyyy-MM-dd} 會員自行刪除帳號）";
        member.FailedAttemptCount = 0;
        member.LockedUntil = null;
        member.UpdatedAt = now;

        await db.MemberCards.Where(c => c.Membership.MemberId == memberId && c.Status == "active")
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, "revoked").SetProperty(c => c.RevokedAt, now).SetProperty(c => c.UpdatedAt, now), cancellationToken);
        await db.JerseyIssues.Where(j => j.MemberId == memberId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.RecipientName, member.Name).SetProperty(j => j.Phone, (string?)null).SetProperty(j => j.Address, (string?)null).SetProperty(j => j.UpdatedAt, now), cancellationToken);
        await db.MembershipOrders.Where(o => o.MemberId == memberId && (o.Status == "created" || o.Status == "pending_payment"))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "cancelled").SetProperty(o => o.UpdatedAt, now), cancellationToken);
        await db.AppDevices.Where(d => d.MemberId == memberId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.MemberId, (Guid?)null), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await sessions.RevokeAllAsync(memberId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("會員 {MemberNo} 已自行刪除帳號", member.MemberNo);
    }

    // ═══════════════════════════ LINE ═══════════════════════════

    public MemberLineAuthorizeDto LineAuthorize(MemberLineAuthorizeRequest request, ClubScope club, Guid? boundMemberId)
    {
        if (!line.IsConfigured)
        {
            throw new FeatureNotConfiguredException("LINE 登入尚未啟用，請先使用 Email 登入。", "line_not_configured");
        }

        if (request.Mode is not ("login" or "bind"))
        {
            throw new MemberValidationException("mode 只能是 login 或 bind。", "invalid_mode");
        }

        if (request.Mode == "bind" && boundMemberId is null)
        {
            throw new MemberUnauthenticatedException("綁定 LINE 需要先登入。");
        }

        var allowed = line.AllowedRedirectUris;
        var redirectUri = string.IsNullOrWhiteSpace(request.RedirectUri) ? allowed[0] : request.RedirectUri.Trim();
        if (!allowed.Contains(redirectUri, StringComparer.Ordinal))
        {
            throw new MemberValidationException("不允許的回呼網址。", "invalid_redirect_uri");
        }

        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var state = secureTokens.Protect(MemberSecureTokens.PurposeLineState,
            new MemberSecureTokens.LineStatePayload(request.Mode, club.ClubCode, nonce, redirectUri, request.Mode == "bind" ? boundMemberId : null),
            MemberSecureTokens.LineStateLifetime);
        return new MemberLineAuthorizeDto { AuthorizeUrl = line.BuildAuthorizeUrl(state, nonce, redirectUri), State = state };
    }

    public async Task<(MemberLineCallbackDto Dto, MemberSessionTokens? Tokens)> LineCallbackAsync(
        MemberLineCallbackRequest request, Guid? authenticatedMemberId, CancellationToken cancellationToken)
    {
        if (!line.IsConfigured)
        {
            throw new FeatureNotConfiguredException("LINE 登入尚未啟用，請先使用 Email 登入。", "line_not_configured");
        }

        var state = secureTokens.TryUnprotect<MemberSecureTokens.LineStatePayload>(MemberSecureTokens.PurposeLineState, request.State)
                    ?? throw new MemberValidationException("LINE 授權已逾時，請重新操作一次。", "state_invalid");
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length > 1024)
        {
            throw new MemberValidationException("LINE 授權碼不正確。", "state_invalid");
        }

        var identity = await line.ExchangeAsync(request.Code, state.RedirectUri, state.Nonce, cancellationToken);
        var hash = MemberSecureTokens.HashLineUserId(identity.UserId);
        var holder = await db.Members.FirstOrDefaultAsync(m => m.LineUserIdHash == hash, cancellationToken);

        if (state.Mode == "bind")
        {
            // 綁定：state 內的會員必須就是目前登入的人（state 外流給別人也不能替別人的帳號綁 LINE）。
            if (authenticatedMemberId is null || authenticatedMemberId != state.MemberId)
            {
                throw new MemberUnauthenticatedException("綁定 LINE 需要先登入。");
            }

            var me = await db.Members.FirstAsync(m => m.Id == authenticatedMemberId, cancellationToken);
            if (me.LineUserIdHash is not null)
            {
                throw new MemberConflictException("已綁定 LINE", "你的帳號已經綁定 LINE 了。", "line_already_bound");
            }

            if (holder is not null)
            {
                throw new MemberConflictException("LINE 已被使用", "這個 LINE 帳號已經綁定在另一個會員帳號上。", "line_in_use");
            }

            me.LineUserIdEncrypted = secureTokens.ProtectLineUserId(identity.UserId);
            me.LineUserIdHash = hash;
            me.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return (new MemberLineCallbackDto { Status = "bound" }, null);
        }

        if (holder is { Status: "active" })
        {
            holder.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            var tokens = await sessions.IssueAsync(holder.Id, persistent: true, cancellationToken);
            return (new MemberLineCallbackDto { Status = "logged_in", Session = ToSessionDto(tokens, holder, exposeRefresh: false) }, tokens);
        }

        if (holder is { Status: "suspended" })
        {
            throw new MemberForbiddenException("這個帳號已停用，請聯繫客服。", "account_suspended");
        }

        // 找不到會員：不自動併入 Email 相同的既有帳號（LINE 提供的 Email 不保證經過驗證，自動併入等於讓人用別人的 Email 接管帳號）。
        // 改成帶票據讓使用者補 Email 完成註冊；若該 Email 已有帳號，complete 會回 409，引導先用 Email 登入再到設定綁定 LINE。
        var ticket = secureTokens.Protect(MemberSecureTokens.PurposeLineTicket,
            new MemberSecureTokens.LineTicketPayload(identity.UserId, identity.DisplayName, identity.Email, state.Club), MemberSecureTokens.LineTicketLifetime);
        return (new MemberLineCallbackDto
        {
            Status = "signup_required", Ticket = ticket, DisplayName = identity.DisplayName, SuggestedEmail = identity.Email,
        }, null);
    }

    public async Task<(MemberSessionDto? Session, MemberSessionTokens Tokens)> LineCompleteAsync(
        MemberLineCompleteRequest request, ClubScope club, CancellationToken cancellationToken)
    {
        var ticket = secureTokens.TryUnprotect<MemberSecureTokens.LineTicketPayload>(MemberSecureTokens.PurposeLineTicket, request.Ticket)
                     ?? throw new MemberValidationException("LINE 註冊已逾時，請重新操作一次。", "ticket_invalid");
        if (!string.Equals(ticket.Club, club.ClubCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new MemberValidationException("LINE 註冊已逾時，請重新操作一次。", "ticket_invalid");
        }

        var emailAddress = NormalizeEmail(request.Email);
        var name = RequireName(string.IsNullOrWhiteSpace(request.Name) ? ticket.DisplayName : request.Name);
        var hash = MemberSecureTokens.HashLineUserId(ticket.LineUserId);
        if (await db.Members.AsNoTracking().AnyAsync(m => m.LineUserIdHash == hash, cancellationToken))
        {
            throw new MemberConflictException("LINE 已被使用", "這個 LINE 帳號已經註冊過了，請直接用 LINE 登入。", "line_in_use");
        }

        if (await db.Members.AsNoTracking().AnyAsync(m => m.Email == emailAddress, cancellationToken))
        {
            throw new MemberConflictException("Email 已註冊", "這個 Email 已經有帳號了。請先用 Email 登入，再到會員設定綁定 LINE。", "email_taken");
        }

        var member = new Member
        {
            Id = Guid.NewGuid(), Name = name, Email = emailAddress, PasswordHash = "!line-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            Phone = OptionalPhone(request.Phone), BirthOn = OptionalBirthOn(request.BirthOn), SignupSource = "line", Status = "active",
            Locale = DbLocale(request.Lang), LineUserIdEncrypted = secureTokens.ProtectLineUserId(ticket.LineUserId), LineUserIdHash = hash,
            LastLoginAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        await SaveNewMemberAsync(member, club, cancellationToken);
        await memberships.EnsureRegisteredAsync(member.Id, member.Name, club.ClubId, cancellationToken); // LINE 已證明身分，直接成為一般會員
        await SendVerificationAsync(member, club, LangOrDefault(request.Lang), cancellationToken); // Email 仍需驗證（密碼登入與重設才安全）

        var tokens = await sessions.IssueAsync(member.Id, persistent: true, cancellationToken);
        return (ToSessionDto(tokens, member, exposeRefresh: false), tokens);
    }

    public async Task UnbindLineAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var member = await db.Members.FirstAsync(m => m.Id == memberId, cancellationToken);
        if (member.LineUserIdHash is null)
        {
            return;
        }

        // 至少保留一種登入方式（規劃書 §3.14、App 規劃書 §4.2）：沒設定過密碼就不能解除 LINE。
        if (!HasPassword(member))
        {
            throw new MemberConflictException("無法解除綁定", "你的帳號目前只能用 LINE 登入，請先設定密碼，再解除 LINE 綁定。", "password_required");
        }

        member.LineUserIdEncrypted = null;
        member.LineUserIdHash = null;
        member.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ═══════════════════════════ 共用 ═══════════════════════════

    public static MemberSessionDto ToSessionDto(MemberSessionTokens tokens, Member member, bool exposeRefresh) => new()
    {
        AccessToken = tokens.AccessToken, AccessTokenExpiresAt = tokens.AccessTokenExpiresAtUtc,
        RefreshToken = exposeRefresh ? tokens.RefreshToken : null,
        RefreshTokenExpiresAt = exposeRefresh ? tokens.RefreshTokenExpiresAtUtc : null,
        Member = ToSummary(member),
    };

    private async Task<(string Name, string Domain)> ClubDisplayAsync(Guid clubId, string lang, CancellationToken cancellationToken)
    {
        var club = await db.Clubs.AsNoTracking().Include(c => c.ClubsI18ns).FirstAsync(c => c.Id == clubId, cancellationToken);
        var wanted = lang == "en" ? "en" : "zh-Hant";
        var name = club.ClubsI18ns.FirstOrDefault(i => i.Locale == wanted)?.Name
                   ?? club.ClubsI18ns.FirstOrDefault(i => i.Locale == "zh-Hant")?.Name ?? club.Code;
        return (name, club.Domain);
    }

    /// <summary>信件連結：<c>{站台網址}/{zh|en}/member/{verify-email|reset-password}?token=…</c>。站台網址預設 <c>https://{俱樂部網域}</c>，
    /// 本機開發用 <c>MEMBER_EMAIL_LINK_BASE_URL</c>（如 <c>http://localhost:3000</c>）覆寫。前端頁面路徑見 README E 批。</summary>
    private string BuildLink(string domain, string lang, string page, string token)
    {
        var baseUrl = configuration["MEMBER_EMAIL_LINK_BASE_URL"] is { Length: > 0 } configured ? configured.TrimEnd('/') : $"https://{domain}";
        return $"{baseUrl}/{lang}/member/{page}?token={Uri.EscapeDataString(token)}";
    }
}
