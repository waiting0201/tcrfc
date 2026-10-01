using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 慈善整合測試一律只能連到慈善庫 <see cref="RequiredDatabaseName"/>，不得誤連到同一個 SQL Server instance 上的
/// 任何其他資料庫（含主站的 <c>tcrfc_club</c>、使用者其他專案的約 25 個資料庫）。與 <see cref="TestDatabaseGuard"/>
/// 是對稱的一對：那個只認 <c>tcrfc_club</c>，這個只認 <c>tcrfc_charity</c>，兩邊互不放寬。
///
/// 🔴 連線字串來源：環境變數 <c>CHARITY_SQL_CONNECTION_STRING</c> 優先，其次是 <c>apps/api/appsettings.Development.json</c>
/// （本機設定檔，已被 .gitignore 排除；CI 與 docker 用環境變數）。🔴 <b>刻意不像 <see cref="TestLocalSettings"/> 那樣在模組載入時把它寫進
/// 行程環境變數</b>：環境變數是行程全域的，寫進去等於讓<b>每一個</b>俱樂部測試主機都啟用慈善平台（進而要求
/// <c>JWT_SIGNING_KEY_CHARITY</c>）；只有慈善 fixture 才該啟用它，由 <see cref="CharityApiFixture"/> 自己設定。
/// </summary>
internal static class CharityTestDatabaseGuard
{
    public const string RequiredDatabaseName = "tcrfc_charity";
    private const string Key = "CHARITY_SQL_CONNECTION_STRING";

    public static async Task<string> ResolveAndVerifyAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(Key) ?? ReadFromLocalSettings();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{Key} 未設定（環境變數與 apps/api/appsettings.Development.json 都沒有），無法執行慈善整合測試。"
                + $"請確認已用 ./deploy/local-ddl.sh --apply 與 ./db/seed/apply-charity-seed.sh 建立並灌種子到本機慈善庫（{RequiredDatabaseName}）。");
        }

        string databaseName;
        try
        {
            databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{Key} 格式無法解析（{ex.Message}）。", ex);
        }

        // 🔴 硬性防呆：精確等於 tcrfc_charity（不做模糊比對）。寧可整套慈善測試在啟動階段拒絕執行，
        // 也不要讓測試安靜地寫壞別的資料庫（尤其是 tcrfc_club）。
        if (!string.Equals(databaseName, RequiredDatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{Key} 指向資料庫 '{databaseName}'，但慈善整合測試只允許連到 '{RequiredDatabaseName}'。"
                + "這條檢查是為了防止 dotnet test 誤連到其他專案或主站的資料庫把資料寫壞。");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"{Key} 已設定但連不上資料庫（{ex.Message}）。請確認 SQL Server 容器已啟動、{RequiredDatabaseName} 已建立並灌種子。", ex);
        }

        return connectionString;
    }

    private static string? ReadFromLocalSettings()
    {
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Tcrfc.Api.csproj")))
            {
                dir = dir.Parent;
            }

            var path = dir is null ? null : Path.Combine(dir.FullName, "appsettings.Development.json");
            if (path is null || !File.Exists(path))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty(Key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
