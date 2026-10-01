using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Tcrfc.Api.Images;

namespace Tcrfc.Api.CharityPlatform.Storage;

/// <summary>
/// 慈善平台的圖片儲存接縫（店家 Logo、項目封面）。規則與主站相同（規劃書 §4.0 圖片上傳通則：重新編碼為 WebP、
/// 長邊上限 2560、不留原始檔、去 EXIF、固定產 1280／640／320＋160 方形縮圖），圖片處理直接重用純函式
/// <see cref="ImageProcessor"/> 與鍵推導 <see cref="ImageObjectKey"/>；差別只在<b>容器</b>：
/// 慈善圖片放在自己的容器（<c>AZURE_BLOB_CONTAINER_CHARITY</c>，預設 <c>charity-images</c>），
/// 連線字串用 <c>AZURE_BLOB_CONNECTION_STRING_CHARITY</c>（docs/20 §7.2：慈善圖片是否改走獨立 Storage Account
/// 是待確認項，本機先與主站同一個 Azurite、容器分開，日後換帳號只要改這個連線字串）。
/// 🔴 刻意不包 <c>BlobImageStorageService</c>：它的「容器已建立」旗標是 <c>static</c>（全行程只認得第一個容器），
/// 兩個容器共用會讓後建立的那個永遠不被確認存在。
/// </summary>
public interface ICharityImageStorage
{
    /// <summary>物件鍵 → 完整公開網址；鍵為空或儲存體未設定回傳 <c>null</c>。</summary>
    string? Resolve(string? objectKey);

    Task<UploadedImageInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken);

    /// <summary>刪除主檔與全部衍生檔；fail-open（理由同 <see cref="IImageStorageService.DeleteAsync"/>）。</summary>
    Task DeleteAsync(string? mainObjectKey, CancellationToken cancellationToken);
}

public sealed class BlobCharityImageStorage(BlobContainerClient container, ILogger<BlobCharityImageStorage> logger) : ICharityImageStorage
{
    private const string ContentType = "image/webp";
    private const string CacheControl = "public, max-age=31536000, immutable"; // 物件鍵含隨機值，內容不可變

    private readonly SemaphoreSlim _ensureLock = new(1, 1);
    private volatile bool _ensured;

    public string? Resolve(string? objectKey)
        => string.IsNullOrWhiteSpace(objectKey) ? null : container.GetBlobClient(objectKey).Uri.ToString();

    public async Task<UploadedImageInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
    {
        if (rawBytes.Length > ImageUploadOptions.MaxUploadBytes)
        {
            throw new ImageTooLargeException();
        }

        // 純轉檔在記憶體完成，失敗在這裡就丟出，還沒碰到儲存體。
        var processed = ImageProcessor.Process(rawBytes);
        await EnsureContainerAsync(cancellationToken);

        var mainKey = $"{objectKeyPrefix}/{Guid.NewGuid():N}{ImageObjectKey.Extension}";
        try
        {
            await UploadObjectAsync(mainKey, processed.MainWebPBytes, cancellationToken);
            foreach (var derivative in processed.Derivatives)
            {
                await UploadObjectAsync(ImageObjectKey.ForSuffix(mainKey, derivative.SizeLabel), derivative.WebPBytes, cancellationToken);
            }
        }
        catch
        {
            // 五個物件寫到一半失敗：盡力補償刪除本次已寫入的（用 None，請求被取消時補償也要跑完）。
            await DeleteAsync(mainKey, CancellationToken.None);
            throw;
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
            foreach (var key in ImageObjectKey.AllObjectKeys(mainObjectKey))
            {
                await container.GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // fail-open：資料庫已寫入成功，清理舊物件失敗不該讓請求變 500，只留警告日誌與可能的孤兒物件。
            logger.LogWarning(ex, "刪除慈善圖片物件失敗（主檔鍵：{MainObjectKey}），忽略此錯誤繼續執行", mainObjectKey);
        }
    }

    private async Task UploadObjectAsync(string key, byte[] bytes, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        await container.GetBlobClient(key).UploadAsync(
            stream,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = ContentType, CacheControl = CacheControl } },
            cancellationToken);
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken)
    {
        if (_ensured)
        {
            return;
        }

        await _ensureLock.WaitAsync(cancellationToken);
        try
        {
            if (_ensured)
            {
                return;
            }

            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            _ensured = true; // 成功之後才設旗標（失敗時卡在「已確保」會讓之後的錯誤訊息誤導）
        }
        finally
        {
            _ensureLock.Release();
        }
    }
}

/// <summary>未設定 <c>AZURE_BLOB_CONNECTION_STRING_CHARITY</c> 時的替身：讀取一律回傳沒有圖片，上傳丟訊息清楚的例外
/// （不讓行程無法啟動，多數環境與測試完全不需要上傳）。</summary>
public sealed class UnavailableCharityImageStorage : ICharityImageStorage
{
    public string? Resolve(string? objectKey) => null;

    public Task<UploadedImageInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
        => throw new Common.CharityServiceUnavailableException("圖片上傳功能尚未設定儲存空間，請聯繫系統管理員。");

    public Task DeleteAsync(string? mainObjectKey, CancellationToken cancellationToken) => Task.CompletedTask;
}
