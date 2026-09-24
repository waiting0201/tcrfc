namespace Tcrfc.Api.Videos;

/// <summary>
/// 純驗證邏輯（不含任何 I/O、不轉檔）：以檔頭位元組判斷是否為 MP4／ISO Base Media File Format
/// 容器，逐字比照 <c>Images/ImageProcessor</c>「不看副檔名、只信任實際檔頭」的既有原則
/// （docs/18-work-errors.md 沒有專門記過影片驗證，但同一條紀律：假副檔名檔案要擋得下來）。
/// </summary>
public static class VideoValidator
{
    /// <summary>
    /// ISO Base Media File Format（MP4／MOV／M4A 等共用的容器格式）的第一個 box 幾乎一定是
    /// <c>ftyp</c>：位元組配置為 4 位元組大小（big-endian）＋ 4 位元組型別字串。合法 MP4 檔案的
    /// 第 4–7 個位元組（0-based）因此應該是 ASCII 的 <c>"ftyp"</c>。這是業界慣用的 MP4 magic
    /// bytes 偵測法（不倚賴副檔名），能擋下「副檔名是 .mp4 但內容其實是純文字或其他格式」這類
    /// 假副檔名檔案。
    /// </summary>
    private static readonly byte[] FtypSignature = "ftyp"u8.ToArray();

    public static void Validate(byte[] rawBytes)
    {
        if (rawBytes.Length == 0)
        {
            throw new EmptyVideoException();
        }

        if (rawBytes.Length > VideoUploadOptions.MaxUploadBytes)
        {
            throw new VideoTooLargeException();
        }

        if (rawBytes.Length < 12)
        {
            // 連 ftyp box 的表頭都放不下，不可能是合法的 MP4 檔案。
            throw new UnsupportedVideoFormatException();
        }

        var candidate = rawBytes.AsSpan(4, 4);
        if (!candidate.SequenceEqual(FtypSignature))
        {
            throw new UnsupportedVideoFormatException();
        }
    }
}
