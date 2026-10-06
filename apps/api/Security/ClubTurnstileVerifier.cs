using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Tcrfc.Api.Security;

/// <summary>
/// 俱樂部側（主站／藍鯨共用）公開表單的人機驗證（主站規劃書 10 表單中心「防機器人（reCAPTCHA / Turnstile）」、
/// 每張表單的「是否啟用 CAPTCHA」旗標 <c>forms.captcha_enabled</c>）。第二道防線，第一道是依訪客 IP 的限流，
/// 另有 honeypot。設定鍵 <see cref="CloudflareClubTurnstileVerifier.SecretConfigKey"/>（放 VM 的 club.env）；
/// 沒設就是 <see cref="NotConfiguredClubTurnstileVerifier"/>，一律放行。
/// 與慈善平台的 <c>ITurnstileVerifier</c> 刻意各自一份（兩邊互不引用，<c>CharityArchitectureTests</c> 守界線），
/// 密鑰也是不同的鍵。
/// </summary>
public interface IClubTurnstileVerifier
{
    /// <summary>已設定密鑰（＝部署端有開啟人機驗證）。未設定時表單端點不應要求 token。</summary>
    bool IsEnabled { get; }

    /// <summary>驗證通過回傳 <c>true</c>。缺 token 回 <c>false</c>。<b>驗證服務本身</b>的技術性失敗
    /// （逾時、5xx、回應無法解析）放行（fail-open）並記警告：Cloudflare 一時不可用不能讓所有訪客無法送出表單，
    /// 第一道的 IP 限流與 honeypot 仍在。</summary>
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}

public sealed class NotConfiguredClubTurnstileVerifier : IClubTurnstileVerifier
{
    public bool IsEnabled => false;

    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken) => Task.FromResult(true);
}

public sealed class CloudflareClubTurnstileVerifier(HttpClient http, string secret, ILogger<CloudflareClubTurnstileVerifier> logger) : IClubTurnstileVerifier
{
    public const string SecretConfigKey = "TURNSTILE_SECRET_KEY";
    public const string HttpClientName = "club-turnstile";
    public const string SiteVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public bool IsEnabled => true;

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false; // 已啟用驗證，沒帶權杖一律不過
        }

        try
        {
            var form = new Dictionary<string, string> { ["secret"] = secret, ["response"] = token };
            if (!string.IsNullOrWhiteSpace(remoteIp) && remoteIp != "unknown")
            {
                form["remoteip"] = remoteIp;
            }

            using var response = await http.PostAsync(SiteVerifyUrl, new FormUrlEncodedContent(form), cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken);
            return body?.Success == true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // 呼叫端主動取消（請求被中止）不算 Cloudflare 異常，交還給上層。
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            logger.LogWarning(ex, "Turnstile 驗證服務暫時無法使用，本次改為放行（IP 限流與誘捕欄位仍然生效）");
            return true;
        }
    }

    private sealed record SiteVerifyResponse([property: JsonPropertyName("success")] bool Success);
}
