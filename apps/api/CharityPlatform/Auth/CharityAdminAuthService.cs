using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.CharityPlatform.Auth;

/// <summary>
/// 慈善後台登入核心：驗密碼、鎖定政策、2FA（帳號持有人自行選用，已啟用者登入仍須驗證碼）、核發與輪替權杖。
/// 與主站 <c>AdminAuthService</c> 逐點對應，但<b>讀寫的是慈善資料庫的獨立帳號體系</b>
/// （<c>admin_users</c>／<c>admin_refresh_tokens</c>），簽的是慈善後台自己的權杖
/// （<see cref="CharityTokenService"/>）。登入／更新權杖／登出的請求與回應形狀刻意與主站相同
/// （<see cref="LoginRequest"/>／<see cref="LoginResponse"/>），讓兩個後台前端的登入程式碼可以共用。
/// 🔴 不寫登入紀錄表（慈善庫沒有，也不需要：稽核紀錄 <c>audit_logs</c> 只記規劃書 §11.2 的三類操作）。
/// </summary>
public sealed class CharityAdminAuthService(
    CharityDbContext db, CharityTokenService tokenService, CharityDataProtector protector, CharityPermissionChecker permissions)
{
    // 5 次失敗鎖 15 分鐘，同主站（OWASP Authentication Cheat Sheet 建議範圍）。
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // 帳號枚舉時序側錄防線，理由同主站 AdminAuthService：「帳號不存在」也要跑一次完整的 Argon2id 比對。
    private static readonly string DummyPasswordHashForTimingSafety = PasswordHasher.Hash("dummy-password-not-a-real-account");

    public async Task<LoginResult> LoginAsync(
        string username, string password, string? totpCode, CancellationToken cancellationToken)
    {
        var user = await db.AdminUsers.SingleOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null)
        {
            PasswordHasher.Verify(password, DummyPasswordHashForTimingSafety);
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
                // 密碼已驗證正確、尚未提供驗證碼：流程還沒走完，不計入鎖定門檻。
                return new LoginResult(LoginOutcome.TotpRequired, Message: "請輸入兩階段驗證碼。");
            }

            var secret = protector.DecryptTwoFactorSecret(user.TwoFactorSecretEncrypted!);
            if (!TotpService.Verify(secret, totpCode))
            {
                await RegisterFailedAttemptAsync(user, cancellationToken);
                return new LoginResult(LoginOutcome.InvalidCredentials, Message: "兩階段驗證碼錯誤。");
            }
        }

        user.FailedAttemptCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var (accessToken, accessExpires) = tokenService.IssueAccessToken(user.Id, user.Username, user.IsSuperAdmin);
        var (refreshRaw, refreshExpires) = await IssueRefreshTokenAsync(user.Id, cancellationToken);

        return new LoginResult(
            LoginOutcome.Success, user.Id, user.Username, user.IsSuperAdmin, user.MustChangePassword, user.TwoFactorEnabled,
            accessToken, accessExpires, refreshRaw, refreshExpires);
    }

    /// <summary>
    /// 更新權杖輪替＋重放偵測。找不到、已撤銷、過期、帳號停用都回傳 <c>null</c>（呼叫端一律回 401，不區分原因）。
    /// 已撤銷的權杖又被拿來用＝被偷過：撤銷該帳號名下全部有效權杖，逼真正的使用者也重新登入。
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
        var (refreshRaw, refreshExpires, newTokenId) = await IssueRefreshTokenWithIdAsync(user.Id, cancellationToken);

        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedById = newTokenId;
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

        // 密碼政策與主站共用同一條規則（長度、不得等於帳號），不另寫一份。
        AdminAuthService.ValidatePasswordPolicy(newPassword, user.Username);

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = adminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TwoFactorSetupResponse> BeginTwoFactorSetupAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        var user = await db.AdminUsers.SingleAsync(u => u.Id == adminUserId, cancellationToken);
        var secret = TotpService.GenerateSecret();
        user.TwoFactorSecretEncrypted = protector.EncryptTwoFactorSecret(secret);
        // 尚未證明驗證器讀得到正確的碼，不動 TwoFactorEnabled，等 Confirm 成功才打開。
        await db.SaveChangesAsync(cancellationToken);

        var base32 = TotpService.ToBase32(secret);
        var otpAuthUrl = $"otpauth://totp/TCRFC%20Charity%20Admin:{Uri.EscapeDataString(user.Username)}" +
                          $"?secret={base32}&issuer=TCRFC%20Charity%20Admin&digits=6&period=30";
        return new TwoFactorSetupResponse(base32, otpAuthUrl);
    }

    public async Task<bool> ConfirmTwoFactorAsync(Guid adminUserId, string code, CancellationToken cancellationToken)
    {
        var user = await db.AdminUsers.SingleAsync(u => u.Id == adminUserId, cancellationToken);
        if (user.TwoFactorSecretEncrypted is null)
        {
            throw new AdminAuthValidationException("尚未開始兩階段驗證設定，請先取得設定密鑰。");
        }

        var secret = protector.DecryptTwoFactorSecret(user.TwoFactorSecretEncrypted);
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

    public async Task<CharityMeResponse> GetMeAsync(CharityAdminIdentity identity, CancellationToken cancellationToken)
    {
        var user = await db.AdminUsers.AsNoTracking()
            .Include(u => u.AdminRoles)
            .SingleAsync(u => u.Id == identity.AdminUserId, cancellationToken);

        var roles = user.AdminRoles
            .Select(r => new CharityMeRoleDto { Code = r.Code, NameZh = r.NameZh, NameEn = r.NameEn })
            .OrderBy(r => r.Code, StringComparer.Ordinal)
            .ToList();

        var held = await permissions.GetAllHeldPermissionCodesAsync(user.Id, identity.IsSuperAdmin, cancellationToken);

        return new CharityMeResponse
        {
            AdminUserId = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            IsSuperAdmin = identity.IsSuperAdmin,
            TwoFactorEnabled = user.TwoFactorEnabled,
            MustChangePassword = user.MustChangePassword,
            Roles = roles,
            Permissions = held,
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

    private async Task<(string RawToken, DateTime ExpiresAtUtc)> IssueRefreshTokenAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        var (raw, expires, _) = await IssueRefreshTokenWithIdAsync(adminUserId, cancellationToken);
        return (raw, expires);
    }

    private async Task<(string RawToken, DateTime ExpiresAtUtc, Guid TokenId)> IssueRefreshTokenWithIdAsync(
        Guid adminUserId, CancellationToken cancellationToken)
    {
        var raw = AdminTokenService.GenerateRefreshTokenValue();
        var expiresAt = DateTime.UtcNow.Add(CharityTokenService.RefreshTokenLifetime);
        var id = Guid.NewGuid();

        db.AdminRefreshTokens.Add(new AdminRefreshToken
        {
            Id = id,
            AdminUserId = adminUserId,
            TokenHash = AdminTokenService.HashRefreshToken(raw),
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(cancellationToken);

        return (raw, expiresAt, id);
    }
}

public sealed record CharityMeRoleDto
{
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
}

/// <summary>慈善後台的個人檔案：⛔ 不含密碼雜湊、2FA 密文或任何機密欄位。
/// <see cref="Permissions"/> 只給程式判斷（顯示或隱藏按鈕）用，介面不得顯示（規劃書 §4.0）。
/// 慈善是單一法人，沒有俱樂部授權清單與站台切換器。</summary>
public sealed record CharityMeResponse
{
    public required Guid AdminUserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsSuperAdmin { get; init; }
    public required bool TwoFactorEnabled { get; init; }
    public required bool MustChangePassword { get; init; }
    public required IReadOnlyList<CharityMeRoleDto> Roles { get; init; }
    public required IReadOnlyList<string> Permissions { get; init; }
}
