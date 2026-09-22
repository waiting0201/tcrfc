namespace Tcrfc.Api.Images;

/// <summary>
/// 後台圖片上傳通則（主站規劃書 §4.0，v3.9）的數字常數，唯一來源——不得在別處重複寫死同一組數字。
/// </summary>
public static class ImageUploadOptions
{
    /// <summary>單檔上限 10 MB（規劃書 §4.0「上傳限制」）。</summary>
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    /// <summary>主檔長邊上限，超過等比縮小；不足則維持原尺寸，不放大補齊。</summary>
    public const int MainLongEdge = 2560;

    /// <summary>固定產出的三個長邊尺寸衍生檔（由大到小）。不放大——若主檔本身小於某個尺寸，
    /// 該檔改用主檔實際尺寸（見 <see cref="ImageProcessor"/>），仍固定產出四個物件。</summary>
    public static readonly int[] DerivativeLongEdges = [1280, 640, 320];

    /// <summary>後台列表用縮圖：置中裁切為正方形。</summary>
    public const int ThumbnailSize = 160;

    /// <summary>WebP 有損編碼品質（1–100）。80 是視覺品質與檔案大小的常見平衡點，
    /// 規劃書沒有給數字，這是執行層決定，不是規格——之後要調整走 docs/17 記錄即可，不必動這裡的程式碼結構。</summary>
    public const int WebPQuality = 80;
}
