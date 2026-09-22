namespace Tcrfc.Api.Images;

/// <summary>一個已完成的衍生檔：長邊尺寸標籤（1280／640／320／thumb）＋ WebP 位元組。</summary>
public sealed record ProcessedDerivative(string SizeLabel, int Width, int Height, byte[] WebPBytes);

/// <summary>
/// <see cref="ImageProcessor.Process"/> 的輸出：主檔（已重新編碼、已去除中繼資料）＋固定四個衍生檔，
/// 全部是解碼、縮放、轉檔完成、純粹待寫入儲存體的位元組，本類別不做任何 I/O。
/// </summary>
public sealed record ProcessedImageSet(
    byte[] MainWebPBytes,
    int MainWidth,
    int MainHeight,
    IReadOnlyList<ProcessedDerivative> Derivatives);
