using Azure.Storage.Blobs;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.Images;

/// <summary>依 <see cref="BlobContainerClient.Uri"/>（或有設定 <c>AZURE_BLOB_PUBLIC_BASE_URL</c> 時的 CDN 基底，見 <see cref="PublicBlobBaseUrl"/>）拼出完整網址，跟
/// <see cref="BlobImageStorageService"/> 共用同一個容器單例（見 Program.cs 的 DI 註冊）。</summary>
public sealed class BlobImagePublicUrlResolver(BlobContainerClient container, PublicBlobBaseUrl publicBaseUrl) : IImagePublicUrlResolver
{
    public string? Resolve(string? objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return null;
        }

        return publicBaseUrl.Build(container, objectKey);
    }
}
