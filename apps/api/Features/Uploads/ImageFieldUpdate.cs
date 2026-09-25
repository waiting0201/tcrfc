namespace Tcrfc.Api.Features.Uploads;

/// <summary>
/// 選用圖片欄位的三態更新（S1-12，OG 圖片覆寫）：維持不變／清空／換成新值＋尺寸。
/// 形狀比照 <c>Features/AdminNews/CoverKeyUpdate.cs</c>，差異是這裡額外攜帶
/// <see cref="Width"/>／<see cref="Height"/>——<c>cover_key</c> 當初沒有寬高欄位（既有缺口，見
/// docs/14-invariants.md），但本輪新增的 <c>og_image_key</c> 一開始就照「圖片欄位組」通則
/// （<c>_key</c>／<c>_width</c>／<c>_height</c>）設計，三個欄位必須一起換、一起清空，故獨立
/// 宣告一個共用型別供 <c>Features/AdminNews</c>／<c>Features/AdminPages</c>／
/// <c>Features/AdminSeo</c> 三處共用，不要各自重複宣告一份幾乎相同的 record struct。
/// </summary>
public readonly record struct ImageFieldUpdate(bool Change, string? Key, int? Width, int? Height)
{
    /// <summary>維持資料庫目前的值，完全不碰這三個欄位。</summary>
    public static readonly ImageFieldUpdate Keep = new(false, null, null, null);

    /// <summary>清空（移除圖片）——三個欄位一起設為 <c>null</c>。</summary>
    public static readonly ImageFieldUpdate Remove = new(true, null, null, null);

    /// <summary>換成新上傳的圖片。</summary>
    public static ImageFieldUpdate Set(string key, int width, int height) => new(true, key, width, height);
}
