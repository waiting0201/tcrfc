using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.Security;

/// <summary>
/// S1-10（2026-09-25，審查回饋修正）：設定 <see cref="ForwardedHeadersOptions"/>，讓 API 在
/// Cloudflare → Caddy → api 這條鏈路下能拿到訪客真實 IP，而不是誤把 Caddy 容器自己的 IP
/// 當成「客戶端」。
///
/// ### 為什麼原本是錯的
/// `deploy/Caddyfile` 已經用 `trusted_proxies`（Cloudflare 官方 IP 段）＋
/// `client_ip_headers CF-Connecting-IP X-Forwarded-For` 解出訪客真實 IP，並在轉送給
/// <c>api:8080</c> 時於 <c>X-Forwarded-For</c> 帶上這個值——但 ASP.NET Core **預設不會信任**
/// 任何 <c>X-Forwarded-For</c> 標頭，<c>HttpContext.Connection.RemoteIpAddress</c> 一律是
/// TCP 連線本身看到的來源，也就是 Caddy 容器的 Docker 內部 IP。S1-10 最初把這個值直接拿去當
/// 依 IP 分區限流的分區鍵，等於**全站訪客共用同一把「Caddy 的 IP」鑰匙**——濫用防護對任何人
/// 都是同一組額度，形同虛設。
///
/// ### 只信任「這一個」IP，不是整個網段
/// <c>docker-compose.yml</c> 把 <c>internal</c> 網路釘死一個固定子網段
/// （<c>172.28.238.0/24</c>），並給 <c>proxy</c>（Caddy）服務一個固定 IP
/// （<c>172.28.238.2</c>，經由 <c>TRUSTED_PROXY_IP</c> 環境變數傳給這裡）。**刻意不用
/// <c>KnownNetworks</c>／<c>KnownIPNetworks</c> 信任整個子網段**——同一個 Docker 網路裡還有
/// <c>nuxt-tcrfc</c>／<c>admin-web</c> 等其他容器，若信任整個網段，這些容器（或任何拿到該網段
/// 某個 IP 的東西）理論上都能對 <c>api</c> 發送偽造的 <c>X-Forwarded-For</c> 騙過依 IP 分區的
/// 限流，等同讓「不可信任所有來源」這條防線形同虛設。改用
/// <see cref="ForwardedHeadersOptions.KnownProxies"/> 精確指定唯一一個受信任的來源 IP。
///
/// ### 🔴🔴🔴 未設定 <c>TRUSTED_PROXY_IP</c> 時，中介軟體本身「完全不掛」，不是「掛了但清單是空的」
/// 這是本輪開發時**親自踩到的框架陷阱**，務必記住：<see cref="ForwardedHeadersMiddleware"/>
/// 把 <see cref="ForwardedHeadersOptions.KnownProxies"/>／<see cref="ForwardedHeadersOptions.KnownIPNetworks"/>
/// **兩者都是空集合**視為「呼叫端沒有設定限制」，行為是**信任所有來源**、無條件套用
/// <c>X-Forwarded-For</c>——跟大多數人（包含本檔最初的版本）直覺以為的「空清單＝沒有人受信任、
/// 標頭一律被忽略」剛好相反。若只靠「不設定 <c>TrustedProxyIp</c> 時清單留空」這件事本身當防線，
/// 等於本機開發、測試環境、甚至任何漏設這個環境變數的正式部署都會變成**信任任何人送來的
/// <c>X-Forwarded-For</c>**——比完全沒做這個功能更危險。真正的防線是：<b>只有真的設定了
/// <c>TRUSTED_PROXY_IP</c>，<c>Program.cs</c> 才會呼叫 <c>app.UseForwardedHeaders()</c>
/// 把這個中介軟體掛進管線</b>；沒設定時中介軟體根本不在管線裡執行，
/// <c>RemoteIpAddress</c> 一定是連線本身看到的值，不會有任何機會被偽造的標頭覆寫。
/// <see cref="ResolveEffectiveClientIp"/> 呼應同一套邏輯，見該方法內的說明。
/// </summary>
public static class TrustedProxyConfiguration
{
    /// <summary>本機開發與正式環境共用同一個鍵名：<c>TRUSTED_PROXY_IP</c>
    /// （<c>docker-compose.yml</c> 的 <c>api</c> 服務環境變數）。</summary>
    public const string ConfigKey = "TRUSTED_PROXY_IP";

    /// <summary><c>Program.cs</c> 用這個判斷要不要呼叫 <c>app.UseForwardedHeaders()</c>——
    /// 見本類別檔頭「未設定時中介軟體本身完全不掛」的完整說明，這是真正的防線所在，
    /// 不是靠 <see cref="Configure"/> 產生的空清單。</summary>
    public static bool IsEnabled(string? trustedProxyIp) => !string.IsNullOrWhiteSpace(trustedProxyIp);

    public static void Configure(ForwardedHeadersOptions options, string? trustedProxyIp)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;

        // 只有一層代理（Caddy）在 API 與外界之間，不需要往前多跳（ForwardLimit 預設就是 1，
        // 這裡寫明白是為了讓「這條鏈路只有一個受信任跳點」這件事不必回頭查框架預設值）。
        options.ForwardLimit = 1;

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        if (!string.IsNullOrWhiteSpace(trustedProxyIp) && IPAddress.TryParse(trustedProxyIp, out var proxyIp))
        {
            options.KnownProxies.Add(proxyIp);
        }
    }

    /// <summary>
    /// 純函式版本，供 <c>Tcrfc.Api.Tests</c> 直接驗證「這個連線來源＋這個 X-Forwarded-For」實際
    /// 會被解析成什麼客戶端 IP，不需要透過完整 HTTP 主機。**行為刻意對齊 <c>Program.cs</c> 的
    /// 真實管線**：<paramref name="trustedProxyIp"/> 未設定時直接回傳
    /// <paramref name="connectionRemoteIp"/>，完全不建構或呼叫
    /// <see cref="ForwardedHeadersMiddleware"/>——因為 <c>Program.cs</c> 在這種情況下根本不會把
    /// 這個中介軟體掛進管線（見本類別檔頭「未設定時中介軟體本身完全不掛」），如果這裡改成
    /// 「建構一個 <c>KnownProxies</c> 是空集合的中介軟體再呼叫」，因為框架本身的陷阱行為
    /// （空清單＝信任所有來源），會得到跟真實管線不一致、而且錯誤地看似安全的測試結果。
    ///
    /// 🔴 <c>Tcrfc.Api.Tests</c> 是 <c>Microsoft.NET.Sdk</c>（不是 <c>Sdk.Web</c>），實測發現
    /// 即使明確加 <c>&lt;FrameworkReference Include="Microsoft.AspNetCore.App" /&gt;</c>，
    /// <c>ResolveTargetingPackAssets</c> 仍能在中繼輸出看到
    /// <c>Microsoft.AspNetCore.HttpOverrides.dll</c>，卻不會出現在最終傳給 <c>csc</c> 的
    /// <c>-reference</c> 清單裡（原因不明，懷疑是 RAR 衝突解決或套件裁剪管線的交互作用），
    /// 導致測試專案無法直接 <c>using Microsoft.AspNetCore.HttpOverrides;</c>。改把「建構
    /// <see cref="ForwardedHeadersMiddleware"/> 並跑一次」這個動作留在本專案（<c>Sdk.Web</c>，
    /// 這些型別本來就完整可用），只讓測試專案呼叫這支回傳 <see cref="string"/> 的方法——測試專案
    /// 因此完全不需要參照 <c>Microsoft.AspNetCore.Http</c>／<c>HttpOverrides</c> 任何型別，
    /// 只需要 BCL 的 <see cref="IPAddress"/>，繞開整個參照解析問題，見
    /// <c>Tcrfc.Api.Tests/TrustedProxyConfigurationTests.cs</c> 檔頭的完整說明。
    /// </summary>
    public static string ResolveEffectiveClientIp(IPAddress connectionRemoteIp, string? forwardedFor, string? trustedProxyIp)
    {
        if (!IsEnabled(trustedProxyIp))
        {
            return connectionRemoteIp.ToString();
        }

        var options = new ForwardedHeadersOptions();
        Configure(options, trustedProxyIp);

        string? resolved = null;
        RequestDelegate terminal = context =>
        {
            resolved = ClientIpResolver.Resolve(context);
            return Task.CompletedTask;
        };

        var middleware = new ForwardedHeadersMiddleware(terminal, NullLoggerFactory.Instance, Options.Create(options));

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = connectionRemoteIp;
        if (forwardedFor is not null)
        {
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        }

        middleware.Invoke(context).GetAwaiter().GetResult();

        return resolved ?? throw new InvalidOperationException("終端 delegate 沒有被呼叫到，ForwardedHeadersMiddleware 的行為與預期不符。");
    }
}
