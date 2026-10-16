namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 「不得靜默略過」開關（E-335 第四次）。環境變數 <c>TCRFC_TEST_REQUIRE_ALL=1</c>（CI 的測試步驟一律設）時，
/// 需要外部執行檔（Azurite、redis-server）的測試找不到執行檔就<b>失敗</b>，不得 skip。
/// 本機沒設時維持原行為（沒裝 Azurite 的機器照常 skip）。
/// </summary>
public static class TestRequirements
{
    public const string EnvName = "TCRFC_TEST_REQUIRE_ALL";

    public static bool RequireAll =>
        string.Equals(Environment.GetEnvironmentVariable(EnvName), "1", StringComparison.Ordinal);
}
