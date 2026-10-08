using Tcrfc.Api.Features.AdminAuth;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>後台密碼政策（2026-10-03：長度下限 10 → 9；2026-10-08：9 → 6）。純記憶體測試，不連資料庫；主站與慈善後台共用這一支。</summary>
public sealed class AdminPasswordPolicyTests
{
    [Fact]
    public void 下限常數為6()
    {
        Assert.Equal(6, AdminAuthService.MinPasswordLength);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("12345")]
    public void 少於6字元_拒絕_訊息引用下限(string password)
    {
        var ex = Assert.Throws<AdminAuthValidationException>(() => AdminAuthService.ValidatePasswordPolicy(password, "someone"));
        Assert.Contains("6", ex.Message);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("1234567890")]
    [InlineData("中文密碼六字")]
    public void 至少6字元_通過(string password)
    {
        AdminAuthService.ValidatePasswordPolicy(password, "someone");
    }

    [Fact]
    public void 密碼等於帳號_即使夠長也拒絕()
    {
        Assert.Throws<AdminAuthValidationException>(() => AdminAuthService.ValidatePasswordPolicy("Same-Name-123", "same-name-123"));
    }
}
