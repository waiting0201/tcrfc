namespace Tcrfc.Api.Features.MemberAuth;

/// <summary><c>TokenDelivery</c>：<c>cookie</c>（預設，瀏覽器：更新權杖放 HttpOnly Cookie，JS 碰不到）／<c>body</c>（App 與伺服器端代理：更新權杖放回應本文，由呼叫端存進安全儲存區）。</summary>
public sealed record MemberRegisterRequest(string Club, string Email, string Password, string Name, string? Phone, DateOnly? BirthOn, string? Lang);

public sealed record MemberVerifyEmailRequest(string Token);

public sealed record MemberResendVerificationRequest(string Email, string Club, string? Lang);

/// <summary><c>DeviceInstallId</c>（App 專用，AP-3）：帶了就把更新權杖鏈掛在該裝置（<c>app_devices</c>），並強制 <c>body</c> 交付；裝置須已註冊。</summary>
public sealed record MemberLoginRequest(string Email, string Password, bool RememberMe = false, string? TokenDelivery = null, string? DeviceInstallId = null);

public sealed record MemberRefreshRequest(string? RefreshToken = null, string? TokenDelivery = null);

public sealed record MemberForgotPasswordRequest(string Email, string Club, string? Lang);

public sealed record MemberResetPasswordRequest(string Token, string NewPassword);

/// <summary><c>CurrentPassword</c>：已設定密碼者必填；LINE 註冊、尚未設定密碼者留空（直接設定第一組密碼）。</summary>
public sealed record MemberChangePasswordRequest(string? CurrentPassword, string NewPassword, string? TokenDelivery = null, string? DeviceInstallId = null);

public sealed record MemberUpdateProfileRequest(string Name, string? Phone, DateOnly? BirthOn, string? Locale);

/// <summary><c>Password</c>：已設定密碼者必填；沒有密碼的 LINE 帳號改填 <c>Confirm = "DELETE"</c>。</summary>
public sealed record MemberDeleteAccountRequest(string? Password, string? Confirm);

public sealed record MemberLineAuthorizeRequest(string Club, string Mode, string? RedirectUri);

public sealed record MemberLineCallbackRequest(string Code, string State, string? TokenDelivery = null, string? DeviceInstallId = null);

public sealed record MemberLineCompleteRequest(string Club, string Ticket, string Email, string? Name, string? Phone, DateOnly? BirthOn, string? Lang, string? TokenDelivery = null, string? DeviceInstallId = null);

public sealed record MemberSummaryDto
{
    public required string MemberNo { get; init; }
    public required string Name { get; init; }
    public required bool EmailVerified { get; init; }
    public required bool HasPassword { get; init; }
    public required bool LineBound { get; init; }
}

/// <summary>登入成功的回應。<c>RefreshToken</c> 只有 <c>tokenDelivery=body</c> 才有值；<c>cookie</c> 模式下它在 <c>Set-Cookie</c>，回應本文沒有。</summary>
public sealed record MemberSessionDto
{
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpiresAt { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime? RefreshTokenExpiresAt { get; init; }
    public required MemberSummaryDto Member { get; init; }
}

public sealed record MemberRegisteredDto
{
    public required string MemberNo { get; init; }
    public required bool EmailVerificationRequired { get; init; }

    /// <summary>驗證信是否真的寄出（寄信供應商尚未串接時為 false，前端要如實告知，見 README E 批）。</summary>
    public required bool EmailSent { get; init; }
}

/// <summary>會員自己的 App 裝置（AP-3）。<c>DeviceId</c> 是裝置列的 id（不是 <c>device_install_id</c>）；<c>HasActiveSession</c>＝這支裝置的登入仍有效。</summary>
public sealed record MemberDeviceDto
{
    public required Guid DeviceId { get; init; }
    public required string Platform { get; init; }
    public string? OsVersion { get; init; }
    public string? AppVersion { get; init; }
    public required DateTime LastActiveAt { get; init; }
    public required bool HasActiveSession { get; init; }
}

public sealed record MemberProfileDto
{
    public required string MemberNo { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public DateOnly? BirthOn { get; init; }
    /// <summary><c>zh</c>／<c>en</c>／null（未設定）。</summary>
    public string? Locale { get; init; }
    public required bool EmailVerified { get; init; }
    public required bool HasPassword { get; init; }
    public required bool LineBound { get; init; }
    /// <summary>註冊來源：<c>web</c>／<c>line</c>／<c>admin</c>／<c>app</c>，與中文標籤。</summary>
    public required string SignupSource { get; init; }
    public required string SignupSourceLabel { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record MemberLineAuthorizeDto
{
    public required string AuthorizeUrl { get; init; }
    /// <summary>前端要自己存起來（sessionStorage），LINE 導回時與網址上的 <c>state</c> 比對；不符就丟棄（防登入 CSRF）。</summary>
    public required string State { get; init; }
}

/// <summary>LINE 回呼的結果：<c>Status</c>＝<c>logged_in</c>（有 <c>Session</c>）／<c>bound</c>（綁定完成）／<c>signup_required</c>（找不到會員，帶 <c>Ticket</c> 讓使用者補 Email 完成註冊）。</summary>
public sealed record MemberLineCallbackDto
{
    public required string Status { get; init; }
    public MemberSessionDto? Session { get; init; }
    public string? Ticket { get; init; }
    public string? DisplayName { get; init; }
    public string? SuggestedEmail { get; init; }
}
