using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.Security;

/// <summary>
/// S1-10（2026-09-25，審查回饋修正）／S1-17 修正（2026-09-29，多重受信任來源）：設定
/// <see cref="ForwardedHeadersOptions"/>，讓 API 在「多條可能直連 <c>api</c> 的路徑」下都能拿到
/// 訪客真實 IP，而不是誤把某個中繼容器自己的 IP 當成「客戶端」。
///
/// ### 為什麼原本是錯的（S1-10）
/// `deploy/Caddyfile` 已經用 `trusted_proxies`（Cloudflare 官方 IP 段）＋
/// `client_ip_headers CF-Connecting-IP X-Forwarded-For` 解出訪客真實 IP，並在轉送給
/// <c>api:8080</c> 時於 <c>X-Forwarded-For</c> 帶上這個值——但 ASP.NET Core **預設不會信任**
/// 任何 <c>X-Forwarded-For</c> 標頭，<c>HttpContext.Connection.RemoteIpAddress</c> 一律是
/// TCP 連線本身看到的來源，也就是 Caddy 容器的 Docker 內部 IP。S1-10 最初把這個值直接拿去當
/// 依 IP 分區限流的分區鍵，等於**全站訪客共用同一把「Caddy 的 IP」鑰匙**——濫用防護對任何人
/// 都是同一組額度，形同虛設。
///
/// ### S1-17 修正：為什麼從「只信任 Caddy 一個 IP」改成「信任一組固定 IP」
/// 10 表單中心的公開送出端點改由 <c>apps/web</c>（<c>nuxt-tcrfc</c>／<c>nuxt-bw</c> 共用同一個
/// 映像檔，藍鯨只是換配色，見 docs/13 §6）的 Nuxt 伺服器端路由（<c>server/api/backend/[...path].ts</c>）代理轉發到
/// <c>POST /api/v1/{club}/forms/{formCode}/submissions</c>，不是瀏覽器直接呼叫 <c>API_DOMAIN</c>。
/// 這條路徑下，<c>api</c> 看到的 TCP 連線來源變成 <c>nuxt-tcrfc</c>／<c>nuxt-bw</c> 容器自己的
/// Docker 內部 IP，不是 Caddy——如果只信任 Caddy 這一個 IP，這條路徑會退回 S1-10 修正前的狀況
/// （所有訪客共用同一把「nuxt 容器的 IP」鑰匙）。因此改為信任**一組**固定 IP，
/// 而不是放寬成信任整個網段（見下一段「不信任整個網段」，理由不變）：
/// <list type="bullet">
/// <item><c>172.28.238.2</c>——Caddy，服務瀏覽器／App 直接呼叫 <c>API_DOMAIN</c> 或後台 SPA 的路徑</item>
/// <item><c>172.28.238.3</c>——<c>nuxt-tcrfc</c>（主站前台 SSR 容器），代理表單送出</item>
/// <item><c>172.28.238.4</c>——<c>nuxt-bw</c>（藍鯨前台 SSR 容器，與 nuxt-tcrfc 同映像檔），代理表單送出</item>
/// </list>
/// 這三個 IP 是**三條互斥的直連路徑**，不是同一個請求會依序經過的三層代理——任何單一請求
/// 到達 <c>api</c> 時，連線來源只會是其中一個。<c>ForwardLimit</c> 因此仍然維持 <c>1</c>
/// （見下方「為什麼 ForwardLimit 不用跟著調高」），不要誤解成「三層代理要設 3」。
///
/// 🔴 **刻意不包含 <c>nuxt-charity</c>**：慈善捐款平台是獨立產品規劃，「10 表單中心」是主站規劃書
/// §3.10 的主站／藍鯨機制，慈善站台目前沒有已知的等價代理路徑會打這個 Rate Limiting 政策。
/// 若日後慈善前台也新增類似的伺服器端代理轉發到本 API 的公開寫入端點，要重新評估是否該把
/// <c>nuxt-charity</c> 的固定 IP 也加進來——不要假設現狀涵蓋了它。
///
/// ### 為什麼 ForwardLimit 不用跟著調高
/// <c>nuxt-tcrfc</c>／<c>nuxt-bw</c> 的代理路由**只轉發 Caddy 已經解析好、只有單一值的
/// <c>X-Forwarded-For</c>**，不會在自己這一層再往後面加一段（見該路由檔頭「只轉上游代理加的值」）。
/// 也就是說，不論請求是「瀏覽器 → Caddy → api」還是「瀏覽器 → Caddy → nuxt-tcrfc/nuxt-bw → api」，
/// <c>api</c> 收到的 <c>X-Forwarded-For</c> 都只有**一個值**（訪客真實 IP）。
/// <c>ForwardLimit</c> 控制的是「要從標頭裡剝幾層」，跟「KnownProxies 裡列了幾個 IP」是兩件事——
/// 標頭永遠只有一層要剝，維持 <c>ForwardLimit = 1</c> 即可。
///
/// ### 不信任整個網段
/// <c>docker-compose.yml</c> 把 <c>internal</c> 網路釘死一個固定子網段
/// （<c>172.28.238.0/24</c>），並給 <c>proxy</c>／<c>nuxt-tcrfc</c>／<c>nuxt-bw</c> 各自一個固定 IP
/// （經由 <c>TRUSTED_PROXY_IPS</c> 環境變數傳給這裡）。**刻意不用 <c>KnownNetworks</c>／
/// <c>KnownIPNetworks</c> 信任整個子網段**——同一個 Docker 網路裡還有 <c>admin-web</c>／
/// <c>nuxt-charity</c>／<c>redis</c> 等其他容器，若信任整個網段，這些容器（或任何拿到該網段
/// 某個 IP 的東西）理論上都能對 <c>api</c> 發送偽造的 <c>X-Forwarded-For</c> 騙過依 IP 分區的
/// 限流，等同讓「不可信任所有來源」這條防線形同虛設。改用
/// <see cref="ForwardedHeadersOptions.KnownProxies"/> 精確列舉每一個受信任的來源 IP。
///
/// ### 🔴🔴🔴 未設定 <c>TRUSTED_PROXY_IPS</c> 時，中介軟體本身「完全不掛」，不是「掛了但清單是空的」
/// 這是 S1-10 開發時**親自踩到的框架陷阱**，務必記住：<see cref="ForwardedHeadersMiddleware"/>
/// 把 <see cref="ForwardedHeadersOptions.KnownProxies"/>／<see cref="ForwardedHeadersOptions.KnownIPNetworks"/>
/// **兩者都是空集合**視為「呼叫端沒有設定限制」，行為是**信任所有來源**、無條件套用
/// <c>X-Forwarded-For</c>——跟大多數人（包含本檔最初的版本）直覺以為的「空清單＝沒有人受信任、
/// 標頭一律被忽略」剛好相反。若只靠「不設定 <c>TrustedProxyIps</c> 時清單留空」這件事本身當防線，
/// 等於本機開發、測試環境、甚至任何漏設這個環境變數的正式部署都會變成**信任任何人送來的
/// <c>X-Forwarded-For</c>**——比完全沒做這個功能更危險。真正的防線是：<b>只有真的設定了
/// <c>TRUSTED_PROXY_IPS</c>，<c>Program.cs</c> 才會呼叫 <c>app.UseForwardedHeaders()</c>
/// 把這個中介軟體掛進管線</b>；沒設定時中介軟體根本不在管線裡執行，
/// <c>RemoteIpAddress</c> 一定是連線本身看到的值，不會有任何機會被偽造的標頭覆寫。
/// <see cref="ResolveEffectiveClientIp"/> 呼應同一套邏輯，見該方法內的說明。
/// </summary>
public static class TrustedProxyConfiguration
{
    /// <summary>本機開發與正式環境共用同一個鍵名：<c>TRUSTED_PROXY_IPS</c>
    /// （<c>docker-compose.yml</c> 的 <c>api</c> 服務環境變數）。
    /// S1-17 修正起改為**複數**——值是逗號分隔的一組 IP（見類別檔頭），不再是單一 IP 字串。
    /// 舊鍵名 <c>TRUSTED_PROXY_IP</c>（單數）已停用，不做向後相容：本專案還沒有對外部署過
    /// 依賴這個鍵名的環境，改名即改乾淨，不留兩套鍵名互相打架。</summary>
    public const string ConfigKey = "TRUSTED_PROXY_IPS";

    /// <summary>分隔逗號／分號兩種寫法，兩邊留白一律 trim，空字串一律略過。</summary>
    private static readonly char[] Separators = [',', ';'];

    /// <summary><c>Program.cs</c> 用這個判斷要不要呼叫 <c>app.UseForwardedHeaders()</c>——
    /// 見本類別檔頭「未設定時中介軟體本身完全不掛」的完整說明，這是真正的防線所在，
    /// 不是靠 <see cref="Configure"/> 產生的空清單。
    ///
    /// 🔴 S1-17 修正時關掉的一個潛在缺口：判準改成「至少解析出一個合法 IP」
    /// （<c>ParseIps(...).Count &gt; 0</c>），不是單純「字串非空白」。S1-10 原本的版本是
    /// <c>!string.IsNullOrWhiteSpace(trustedProxyIp)</c>——如果設定值是打錯字的非空字串
    /// （例如整段複製貼上打錯一個字元），舊版 <c>IsEnabled</c> 仍會回傳 <c>true</c>、
    /// <c>Program.cs</c> 仍會掛上 <c>UseForwardedHeaders()</c>，但 <see cref="Configure"/> 裡的
    /// <c>IPAddress.TryParse</c> 會失敗、<c>KnownProxies</c> 最終是空集合——**這正好撞上類別檔頭
    /// 說的「空的 KnownProxies＝信任所有來源」陷阱**，形同開了後門。現在的判準確保「有掛中介軟體」
    /// 與「KnownProxies 至少有一個合法項目」這兩件事一定同時成立或同時不成立。</summary>
    public static bool IsEnabled(string? trustedProxyIps) => ParseIps(trustedProxyIps).Count > 0;

    public static void Configure(ForwardedHeadersOptions options, string? trustedProxyIps)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;

        // 每一條受信任路徑（Caddy 直連、或經 nuxt-tcrfc／nuxt-bw 代理）到達 api 時，
        // X-Forwarded-For 都只帶單一值（見類別檔頭「為什麼 ForwardLimit 不用跟著調高」），
        // 不因為 KnownProxies 列了不只一個 IP 就需要調高——這裡寫明白是為了讓「這件事已經想過、
        // 不是漏改」不必回頭重新推導一次。
        options.ForwardLimit = 1;

        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var ip in ParseIps(trustedProxyIps))
        {
            options.KnownProxies.Add(ip);
        }
    }

    private static List<IPAddress> ParseIps(string? trustedProxyIps)
    {
        if (string.IsNullOrWhiteSpace(trustedProxyIps))
        {
            return [];
        }

        var result = new List<IPAddress>();
        foreach (var raw in trustedProxyIps.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPAddress.TryParse(raw, out var parsed))
            {
                result.Add(parsed);
            }
            // 解析失敗的項目刻意略過（不丟例外）：比照原本單一 IP 版本的行為——設定值打錯字
            // 不該讓整個 api 啟動失敗，代價是那個沒解析成功的來源就是「沒被信任」，方向是安全的
            // （寧可少信任一個該信任的來源，也不要多信任一個不該信任的來源）。
        }
        return result;
    }

    /// <summary>
    /// 純函式版本，供 <c>Tcrfc.Api.Tests</c> 直接驗證「這個連線來源＋這個 X-Forwarded-For」實際
    /// 會被解析成什麼客戶端 IP，不需要透過完整 HTTP 主機。**行為刻意對齊 <c>Program.cs</c> 的
    /// 真實管線**：<paramref name="trustedProxyIps"/> 未設定時直接回傳
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
    public static string ResolveEffectiveClientIp(IPAddress connectionRemoteIp, string? forwardedFor, string? trustedProxyIps)
    {
        if (!IsEnabled(trustedProxyIps))
        {
            return connectionRemoteIp.ToString();
        }

        var options = new ForwardedHeadersOptions();
        Configure(options, trustedProxyIps);

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
