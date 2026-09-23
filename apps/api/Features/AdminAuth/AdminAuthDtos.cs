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
