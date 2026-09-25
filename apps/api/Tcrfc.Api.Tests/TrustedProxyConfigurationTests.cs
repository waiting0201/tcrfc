using System.Net;
using Tcrfc.Api.Security;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-10（審查回饋修正，2026-09-25）：驗證「限流依真實訪客 IP」這件事本身——不透過
/// <c>WebApplicationFactory</c> 打真正 HTTP（那條路徑在 <c>TestServer</c> 底下
/// <c>HttpContext.Connection.RemoteIpAddress</c> 永遠是 <c>null</c>，經實測確認，導致
/// <c>ForwardedHeadersMiddleware</c> 的信任判斷永遠不可能命中，無法在那個環境下驗證
/// 「受信任代理」這條路徑），改呼叫 <see cref="TrustedProxyConfiguration.ResolveEffectiveClientIp"/>
/// ——跟 <c>Program.cs</c> 真正管線用的是同一支 <see cref="TrustedProxyConfiguration.Configure"/>
/// 設定，內部真的建構並執行一次 <c>ForwardedHeadersMiddleware</c>，不是重寫一份邏輯。
///
/// 🔴 本檔刻意不直接 <c>using Microsoft.AspNetCore.Http</c>／<c>HttpOverrides</c>——本測試專案是
/// <c>Microsoft.NET.Sdk</c>（不是 <c>Sdk.Web</c>），這兩個型別的參照解析在這裡有已知問題（見
/// <see cref="TrustedProxyConfiguration.ResolveEffectiveClientIp"/> 上的完整說明），改用該方法
/// 提供的純字串進出介面即可，不需要碰任何 ASP.NET Core 型別。
/// </summary>
public sealed class TrustedProxyConfigurationTests
{
    private const string TrustedProxyIp = "172.28.238.2";

    [Fact]
    public void 受信任代理轉來的XForwardedFor_不同來源IP各自解析出不同的真實IP()
    {
        var visitorA = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(TrustedProxyIp), forwardedFor: "203.0.113.10", trustedProxyIp: TrustedProxyIp);
        var visitorB = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse(TrustedProxyIp), forwardedFor: "203.0.113.20", trustedProxyIp: TrustedProxyIp);

        Assert.Equal("203.0.113.10", visitorA);
        Assert.Equal("203.0.113.20", visitorB);
        Assert.NotEqual(visitorA, visitorB); // 兩個訪客各自的限流分區鍵不同，各有獨立額度。
    }

    [Fact]
    public void 不受信任來源送來的XForwardedFor_整個被忽略_改用連線本身的IP()
    {
        // 連線來源（172.28.238.99）不在 TrustedProxyConfiguration 設定的 KnownProxies 內
        // （只信任 172.28.238.2），即使帶了 X-Forwarded-For，也不應該被採信。
        var untrustedConnectionIp = IPAddress.Parse("172.28.238.99");

        var resolved = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            untrustedConnectionIp, forwardedFor: "203.0.113.99", trustedProxyIp: TrustedProxyIp);

        Assert.Equal(untrustedConnectionIp.ToString(), resolved);
        Assert.NotEqual("203.0.113.99", resolved);
    }

    [Fact]
    public void 未設定TRUSTED_PROXY_IP時_任何XForwardedFor一律不採信_行為等同本機開發與測試環境()
    {
        // 對應 Program.cs 的既有註解：測試／本機 dotnet run 不設定這個環境變數時，
        // KnownProxies 是空集合，中介軟體找不到任何相符來源，維持連線本身的 IP。
        var resolved = TrustedProxyConfiguration.ResolveEffectiveClientIp(
            IPAddress.Parse("127.0.0.1"), forwardedFor: "203.0.113.1", trustedProxyIp: null);

        Assert.Equal("127.0.0.1", resolved);
    }
}
