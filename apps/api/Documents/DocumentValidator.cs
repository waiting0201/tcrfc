namespace Tcrfc.Api.Documents;

/// <summary>檔頭（magic bytes）判斷檔案格式，逐字比照 <c>Videos.VideoValidator</c>「不看副檔名、只信任實際內容」的原則。</summary>
public static class DocumentValidator
{
    /// <summary>回傳 <c>(副檔名, Content-Type)</c>；不支援的格式丟 <see cref="UnsupportedDocumentFormatException"/>。</summary>
    public static (string Extension, string ContentType) Validate(byte[] rawBytes)
    {
        if (rawBytes.Length == 0)
        {
            throw new EmptyDocumentException();
        }

        if (rawBytes.Length > DocumentUploadOptions.MaxUploadBytes)
        {
            throw new DocumentTooLargeException();
        }

        // PDF：以 "%PDF-" 開頭。
        if (rawBytes.Length >= 5 && rawBytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
        {
            return (".pdf", DocumentUploadOptions.PdfContentType);
        }

        // ZIP：本機檔頭 PK\x03\x04（空壓縮檔的 PK\x05\x06 不收）。
        if (rawBytes.Length >= 4 && rawBytes[0] == 0x50 && rawBytes[1] == 0x4B && rawBytes[2] == 0x03 && rawBytes[3] == 0x04)
        {
            return (".zip", DocumentUploadOptions.ZipContentType);
        }

        throw new UnsupportedDocumentFormatException();
    }
}
