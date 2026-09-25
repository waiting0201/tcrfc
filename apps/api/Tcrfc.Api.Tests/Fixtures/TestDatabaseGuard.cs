using Microsoft.Data.SqlClient;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// S0-13（2026-09-25）：整合測試一律只能連到專用的測試資料庫 <see cref="RequiredDatabaseName"/>，
/// 不得誤連到本機開發／無頭瀏覽器實走在用的 <c>tcrfc_club_dev</c>，或這個 SQL Server instance 上
/// 其他任何資料庫（同一個 instance 裡還有使用者其他專案的約 25 個資料庫，見
/// <c>docs/14-invariants.md</c>）。
///
/// 在這之前，六個 fixture（<see cref="ApiFixture"/>、<see cref="AdminWriteApiFixture"/> 等）
/// 各自重複「讀 <c>CLUB_SQL_CONNECTION_STRING</c> → 檢查非空 → 開連線」這段邏輯，且完全沒有檢查
/// 連到的究竟是哪一個資料庫——`dotnet test` 與無頭瀏覽器實走因此共用同一個
/// <c>tcrfc_club_dev</c>，同一天發生三次互相干擾（測試把實走中帳號的 2FA 狀態、<c>settings</c>
/// 的 SEO 值重置回種子）。這個類別把驗證邏輯集中成一份，並加上「資料庫名稱必須精確等於測試庫」
/// 這道新防線，六個 fixture 一律呼叫 <see cref="ResolveAndVerifyAsync"/> 取代自己重複的檢查。
///
/// 建立與灌種子見 <c>db/seed/setup-test-db.sh</c>（一鍵從零建置 <see cref="RequiredDatabaseName"/>）。
/// </summary>
internal static class TestDatabaseGuard
{
    /// <summary>
    /// 整合測試唯一允許連線的資料庫名稱。這個名字與 <c>deploy/local-ddl.sh</c>、
    /// <c>db/seed/apply-seed.sh</c>／<c>db/seed/setup-test-db.sh</c> 的資料庫名稱白名單機制是
    /// 同一個名字——改這裡務必同步改那幾支腳本與 <c>apps/api/README.md</c>「怎麼跑」一節。
    /// </summary>
    public const string RequiredDatabaseName = "tcrfc_club_test";

    /// <summary>
    /// 讀取、驗證並回傳 <c>CLUB_SQL_CONNECTION_STRING</c>。任何一關沒過都會丟
    /// <see cref="InvalidOperationException"/>，讓 xUnit 把使用這個 fixture 的每一項測試都
    /// 回報為「失敗」並附上這裡的訊息，不會悄悄跳過看起來像通過。
    /// </summary>
    public static async Task<string> ResolveAndVerifyAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CLUB_SQL_CONNECTION_STRING 未設定，無法執行整合測試。請先執行 "
                + "./db/seed/setup-test-db.sh 建立並灌種子到整合測試專用庫（"
                + $"{RequiredDatabaseName}），再把 CLUB_SQL_CONNECTION_STRING 指向它後重跑 "
                + "dotnet test（見 apps/api/README.md「怎麼跑」）。");
        }

        string databaseName;
        try
        {
            databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"CLUB_SQL_CONNECTION_STRING 格式無法解析（{ex.Message}）。", ex);
        }

        // 🔴 硬性防呆：不判斷「看起來像不像測試庫」，一律要求精確等於 RequiredDatabaseName。
        // 這條檢查存在的唯一理由就是防止 dotnet test 誤連到 tcrfc_club_dev（本機開發／無頭瀏覽器
        // 實走在用）或這個 SQL Server instance 上其他任何資料庫——寧可整套測試在啟動階段就直接
        // 拒絕執行，也不要讓測試安靜地寫壞別的資料庫（S0-13 的教訓，2026-09-25 一天發生三次）。
        if (!string.Equals(databaseName, RequiredDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"CLUB_SQL_CONNECTION_STRING 指向資料庫 '{databaseName}'，但整合測試只允許連到專用測試庫 "
                + $"'{RequiredDatabaseName}'（S0-13，見 docs/14-invariants.md）。這條檢查是為了防止 "
                + "dotnet test 誤連到 tcrfc_club_dev 或這個 SQL Server instance 上其他資料庫，把它們的"
                + "資料寫壞或重置回種子。請先執行 ./db/seed/setup-test-db.sh 建立"
                + $"（或 --recreate 重建）{RequiredDatabaseName}，再把 CLUB_SQL_CONNECTION_STRING 的 "
                + $"Database 改成 {RequiredDatabaseName} 後重跑 dotnet test。");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"CLUB_SQL_CONNECTION_STRING 已設定但連不上測試資料庫（{ex.Message}）。請確認 SQL Server "
                + $"容器已啟動、{RequiredDatabaseName} 已用 ./db/seed/setup-test-db.sh 建立並灌種子。", ex);
        }

        return connectionString;
    }
}
