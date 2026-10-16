using Azure.Storage.Blobs;
using Tcrfc.Api.Common;
using Tcrfc.Api.Images;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// App 設定下發的 <c>mediaBaseUrl</c>（<c>GET /api/v1/app/config</c>）：內文圖片網址 = <c>mediaBaseUrl + "/" + key</c>，
/// 必須和 API 自己組的圖片網址（<see cref="IImagePublicUrlResolver.Resolve"/>）逐字一致。純邏輯，不連資料庫。
/// </summary>
public sealed class AppMediaBaseUrlTests
{
    private static readonly BlobContainerClient Container = new(new Uri("http://127.0.0.1:10000/devstoreaccount1/images"));

    [Fact]
    public void 有設定CDN基底_基底是CDN加容器名()
    {
        IImagePublicUrlResolver resolver = new BlobImagePublicUrlResolver(Container, new PublicBlobBaseUrl("https://img.example.com"));
        Assert.Equal("https://img.example.com/images", resolver.ResolveBase());
    }

    [Fact]
    public void 未設定CDN基底_基底是容器網址()
    {
        IImagePublicUrlResolver resolver = new BlobImagePublicUrlResolver(Container, PublicBlobBaseUrl.None);
        Assert.Equal("http://127.0.0.1:10000/devstoreaccount1/images", resolver.ResolveBase());
    }

    [Theory]
    [InlineData("tcrfc/articles/3f2a/body/0/image/main.webp")]
    [InlineData("bw/articles/3f2a/body/1/images-2/1280.webp")]
    public void 基底加斜線加物件鍵_等於API自己組的圖片網址(string key)
    {
        foreach (var publicBase in new[] { PublicBlobBaseUrl.None, new PublicBlobBaseUrl("https://img.example.com") })
        {
            IImagePublicUrlResolver resolver = new BlobImagePublicUrlResolver(Container, publicBase);
            Assert.Equal(resolver.Resolve(key), resolver.ResolveBase() + "/" + key);
        }
    }

    [Fact]
    public void 沒有圖片儲存_基底為null()
        => Assert.Null(((IImagePublicUrlResolver)new UnavailableImagePublicUrlResolver()).ResolveBase());
}
