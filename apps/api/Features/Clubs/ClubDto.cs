namespace Tcrfc.Api.Features.Clubs;

/// <summary>
/// 俱樂部主檔的公開欄位。前台用作「事實單一來源」（主站規劃書 §7 GEO-03：名稱與簡介只在一處維護）。
/// 不含 <c>invoice_title</c>／<c>tax_id</c>（發票與稅務用途，不是前台事實內容，本次不公開）、
/// <c>status</c>（後台操作狀態，前台不需要）。
/// </summary>
public sealed record ClubDto
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Domain { get; init; }
    public string? LogoLightKey { get; init; }
    public string? LogoDarkKey { get; init; }
    public string? FaviconKey { get; init; }
    public string? OgImageKey { get; init; }
    public string? BrandColor { get; init; }
    public string? BrandSecondaryColor { get; init; }
    public required string DefaultLocale { get; init; }
}
