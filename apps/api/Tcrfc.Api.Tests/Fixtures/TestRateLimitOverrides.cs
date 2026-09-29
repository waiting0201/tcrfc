using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Tcrfc.Api.Features.AdminAuth;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 2026-09-29（後台登入端點依 IP 限流，第二次修正——回應「不要用行程全域環境變數覆寫額度」的
/// 回饋）：`admin-login`／`admin-refresh` 兩個政策的正式環境預設額度改成嚴格數字（每 IP 每分鐘
/// 5／30，見 <see cref="AdminAuthRateLimitOptions"/>）之後，一般用途的整合測試 fixture 不能再
/// 讓這兩個政策用正式環境的嚴格額度運作——<c>TestServer</c> 底下所有請求共用同一個「unknown」
/// IP 分區（見 <c>ClientIpResolver</c> 檔頭），任何一支測試檔只要在 1 分鐘內對 <c>/login</c>
/// 打超過 5 次就會被 429 擋下。
///
/// ── 這裡改用 <see cref="IWebHostBuilder.ConfigureAppConfiguration"/>，不是
/// <see cref="Environment.SetEnvironmentVariable"/> ──
/// **第一版**用 <c>Environment.SetEnvironmentVariable</c> 覆寫，這是「行程全域」狀態：同一個
/// <c>dotnet test</c> 行程裡的每一個 <c>WebApplicationFactory</c> 讀的、寫的都是同一份系統環境
/// 變數。這組覆寫需要**兩種互斥的值**同時存在（一般測試要寬鬆值 <see cref="LoosePermitLimit"/>，
/// 驗證「額度用盡回 429」的測試要一個很小的值，見
/// <see cref="AdminAuthRateLimitTestApiFixture"/>）——用行程全域變數做這件事，一旦兩種 fixture
/// 的初始化時機有任何重疊（例如平行執行、或初始化時序調整），後寫入的值會覆蓋先寫入的值，
/// 造成間歇性失敗。**本測試組件目前用 <c>[assembly: CollectionBehavior(DisableTestParallelization
/// = true)]</c>（見 <c>AssemblyInfo.cs</c>）停用了整個組件的平行執行，讓這個問題在目前設定下不會
/// 真的發生**——但這是仰賴一個組件層級的全域開關，不是這組設定本身該有的隔離範圍，也是這裡改掉
/// 的直接原因（詳見 <c>apps/api/README.md</c>「S1-18d」段對既有 `REDIS_HOST` 等覆寫的殘留風險
/// 說明）。
///
/// 改用 <see cref="IWebHostBuilder.ConfigureAppConfiguration"/> 加入的 in-memory 設定來源後，
/// 每個 <c>WebApplicationFactory</c> 的覆寫值只存在於**該實例自己建出來的 <see cref="IConfiguration"/>
/// 物件**（透過該主機的 DI 容器解析），不是行程全域狀態——即使兩個 fixture 真的並行初始化，也不會
/// 互相干擾，不必依賴 <c>DisableTestParallelization</c> 維持不變。
///
/// ── 為什麼 `Program.cs` 讀取額度的時機來得及看到這裡的覆寫（已實測驗證，不是憑印象判斷）──
/// `Program.cs` 對這兩個政策的額度解析是**惰性**的：只有在真的有 HTTP 請求打進
/// `admin-login`／`admin-refresh`、且是該分區鍵第一次出現時，<c>FixedWindowRateLimiterOptions</c>
/// 的 <c>PermitLimit</c> 才會被讀取一次——這個時間點遠晚於 <c>WebApplicationFactory</c> 完成
/// <c>ConfigureWebHost</c>、整個 <c>IHost</c> 建置完成之後（跟 <c>REDIS_HOST</c> 在 `Program.cs`
/// 頂層、`builder.Build()` **之前**就同步讀取、決定 DI 要注入哪個 `IQueryCache` 實作，時機完全
/// 不同——那個情境下 `ConfigureAppConfiguration` 確實太晚生效，這裡不是）。`Program.cs` 改讀
/// <c>httpContext.RequestServices.GetRequiredService&lt;IConfiguration&gt;()</c>（DI 容器裡
/// `Build()` 完成後的那一份，保證含有這裡加入的設定來源）。**已用一個獨立的最小重現專案實測
/// 驗證**（不是在本測試專案本身跑，因為本測試專案的每個 fixture 都需要真的資料庫連線，沒辦法
/// 快速反覆驗證這個框架行為）：① 完全比照 `Program.cs` 頂層、`builder.Build()` 之前的同步讀取，
/// 確認讀不到 `ConfigureAppConfiguration` 的覆寫（跟 `REDIS_HOST` 情境一致，三個測試方法都通過）；
/// ② 從 `httpContext.RequestServices` 解析 `IConfiguration` 的惰性讀取，確認讀得到覆寫——
/// 本檔案採用的就是這個讀法。
/// </summary>
public static class TestRateLimitOverrides
{
    /// <summary>
    /// 刻意選一個遠高於任何合理測試呼叫次數的數字（既有測試實測頂多幾十次呼叫）。
    /// 不用 <see cref="int.MaxValue"/> 之類的極端值，避免哪天真的有測試需要驗證「這個數字本身
    /// 有沒有被讀到」時，看到一個明顯不像業務數字的極端值反而不容易判斷是不是設定錯誤。
    /// </summary>
    public const string LoosePermitLimit = "100000";

    /// <summary>
    /// 一般整合測試 fixture 覆寫 <see cref="WebApplicationFactory{TEntryPoint}.ConfigureWebHost"/>
    /// 時呼叫——把兩個政策的額度都調到遠高於任何合理測試呼叫次數，讓它們在一般整合測試裡形同
    /// 「不限流」。真正需要驗證「額度用盡後回 429」這個行為的測試，**不要用這個方法**，改用
    /// <see cref="ApplyAdminAuthOverrides"/> 搭配一個很小的專用額度（見
    /// <see cref="AdminAuthRateLimitTestApiFixture"/>）。
    /// </summary>
    public static void ApplyLooseAdminAuthOverrides(IWebHostBuilder builder)
        => ApplyAdminAuthOverrides(builder, LoosePermitLimit);

    /// <summary>把 <c>admin-login</c>／<c>admin-refresh</c> 兩個政策的額度都覆寫成
    /// <paramref name="permitLimit"/>，只影響呼叫端這一個 <see cref="IWebHostBuilder"/>
    /// 建出來的測試主機，不是行程全域環境變數。</summary>
    public static void ApplyAdminAuthOverrides(IWebHostBuilder builder, string permitLimit)
    {
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AdminAuthRateLimitOptions.LoginPermitLimitConfigKey] = permitLimit,
                [AdminAuthRateLimitOptions.RefreshPermitLimitConfigKey] = permitLimit,
            });
        });
    }
}
