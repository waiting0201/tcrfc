using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 專門驗證「Redis 掛掉不得讓請求失敗」（docs/17-deployment.md §4 硬規則 1）的 fixture。
/// 把 <c>REDIS_HOST</c>／<c>REDIS_PORT</c> 指向一個確定沒有任何服務在聽的本機連接埠
/// （先短暫綁定 <see cref="TcpListener"/> 取得一個目前閒置的埠號再立刻釋放），這樣
/// <c>RedisQueryCache</c> 真的會拿到連線失敗，而不是靠猜一個「應該沒人用」的埠號。
/// </summary>
public sealed class RedisUnavailableApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CLUB_SQL_CONNECTION_STRING 未設定，無法執行整合測試（見 ApiFixture 的說明與 apps/api/README.md）。");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"CLUB_SQL_CONNECTION_STRING 已設定但連不上本機資料庫（{ex.Message}）。", ex);
        }

        var unusedPort = GetUnusedLocalPort();

        Environment.SetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING", connectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CLUB", TestJwtSigningKey.Value);
        Environment.SetEnvironmentVariable("REDIS_HOST", "127.0.0.1");
        Environment.SetEnvironmentVariable("REDIS_PORT", unusedPort.ToString());
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", "unused-in-this-test");
        // 見 ApiFixture 同一行的註解：這個 fixture 也不該讓寫入端點開著，明確清掉，不依賴「反正
        // 沒設定過」——不同 [Collection] 的 fixture 之間 InitializeAsync 順序不保證。

        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    private static int GetUnusedLocalPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            // 立刻釋放：測試要的是「這個埠現在沒人聽」，不是真的佔用它——佔用了反而會變成
            // TCP 連線被接受但沒人回應，逾時行為會跟「連線被拒絕」不一樣，改變了驗證的情境。
            listener.Stop();
        }
    }
}
