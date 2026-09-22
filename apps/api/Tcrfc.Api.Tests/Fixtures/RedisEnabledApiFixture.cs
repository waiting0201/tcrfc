using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using StackExchange.Redis;
using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 驗證「Redis 真的正常運作時，快取行為是否符合設計」的 fixture（S0-7d 續作，2026-09-21）——
/// 跟 <see cref="RedisUnavailableApiFixture"/>（驗證 Redis 掛掉的 fail-open）互補，這裡驗證
/// key 命名、qualifier 是否涵蓋每個參數、club／locale 維度是否真的隔離這些「Redis 正常時該長
/// 什麼樣」的行為。
///
/// 🔴 真的啟動一個 <c>redis-server</c> 子行程（沿用上次任務用 <c>brew install redis</c> 裝的
/// 那個二進位檔，繞過這個沙盒環境 Docker Desktop 拉 <c>redis:8-alpine</c> 極慢的問題），
/// 不 mock Redis 用戶端——跟本專案「不 mock 資料庫」的既有紀律一致。
/// </summary>
public sealed class RedisEnabledApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string RedisPassword = "tcrfc-test-only";
    private Process? _redisProcess;

    /// <summary>
    /// 給測試直接檢查 key／TTL 用的獨立連線——跟應用程式自己那條連線分開，純粹用來斷言，
    /// 不會影響應用程式那一側的快取行為。
    /// </summary>
    public IConnectionMultiplexer RedisInspector { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CLUB_SQL_CONNECTION_STRING 未設定，無法執行整合測試。請先啟動本機資料庫並灌種子資料"
                + "（見 apps/api/README.md「怎麼跑」），再 export CLUB_SQL_CONNECTION_STRING 後重跑 dotnet test。");
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

        var redisServerPath = ResolveRedisServerPath();
        var port = GetUnusedLocalPort();

        _redisProcess = Process.Start(new ProcessStartInfo
        {
            FileName = redisServerPath,
            ArgumentList =
            {
                "--port", port.ToString(),
                "--requirepass", RedisPassword,
                "--save", "",
                "--appendonly", "no",
                "--daemonize", "no",
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException($"無法啟動 {redisServerPath}。");

        await WaitForRedisReadyAsync(port);

        Environment.SetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING", connectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("REDIS_HOST", "127.0.0.1");
        Environment.SetEnvironmentVariable("REDIS_PORT", port.ToString());
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", RedisPassword);
        // 見 ApiFixture 同一行的註解：這個 fixture 不驗證寫入端點，明確清掉、不依賴「反正沒設定過」。
        Environment.SetEnvironmentVariable("ENABLE_UNSAFE_DEV_WRITES", null);

        RedisInspector = await ConnectionMultiplexer.ConnectAsync($"127.0.0.1:{port},password={RedisPassword}");

        // 立刻建立測試主機，確保上面設定的環境變數在 Program.cs 執行的當下就是這個 fixture 要的值
        // （原因見 ApiFixture 的同一段註解：Program.cs 在 builder.Build() 之前就讀 REDIS_HOST）。
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        try
        {
            RedisInspector?.Dispose();
        }
        catch
        {
            // 收尾動作，不讓 dispose 例外掩蓋測試本身的結果。
        }

        if (_redisProcess is { HasExited: false })
        {
            _redisProcess.Kill(entireProcessTree: true);
            await _redisProcess.WaitForExitAsync();
        }

        _redisProcess?.Dispose();
    }

    private static string ResolveRedisServerPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("REDIS_SERVER_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        string[] candidates =
        [
            "/opt/homebrew/bin/redis-server", // Apple Silicon brew
            "/usr/local/bin/redis-server", // Intel brew／一般 Linux
        ];

        var found = candidates.FirstOrDefault(File.Exists);
        if (found is not null)
        {
            return found;
        }

        throw new InvalidOperationException(
            "找不到 redis-server 執行檔——這組測試需要一個真正在跑的 Redis 行程（不 mock）。"
            + "請先 `brew install redis`，或設定環境變數 REDIS_SERVER_PATH 指到 redis-server 執行檔。");
    }

    private static async Task WaitForRedisReadyAsync(int port)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                return; // 能建立 TCP 連線就視為 redis-server 已經在聽。
            }
            catch
            {
                await Task.Delay(100);
            }
        }

        throw new InvalidOperationException($"redis-server 在 5 秒內沒有開始監聽 127.0.0.1:{port}。");
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
            listener.Stop();
        }
    }
}
