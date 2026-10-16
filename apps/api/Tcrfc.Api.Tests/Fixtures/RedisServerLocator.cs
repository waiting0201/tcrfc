namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 找 redis-server 執行檔的唯一一處（兩個 Redis fixture 共用）。順序：REDIS_SERVER_PATH、brew 路徑、一般 Linux 路徑
/// （apt 安裝在 /usr/bin，E-335）、最後掃 PATH。
/// </summary>
internal static class RedisServerLocator
{
    public static string Resolve()
    {
        var overridePath = Environment.GetEnvironmentVariable("REDIS_SERVER_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        string[] candidates =
        [
            "/opt/homebrew/bin/redis-server", // Apple Silicon brew
            "/usr/local/bin/redis-server", // Intel brew／手動安裝
            "/usr/bin/redis-server", // Debian／Ubuntu apt（CI runner）
        ];

        var found = candidates.FirstOrDefault(File.Exists);
        if (found is not null)
        {
            return found;
        }

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, "redis-server");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "找不到 redis-server 執行檔——這組測試需要一個真正在跑的 Redis 行程（不 mock）。"
            + "請先 `brew install redis`（或 CI 的 apt-get install redis-server），或設定環境變數 REDIS_SERVER_PATH 指到 redis-server 執行檔。");
    }
}
