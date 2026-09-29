using System.Net;
using Tcrfc.Api.Security;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-10（審查回饋修正，2026-09-25）／S1-17 修正（2026-09-29，多重受信任來源）：驗證「限流依
/// 真實訪客 IP」這件事本身——不透過 <c>WebApplicationFactory</c> 打真正 HTTP（那條路徑在
/// <c>TestServer</c> 底下 <c>HttpContext.Connection.RemoteIpAddress</c> 永遠是 <c>null</c>，
/// 經實測確認，導致 <c>ForwardedHeadersMiddleware</c> 的信任判斷永遠不可能命中，無法在那個環境下
/// 驗證「受信任代理」這條路徑），改呼叫
/// <see cref="TrustedProxyConfiguration.ResolveEffectiveClientIp"/>——跟 <c>Program.cs</c> 真正
/// 管線用的是同一支 <see cref="TrustedProxyConfiguration.Configure"/> 設定，內部真的建構並執行
/// 一次 <c>ForwardedHeadersMiddleware</c>，不是重寫一份邏輯。
///
/// 🔴 本檔刻意不直接 <c>using Microsoft.AspNetCore.Http</c>／<c>HttpOverrides</c>——本測試專案是
/// <c>Microsoft.NET.Sdk</c>（不是 <c>Sdk.Web</c>），這兩個型別的參照解析在這裡有已知問題（見
/// <see cref="TrustedProxyConfiguration.ResolveEffectiveClientIp"/> 上的完整說明），改用該方法
/// 提供的純字串進出介面即可，不需要碰任何 ASP.NET Core 型別。
///
/// S1-17 修正新增的情境：10 表單中心公開送出改由 <c>nuxt-tcrfc</c>／<c>nuxt-bw</c> 的 Nuxt 伺服器端
/// 路由代理轉發，<c>api</c> 因此多了「經 Nuxt 容器代轉」這條路徑，需要驗證：① 經這條路徑送來的
/// 請求也能正確解出訪客真實 IP（不再退回 S1-10 修正前「全站共用一把 nuxt 容器 IP」的狀況）；
/// ② 不在信任清單內的容器（模擬 <c>nuxt-charity</c>／<c>admin-web</c> 之類刻意不信任的來源）
/// 送來的偽造標頭仍然不被採信；③ 同一個訪客不論走哪一條受信任路徑，解析出的 IP 一致
/// （限流分區鍵不會因為走了不同路徑而變成兩個人）。
/// </summary>
public sealed class TrustedProxyConfigurationTests
{
    private const string CaddyIp = "172.28.238.2";
    private const string NuxtTcrfcIp = "172.28.238.3";
    private const string NuxtBwIp = "172.28.238.4";

    /// <summary>對齊 <c>docker-compose.yml</c> 的 <c>api</c> 服務 <c>TRUSTED_PROXY_IPS</c> 實際值。</summary>
    private const string TrustedProxyIps = $"{CaddyIp},{NuxtTcrfcIp},{NuxtBwIp}";

    [Fact]
    public void 受信任代理轉來的XForwardedFor_不同來源IP各自解析出不同的真實IP()
    {
        var visitorA = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(CaddyIp), forwardedFor: "203.0.113.10", trustedProxyIps: TrustedProxyIps);
        var visitorB = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(CaddyIp), forwardedFor: "203.0.113.20", trustedProxyIps: TrustedProxyIps);

        Assert.Equal("203.0.113.10", visitorA);
        Assert.Equal("203.0.113.20", visitorB);
        Assert.NotEqual(visitorA, visitorB); // 兩個訪客各自的限流分區鍵不同，各有獨立額度。
    }

    [Fact]
    public void 經NuxtTcrfc代理轉來的XForwardedFor_也能正確解析出訪客真實IP()
    {
        // 模擬 S1-17 起「10 表單中心公開送出改由 nuxt-tcrfc 的伺服器端路由代理轉發」這條新路徑：
        // api 看到的 TCP 連線來源是 nuxt-tcrfc 的固定 IP（172.28.238.3），不是 Caddy，
        // 但 nuxt-tcrfc 只轉發 Caddy 已解析好的單一值 X-Forwarded-For（不會疊加自己的 IP）。
        var resolved = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(NuxtTcrfcIp), forwardedFor: "203.0.113.30", trustedProxyIps: TrustedProxyIps);

        Assert.Equal("203.0.113.30", resolved);
    }

    [Fact]
    public void 經NuxtBw代理轉來的XForwardedFor_也能正確解析出訪客真實IP()
    {
        var resolved = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(NuxtBwIp), forwardedFor: "203.0.113.40", trustedProxyIps: TrustedProxyIps);

        Assert.Equal("203.0.113.40", resolved);
    }

    [Fact]
    public void 同一個訪客不論經Caddy直連或經Nuxt代理_解析出的真實IP一致_限流分區鍵不會因路徑不同而分裂()
    {
        // 同一個訪客真實 IP，一次模擬「瀏覽器直打 API_DOMAIN → Caddy → api」，一次模擬
        // 「瀏覽器打頁面 → Caddy → nuxt-tcrfc 代理 → api」，兩條路徑到達 api 時的 TCP 連線來源
        // 不同（Caddy vs nuxt-tcrfc），但都應該解析回同一個真實 IP——否則同一個人會在兩條路徑上
        // 分別累積兩份獨立的限流額度，形同放寬了防護強度。
        var viaCaddy = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(CaddyIp), forwardedFor: "203.0.113.50", trustedProxyIps: TrustedProxyIps);
        var viaNuxt = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(NuxtTcrfcIp), forwardedFor: "203.0.113.50", trustedProxyIps: TrustedProxyIps);

        Assert.Equal("203.0.113.50", viaCaddy);
        Assert.Equal(viaCaddy, viaNuxt);
    }

    [Fact]
    public void 不受信任來源送來的XForwardedFor_整個被忽略_改用連線本身的IP()
    {
        // 連線來源（172.28.238.99）不在 TrustedProxyConfiguration 設定的 KnownProxies 內
        // （只信任 Caddy／nuxt-tcrfc／nuxt-bw 三個固定 IP），即使帶了 X-Forwarded-For，
        // 也不應該被採信。
        var untrustedConnectionIp = IPAddress.Parse("172.28.238.99");

        var resolved = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            untrustedConnectionIp, forwardedFor: "203.0.113.99", trustedProxyIps: TrustedProxyIps);

        Assert.Equal(untrustedConnectionIp.ToString(), resolved);
        Assert.NotEqual("203.0.113.99", resolved);
    }

    [Fact]
    public void 刻意不信任的NuxtCharity容器_偽造XForwardedFor不被採信()
    {
        // S1-17 修正刻意不把 nuxt-charity 列入信任清單（慈善站台目前沒有已知的等價代理路徑），
        // 模擬它（或任何拿到這個網段某個 IP 的容器／訪客）嘗試偽造 X-Forwarded-For 騙過限流。
        var nuxtCharityIp = IPAddress.Parse("172.28.238.5");

        var resolved = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            nuxtCharityIp, forwardedFor: "203.0.113.66", trustedProxyIps: TrustedProxyIps);

        Assert.Equal(nuxtCharityIp.ToString(), resolved);
        Assert.NotEqual("203.0.113.66", resolved);
    }

    [Fact]
    public void 訪客直接偽造XForwardedFor送到未受信任的連線來源_不被採信()
    {
        // 「訪客直接偽造 XFF」的情境：連線本身不是任何受信任的代理（例如訪客想像中「直接打
        // api 容器」），無論 X-Forwarded-For 寫什麼都不該被採信——這是 ForwardedHeadersMiddleware
        // 的信任判斷只看「連線來源是不是 KnownProxies 之一」，不看標頭本身內容的直接體現。
        var spoofedConnectionIp = IPAddress.Parse("198.51.100.1");

        var resolved = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            spoofedConnectionIp, forwardedFor: "203.0.113.1, 203.0.113.2", trustedProxyIps: TrustedProxyIps);

        Assert.Equal(spoofedConnectionIp.ToString(), resolved);
    }

    [Fact]
    public void 未設定TRUSTED_PROXY_IPS時_任何XForwardedFor一律不採信_行為等同本機開發與測試環境()
    {
        // 對應 Program.cs 的既有註解：測試／本機 dotnet run 不設定這個環境變數時，
        // KnownProxies 是空集合，中介軟體找不到任何相符來源，維持連線本身的 IP。
        var resolved = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse("127.0.0.1"), forwardedFor: "203.0.113.1", trustedProxyIps: null);

        Assert.Equal("127.0.0.1", resolved);
    }

    [Fact]
    public void 設定值只有打錯字的非空字串時_視同未設定_不會半信任卻清單是空的()
    {
        // 關掉的潛在缺口（見 TrustedProxyConfiguration.IsEnabled 檔頭「S1-17 修正時關掉的一個
        // 潛在缺口」）：如果只用「字串非空白」當判準，這種打錯字的設定值會讓 Program.cs 誤以為
        // 「有設定」而掛上 UseForwardedHeaders()，但 Configure 內部 IPAddress.TryParse 全部失敗、
        // KnownProxies 最終是空集合——空的 KnownProxies 對 ForwardedHeadersMiddleware 而言是
        // 「信任所有來源」，等於開了後門。現在的判準是「至少解析出一個合法 IP」，這裡驗證
        // IsEnabled 對這種輸入正確回傳 false（不會掛上中介軟體），且 ResolveEffectiveClientIp
        // 的行為與「完全未設定」一致。
        const string malformed = "這不是一個IP位址";

        Assert.False(TrustedProxyConfiguration.IsEnabled(malformed));

        var resolved = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(CaddyIp), forwardedFor: "203.0.113.1", trustedProxyIps: malformed);

        Assert.Equal(CaddyIp, resolved); // 沒有被偽造的標頭覆寫，維持連線本身的 IP。
    }

    [Fact]
    public void 清單裡混一個打錯字的項目時_其餘合法IP仍然生效()
    {
        // 逗號分隔清單裡若有一項解析失敗，不該讓整個清單失效——只有那一項不生效，其餘合法項目
        // 正常運作。驗證 IsEnabled 仍為 true，且合法的那個 IP（Caddy）仍能正確解析。
        var mixedList = $"{CaddyIp},not-an-ip,{NuxtTcrfcIp}";

        Assert.True(TrustedProxyConfiguration.IsEnabled(mixedList));

        var resolved = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(CaddyIp), forwardedFor: "203.0.113.77", trustedProxyIps: mixedList);

        Assert.Equal("203.0.113.77", resolved);
    }

    [Fact]
    public void 清單允許逗號與分號混用且兩側留白會被trim()
    {
        var spaced = $"  {CaddyIp} ; {NuxtTcrfcIp}  ,{NuxtBwIp} ";

        var resolvedViaCaddy = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(CaddyIp), forwardedFor: "203.0.113.80", trustedProxyIps: spaced);
        var resolvedViaBw = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(NuxtBwIp), forwardedFor: "203.0.113.81", trustedProxyIps: spaced);

        Assert.Equal("203.0.113.80", resolvedViaCaddy);
        Assert.Equal("203.0.113.81", resolvedViaBw);
    }
}
