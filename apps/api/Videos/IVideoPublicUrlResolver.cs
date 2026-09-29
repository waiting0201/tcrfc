namespace Tcrfc.Api.Videos;

/// <summary>
/// 把資料庫存的影片物件鍵（<c>banners.video_key</c>）換成一個完整可公開存取的網址，
/// 跟 <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/> 是同一個機制、分開宣告——
/// 影片走獨立的 Blob 容器（<c>AZURE_BLOB_CONTAINER_VIDEOS</c>，見 <c>Program.cs</c>／
/// <c>Videos/BlobVideoStorageService.cs</c> 的具名 DI 說明），不能共用圖片那顆
/// <see cref="Azure.Storage.Blobs.BlobContainerClient"/> 單例算出來的網址（容器名稱不同，
/// 算出來的網址會指到錯誤的容器）。
/// </summary>
public interface IVideoPublicUrlResolver
{
    /// <summary><paramref name="objectKey"/> 為 <c>null</c>／空字串時回傳 <c>null</c>
    /// （沒有影片，呼叫端不應該輸出任何 <c>&lt;video&gt;</c> 來源網址）。</summary>
    string? Resolve(string? objectKey);
}
