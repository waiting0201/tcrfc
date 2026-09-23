using Tcrfc.Api.Security;
using Xunit;

namespace Tcrfc.Api.Tests;

public sealed class TotpServiceTests
{
    [Fact]
    public void Verify_目前時間視窗算出的碼_通過()
    {
        var secret = TotpService.GenerateSecret();
        var code = TotpService.GenerateCurrentCodeForTesting(secret);
        Assert.True(TotpService.Verify(secret, code));
    }

    [Fact]
    public void Verify_錯誤的六位數字_不通過()
    {
        var secret = TotpService.GenerateSecret();
        var realCode = TotpService.GenerateCurrentCodeForTesting(secret);
        // 找一個確定不等於正確碼的六位數字。
        var wrongCode = realCode == "000000" ? "111111" : "000000";
        Assert.False(TotpService.Verify(secret, wrongCode));
    }

    [Fact]
    public void Verify_用另一組密鑰算出的碼_不通過()
    {
        var secretA = TotpService.GenerateSecret();
        var secretB = TotpService.GenerateSecret();
        var codeForB = TotpService.GenerateCurrentCodeForTesting(secretB);
        Assert.False(TotpService.Verify(secretA, codeForB));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")] // 5 碼
    [InlineData("1234567")] // 7 碼
    [InlineData("abcdef")] // 非數字
    public void Verify_格式不正確的碼_回傳false而不丟例外(string malformedCode)
    {
        var secret = TotpService.GenerateSecret();
        Assert.False(TotpService.Verify(secret, malformedCode));
    }

    [Fact]
    public void Base32往返_還原回原始位元組()
    {
        var secret = TotpService.GenerateSecret();
        var base32 = TotpService.ToBase32(secret);
        var roundTripped = TotpService.FromBase32(base32);
        Assert.Equal(secret, roundTripped);
    }

    [Fact]
    public void ToBase32_只含合法字元()
    {
        var secret = TotpService.GenerateSecret();
        var base32 = TotpService.ToBase32(secret);
        Assert.Matches("^[A-Z2-7]+$", base32);
    }
}
