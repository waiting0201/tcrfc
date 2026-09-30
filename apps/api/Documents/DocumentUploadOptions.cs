namespace Tcrfc.Api.Documents;

/// <summary>
/// 後台「檔案」上傳（非圖片、非影片：新聞稿 PDF、品牌識別包壓縮檔、贊助提案 PDF）的數字常數，唯一來源。
/// 🔴 **執行層決定，規劃書不記技術選型**（CLAUDE.md 全域規定）：規劃書 §4.0 的圖片上傳通則只涵蓋圖片，
/// E3「上傳提案 PDF」與 B6 新聞稿／品牌識別包沒有給檔案大小與格式上限，這裡採最小可行：
/// 只收 PDF 與 ZIP（以檔頭判斷，不信任副檔名），單檔 ≤ 50 MB（與 Hero 影片同一個量級，
/// Kestrel 請求主體上限 <c>ImageUploadOptions.MaxUploadBytes + VideoUploadOptions.MaxUploadBytes + 1 MB</c>
/// 足以容納「檔案 ≤ 50 MB ＋ 封面圖 ≤ 10 MB」的同一次 multipart 請求）。
/// 伺服器端不掃毒、不檢查壓縮檔內容——這是已知邊界，見 apps/api/README.md 的 B6／E3 段。
/// </summary>
public static class DocumentUploadOptions
{
    public const long MaxUploadBytes = 50 * 1024 * 1024;

    public const string PdfContentType = "application/pdf";
    public const string ZipContentType = "application/zip";
}
