namespace Tcrfc.Api.Images;

/// <summary>
/// 把資料庫存的物件鍵（例如 <c>articles.cover_key</c>／<c>clubs.og_image_key</c>）換成一個
/// 完整可公開存取的網址（S1-12 驗收退回後補做，2026-09-25：OG 圖片要真的輸出 <c>og:image</c>，
/// 前台需要完整網址，不能只有物件鍵）。
///
/// 🔴 **這是本專案第一次真的把圖片物件鍵換成公開網址**——先前所有前台圖片顯示（新聞封面、
/// 球員照片……）全部繞過這條路，改用 mockup 既有的靜態檔名慣例（見
/// <c>apps/web/app/utils/news.ts</c> 檔頭「已知資料落差」），因為種子資料的 <c>*_key</c> 欄位
/// 從未真正寫入過 Blob。這裡補的是「有鍵就能算出網址」這個機制本身，不代表容器的公開讀取權限
/// 或 CDN 網域已經設定完成——那是部署層的決定（見 apps/api/README.md「S1-12」段的判斷說明），
/// 這裡回傳的網址在容器沒開公開讀取前不會是真的可存取，但**格式與計算規則是正確的**，等部署層
/// 把容器設為公開讀取或接上 CDN 自訂網域，不需要再改這裡的程式碼。
/// </summary>
public interface IImagePublicUrlResolver
{
    /// <summary><paramref name="objectKey"/> 為 <c>null</c>／空字串時回傳 <c>null</c>
    /// （沒有圖片，呼叫端不應該輸出任何 <c>og:image</c> 之類的標籤）。</summary>
    string? Resolve(string? objectKey);
}
