using Microsoft.Data.SqlClient;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 整合測試的資料庫必須開 <c>READ_COMMITTED_SNAPSHOT</c>（RCSI），與正式庫（Azure SQL Database 預設開）的並行語意一致（<c>E-337</c>、<c>E-338</c>）。
/// 關著時讀取會被寫入擋住，會<b>掩蓋</b>「檢查 → 動作」的競態（讀到的永遠是最新提交的值），也會製造正式環境不會有的讀寫死結；
/// 兩個方向都讓測試的結果不能代表正式環境。建庫一律走 <c>deploy/local-ddl.sh</c>（內含 <c>ensure_rcsi</c>）。
/// </summary>
internal static class RcsiGuard
{
    internal sealed class RcsiDisabledException(string message) : InvalidOperationException(message);

    public static async Task AssertEnabledAsync(SqlConnection openConnection)
    {
        await using var command = openConnection.CreateCommand();
        command.CommandText = "SELECT is_read_committed_snapshot_on, name FROM sys.databases WHERE name = DB_NAME()";
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync() && !reader.GetBoolean(0))
        {
            var name = reader.GetString(1);
            throw new RcsiDisabledException(
                $"資料庫 '{name}' 的 READ_COMMITTED_SNAPSHOT 是關閉的。正式庫（Azure SQL）預設開啟，測試庫必須一致，否則並行測試不能代表正式環境（docs/18 E-337／E-338）。"
                + $"請用 ./deploy/local-ddl.sh 重建，或執行：ALTER DATABASE [{name}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;");
        }
    }
}
