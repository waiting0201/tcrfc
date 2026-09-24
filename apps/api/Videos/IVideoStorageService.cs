namespace Tcrfc.Api.Videos;

/// <summary>上傳成功後回傳給呼叫端的資訊。<see cref="Key"/> 是物件鍵，存進資料表對應欄位
/// （<c>banners.video_key</c>）。跟 <c>Images.UploadedImageInfo</c> 不同，這裡沒有寬高——
/// 影片不做伺服器端轉檔／量測，播放尺寸交由前端 <c>&lt;video&gt;</c> 元素自行取得。</summary>
public sealed record UploadedVideoInfo(string Key, long SizeBytes);

/// <summary>
/// 後台影片上傳共用元件的儲存層接縫（v3.14，Hero 輪播「圖／影片」，見
/// docs/17-deployment.md §6「Hero 輪播影片上傳」）。正式環境接 Azure Blob Storage，
/// 本機開發接 Azurite——跟 <c>Images.IImageStorageService</c> 同一組帳號、不同容器
/// （<c>AZURE_BLOB_CONTAINER_VIDEOS</c>，預設 <c>videos</c>，見 <c>Program.cs</c>）。
/// </summary>
public interface IVideoStorageService
{
    /// <summary>
    /// 驗證＋寫入物件儲存，成功才回傳。**不轉碼、不產生任何衍生檔**——收到的位元組原封不動
    /// 存成一個物件（跟圖片上傳「一張圖產五個物件」的既有元件刻意不同，見
    /// <see cref="VideoUploadOptions"/> 檔頭「伺服器端不轉碼」的理由）。任何一步失敗（格式不支援、
    /// 檔案過大、儲存體寫入失敗）一律丟出**原例外**：格式驗證失敗時根本還沒碰到儲存體。
    /// </summary>
    /// <param name="rawBytes">上傳檔案的原始位元組（呼叫端已先擋過 multipart 的 Content-Length）。</param>
    /// <param name="objectKeyPrefix">物件鍵的路徑前綴（例如 <c>"tcrfc/banners/{id}/video"</c>），
    /// 呼叫端負責組出「這支影片屬於哪一筆資料的哪一個欄位」，本服務只在後面接一段隨機值＋副檔名。</param>
    Task<UploadedVideoInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken);

    /// <summary>依物件鍵刪除。<paramref name="objectKey"/> 為 <c>null</c>／空字串時視為沒有影片，
    /// 直接略過。🔴 fail-open：儲存體暫時不可用時只記警告日誌，不丟例外——跟
    /// <c>IImageStorageService.DeleteAsync</c> 同一種收尾動作，不應該讓一個非關鍵的清理步驟
    /// 讓整個請求失敗。</summary>
    Task DeleteAsync(string? objectKey, CancellationToken cancellationToken);
}
