using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Tcrfc.Api.Images;

/// <summary>
/// <see cref="IImageStorageService"/> 的 Azure Blob Storage 實作（本機開發接 Azurite，兩者共用
/// 同一組 SDK 與連線字串格式，見 <c>Program.cs</c> 怎麼組出 <see cref="BlobContainerClient"/>）。
/// </summary>
public sealed class BlobImageStorageService(
    BlobContainerClient container,
    ILogger<BlobImageStorageService> logger) : IImageStorageService
{
    private const string ContentType = "image/webp";

    // 物件鍵含隨機值，同一把鍵永遠指向同一份內容，可放心設定長效不可變快取。
    private const string CacheControl = "public, max-age=31536000, immutable";

    private static volatile bool _containerEnsured;
    private static readonly SemaphoreSlim EnsureContainerLock = new(1, 1);

    public async Task<UploadedImageInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
    {
        if (rawBytes.Length > ImageUploadOptions.MaxUploadBytes)
        {
            throw new ImageTooLargeException();
        }

        // 純轉檔（驗證格式、去 EXIF、縮圖）全部在記憶體完成，失敗在這裡就會丟出，
        // 還沒有碰到儲存體——不會有「轉檔失敗但已經寫了一半」的情況。
        var processed = ImageProcessor.Process(rawBytes);

        await EnsureContainerAsync(cancellationToken);

        var mainKey = $"{objectKeyPrefix}/{Guid.NewGuid():N}{ImageObjectKey.Extension}";

        await UploadObjectAsync(mainKey, processed.MainWebPBytes, cancellationToken);
        foreach (var derivative in processed.Derivatives)
        {
            var derivativeKey = ImageObjectKey.ForSuffix(mainKey, derivative.SizeLabel);
            await UploadObjectAsync(derivativeKey, derivative.WebPBytes, cancellationToken);
        }

        return new UploadedImageInfo(mainKey, processed.MainWidth, processed.MainHeight, processed.MainWebPBytes.LongLength);
    }

    public async Task DeleteAsync(string? mainObjectKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mainObjectKey))
        {
            return;
        }

        try
        {
            await EnsureContainerAsync(cancellationToken);

            foreach (var key in ImageObjectKey.AllObjectKeys(mainObjectKey))
            {
                await container.GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // 🔴 fail-open（見 IImageStorageService.DeleteAsync 上的說明）：呼叫這個方法時，
            // 資料庫的新值已經寫入成功，清理舊物件失敗不該讓整個請求變成 500，只留下警告日誌
            // 與可能的孤兒物件（README「已知缺口」有記錄，不是本次沒發現）。
            logger.LogWarning(ex, "刪除舊圖片物件失敗（主檔鍵：{MainObjectKey}），忽略此錯誤繼續執行", mainObjectKey);
        }
    }

    private async Task UploadObjectAsync(string key, byte[] bytes, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        await container.GetBlobClient(key).UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = ContentType, CacheControl = CacheControl },
                // 物件鍵每次上傳都帶新的隨機值（見 UploadAsync），理論上不會撞名，但把 Overwrite
                // 設為 true 純粹是防呆——就算真的撞名（例如 Guid 極端碰撞），也是覆寫成同一次
                // 上傳自己產生的內容，不是覆寫別人的圖。
                Conditions = null,
            },
            cancellationToken);
    }

    /// <summary>容器不存在時（本機首次對 Azurite 開發、或正式環境第一次部署）自動建立一次。
    /// 🔴 旗標只在「真的成功」之後才設為 <c>true</c>——若寫成先設旗標再呼叫 API，
    /// 一旦這次呼叫失敗（例如本機曾經發生的 Azurite API 版本不相容），旗標會錯誤地卡在
    /// 「已確保」，之後每次上傳都會直接對一個從未真正建立成功的容器寫入，得到誤導性的
    /// 「容器不存在」錯誤而不是當初那個真正的失敗原因（本機驗證時實際踩過，見 README）。
    /// 用 <see cref="SemaphoreSlim"/> 而不是 <see cref="Interlocked"/> 是因為中間要 <c>await</c>，
    /// <c>Interlocked.CompareExchange</c> 包不住一段非同步呼叫。</summary>
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
