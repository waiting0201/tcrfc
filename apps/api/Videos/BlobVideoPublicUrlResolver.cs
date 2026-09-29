using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;

namespace Tcrfc.Api.Videos;

/// <summary>依影片專用容器的 <see cref="BlobContainerClient.Uri"/> 拼出完整網址，跟
/// <see cref="BlobVideoStorageService"/> 共用同一個具名（<c>"videos"</c>）容器單例
/// （見 Program.cs 的 DI 註冊），理由比照
/// <see cref="Tcrfc.Api.Images.BlobImagePublicUrlResolver"/> 對圖片容器的作法。</summary>
public sealed class BlobVideoPublicUrlResolver([FromKeyedServices("videos")] BlobContainerClient container) : IVideoPublicUrlResolver
{
    public string? Resolve(string? objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return null;
        }

        return container.GetBlobClient(objectKey).Uri.ToString();
    }
}
