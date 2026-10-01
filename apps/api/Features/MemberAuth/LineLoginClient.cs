using System.Text.Json;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.MemberAuth;

public sealed record LineIdentity(string UserId, string? DisplayName, string? Email);

/// <summary>
/// LINE Login（OAuth 2.1 / OpenID Connect）接縫。<b>憑證一律由設定讀取，不寫死、不進版控</b>：
/// <c>LINE_LOGIN_CHANNEL_ID</c>、<c>LINE_LOGIN_CHANNEL_SECRET</c>、<c>LINE_LOGIN_REDIRECT_URIS</c>（逗號分隔的白名單，前端回呼網址必須逐字相符，防 open redirect／授權碼外流）。
/// 任一缺值時 <see cref="IsConfigured"/> 為 false，端點回 503（<see cref="FeatureNotConfiguredException"/>）優雅降級，不是 500。
/// 流程：<see cref="BuildAuthorizeUrl"/> 組出導向 LINE 的網址（含 state、nonce）→ 使用者授權後瀏覽器帶 <c>code</c> 回前端 →
/// 前端把 <c>code</c>＋<c>state</c> 交給本 API → <see cref="ExchangeAsync"/> 用 code 換 id_token，再呼叫 LINE 的 verify 端點驗證 id_token 並比對 nonce，取得 LINE userId。
/// 不用 access token 打 profile API：id_token 經 LINE 伺服器驗證即可確認身分，少一次往返，也確保 nonce 被檢查。
/// </summary>
public interface ILineLoginClient
{
    bool IsConfigured { get; }

    IReadOnlyList<string> AllowedRedirectUris { get; }

    string BuildAuthorizeUrl(string state, string nonce, string redirectUri);

    /// <summary>用授權碼換身分。授權碼無效、nonce 不符、LINE 拒絕一律丟 <see cref="MemberValidationException"/>（code <c>line_exchange_failed</c>）。</summary>
    Task<LineIdentity> ExchangeAsync(string code, string redirectUri, string nonce, CancellationToken cancellationToken);
}

public sealed class LineLoginClient(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<LineLoginClient> logger) : ILineLoginClient
{
    public const string HttpClientName = "line-login";
    private const string AuthorizeEndpoint = "https://access.line.me/oauth2/v2.1/authorize";
    private const string TokenEndpoint = "https://api.line.me/oauth2/v2.1/token";
    private const string VerifyEndpoint = "https://api.line.me/oauth2/v2.1/verify";

    private string? ChannelId => configuration["LINE_LOGIN_CHANNEL_ID"];
    private string? ChannelSecret => configuration["LINE_LOGIN_CHANNEL_SECRET"];

    public IReadOnlyList<string> AllowedRedirectUris => (configuration["LINE_LOGIN_REDIRECT_URIS"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool IsConfigured
        => !string.IsNullOrWhiteSpace(ChannelId) && !string.IsNullOrWhiteSpace(ChannelSecret) && AllowedRedirectUris.Count > 0;

    public string BuildAuthorizeUrl(string state, string nonce, string redirectUri)
    {
        EnsureConfigured();
        return $"{AuthorizeEndpoint}?response_type=code&client_id={Uri.EscapeDataString(ChannelId!)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(state)}" +
               $"&scope={Uri.EscapeDataString("openid profile email")}&nonce={Uri.EscapeDataString(nonce)}";
    }

    public async Task<LineIdentity> ExchangeAsync(string code, string redirectUri, string nonce, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var http = httpClientFactory.CreateClient(HttpClientName);
        http.Timeout = TimeSpan.FromSeconds(10);
        try
        {
            using var tokenResponse = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = ChannelId!,
                ["client_secret"] = ChannelSecret!,
            }), cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("LINE token 端點回 {Status}", (int)tokenResponse.StatusCode); // 不記回應本文：可能含授權碼相關資訊
                throw new MemberValidationException("LINE 授權已失效，請重新操作一次。", "line_exchange_failed");
            }

            using var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
            var idToken = tokenJson.RootElement.TryGetProperty("id_token", out var idTokenElement) ? idTokenElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(idToken))
            {
                throw new MemberValidationException("LINE 授權已失效，請重新操作一次。", "line_exchange_failed");
            }

            using var verifyResponse = await http.PostAsync(VerifyEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id_token"] = idToken,
                ["client_id"] = ChannelId!,
                ["nonce"] = nonce,
            }), cancellationToken);
            if (!verifyResponse.IsSuccessStatusCode)
            {
                logger.LogWarning("LINE verify 端點回 {Status}", (int)verifyResponse.StatusCode);
                throw new MemberValidationException("LINE 授權驗證失敗，請重新操作一次。", "line_exchange_failed");
            }

            using var verified = JsonDocument.Parse(await verifyResponse.Content.ReadAsStringAsync(cancellationToken));
            var root = verified.RootElement;
            var sub = root.TryGetProperty("sub", out var subElement) ? subElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(sub))
            {
                throw new MemberValidationException("LINE 授權驗證失敗，請重新操作一次。", "line_exchange_failed");
            }

            // 用 verify 端點已驗過 nonce；這裡再比對一次，避免未來有人換掉 verify 的實作時悄悄失去這層檢查。
            var returnedNonce = root.TryGetProperty("nonce", out var nonceElement) ? nonceElement.GetString() : null;
            if (!string.Equals(returnedNonce, nonce, StringComparison.Ordinal))
            {
                throw new MemberValidationException("LINE 授權驗證失敗，請重新操作一次。", "line_exchange_failed");
            }

            return new LineIdentity(
                sub,
                root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null,
                root.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "LINE 登入呼叫失敗");
            throw new MemberValidationException("目前無法連線到 LINE，請稍後再試。", "line_unreachable");
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new FeatureNotConfiguredException("LINE 登入尚未啟用，請先使用 Email 登入。", "line_not_configured");
        }
    }
}
