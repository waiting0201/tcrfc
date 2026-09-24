using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Tcrfc.Api.Videos;

/// <summary>
/// <see cref="IVideoStorageService"/> 的 Azure Blob Storage 實作（本機開發接 Azurite）。
/// 用**具名服務**（<c>[FromKeyedServices("videos")]</c>）注入獨立於圖片的
/// <see cref="BlobContainerClient"/>，兩者共用同一組連線字串、不同容器名稱，見 <c>Program.cs</c>。
/// </summary>
public sealed class BlobVideoStorageService(
    [FromKeyedServices("videos")] BlobContainerClient container,
    ILogger<BlobVideoStorageService> logger) : IVideoStorageService
{
    // 物件鍵含隨機值，同一把鍵永遠指向同一份內容，可放心設定長效不可變快取。
    private const string CacheControl = "public, max-age=31536000, immutable";

    private static volatile bool _containerEnsured;
    private static readonly SemaphoreSlim EnsureContainerLock = new(1, 1);

    public async Task<UploadedVideoInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
    {
        // 純驗證（格式、大小）在記憶體完成，失敗在這裡就會丟出，還沒有碰到儲存體。
        VideoValidator.Validate(rawBytes);

        await EnsureContainerAsync(cancellationToken);

        var key = $"{objectKeyPrefix}/{Guid.NewGuid():N}{VideoUploadOptions.Extension}";

        using var stream = new MemoryStream(rawBytes, writable: false);
        await container.GetBlobClient(key).UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = VideoUploadOptions.ContentType, CacheControl = CacheControl },
                Conditions = null,
            },
            cancellationToken);

        return new UploadedVideoInfo(key, rawBytes.LongLength);
    }

    public async Task DeleteAsync(string? objectKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return;
        }

        try
        {
            await EnsureContainerAsync(cancellationToken);
            await container.GetBlobClient(objectKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            // 🔴 fail-open（見 IVideoStorageService.DeleteAsync 上的說明）：資料庫的新值已經寫入
            // 成功，清理舊物件失敗不該讓整個請求變成 500，只留下警告日誌與可能的孤兒物件。
            logger.LogWarning(ex, "刪除舊影片物件失敗（物件鍵：{ObjectKey}），忽略此錯誤繼續執行", objectKey);
        }
    }

    /// <summary>容器不存在時（本機首次對 Azurite 開發、或正式環境第一次部署）自動建立一次。
    /// 旗標只在真的成功之後才設為 <c>true</c>，理由逐字比照
    /// <c>Images/BlobImageStorageService.EnsureContainerAsync</c>。</summary>
    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_containerEnsured)
        {
            return;
        }

        await EnsureContainerLock.WaitAsync(cancellationToken);
        try
        {
            if (_containerEnsured)
            {
                return;
            }

            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            _containerEnsured = true;
        }
        finally
        {
            EnsureContainerLock.Release();
        }
    }
}
