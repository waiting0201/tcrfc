using System.Security.Cryptography;
using Konscious.Security.Cryptography;

namespace Tcrfc.Api.Security;

/// <summary>
/// 後台帳號密碼雜湊。演算法選 Argon2id——docs/12b-database-tables.md §7.6 明訂
/// 「演算法待選型，優先 Argon2id，次選 bcrypt」，本次選型落地。
///
/// 選 <c>Konscious.Security.Cryptography.Argon2</c> 而不是原生綁定套件（如 libsodium 的
/// .NET 包裝）：這是純受控 C#，不依賴平台原生函式庫，容器化部署（Docker）不會遇到
/// 「映像檔裡缺一個 .so」這類問題，符合 docs/17-deployment.md 的單一 VM Docker 部署模型。
///
/// 輸出格式是自訂的 PHC 風格字串：<c>$argon2id$v=19$m={memoryKiB},t={iterations},p={parallelism}$
/// {base64 salt}${base64 hash}</c>——把參數與 salt 一起存進雜湊值本身，日後調整記憶體／
/// 迭代參數不會讓舊雜湊值變得無法驗證（驗證時讀字串裡記的參數，不是讀程式裡的目前常數）。
/// </summary>
public static class PasswordHasher
{
    // OWASP Password Storage Cheat Sheet（2024 版）對 Argon2id 的建議下限之一：
    // m=19MiB, t=2, p=1 是最低建議；這裡用更保守的 m=64MiB, t=3, p=1，
    // 在單一 Azure VM（docs/17-deployment.md，Basic 層資源有限）上仍可接受
    // （後台登入是低頻操作，不像 API 熱路徑，多花 100-200ms 換取更高的離線暴力破解成本值得）。
    private const int MemoryKiB = 65536; // 64 MiB
    private const int Iterations = 3;
    private const int Parallelism = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt, MemoryKiB, Iterations, Parallelism, HashSize);

        return $"$argon2id$v=19$m={MemoryKiB},t={Iterations},p={Parallelism}$" +
               $"{Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// 比對密碼是否符合雜湊值。⛔ 全程不比較明文密碼，也不用字串相等比較雜湊結果
    /// （<see cref="CryptographicOperations.FixedTimeEquals"/> 是固定時間比較，
    /// 避免時序攻擊透露雜湊值差異的位置）。
    /// </summary>
    public static bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(encodedHash))
        {
            return false;
        }

        if (!TryParse(encodedHash, out var memoryKiB, out var iterations, out var parallelism, out var salt, out var expectedHash))
        {
            return false;
        }

        var actualHash = ComputeHash(password, salt, memoryKiB, iterations, parallelism, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memoryKiB, int iterations, int parallelism, int hashSize)
    {
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKiB,
        };
        return argon2.GetBytes(hashSize);
    }

    private static bool TryParse(
        string encodedHash, out int memoryKiB, out int iterations, out int parallelism,
        out byte[] salt, out byte[] hash)
    {
        memoryKiB = iterations = parallelism = 0;
        salt = hash = [];

        // 形狀：$argon2id$v=19$m=65536,t=3,p=1$<salt base64>$<hash base64>
        var parts = encodedHash.Split('$', StringSplitOptions.None);
        if (parts.Length != 6 || parts[1] != "argon2id")
        {
            return false;
        }

        var paramsPart = parts[3]; // "m=65536,t=3,p=1"
        var paramValues = new Dictionary<string, int>();
        foreach (var kv in paramsPart.Split(','))
        {
            var kvParts = kv.Split('=', 2);
            if (kvParts.Length != 2 || !int.TryParse(kvParts[1], out var value))
            {
                return false;
            }
            paramValues[kvParts[0]] = value;
        }

        if (!paramValues.TryGetValue("m", out memoryKiB) ||
            !paramValues.TryGetValue("t", out iterations) ||
            !paramValues.TryGetValue("p", out parallelism))
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[4]);
            hash = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        return true;
    }
}
