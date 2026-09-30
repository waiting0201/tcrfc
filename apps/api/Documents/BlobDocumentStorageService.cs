using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Tcrfc.Api.Documents;

/// <summary><see cref="IDocumentStorageService"/> 的 Azure Blob 實作（本機接 Azurite）。兩個具名容器：
/// <c>documents-public</c>／<c>documents-private</c>（設定鍵 <c>AZURE_BLOB_CONTAINER_DOCUMENTS</c>／
/// <c>AZURE_BLOB_CONTAINER_PROPOSALS</c>），見 <c>Program.cs</c>。</summary>
public sealed class BlobDocumentStorageService(
    [FromKeyedServices("documents-public")] BlobContainerClient publicContainer,
    [FromKeyedServices("documents-private")] BlobContainerClient privateContainer,
    ILogger<BlobDocumentStorageService> logger) : IDocumentStorageService
{
    private const string PublicCacheControl = "public, max-age=31536000, immutable";

    private static readonly HashSet<string> EnsuredContainers = [];
    private static readonly SemaphoreSlim EnsureLock = new(1, 1);

    public async Task<UploadedDocumentInfo> UploadAsync(
        DocumentBucket bucket, byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
    {
        var (extension, contentType) = DocumentValidator.Validate(rawBytes);
        var container = Pick(bucket);
        await EnsureContainerAsync(container, cancellationToken);

        var key = $"{objectKeyPrefix}/{Guid.NewGuid():N}{extension}";
        using var stream = new MemoryStream(rawBytes, writable: false);
        await container.GetBlobClient(key).UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    CacheControl = bucket == DocumentBucket.Public ? PublicCacheControl : "private, no-store",
                },
            },
            cancellationToken);

        return new UploadedDocumentInfo(key, rawBytes.LongLength, contentType);
    }

    public async Task DeleteAsync(DocumentBucket bucket, string? objectKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return;
        }

        try
        {
            var container = Pick(bucket);
            await EnsureContainerAsync(container, cancellationToken);
            await container.GetBlobClient(objectKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // fail-open：理由同 BlobVideoStorageService.DeleteAsync。
            logger.LogWarning(ex, "刪除檔案物件失敗（物件鍵：{ObjectKey}），忽略此錯誤繼續執行", objectKey);
        }
    }

    public async Task<DocumentReadResult?> OpenReadAsync(DocumentBucket bucket, string objectKey, CancellationToken cancellationToken)
    {
        var container = Pick(bucket);
        await EnsureContainerAsync(container, cancellationToken);
        var blob = container.GetBlobClient(objectKey);
        try
        {
            var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new DocumentReadResult(
                response.Value.Content, response.Value.Details.ContentType, response.Value.Details.ContentLength);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private BlobContainerClient Pick(DocumentBucket bucket) => bucket == DocumentBucket.Public ? publicContainer : privateContainer;

    private static async Task EnsureContainerAsync(BlobContainerClient container, CancellationToken cancellationToken)
    {
        lock (EnsuredContainers)
        {
            if (EnsuredContainers.Contains(container.Uri.ToString()))
            {
                return;
            }
        }

        await EnsureLock.WaitAsync(cancellationToken);
        try
        {
            // 容器一律以「無公開存取」建立；是否對外公開讀取是部署層決定（見 IImagePublicUrlResolver 檔頭）。
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            lock (EnsuredContainers)
            {
                EnsuredContainers.Add(container.Uri.ToString());
            }
        }
        finally
        {
            EnsureLock.Release();
        }
    }
}

public sealed class BlobDocumentPublicUrlResolver([FromKeyedServices("documents-public")] BlobContainerClient container) : IDocumentPublicUrlResolver
{
    public string? Resolve(string? objectKey)
        => string.IsNullOrWhiteSpace(objectKey) ? null : container.GetBlobClient(objectKey).Uri.ToString();
}

/// <summary><c>AZURE_BLOB_CONNECTION_STRING</c> 未設定時的替身，比照 <c>UnavailableVideoStorageService</c>。</summary>
public sealed class UnavailableDocumentStorageService : IDocumentStorageService
{
    public Task<UploadedDocumentInfo> UploadAsync(DocumentBucket bucket, byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
        => throw new InvalidOperationException(
            "檔案上傳功能尚未設定物件儲存（AZURE_BLOB_CONNECTION_STRING 未設定）。本機開發請參考 apps/api/README.md「本機開發：Azurite」。");

    public Task DeleteAsync(DocumentBucket bucket, string? objectKey, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<DocumentReadResult?> OpenReadAsync(DocumentBucket bucket, string objectKey, CancellationToken cancellationToken)
        => throw new InvalidOperationException("檔案儲存尚未設定（AZURE_BLOB_CONNECTION_STRING 未設定）。");
}

public sealed class UnavailableDocumentPublicUrlResolver : IDocumentPublicUrlResolver
{
    public string? Resolve(string? objectKey) => null;
}
