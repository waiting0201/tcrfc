namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 測試專用的 JWT 簽章金鑰，固定值（不是隨機產生）——所有 fixture 用同一把，
/// 讓 <see cref="TestAdminTokens"/> 簽出來的權杖與行程內建立的 <c>WebApplicationFactory</c>
/// 用同一把金鑰驗證，兩邊才能對得上。⛔ 只用於測試，絕不是正式環境的值。
/// </summary>
public static class TestJwtSigningKey
{
    public const string Value = "test-only-jwt-signing-key-not-for-production-use-32bytes-min";
}
