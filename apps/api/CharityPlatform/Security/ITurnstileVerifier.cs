using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Tcrfc.Api.CharityPlatform.Security;

/// <summary>
/// 捐款建單的人機驗證（規劃書 §3.3「防濫用：Turnstile 或同等機制」、§11.2）。第二道防線，第一道是依 IP 的限流
/// （<see cref="Common.CharityRateLimitPolicies"/>）。Cloudflare Turnstile 的網站金鑰與密鑰尚未建立，
/// 所以預設是 <see cref="NotConfiguredTurnstileVerifier"/>（一律放行）；設定了 <c>TURNSTILE_SECRET_KEY_CHARITY</c>
/// 就改用 <see cref="CloudflareTurnstileVerifier"/>，前端一併送 <c>turnstileToken</c> 即可，API 契約不變。
/// </summary>
public interface ITurnstileVerifier
{
    /// <summary>驗證通過回傳 <c>true</c>。實作遇到<b>驗證服務本身</b>的技術性失敗時<b>放行</b>（fail-open）並記警告：
    /// 人機驗證是濫用防線的一層，不能因為 Cloudflare 一時不可用就讓所有捐款人無法捐款（捐款是營收與公益動機，
    /// 第一道的 IP 限流仍在）。</summary>
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}

public sealed class NotConfiguredTurnstileVerifier : ITurnstileVerifier
{
    public Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken) => Task.FromResult(true);
}

public sealed class CloudflareTurnstileVerifier(HttpClient http, string secret, ILogger<CloudflareTurnstileVerifier> logger) : ITurnstileVerifier
{
    public const string SecretConfigKey = "TURNSTILE_SECRET_KEY_CHARITY";
    public const string SiteVerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

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
            logger.LogWarning(ex, "Turnstile 驗證服務暫時無法使用，本次改為放行（第一道 IP 限流仍然生效）");
            return true;
        }
    }

    private sealed record SiteVerifyResponse([property: JsonPropertyName("success")] bool Success);
}
