namespace Tcrfc.Api.Documents;

/// <summary>檔案上傳驗證失敗的例外家族，全部對應 400，訊息是日常中文，比照 <c>Images.ImageProcessingException</c>。</summary>
public abstract class DocumentProcessingException(string message) : Exception(message);

public sealed class EmptyDocumentException()
    : DocumentProcessingException("沒有收到檔案，請重新選擇檔案。");

public sealed class DocumentTooLargeException()
    : DocumentProcessingException("檔案太大（上限 50 MB），請換一個或先壓縮。");

public sealed class UnsupportedDocumentFormatException()
    : DocumentProcessingException("檔案格式不支援，請上傳 PDF 或 ZIP 壓縮檔。");
