using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Tcrfc.Api.CharityPlatform.Storage;
using Tcrfc.Api.Common;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Images;
using Tcrfc.Api.Videos;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 公開網址基底（CDN 子網域）：有設定／未設定／尾斜線／特殊字元 key／格式驗證，以及四個解析器都真的套用。
/// 純單元測試，不連 Azurite（BlobContainerClient 只用來組網址，不發請求）。
/// </summary>
public sealed class PublicBlobBaseUrlTests
{
    private const string Conn = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    private static BlobContainerClient Container(string name = "images") => new(Conn, name);

    private static IConfiguration Config(string? value) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["K"] = value }).Build();

    [Fact]
    public void Unset_FallsBackToContainerUri_ExactlyAsBefore()
    {
        var c = Container();
        var key = "news/2026/a.webp";
        Assert.Equal(c.GetBlobClient(key).Uri.ToString(), PublicBlobBaseUrl.None.Build(c, key));
        Assert.Null(PublicBlobBaseUrl.FromConfiguration(Config(null), "K", false).Value);
        Assert.Null(PublicBlobBaseUrl.FromConfiguration(Config("   "), "K", false).Value);
    }

    [Theory]
    [InlineData("https://img-stg.tcrfc.tw")]
    [InlineData("https://img-stg.tcrfc.tw/")]
    [InlineData("  https://img-stg.tcrfc.tw//  ")]
    public void Set_BuildsBaseContainerKey_TrailingSlashesHandled(string raw)
    {
        var b = PublicBlobBaseUrl.FromConfiguration(Config(raw), "K", false);
        Assert.Equal("https://img-stg.tcrfc.tw/images/news/2026/a.webp", b.Build(Container(), "news/2026/a.webp"));
    }

    [Theory]
    [InlineData("a b.webp")]
    [InlineData("news/中文 檔名#1?x=1%.webp")]
    [InlineData("p/a+b&c=d;e,f'g(h)!.webp")]
    [InlineData("folder/子資料夾/img [1].webp")]
    public void SpecialCharacterKeys_EncodedSameAsAzureSdk(string key)
    {
        var c = Container();
        var b = PublicBlobBaseUrl.FromConfiguration(Config("https://img.example.com"), "K", false);
        // 取 SDK 輸出的「容器名之後」路徑，與自行組出的結果逐字比對。
        var sdkUrl = c.GetBlobClient(key).Uri.AbsoluteUri;
        var sdkSuffix = sdkUrl[(sdkUrl.IndexOf("/images/", StringComparison.Ordinal) + "/images/".Length)..];
        Assert.Equal($"https://img.example.com/images/{sdkSuffix}", b.Build(c, key));
    }

    [Fact]
    public void BasePathPrefix_IsPreserved()
    {
        var b = PublicBlobBaseUrl.FromConfiguration(Config("https://cdn.example.com/media/"), "K", false);
        Assert.Equal("https://cdn.example.com/media/images/x.webp", b.Build(Container(), "x.webp"));
    }

    [Theory]
    [InlineData("img.example.com")]
    [InlineData("/relative/path")]
    [InlineData("ftp://img.example.com")]
    [InlineData("http://img.example.com")]
    [InlineData("https://user:pw@img.example.com")]
    [InlineData("https://img.example.com/?sas=1")]
    [InlineData("https://img.example.com/#frag")]
    public void InvalidOrInsecure_ThrowsInProduction(string raw)
        => Assert.Throws<InvalidOperationException>(() => PublicBlobBaseUrl.FromConfiguration(Config(raw), "K", allowHttp: false));

    [Fact]
    public void Http_AllowedOnlyWhenDevelopment()
    {
        var b = PublicBlobBaseUrl.FromConfiguration(Config("http://localhost:9000"), "K", allowHttp: true);
        Assert.Equal("http://localhost:9000/images/x.webp", b.Build(Container(), "x.webp"));
        Assert.Throws<InvalidOperationException>(() => PublicBlobBaseUrl.FromConfiguration(Config("garbage"), "K", allowHttp: true));
    }

    [Fact]
    public void FourResolvers_UseBaseUrl_AndContainerName()
    {
        var b = new PublicBlobBaseUrl("https://img.example.com");
        Assert.Equal("https://img.example.com/images/a.webp", new BlobImagePublicUrlResolver(Container("images"), b).Resolve("a.webp"));
        Assert.Equal("https://img.example.com/videos/v.mp4", new BlobVideoPublicUrlResolver(Container("videos"), b).Resolve("v.mp4"));
        Assert.Equal("https://img.example.com/documents/d.pdf", new BlobDocumentPublicUrlResolver(Container("documents"), b).Resolve("d.pdf"));
        var charity = new BlobCharityImageStorage(Container("charity-images"), new PublicBlobBaseUrl("https://img-charity.example.com"), NullLogger<BlobCharityImageStorage>.Instance);
        Assert.Equal("https://img-charity.example.com/charity-images/l.webp", charity.Resolve("l.webp"));
        Assert.Null(charity.Resolve(" "));
    }

    [Fact]
    public void Resolvers_WithoutBase_KeepContainerUri()
    {
        var c = Container("images");
        Assert.Equal(c.GetBlobClient("a.webp").Uri.ToString(), new BlobImagePublicUrlResolver(c, PublicBlobBaseUrl.None).Resolve("a.webp"));
    }
}
