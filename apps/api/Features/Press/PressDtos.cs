namespace Tcrfc.Api.Features.Press;

/// <summary>7.8 媒體專區資源。檔案不直接給網址：一律經 <see cref="DownloadPath"/>（累計下載次數後轉址）。</summary>
public sealed record PressResourceDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }

    /// <summary><c>press_release</c>（新聞稿）／<c>brand_kit</c>（品牌識別包）／<c>hires_image</c>（高解析圖）。</summary>
    public required string ResourceType { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public DateOnly? PublishedOn { get; init; }
    public int? FileBytes { get; init; }

    /// <summary>檔案副檔名（<c>.pdf</c>／<c>.zip</c>／<c>.webp</c>），供前台顯示檔案類型。</summary>
    public string? FileExtension { get; init; }
    public string? CoverUrl { get; init; }

    /// <summary>封面圖片替代文字（§4.0）：請求語系優先、空白回退繁中；沒有封面圖時為 null。</summary>
    public string? CoverAlt { get; init; }

    /// <summary>下載連結（相對於 API 根目錄的路徑，<c>GET</c> 會累計下載次數後 302 轉址到檔案）。</summary>
    public required string DownloadPath { get; init; }
}
