using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminAuth;

/// <summary>
/// J1 帳號管理的登入核心：驗密碼、鎖定政策、2FA、核發與輪替權杖。
/// 端點層（<see cref="AdminAuthEndpoints"/>）只負責 HTTP 形狀（讀寫 Cookie、狀態碼），
/// 業務規則全部在這裡。
///
/// ⚠️ 2026-09-23（使用者裁決）：J3「登入紀錄」（`admin_login_logs` 表）已撤回——
/// docs/12-database-schema.md §13.1 是委託方明文「本版資料庫設計不含 log」的既有指示，
/// 本服務新增該表時沒有先查這條，屬派工與執行雙方的疏漏，撤回後回到文件記載的狀態。
/// **鎖定政策（`FailedAttemptCount`／`LockedUntil`）與登入紀錄是兩個獨立機制，前者
/// 一律只讀寫 `AdminUser` 本身的欄位，撤回登入紀錄不影響鎖定功能**——見
/// <see cref="RegisterFailedAttemptAsync"/>，從未依賴任何日誌表。
/// </summary>
public sealed class AdminAuthService(
    ClubDbContext db, AdminTokenService tokenService, TwoFactorSecretProtector twoFactorProtector,
    IPermissionChecker permissionChecker)
{
    // 5 次失敗鎖 15 分鐘——業界常見門檻（OWASP Authentication Cheat Sheet 建議範圍
    // 3–5 次），本專案沒有更嚴格的規劃書條文可依循，屬執行層判斷，見 apps/api/README.md。
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<LoginResult> LoginAsync(
        string username, string password, string? totpCode, CancellationToken cancellationToken)
    {
        var user = await db.AdminUsers.SingleOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null)
        {
            return new LoginResult(LoginOutcome.InvalidCredentials, Message: "帳號或密碼錯誤。");
        }

        if (user.Status != "active")
        {
            return new LoginResult(LoginOutcome.Disabled, Message: "帳號已停用，請聯繫系統管理員。");
        }

        if (user.LockedUntil is not null && user.LockedUntil > DateTime.UtcNow)
        {
            return new LoginResult(LoginOutcome.Locked, LockedUntilUtc: user.LockedUntil,
                Message: $"帳號已被鎖定，請於 {user.LockedUntil:yyyy-MM-dd HH:mm} UTC 後再試。");
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            await RegisterFailedAttemptAsync(user, cancellationToken);
            return new LoginResult(LoginOutcome.InvalidCredentials, Message: "帳號或密碼錯誤。");
        }

        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(totpCode))
            {
                // ⚠️ 密碼已驗證正確，但尚未提供 2FA 碼——這不是失敗嘗試，不計入鎖定門檻，
                // 只是流程還沒走完，讓前端知道要再問一次驗證碼。
                return new LoginResult(LoginOutcome.TotpRequired, Message: "請輸入兩階段驗證碼。");
            }

            var secret = twoFactorProtector.Decrypt(user.TwoFactorSecretEncrypted!);
            if (!TotpService.Verify(secret, totpCode))
            {
                await RegisterFailedAttemptAsync(user, cancellationToken);
                return new LoginResult(LoginOutcome.InvalidCredentials, Message: "兩階段驗證碼錯誤。");
            }
        }

        // 成功：清鎖定狀態、更新最後登入時間、核發權杖。
        user.FailedAttemptCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var (accessToken, accessExpires) = tokenService.IssueAccessToken(user.Id, user.Username, user.IsSuperAdmin);
        var (refreshRaw, refreshExpires) = await IssueRefreshTokenAsync(user.Id, null, cancellationToken);

        return new LoginResult(
            LoginOutcome.Success, user.Id, user.Username, user.IsSuperAdmin, user.MustChangePassword, user.TwoFactorEnabled,
            accessToken, accessExpires, refreshRaw, refreshExpires);
    }

    /// <summary>
    /// 更新權杖輪替＋重放偵測。找不到、已撤銷、過期都回傳 <c>null</c>（呼叫端一律回 401，
    /// 不區分原因——區分「查無」「已撤銷」「過期」對呼叫端沒有可採取的不同行動，
    /// 只會多洩露伺服器內部狀態）。
    /// </summary>
    public async Task<LoginResult?> RefreshAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        var hash = AdminTokenService.HashRefreshToken(rawRefreshToken);
        var existing = await db.AdminRefreshTokens
            .Include(t => t.AdminUser)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (existing.RevokedAt is not null)
        {
            // 🔴 重放偵測：這把權杖曾經被撤銷過（正常輪替流程一定會撤銷舊的），但現在又有人拿它來用
            // ——代表這把權杖被偷過（例如從瀏覽器歷史、代理伺服器日誌外洩），舊使用者已經拿到過
            // 輪替後的新權杖，繼續用舊的只可能是攻擊者。防禦動作：撤銷這個帳號名下全部有效權杖，
            // 逼真正的使用者也要重新登入——寧可誤傷合法使用者一次，不留一個持續有效的偷來的權杖。
            var allActive = await db.AdminRefreshTokens
                .Where(t => t.AdminUserId == existing.AdminUserId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var token in allActive)
            {
                token.RevokedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (existing.ExpiresAt < DateTime.UtcNow)
        {
            return null;
        }

        var user = existing.AdminUser;
        if (user.Status != "active")
        {
            return null;
        }

        var (accessToken, accessExpires) = tokenService.IssueAccessToken(user.Id, user.Username, user.IsSuperAdmin);
        var (refreshRaw, refreshExpires) = await IssueRefreshTokenAsync(user.Id, existing.Id, cancellationToken);

        existing.RevokedAt = DateTime.UtcNow;
        // ReplacedById 要指向新權杖，但新權杖是在 IssueRefreshTokenAsync 內建立並 SaveChanges 過的，
        // 這裡用剛存好的 id 回填、再存一次——兩次 SaveChanges 換取程式碼可讀性，這條路徑頻率低
        // （每 15 分鐘一次的存取權杖過期才觸發），不是效能敏感路徑。
        var newToken = await db.AdminRefreshTokens.SingleAsync(t => t.TokenHash == AdminTokenService.HashRefreshToken(refreshRaw), cancellationToken);
        existing.ReplacedById = newToken.Id;
        await db.SaveChangesAsync(cancellationToken);

        return new LoginResult(
            LoginOutcome.Success, user.Id, user.Username, user.IsSuperAdmin, user.MustChangePassword, user.TwoFactorEnabled,
            accessToken, accessExpires, refreshRaw, refreshExpires);
    }

    public async Task LogoutAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        var hash = AdminTokenService.HashRefreshToken(rawRefreshToken);
        var existing = await db.AdminRefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is not null && existing.RevokedAt is null)
        {
            existing.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ChangePasswordAsync(Guid adminUserId, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await db.AdminUsers.SingleAsync(u => u.Id == adminUserId, cancellationToken);
        if (!PasswordHasher.Verify(currentPassword, user.PasswordHash))
        {
            return false;
        }

        ValidatePasswordPolicy(newPassword, user.Username);

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>密碼政策——J1「密碼政策」的最小落地：長度、不得等於帳號本身。
    /// ⚠️ 規劃書沒有寫死具體規則（長度、字元類別、輪替週期），這是執行層判斷，
    /// 見 apps/api/README.md「密碼政策」段落列出的取捨。</summary>
    public static void ValidatePasswordPolicy(string password, string username)
    {
        if (password.Length < 10)
        {
            throw new AdminAuthValidationException("密碼長度至少需要 10 個字元。");
        }
        if (string.Equals(password, username, StringComparison.OrdinalIgnoreCase))
        {
            throw new AdminAuthValidationException("密碼不得與帳號相同。");
        }
    }

    public async Task<TwoFactorSetupResponse> BeginTwoFactorSetupAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        var user = await db.AdminUsers.SingleAsync(u => u.Id == adminUserId, cancellationToken);
        var secret = TotpService.GenerateSecret();
        user.TwoFactorSecretEncrypted = twoFactorProtector.Encrypt(secret);
        // ⚠️ 這裡刻意不動 TwoFactorEnabled——設定尚未完成（使用者還沒證明自己的驗證器 App
        // 讀得到正確的碼），要等 ConfirmTwoFactorAsync 成功才真的打開。
        await db.SaveChangesAsync(cancellationToken);

        var base32 = TotpService.ToBase32(secret);
        var otpAuthUrl = $"otpauth://totp/TCRFC%20Admin:{Uri.EscapeDataString(user.Username)}" +
                          $"?secret={base32}&issuer=TCRFC%20Admin&digits=6&period=30";
        return new TwoFactorSetupResponse(base32, otpAuthUrl);
    }

    public async Task<bool> ConfirmTwoFactorAsync(Guid adminUserId, string code, CancellationToken cancellationToken)
    {
        var user = await db.AdminUsers.SingleAsync(u => u.Id == adminUserId, cancellationToken);
        if (user.TwoFactorSecretEncrypted is null)
        {
            throw new AdminAuthValidationException("尚未開始 2FA 設定，請先呼叫設定端點取得密鑰。");
        }

        var secret = twoFactorProtector.Decrypt(user.TwoFactorSecretEncrypted);
        if (!TotpService.Verify(secret, code))
        {
            return false;
        }

        user.TwoFactorEnabled = true;
        user.TwoFactorConfirmedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DisableTwoFactorAsync(Guid adminUserId, string password, CancellationToken cancellationToken)
    {
        var user = await db.AdminUsers.SingleAsync(u => u.Id == adminUserId, cancellationToken);
        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            return false;
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecretEncrypted = null;
        user.TwoFactorConfirmedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// 前端 agent 回報缺口①：站台切換器需要知道「登入的這個人可以切到哪些俱樂部」，目前沒有任何
    /// 端點回傳這個資訊——登入回應（<see cref="LoginResponse"/>）只有 <c>Username</c>／
    /// <c>IsSuperAdmin</c> 兩個身分欄位，前端拿不到俱樂部授權清單。
    ///
    /// ⚠️ 這裡重新呼叫一次 <see cref="AdminAccountGate.RequireActiveAccountAsync"/>（跟
    /// <see cref="AdminClubAuthorizer"/>／<see cref="AdminSystemAuthorizer"/> 同一個檢查）而不是
    /// 只信任呼叫端已經驗證過的 JWT claims——理由跟那兩個授權器一樣：帳號可能在權杖簽發後被停用，
    /// 「這支端點回應的是不是最新的帳號狀態」比「省一次查詢」重要。
    /// </summary>
    public async Task<MeResponse> GetMeAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var identity = await AdminAccountGate.RequireActiveAccountAsync(db, httpContext, cancellationToken);

        var user = await db.AdminUsers.AsNoTracking()
            .Include(u => u.AdminRoles)
            .FirstOrDefaultAsync(u => u.Id == identity.AdminUserId, cancellationToken)
            ?? throw new AdminForbiddenException("帳號已停用或不存在，請聯繫系統管理員。");

        string? primaryClubCode = null;
        if (user.PrimaryClubId is Guid primaryClubId)
        {
            primaryClubCode = await db.Clubs.AsNoTracking()
                .Where(c => c.Id == primaryClubId)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(cancellationToken);
        }

        List<MeClubGrantDto> clubGrants;
        if (identity.IsSuperAdmin)
        {
            // 規劃書 §6「系統管理員（is_super_admin）跳過整個資料範圍查詢」——站台切換器對系統
            // 管理員應該顯示全部啟用中的俱樂部，不是查 AdminUserClub（系統管理員通常根本沒有
            // 任何一筆授權紀錄，種子資料的 sa@system.local／super.admin@tcrfc.test 皆是如此）。
            clubGrants = await db.Clubs.AsNoTracking()
                .Where(c => c.Status == "active")
                .OrderBy(c => c.SortOrder)
                .Select(c => new MeClubGrantDto
                {
                    ClubCode = c.Code,
                    ClubNameZh = c.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                    ClubNameEn = c.ClubsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                    IsPrimary = c.Id == user.PrimaryClubId,
                    ExpiresOn = null,
                })
                .ToListAsync(cancellationToken);
        }
        else
        {
            // 有效範圍＝ AdminUserClub 中 is_active 且 expires_on 未到期的 club_id 集合
            // （規劃書 §6「資料範圍規則」，與 AdminClubAuthorizer 第③步同一條規則）。
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            clubGrants = await db.AdminUserClubs.AsNoTracking()
                .Where(g => g.AdminUserId == user.Id && g.IsActive && (g.ExpiresOn == null || g.ExpiresOn >= today))
                .OrderBy(g => g.Club.SortOrder)
                .Select(g => new MeClubGrantDto
                {
                    ClubCode = g.Club.Code,
                    ClubNameZh = g.Club.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                    ClubNameEn = g.Club.ClubsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                    IsPrimary = g.ClubId == user.PrimaryClubId,
                    ExpiresOn = g.ExpiresOn,
                })
                .ToListAsync(cancellationToken);
        }

        var roles = user.AdminRoles
            .Select(r => new MeRoleDto { Code = r.Code, NameZh = r.NameZh, NameEn = r.NameEn })
            .OrderBy(r => r.Code, StringComparer.Ordinal)
            .ToList();

        var heldPermissions = await permissionChecker.GetAllHeldPermissionsAsync(user.Id, identity.IsSuperAdmin, cancellationToken);
        var permissions = heldPermissions
            .Select(kv => new MePermissionDto { Code = kv.Key, ScopeTypes = kv.Value })
            .OrderBy(p => p.Code, StringComparer.Ordinal)
            .ToList();

        return new MeResponse
        {
            AdminUserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            IsSuperAdmin = identity.IsSuperAdmin,
            PrimaryClubCode = primaryClubCode,
            ClubGrants = clubGrants,
            Roles = roles,
            Permissions = permissions,
        };
    }

    private async Task RegisterFailedAttemptAsync(AdminUser user, CancellationToken cancellationToken)
    {
        user.FailedAttemptCount += 1;
        if (user.FailedAttemptCount >= MaxFailedAttempts)
        {
            user.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(string RawToken, DateTime ExpiresAtUtc)> IssueRefreshTokenAsync(
        Guid adminUserId, Guid? replacesTokenId, CancellationToken cancellationToken)
    {
        var raw = AdminTokenService.GenerateRefreshTokenValue();
        var expiresAt = DateTime.UtcNow.Add(AdminTokenService.RefreshTokenLifetime);

        db.AdminRefreshTokens.Add(new AdminRefreshToken
        {
            Id = Guid.NewGuid(),
            AdminUserId = adminUserId,
            TokenHash = AdminTokenService.HashRefreshToken(raw),
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(cancellationToken);

        return (raw, expiresAt);
    }
}

public sealed class AdminAuthValidationException(string message) : Exception(message);
