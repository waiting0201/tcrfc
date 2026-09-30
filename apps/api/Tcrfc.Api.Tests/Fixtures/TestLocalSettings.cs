using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 讓 <c>dotnet test</c> 在沒有 export 環境變數時，也能從 <c>apps/api/appsettings.Development.json</c>
/// （本機設定，已被 .gitignore 排除，由 <c>apps/api/scripts/init-local-settings.sh</c> 產生）取得
/// <c>CLUB_SQL_CONNECTION_STRING</c>。
///
/// 🔴 只補「環境變數尚未設定」的情況（環境變數優先，與 ASP.NET Core 設定來源順序一致）。
/// 🔴 只讀 <c>CLUB_SQL_CONNECTION_STRING</c> 這一個鍵：JWT 金鑰由各 fixture 自己以環境變數設成
/// <see cref="TestJwtSigningKey.Value"/>（E-79：必須在 <c>builder.Build()</c> 前生效，環境變數優先於檔案，
/// 所以不受檔案內的正式風格金鑰影響）；Redis／Blob 等也不從檔案帶入，避免改變各 fixture
/// 刻意設定的行為。
/// 🔴 「只准連 tcrfc_club」的檢查仍由 <see cref="TestDatabaseGuard"/> 執行，來源是檔案或環境變數都一樣。
///
/// 用 ModuleInitializer 在測試組件載入時執行一次，這樣 30 幾處直接讀
/// <c>Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")</c> 的測試也一併涵蓋，不必逐一改。
/// </summary>
internal static class TestLocalSettings
{
    private const string Key = "CLUB_SQL_CONNECTION_STRING";

    [ModuleInitializer]
    internal static void Init()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Key)))
        {
            return;
        }

        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Tcrfc.Api.csproj")))
            {
                dir = dir.Parent;
            }
            if (dir is null)
            {
                return;
            }

            var path = Path.Combine(dir.FullName, "appsettings.Development.json");
            if (!File.Exists(path))
            {
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty(Key, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                Environment.SetEnvironmentVariable(Key, value.GetString());
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // 讀不到就維持「未設定」，由 TestDatabaseGuard 丟出明確的錯誤訊息；不在這裡吞掉後假裝成功。
        }
    }
}
