using Tcrfc.Api.Features.Seo;

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

    /// <summary>隊徽完整可公開存取網址（S1-12f 新增），由 <c>LogoLightKey</c> 經
    /// <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/> 算出，<c>null</c>＝沒有隊徽物件鍵可用
    /// （目前種子資料恆為此情形，見 <see cref="SchemaEligible"/> 說明）。</summary>
    public string? LogoUrl { get; init; }

    /// <summary>GEO-05（S1-12c／S1-12f）：這個俱樂部的資料是否足以輸出 Organization
    /// 結構化資料（<see cref="SchemaType.Organization"/> 必填欄位——名稱、網域、隊徽——齊全）。
    /// 判斷條件單一來源見 <see cref="SchemaRequiredFields"/>，這裡不重新判斷一次（E-39）。
    /// 🔴 **已知現況**：<c>clubs.logo_light_key</c> 目前的種子資料與既有後台（<c>AdminClubDetailDto</c>
    /// 對標誌三組欄位刻意唯讀）皆未提供寫入路徑，本欄位在兩個俱樂部現況下恆為 <c>false</c>——
    /// 這是 GEO-05「缺漏者不輸出該型別」的正確行為，不是本次任務的缺陷，回報見
    /// apps/web/README.md「S1-12f」節。</summary>
    public required bool SchemaEligible { get; init; }
}
