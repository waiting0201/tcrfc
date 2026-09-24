namespace Tcrfc.Api.Images;

/// <summary>上傳成功後回傳給呼叫端（進而回給前端表單）的資訊。<see cref="Key"/> 是主檔物件鍵，
/// 存進資料表對應欄位（例如 <c>articles.cover_key</c>）；四個衍生檔的鍵由 <see cref="ImageObjectKey"/>
/// 從這個主檔鍵推導，不另存欄位。</summary>
public sealed record UploadedImageInfo(string Key, int Width, int Height, long SizeBytes);

/// <summary>
/// 後台圖片上傳共用元件的儲存層接縫（規劃書 §4.0「後台圖片上傳通則」，S0-8）。
/// 正式環境接 Azure Blob Storage，本機開發接 Azurite——兩者共用同一組連線字串格式，
/// 呼叫端（各 Features 模組）完全不需要知道現在接的是哪一個。
/// </summary>
public interface IImageStorageService
{
    /// <summary>
    /// 驗證＋重新編碼＋產生四個衍生檔＋全部寫入物件儲存，成功才回傳。任何一步失敗
    /// （格式不支援、檔案過大、儲存體寫入失敗）一律丟出**原例外**：格式驗證與轉檔失敗時
    /// 根本還沒碰到儲存體，不會有任何物件被寫入；五個物件（主檔＋四個衍生檔）寫到一半才失敗時，
    /// 實作端會盡力刪除本次呼叫已經寫入的物件再拋出原例外（S0-8c，補償刪除本身失敗只會記錄
    /// 警告，不會吞掉或取代原例外）——**盡力**是因為刪除動作本身也可能因為儲存體同時不可用而
    /// 失敗，這種雙重失敗的情況仍可能留下孤兒物件，屬已知殘留風險，見
    /// apps/api/README.md「圖片上傳共用元件」已知缺口。
    /// </summary>
    /// <param name="rawBytes">上傳檔案的原始位元組（呼叫端已先擋過 multipart 的 Content-Length）。</param>
    /// <param name="objectKeyPrefix">物件鍵的路徑前綴（例如 <c>"tcrfc/articles/{id}/cover"</c>），
    /// 呼叫端負責組出「這張圖屬於哪一筆資料的哪一個欄位」，本服務只在後面接一段隨機值＋副檔名。</param>
    Task<UploadedImageInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken);

    /// <summary>
    /// 依主檔鍵刪除主檔與全部衍生檔（規劃書 §4.0「換圖與刪除」：「主檔與其全部衍生檔一起刪」）。
    /// <paramref name="mainObjectKey"/> 為 <c>null</c>／空字串時視為沒有圖片，直接略過。
    /// 🔴 fail-open：儲存體暫時不可用時只記警告日誌，不丟例外——這是刪除舊物件的收尾動作，
    /// 呼叫時資料庫的新值已經寫入成功，不應該讓一個非關鍵的清理步驟讓整個請求失敗
    /// （代價是可能留下孤兒物件，可接受，見 README「已知缺口」）。
    /// </summary>
    Task DeleteAsync(string? mainObjectKey, CancellationToken cancellationToken);
}
