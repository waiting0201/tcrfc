using Tcrfc.Api.Features.AdminAuth;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>後台密碼政策（2026-10-03：長度下限 10 → 9）。純記憶體測試，不連資料庫；主站與慈善後台共用這一支。</summary>
public sealed class AdminPasswordPolicyTests
{
    [Fact]
    public void 下限常數為9()
    {
        Assert.Equal(9, AdminAuthService.MinPasswordLength);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("12345678")]
    public void 少於9字元_拒絕_訊息引用下限(string password)
    {
        var ex = Assert.Throws<AdminAuthValidationException>(() => AdminAuthService.ValidatePasswordPolicy(password, "someone"));
        Assert.Contains("9", ex.Message);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("1234567890")]
    [InlineData("中文密碼也可以九個字元")]
    public void 至少9字元_通過(string password)
    {
        AdminAuthService.ValidatePasswordPolicy(password, "someone");
    }

    [Fact]
    public void 密碼等於帳號_即使夠長也拒絕()
    {
        Assert.Throws<AdminAuthValidationException>(() => AdminAuthService.ValidatePasswordPolicy("Same-Name-123", "same-name-123"));
    }
}
