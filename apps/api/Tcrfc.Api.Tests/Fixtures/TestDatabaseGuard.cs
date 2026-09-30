using Microsoft.Data.SqlClient;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 整合測試一律只能連到本專案的網站庫 <see cref="RequiredDatabaseName"/>，不得誤連到這個
/// SQL Server instance 上任何其他資料庫（同一個 instance 裡還有使用者其他專案的約 25 個資料庫，
/// 見 <c>docs/14-invariants.md</c>）。
///
/// 2026-09-30 使用者裁決：本機只保留 <c>tcrfc_club</c>（網站）與 <c>tcrfc_charity</c>（慈善）兩庫，
/// 不再有獨立測試庫（原 S0-13 的 <c>tcrfc_club_test</c> 已廢除），<c>dotnet test</c> 直接跑在
/// <c>tcrfc_club</c> 上。已知代價：測試會改動後台看到的資料，中途失敗可能留下殘骸，
/// 需要時重灌種子即可（<c>./db/seed/setup-club-db.sh --recreate</c>）。
///
/// 六個 fixture（<see cref="ApiFixture"/>、<see cref="AdminWriteApiFixture"/> 等）一律呼叫
/// <see cref="ResolveAndVerifyAsync"/>，不各自重複「讀 <c>CLUB_SQL_CONNECTION_STRING</c> →
/// 檢查非空 → 開連線」。「只准連本專案庫」的精確名稱檢查保留，用來擋住容器內其他專案的資料庫，
/// 也擋住誤指到 <c>tcrfc_charity</c> 或已廢除的舊名稱。
/// </summary>
internal static class TestDatabaseGuard
{
    /// <summary>
    /// 整合測試唯一允許連線的資料庫名稱。這個名字與 <c>deploy/local-ddl.sh</c>、
    /// <c>db/seed/apply-seed.sh</c>／<c>db/seed/setup-club-db.sh</c> 的資料庫名稱白名單是
    /// 同一個名字——改這裡務必同步改那幾支腳本與 <c>apps/api/README.md</c>「怎麼跑」一節。
    /// </summary>
    public const string RequiredDatabaseName = "tcrfc_club";

    /// <summary>
    /// 讀取、驗證並回傳 <c>CLUB_SQL_CONNECTION_STRING</c>（環境變數優先，其次是 <see cref="TestLocalSettings"/>
    /// 從本機設定檔補上的值）。任何一關沒過都會丟
    /// <see cref="InvalidOperationException"/>，讓 xUnit 把使用這個 fixture 的每一項測試都
    /// 回報為「失敗」並附上這裡的訊息，不會悄悄跳過看起來像通過。
    /// </summary>
    public static async Task<string> ResolveAndVerifyAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CLUB_SQL_CONNECTION_STRING 未設定（環境變數與 apps/api/appsettings.Development.json 都沒有），"
                + "無法執行整合測試。請執行 apps/api/scripts/init-local-settings.sh 產生本機設定檔，"
                + $"並確認已用 ./db/seed/setup-club-db.sh 建立並灌種子到本機網站庫（{RequiredDatabaseName}）"
                + "（見 apps/api/README.md「怎麼跑」）。");
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

        // 🔴 硬性防呆：一律要求精確等於 RequiredDatabaseName（不做模糊比對）。
        // 這條檢查存在的唯一理由就是防止 dotnet test 誤連到這個 SQL Server instance 上其他任何
        // 資料庫（使用者其他專案的資料庫、tcrfc_charity，或已廢除的 tcrfc_club_dev／tcrfc_club_test
        // 舊名稱）——寧可整套測試在啟動階段就直接拒絕執行，也不要讓測試安靜地寫壞別的資料庫。
        if (!string.Equals(databaseName, RequiredDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"CLUB_SQL_CONNECTION_STRING 指向資料庫 '{databaseName}'，但整合測試只允許連到本專案的網站庫 "
                + $"'{RequiredDatabaseName}'（見 docs/14-invariants.md「S0-13」）。這條檢查是為了防止 "
                + "dotnet test 誤連到這個 SQL Server instance 上其他專案的資料庫或 tcrfc_charity，把它們的"
                + "資料寫壞。請把 CLUB_SQL_CONNECTION_STRING 的 Database 改成 "
                + $"{RequiredDatabaseName}（尚未建立請先執行 ./db/seed/setup-club-db.sh）後重跑 dotnet test。");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"CLUB_SQL_CONNECTION_STRING 已設定但連不上資料庫（{ex.Message}）。請確認 SQL Server "
                + $"容器已啟動、{RequiredDatabaseName} 已用 ./db/seed/setup-club-db.sh 建立並灌種子。", ex);
        }

        return connectionString;
    }
}
