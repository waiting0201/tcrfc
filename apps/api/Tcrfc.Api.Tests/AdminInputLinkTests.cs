using Tcrfc.Api.Common;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>版位備援連結：http(s) 或 App 深連結 <c>tcrfc://</c>（App 規劃書 §2.3），其餘協定一律拒絕。</summary>
public sealed class AdminInputLinkTests
{
    [Theory]
    [InlineData("https://example.com/a")]
    [InlineData("http://example.com")]
    [InlineData("tcrfc://schedule/d1")]
    [InlineData("tcrfc://membercard")]
    public void 允許的連結(string url) => Assert.Equal(url, AdminInput.OptionalHttpOrAppLink(url, "備援連結"));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("tcrfc://")]
    [InlineData("tcrfc://a b")]
    [InlineData("otherapp://x")]
    [InlineData("/relative")]
    public void 拒絕的連結(string url) => Assert.ThrowsAny<Exception>(() => AdminInput.OptionalHttpOrAppLink(url, "備援連結"));

    [Fact]
    public void 空值回null() => Assert.Null(AdminInput.OptionalHttpOrAppLink("  ", "備援連結"));

    // ad_creatives.click_url 與版位備援連結共用同一套規則；錯誤要帶欄位鍵。
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("tcrfc://")]
    [InlineData("tcrfc://a b")]
    [InlineData("ftp://x.com/a")]
    public void 點擊目的地拒絕時帶欄位鍵(string url)
    {
        var ex = Assert.ThrowsAny<AdminValidationException>(() => AdminInput.OptionalHttpOrAppLink(url, "點擊目的地", 500, "clickUrl"));
        Assert.True(ex.FieldErrors.ContainsKey("clickUrl"));
    }
}
