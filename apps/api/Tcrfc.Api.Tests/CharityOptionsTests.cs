using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Tcrfc.Api.CharityPlatform.Common;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>慈善設定的解析規則（前台網址的備援順序、背景工作的環境預設）。純函式，不需要資料庫。</summary>
public sealed class CharityOptionsTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static IConfiguration Config(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void 前台網址_明確設定優先_去掉結尾斜線()
        => Assert.Equal("https://donate.example", CharityOptions.ResolvePublicBaseUrl(
            Config((CharityOptions.PublicBaseUrlConfigKey, "https://donate.example/"), (CharityOptions.DomainConfigKey, "ignored.example")), new Env("Production")));

    [Theory]
    [InlineData("donate.example.org", "https://donate.example.org")]
    [InlineData("charity.localhost", "http://charity.localhost")]
    public void 前台網址_沒設定就用compose給的CHARITY_DOMAIN組出(string domain, string expected)
        => Assert.Equal(expected, CharityOptions.ResolvePublicBaseUrl(Config((CharityOptions.DomainConfigKey, domain)), new Env("Production")));

    [Fact]
    public void 前台網址_都沒有時_本機有預設_非本機回null不拿錯的網址去導向金流()
    {
        Assert.Equal("http://charity.localhost", CharityOptions.ResolvePublicBaseUrl(Config(), new Env("Development")));
        Assert.Null(CharityOptions.ResolvePublicBaseUrl(Config(), new Env("Production")));
        Assert.Null(CharityOptions.ResolvePublicBaseUrl(Config((CharityOptions.DomainConfigKey, "  ")), new Env("Staging")));
    }

    [Theory]
    [InlineData("Development", null, false)]       // 本機預設關：背景工作會改動共用種子資料
    [InlineData("Development", "true", true)]
    [InlineData("Production", null, true)]         // 正式環境預設開：不開＝捐款單永遠不逾時、憑證失敗永遠不重試
    [InlineData("Production", "false", false)]
    [InlineData("Development", "false", false)]
    [InlineData("Staging", "garbage", true)]
    public void 背景工作的環境預設(string environment, string? configured, bool expected)
        => Assert.Equal(expected, CharityOptions.WorkersEnabled(Config((CharityOptions.WorkersEnabledConfigKey, configured)), new Env(environment)));

    [Theory]
    [InlineData(null, 30)]
    [InlineData("45", 45)]
    [InlineData("0", 30)]
    [InlineData("-5", 30)]
    [InlineData("abc", 30)]
    public void 逾時分鐘數_壞設定值退回預設_不會變成永不逾時(string? configured, int expected)
        => Assert.Equal(expected, CharityOptions.ResolvePaymentTimeoutMinutes(Config((CharityOptions.PaymentTimeoutMinutesConfigKey, configured))));

    [Fact]
    public void 單號秘密缺值或太短_直接丟例外_不會用空字串簽出可預測的單號()
    {
        Assert.Throws<InvalidOperationException>(() => CharityOptions.ResolveOrderNoSecret(Config()));
        Assert.Throws<InvalidOperationException>(() => CharityOptions.ResolveOrderNoSecret(Config(("JWT_SIGNING_KEY_CHARITY", "short"))));
        Assert.Equal(new string('k', 32), CharityOptions.ResolveOrderNoSecret(Config(("JWT_SIGNING_KEY_CHARITY", new string('k', 32)))));
    }
}
