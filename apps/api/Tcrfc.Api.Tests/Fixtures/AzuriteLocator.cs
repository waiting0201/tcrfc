using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 找 <c>azurite-blob</c> 執行檔的單一入口（fixture 與 <see cref="AzuriteFactAttribute"/> 共用）。
/// 找不到時整組「需要真實 Azurite」的測試明確 skip 並印出原因，不再讓 fixture 初始化丟例外
/// 把整個測試回合弄紅（沒裝 Azurite 的機器、CI 沒準備替身時，其餘測試照常跑）。
/// 環境變數 <c>AZURITE_EXECUTABLE_PATH</c> 可覆寫路徑（指到不存在的檔案＝視為沒有 Azurite）。
/// 🔴 設了 <c>TCRFC_TEST_REQUIRE_ALL=1</c>（CI 一律設）時不得靜默略過：找不到 Azurite 就讓測試失敗（E-335 第四次）。
/// </summary>
public static class AzuriteLocator
{
    public const string MissingReason =
        "找不到 azurite-blob 執行檔，已略過這個需要真實 Azurite 的測試。"
        + "請先 `npm install -g azurite`，或設定環境變數 AZURITE_EXECUTABLE_PATH 指到 azurite-blob 執行檔。";

    private static readonly Lazy<string?> Resolved = new(Resolve);

    public static string? Path => Resolved.Value;

    public static bool IsAvailable => Path is not null;

    /// <summary>找不到又要求「不得略過」時，fixture 初始化呼叫它丟出明確失敗。</summary>
    public static void ThrowIfRequiredButMissing()
    {
        if (!IsAvailable && TestRequirements.RequireAll)
        {
            var overridePath = Environment.GetEnvironmentVariable("AZURITE_EXECUTABLE_PATH");
            throw new InvalidOperationException(
                $"{TestRequirements.EnvName}=1：不允許略過需要 Azurite 的測試，但找不到 azurite-blob"
                + (string.IsNullOrWhiteSpace(overridePath) ? "（PATH 與固定路徑都沒有）。" : $"（AZURITE_EXECUTABLE_PATH={overridePath} 不存在）。")
                + MissingReason);
        }
    }

    private static string? Resolve()
    {
        var overridePath = Environment.GetEnvironmentVariable("AZURITE_EXECUTABLE_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            // 明確指定的路徑優先且不回退：指到不存在的檔案＝視為沒有 Azurite（也方便實際驗證 skip 路徑）。
            return File.Exists(overridePath) ? overridePath : null;
        }

        string[] candidates =
        [
            "/usr/local/bin/azurite-blob", // npm -g 安裝在 macOS／一般 Linux 的預設位置
            "/opt/homebrew/bin/azurite-blob", // Apple Silicon 若透過其他方式安裝
        ];

        var found = candidates.FirstOrDefault(File.Exists);
        if (found is not null)
        {
            return found;
        }

        // 與 RedisServerLocator 一致：最後掃 PATH（CI 把 npm 安裝目錄加進 PATH，不依賴固定路徑）。
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = System.IO.Path.Combine(dir, "azurite-blob");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

/// <summary>需要真實 Azurite 的測試：沒有 <c>azurite-blob</c> 時 skip（附原因），有就照常跑。</summary>
public sealed class AzuriteFactAttribute : FactAttribute
{
    public AzuriteFactAttribute()
    {
        // 要求全部執行時不設 Skip：測試照跑，由 fixture 初始化失敗，紅燈而不是略過。
        if (!AzuriteLocator.IsAvailable && !TestRequirements.RequireAll)
        {
            Skip = AzuriteLocator.MissingReason;
        }
    }
}

/// <summary><see cref="AzuriteFactAttribute"/> 的 Theory 版本。</summary>
public sealed class AzuriteTheoryAttribute : TheoryAttribute
{
    public AzuriteTheoryAttribute()
    {
        // 要求全部執行時不設 Skip：測試照跑，由 fixture 初始化失敗，紅燈而不是略過。
        if (!AzuriteLocator.IsAvailable && !TestRequirements.RequireAll)
        {
            Skip = AzuriteLocator.MissingReason;
        }
    }
}
