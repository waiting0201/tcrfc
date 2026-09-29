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

    /// <summary><see cref="LogoDarkKey"/> 完整可公開存取網址（E-64 修正，2026-09-29）——同一批
    /// 「回傳未解析物件鍵」缺口盤點時一併補上，算法與 <see cref="LogoUrl"/> 相同。</summary>
    public string? LogoDarkUrl { get; init; }

    /// <summary><see cref="FaviconKey"/> 完整可公開存取網址（E-64 修正）。</summary>
    public string? FaviconUrl { get; init; }

    /// <summary><see cref="OgImageKey"/> 完整可公開存取網址（E-64 修正）。⚠️ 這是俱樂部層級的
    /// 全站預設 OG 圖片物件鍵本身，跟 <c>Features/News/ArticleDetailDto.OgImageUrl</c>（單篇文章
    /// 已套用「專屬 &gt; 全站預設 &gt; 封面」優先序後的計算結果）不是同一個值——那裡在全站預設圖
    /// 命中時，也是拿這個俱樂部的 <see cref="OgImageKey"/> 去解析，兩處各自獨立呼叫
    /// <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/>，不是共用同一次計算。</summary>
    public string? OgImageUrl { get; init; }

    /// <summary>GEO-05（S1-12c／S1-12f）：這個俱樂部的資料是否足以輸出 Organization
    /// 結構化資料（<see cref="SchemaType.Organization"/> 必填欄位——名稱、網域、隊徽——齊全）。
    /// 判斷條件單一來源見 <see cref="SchemaRequiredFields"/>，這裡不重新判斷一次（E-39）。
    /// 🔴 **已知現況**：<c>clubs.logo_light_key</c> 目前的種子資料與既有後台（<c>AdminClubDetailDto</c>
    /// 對標誌三組欄位刻意唯讀）皆未提供寫入路徑，本欄位在兩個俱樂部現況下恆為 <c>false</c>——
    /// 這是 GEO-05「缺漏者不輸出該型別」的正確行為，不是本次任務的缺陷，回報見
    /// apps/web/README.md「S1-12f」節。</summary>
    public required bool SchemaEligible { get; init; }
}
