namespace Tcrfc.Api.Images;

/// <summary>
/// 圖片上傳驗證失敗的例外家族，全部對應 400，訊息一律是日常中文（docs/06-conventions.md §1，
/// 後台介面不得出現英文技術詞），由 <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/> 統一轉狀態碼，
/// 跟既有 <c>AdminArticleException</c> 家族同一種做法。
/// </summary>
public abstract class ImageProcessingException(string message) : Exception(message);

/// <summary>沒有選檔，或送出的是空檔案。</summary>
public sealed class EmptyImageException()
    : ImageProcessingException("沒有收到圖片檔案，請重新選擇圖片。");

/// <summary>超過單檔 10 MB 上限（規劃書 §4.0「上傳限制」，逐字沿用規劃書指定的介面文案）。</summary>
public sealed class ImageTooLargeException()
    : ImageProcessingException("圖片檔案太大（上限 10 MB），請換一張或先壓縮。");

/// <summary>
/// 伺服器端以實際檔頭判斷格式後，解不開或不在允許清單內（JPG／PNG／WebP）。
/// ⚠️ 這包含 HEIC/HEIF——ImageSharp 不解 HEIC（docs/17-deployment.md §6 已定案：前端瀏覽器
/// 應先轉成 JPEG 再送，但伺服器端不得假設前端一定轉過，收到解不開的檔一律回絕）。
/// 也包含「副檔名是 .jpg 但內容其實是純文字」這類假副檔名檔案——判斷依據是檔頭，不是副檔名。
/// </summary>
public sealed class UnsupportedImageFormatException()
    : ImageProcessingException("圖片格式不支援，請上傳 JPG、PNG 或 WebP 格式的圖片（不支援 HEIC）。");
