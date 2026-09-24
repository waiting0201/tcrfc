namespace Tcrfc.Api.Features.AdminAuth;

public sealed record LoginRequest(string Username, string Password, string? TotpCode);

public enum LoginOutcome
{
    Success,
    TotpRequired,
    InvalidCredentials,
    Locked,
    Disabled,
}

/// <summary>登入服務內部結果——不是 HTTP 回應形狀，端點負責把它映射成狀態碼與 JSON／Cookie。</summary>
public sealed record LoginResult(
    LoginOutcome Outcome,
    Guid? AdminUserId = null,
    string? Username = null,
    bool IsSuperAdmin = false,
    bool MustChangePassword = false,
    bool TwoFactorEnabled = false,
    string? AccessToken = null,
    DateTime? AccessTokenExpiresAtUtc = null,
    string? RefreshTokenRaw = null,
    DateTime? RefreshTokenExpiresAtUtc = null,
    string? Message = null,
    DateTime? LockedUntilUtc = null);

public sealed record LoginResponse(
    string AccessToken, DateTime AccessTokenExpiresAtUtc, bool MustChangePassword,
    bool TwoFactorEnabled, string Username, bool IsSuperAdmin);

public sealed record RefreshResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record TwoFactorSetupResponse(string Secret, string OtpAuthUrl);

public sealed record TwoFactorConfirmRequest(string Code);

public sealed record TwoFactorDisableRequest(string Password);

/// <summary>前端 agent 回報缺口①：站台切換器需要知道「這個登入的人可以切到哪些俱樂部」。
/// ⛔ 不含密碼雜湊、2FA 密文或任何其他機密欄位——這是給前端顯示用的個人檔案，不是帳號管理端點。</summary>
public sealed record MeClubGrantDto
{
    public required string ClubCode { get; init; }
    public string? ClubNameZh { get; init; }
    public string? ClubNameEn { get; init; }

    /// <summary>是否為 <c>AdminUser.primary_club_id</c>——站台切換器的預設選取值
    /// （規劃書 §4.0「站台切換器」：預設選取 primary_club_id）。</summary>
    public required bool IsPrimary { get; init; }

    /// <summary>系統管理員的授權不受 <c>AdminUserClub</c> 限制（規劃書 §6「系統管理員跳過整個
    /// 資料範圍查詢」），這裡固定回傳 <c>null</c>（無到期日）；非系統管理員回傳實際的
    /// <c>AdminUserClub.expires_on</c>。</summary>
    public DateOnly? ExpiresOn { get; init; }
}

public sealed record MeRoleDto
{
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
}

public sealed record MeResponse
{
    public required Guid AdminUserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsSuperAdmin { get; init; }

    /// <summary><c>AdminUser.primary_club_id</c> 解析出來的俱樂部代碼；帳號從未設定預設俱樂部
    /// 時為 <c>null</c>。</summary>
    public string? PrimaryClubCode { get; init; }

    /// <summary>已過濾到期與停用（系統管理員例外——見 <see cref="MeClubGrantDto.ExpiresOn"/> 說明，
    /// 系統管理員一律回傳「全部啟用中的俱樂部」，不是「被明確授權的俱樂部」，兩者對系統管理員
    /// 而言結果相同，但資料來源不同，見 <c>AdminAuthService.GetMeAsync</c>）。</summary>
    public required IReadOnlyList<MeClubGrantDto> ClubGrants { get; init; }

    public required IReadOnlyList<MeRoleDto> Roles { get; init; }
}
