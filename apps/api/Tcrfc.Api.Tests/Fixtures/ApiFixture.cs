using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 大多數整合測試共用的 fixture：只需要主站庫（mssql-dev），不接 Redis
/// （REDIS_HOST 刻意清空，強制走 <c>NoOpQueryCache</c>，這批測試不驗證快取行為，
/// 快取 fail-open 的驗證見 <see cref="RedisUnavailableApiFixture"/>）。
///
/// 🔴 資料庫不可用時，<see cref="InitializeAsync"/> 直接丟例外——xUnit 會把使用這個 fixture 的
/// 每一個測試都回報為「失敗」並附上這裡的訊息，不會悄悄跳過看起來像通過。
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var connectionString = await TestDatabaseGuard.ResolveAndVerifyAsync();

        // 供 WebApplicationFactory 建立的行程內主機讀取。⚠️ 直接改行程環境變數而不是走
        // ConfigureAppConfiguration，是因為 Program.cs 在 builder.Build() 之前就會讀取
        // REDIS_HOST 來決定要不要注入 RedisQueryCache——那段程式碼跑在測試主機攔截點之前，
        // 用行程環境變數才保證在那個時間點就已經生效（assembly 已停用平行化，不會互相污染）。
        Environment.SetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING", connectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CLUB", TestJwtSigningKey.Value);
        Environment.SetEnvironmentVariable("REDIS_HOST", null);
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", null);
        // 🔴 本輪新增：明確清掉這個變數，不能只靠「反正我沒設定過」。環境變數是行程全域的，
        // xUnit 不保證不同 [Collection] 的 fixture 之間 InitializeAsync 的執行順序——如果
        // AdminWriteApiFixture（會把這個變數設成 "true"）的 InitializeAsync 剛好比這裡先跑，
        // 這個 fixture 建立的 Server 會在還沒被清乾淨的環境變數下啟動，讓「開關關閉」的假設
        // 從一開始就不成立。實際踩過這個坑：加 AdminNews 系列 fixture 前 AdminNewsGateClosedTests
        // 穩定通過，加了之後偶發失敗，才發現是這個順序問題（不是分頁與快取那批既有測試的鍋，
        // 那批完全不碰這個變數）。

        // 立刻建立測試主機（而不是等第一個測試才觸發），確保上面設定的環境變數在 Program.cs
        // 執行的當下就是這個 fixture 要的值。
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }
}
