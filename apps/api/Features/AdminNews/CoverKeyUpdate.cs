namespace Tcrfc.Api.Features.AdminNews;

/// <summary>
/// 🔴🔴🔴 S0-8 修正（規劃書 §4.0／第 53 行「選檔不上傳、儲存才上傳」）：更新文章時，封面圖片欄位
/// 要不要變更、變更後的新值是什麼——三態（維持不變／清空／換成新值），由
/// <see cref="AdminArticlesEndpoints"/> 依「這次請求有沒有夾 <c>file</c> 這個 multipart 欄位」
/// 「<see cref="UpdateArticleRequest.RemoveCover"/> 有沒有打勾」解出來，傳進 repository 前就已經
/// 確定語意，repository（<see cref="AdminArticlesRepository"/>）不重新判斷、只負責套用。
///
/// 為什麼不直接把 <c>string?</c> 塞回 <see cref="UpdateArticleRequest"/>：兩段式做法舊版本是
/// 「呼叫端上傳完拿到 key，原封塞進 <c>CoverKey</c> 欄位」，這個欄位天生分不出「呼叫端沒有要改」
/// 跟「呼叫端故意要清空」——舊契約靠「呼叫端永遠把目前的 key 原封送回來」這個前端慣例硬撐，
/// 單一請求契約下沒有這個前提了（呼叫端可能根本不知道現在的 key 是什麼字串），需要一個明確的
/// 「要不要變更」旗標，不能只看新值是不是 <c>null</c>。
/// </summary>
public readonly record struct CoverKeyUpdate(bool Change, string? NewKey)
{
    /// <summary>維持資料庫目前的值，完全不碰這個欄位。</summary>
    public static readonly CoverKeyUpdate Keep = new(false, null);

    /// <summary>換成新值——<paramref name="newKey"/> 為 <c>null</c> 代表「清空封面圖片」。</summary>
    public static CoverKeyUpdate Set(string? newKey) => new(true, newKey);
}
