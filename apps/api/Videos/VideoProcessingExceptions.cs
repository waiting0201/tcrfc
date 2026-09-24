namespace Tcrfc.Api.Videos;

/// <summary>
/// 影片上傳驗證失敗的例外家族，全部對應 400，訊息一律是日常中文（docs/06-conventions.md §1），
/// 由 <see cref="Tcrfc.Api.Common.ApiExceptionHandler"/> 統一轉狀態碼，逐字比照
/// <c>Images/ImageProcessingException</c> 家族的既有做法。
/// </summary>
public abstract class VideoProcessingException(string message) : Exception(message);

/// <summary>沒有選檔，或送出的是空檔案。</summary>
public sealed class EmptyVideoException()
    : VideoProcessingException("沒有收到影片檔案，請重新選擇影片。");

/// <summary>超過單檔 50 MB 上限（docs/17-deployment.md §6）。</summary>
public sealed class VideoTooLargeException()
    : VideoProcessingException("影片檔案太大（上限 50 MB），請換一支較短或先壓縮。");

/// <summary>
/// 伺服器端以實際檔頭（<c>ftyp</c> box，ISO Base Media File Format 容器格式的識別碼）判斷，
/// 不是 MP4 容器，或副檔名與內容不符。⚠️ **這只驗證容器格式，不驗證內部視訊／音訊編碼是否真的是
/// H.264／AAC**——完整驗證編碼需要解封裝＋讀取軌道資訊的媒體處理函式庫，本服務刻意不引入
/// （見 <see cref="VideoUploadOptions"/> 檔頭「伺服器端不轉碼」的理由），這是已知的驗證邊界，
/// 不是遺漏，見 docs/17-deployment.md §6「已知邊界」。
/// </summary>
public sealed class UnsupportedVideoFormatException()
    : VideoProcessingException("影片格式不支援，請上傳 MP4（H.264／AAC）格式的影片。");
