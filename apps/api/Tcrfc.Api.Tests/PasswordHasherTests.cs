using Tcrfc.Api.Security;
using Xunit;

namespace Tcrfc.Api.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash與Verify_正確密碼回傳true()
    {
        var hash = PasswordHasher.Hash("Correct-Horse-Battery-Staple-1");
        Assert.True(PasswordHasher.Verify("Correct-Horse-Battery-Staple-1", hash));
    }

    [Fact]
    public void Verify_錯誤密碼回傳false()
    {
        var hash = PasswordHasher.Hash("Correct-Horse-Battery-Staple-1");
        Assert.False(PasswordHasher.Verify("Wrong-Password", hash));
    }

    [Fact]
    public void Hash_同一組密碼兩次雜湊值不同_因為salt隨機()
    {
        var hash1 = PasswordHasher.Hash("SamePassword123");
        var hash2 = PasswordHasher.Hash("SamePassword123");
        Assert.NotEqual(hash1, hash2);
        // 但兩者都必須能各自驗證通過。
        Assert.True(PasswordHasher.Verify("SamePassword123", hash1));
        Assert.True(PasswordHasher.Verify("SamePassword123", hash2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-hash")]
    [InlineData("$argon2id$v=19$m=65536,t=3,p=1$onlyonepart")]
    public void Verify_雜湊值格式不正確_回傳false而不丟例外(string malformedHash)
    {
        Assert.False(PasswordHasher.Verify("AnyPassword123", malformedHash));
    }

    [Fact]
    public void Hash格式符合PHC風格()
    {
        var hash = PasswordHasher.Hash("SomePassword123");
        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=1$", hash);
        Assert.Equal(6, hash.Split('$').Length);
    }
}
