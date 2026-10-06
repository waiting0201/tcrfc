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

    /// <summary>簡稱（<c>clubs_i18n.short_name</c>，2026-10-05）：中文「台中磐石」「台中藍鯨」；英文磐石為 <c>Taichung Rock FC</c>，
    /// 藍鯨英文<b>一律沒有</b>（B-5：客戶尚未指定英文全名，開發端不自挑）——請求英文且該語系沒有簡稱時回退繁中簡稱（同其他欄位的回退規則）。
    /// 沒有任何簡稱資料時為 <c>null</c>，用戶端退回 <see cref="Name"/>。</summary>
    public string? ShortName { get; init; }

    /// <summary>未翻譯標示（App 規劃書 §2.5「未翻譯 fallback 繁中並標示」、主站 G-01）：請求的是英文、而這筆的英文主要欄位（俱樂部名稱）是空的，回應內容是回退的繁中時為 true。
    /// 請求繁中時恆為 false。用戶端據此顯示「本內容尚無英文版本」。</summary>
    public required bool IsFallbackLocale { get; init; }
    public string? Description { get; init; }
    public required string Domain { get; init; }
    public string? OgImageKey { get; init; }
    public required string DefaultLocale { get; init; }

    /// <summary><see cref="OgImageKey"/> 完整可公開存取網址（E-64 修正）。⚠️ 這是俱樂部層級的
    /// 全站預設 OG 圖片物件鍵本身，跟 <c>Features/News/ArticleDetailDto.OgImageUrl</c>（單篇文章
    /// 已套用「專屬 &gt; 全站預設 &gt; 封面」優先序後的計算結果）不是同一個值——那裡在全站預設圖
    /// 命中時，也是拿這個俱樂部的 <see cref="OgImageKey"/> 去解析，兩處各自獨立呼叫
    /// <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/>，不是共用同一次計算。</summary>
    public string? OgImageUrl { get; init; }

    /// <summary>GEO-05（S1-12c／S1-12f）：這個俱樂部的資料是否足以輸出 Organization
    /// 結構化資料（<see cref="SchemaType.Organization"/> 必填欄位——名稱、網域——齊全）。
    /// 判斷條件單一來源見 <see cref="SchemaRequiredFields"/>，這裡不重新判斷一次（E-39）。
    /// 🔴 主站規劃書 v3.20：標誌、Favicon、品牌色由前台靜態資產與 CSS 定義、後台不設定，
    /// 本 DTO 不再含標誌欄位，Organization 的 <c>logo</c> 由前台以靜態資產輸出，故不再是資料庫必填欄位。</summary>
    public required bool SchemaEligible { get; init; }
}
