using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 🔴 唯一一個把 <c>ENABLE_UNSAFE_DEV_WRITES=true</c> 打開的 fixture——只有這裡的測試會打得到
/// <c>Features/AdminNews</c> 的寫入端點。其餘所有 fixture（<see cref="ApiFixture"/> 等）刻意
/// **不**設這個變數，用來驗證「預設關閉、路由根本不存在」這件事本身（見
/// <c>AdminNewsGateClosedTests</c>）。
///
/// ⚠️ 不要把這個 fixture 拿去給既有的五組唯讀測試共用——保持「大多數測試在關閉狀態下跑」是
/// 刻意的，這樣才能持續驗證「忘記開這個旗標＝這組端點真的不存在」，不是「反正測試環境永遠開著」。
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
        Environment.SetEnvironmentVariable("ENABLE_UNSAFE_DEV_WRITES", "true");
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
