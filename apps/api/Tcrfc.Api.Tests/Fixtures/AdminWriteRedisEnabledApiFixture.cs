using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using StackExchange.Redis;
using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 本輪新增：驗證「寫入端點成功後，公開讀取 API 的 Redis 快取真的被失效」——單靠
/// <see cref="AdminWriteApiFixture"/>（<c>REDIS_HOST</c> 清空、走 no-op 快取）驗不到這件事，
/// no-op 快取永遠回源，不能證明「有寫入導致快取被清」跟「本來就沒有快取」的差別。
/// 把 <see cref="AdminWriteApiFixture"/> 與 <see cref="RedisEnabledApiFixture"/> 兩者的設定合併：
/// 真正在跑的 <c>redis-server</c> 子行程（不 mock）＋ <c>ENABLE_UNSAFE_DEV_WRITES=true</c>。
/// </summary>
public sealed class AdminWriteRedisEnabledApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string RedisPassword = "tcrfc-test-only";
    private Process? _redisProcess;

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
        Environment.SetEnvironmentVariable("ENABLE_UNSAFE_DEV_WRITES", "true");
        Environment.SetEnvironmentVariable("REDIS_HOST", "127.0.0.1");
        Environment.SetEnvironmentVariable("REDIS_PORT", port.ToString());
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", RedisPassword);

        RedisInspector = await ConnectionMultiplexer.ConnectAsync($"127.0.0.1:{port},password={RedisPassword}");

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
            "/opt/homebrew/bin/redis-server",
            "/usr/local/bin/redis-server",
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
                return;
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
