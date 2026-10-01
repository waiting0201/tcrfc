namespace Tcrfc.Api.Common;

/// <summary>
/// Data Protection 金鑰環路徑的啟動檢查（E-109）。
/// 沒設 <c>DATA_PROTECTION_KEYS_PATH</c> 時 ASP.NET Core 不會報錯，只是把金鑰環放在容器可寫層，
/// 容器一重建，2FA 密鑰、慈善身分證字號與載具、推播權杖、金流與發票憑證全部永久無法解密。
/// 所以 Production 必須在啟動時就失敗，而不是等到第一次解密才爆。非 Production 維持「沒設就不持久化」。
/// </summary>
public static class DataProtectionKeyRing
{
    public const string ConfigKey = "DATA_PROTECTION_KEYS_PATH";

    /// <summary>
    /// 回傳要持久化的目錄（未設且非 Production 時回傳 null，維持現行行為）。
    /// Production 下缺值、目錄不存在、不可寫一律丟 <see cref="InvalidOperationException"/>。
    /// </summary>
    public static DirectoryInfo? Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var path = configuration[ConfigKey];
        var production = environment.IsProduction();

        if (string.IsNullOrWhiteSpace(path))
        {
            if (production)
            {
                throw Fail("未設定或為空白");
            }

            return null;
        }

        var directory = new DirectoryInfo(path.Trim());
        if (production)
        {
            if (!directory.Exists)
            {
                throw Fail($"目錄不存在（{directory.FullName}）");
            }

            AssertWritable(directory);
        }

        return directory;
    }

    // 實際寫入並刪除探測檔：只看權限位元在 bind mount／容器使用者下不可靠（E-109 的 uid 1654 問題）。
    private static void AssertWritable(DirectoryInfo directory)
    {
        var probe = Path.Combine(directory.FullName, $".write-probe-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            throw Fail($"目錄不可寫（{directory.FullName}）：{ex.GetType().Name}", ex);
        }
    }

    private static InvalidOperationException Fail(string reason, Exception? inner = null) => new(
        $"Production 環境的 {ConfigKey} {reason}。Data Protection 金鑰環必須持久化，否則容器重建後 2FA、慈善身分證字號、" +
        "推播權杖與金流／發票憑證將永久無法解密（E-109）。請依 infra/README.md §4.3「金鑰環」建立目錄（uid/gid 1654、700）並掛載。",
        inner);
}
