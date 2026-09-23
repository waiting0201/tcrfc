using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// `AdminNews`／`AdminAuth` 相關測試共用的 fixture。⚠️ 不要把這個 fixture 拿去給既有的五組
/// 唯讀測試共用——分開是為了讓「純唯讀端點」與「需要登入與授權的端點」各自的測試資料互不干擾，
/// 不是因為有任何開發模式旗標需要區分（那個機制已於 2026-09-23 移除，見 apps/api/README.md「S1」）。
/// </summary>
public sealed class AdminWriteApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CLUB_SQL_CONNECTION_STRING 未設定，無法執行整合測試。請先啟動本機資料庫並灌種子資料，"
                + "再 export CLUB_SQL_CONNECTION_STRING 後重跑 dotnet test（見 apps/api/README.md）。");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"CLUB_SQL_CONNECTION_STRING 已設定但連不上本機資料庫（{ex.Message}）。"
                + "請確認資料庫容器已啟動且健康、種子資料已灌入。", ex);
        }

        Environment.SetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING", connectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CLUB", TestJwtSigningKey.Value);
        Environment.SetEnvironmentVariable("REDIS_HOST", null);
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", null);

        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        // 保險：即使個別測試忘記清自己建立的資料，至少不留任何殘留的旗標狀態影響到行程本身
        // （行程本身即將被回收，這裡主要是文件用途，說明「這個 fixture 的生命週期結束＝這組
        // 開關的效力也結束」）。
        await base.DisposeAsync();
    }
}
