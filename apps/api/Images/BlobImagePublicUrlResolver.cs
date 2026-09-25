using Azure.Storage.Blobs;

namespace Tcrfc.Api.Images;

/// <summary>依 <see cref="BlobContainerClient.Uri"/> 拼出完整網址，跟
/// <see cref="BlobImageStorageService"/> 共用同一個容器單例（見 Program.cs 的 DI 註冊）。</summary>
public sealed class BlobImagePublicUrlResolver(BlobContainerClient container) : IImagePublicUrlResolver
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
