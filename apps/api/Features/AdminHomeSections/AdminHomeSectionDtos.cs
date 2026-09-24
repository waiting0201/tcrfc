namespace Tcrfc.Api.Features.AdminHomeSections;

/// <summary>
/// 更新單一首頁區塊的開關、排序與精選指定（規劃書 B3「首頁各區塊開關與排序、精選內容指定」）。
/// 🔴 只有 <c>hero</c> 區塊有意義填 <see cref="FeaturedBannerId"/>（<c>home_sections.featured_banner_id</c>
/// 目前是唯一一欄承載「精選指定」的欄位，見 db/club-schema.sql 該表註解）——其餘八個區塊的
/// 「精選內容指定」（例如最新消息要精選哪幾篇文章）**沒有對應欄位**，本輪不新增，見
/// apps/api/README.md 綱要缺口。其餘區塊送這個欄位一律必須是 <c>null</c>，否則 400。
/// </summary>
public sealed record UpdateHomeSectionRequest
{
    public required bool IsEnabled { get; init; }
    public required int SortOrder { get; init; }
    public Guid? FeaturedBannerId { get; init; }
}

public sealed record AdminHomeSectionDto
{
    public required Guid Id { get; init; }

    /// <summary>固定列舉代碼（見 <see cref="HomeSectionCatalog"/>），僅供前端內部對照使用；
    /// 畫面上顯示 <see cref="NameZh"/>，不直接印出這個代碼（docs/14-invariants.md
    /// 「後台介面不得出現模組代號」的同一個精神——這不是模組代號，但同樣是內部識別鍵）。</summary>
    public required string SectionCode { get; init; }

    public required string NameZh { get; init; }
    public required string NameEn { get; init; }
    public required bool IsEnabled { get; init; }
    public required int SortOrder { get; init; }
    public Guid? FeaturedBannerId { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
