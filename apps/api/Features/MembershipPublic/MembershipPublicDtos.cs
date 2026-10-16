namespace Tcrfc.Api.Features.MembershipPublic;

/// <summary>
/// 會籍方案。對應 8.2 球迷會方案頁、3.14 加入會員頁與 App 會籍方案頁。台中磐石為兩級（<c>standard</c> 磐石會員／<c>premium</c> 尊尚會員），藍鯨方案由藍鯨自訂；
/// 層級 <see cref="Tier"/> 隨方案。<c>Fee</c> 是整數元（建議年費）；App 首版依規劃書不顯示金額，由客戶端決定要不要顯示。
/// <b>不提供會籍期間內升級</b>：方案只供「尚無該球季有效會籍」者購買或到期後續約改選。
/// </summary>
public sealed record MembershipPlanPublicDto
{
    public required string Code { get; init; }
    /// <summary>層級代碼：<c>standard</c>／<c>premium</c>。</summary>
    public required string Tier { get; init; }
    public required string TierLabel { get; init; }
    /// <summary>商店會員折扣比例（0–100 的整數百分比；0＝無折扣）。適用商品由 S1 逐商品勾選。</summary>
    public required int ShopDiscountPercent { get; init; }
    public string? Name { get; init; }
    /// <summary>未翻譯標示（App 規劃書 §2.5）：請求英文而英文方案名稱是空的，回應是回退的繁中時為 true；請求繁中恆為 false。</summary>
    public required bool IsFallbackLocale { get; init; }
    public string? BenefitNote { get; init; }
    public required int Fee { get; init; }
    public required int CardQuota { get; init; }
    public required int JerseyQuota { get; init; }
    public string? MidSeasonRule { get; init; }
    public required string SeasonCode { get; init; }
    public required DateOnly StartsOn { get; init; }
    public required DateOnly EndsOn { get; init; }
}

/// <summary>權益對照表的一格：某條權益在某個方案的對應值（「✓」「✗」或文字如「10%」「一件」）；該方案沒有這一條時 <c>Value</c> 為 <c>null</c>。</summary>
public sealed record BenefitPlanValueDto
{
    public required string PlanCode { get; init; }
    public string? Value { get; init; }
}

public sealed record BenefitItemPublicDto
{
    /// <summary>條目代碼：同一俱樂部各方案以代碼對齊成對照表的一列。</summary>
    public required string Code { get; init; }
    public string? Name { get; init; }
    /// <summary>未翻譯標示（App 規劃書 §2.5）：請求英文而英文權益名稱是空的，回應是回退的繁中時為 true；請求繁中恆為 false。</summary>
    public required bool IsFallbackLocale { get; init; }

    public string? Description { get; init; }

    /// <summary>各方案的對應值，<b>順序與 <see cref="BenefitTablePublicDto.Plans"/> 一致、每個方案一格</b>（沒有該條權益的方案 <c>Value</c> 為 <c>null</c>）。</summary>
    public required IReadOnlyList<BenefitPlanValueDto> Values { get; init; }
}

public sealed record BenefitGroupPublicDto
{
    /// <summary><c>member_card</c>（會員卡）／<c>discount</c>（折扣與優惠）／<c>gift</c>（禮品）／<c>event</c>（活動）／<c>content</c>（內容與溝通）。</summary>
    public required string Group { get; init; }
    public required string GroupLabel { get; init; }
    public required IReadOnlyList<BenefitItemPublicDto> Items { get; init; }
}

/// <summary>權益對照表的一欄：一個目前可購買的方案。</summary>
public sealed record BenefitPlanColumnDto
{
    public required string Code { get; init; }
    public required string Tier { get; init; }
    public required string TierLabel { get; init; }
    public string? Name { get; init; }
    /// <summary>未翻譯標示（App 規劃書 §2.5）：請求英文而英文方案名稱是空的，回應是回退的繁中時為 true；請求繁中恆為 false。</summary>
    public required bool IsFallbackLocale { get; init; }
    public required int Fee { get; init; }
}

/// <summary>
/// 權益對照表（<b>以方案為欄</b>，v3.29）：一份資料、多處使用（加入會員頁、8.2 球迷會頁、會員中心方案頁、App 會籍方案頁），未登入即可讀。
/// <see cref="Plans"/> 是欄（目前可購買的已上架方案，依排序）；每一列 <see cref="BenefitItemPublicDto"/> 以 <c>Code</c> 對齊各方案，<c>Values</c> 與 <see cref="Plans"/> 同序。
/// 沒有任何可購買方案時 <c>Plans</c> 與 <c>Groups</c> 皆為空。
/// </summary>
public sealed record BenefitTablePublicDto
{
    public required IReadOnlyList<BenefitPlanColumnDto> Plans { get; init; }
    public required IReadOnlyList<BenefitGroupPublicDto> Groups { get; init; }
}

public sealed record PartnerStorePublicDto
{
    public required string Slug { get; init; }
    public string? Name { get; init; }

    /// <summary>未翻譯標示（App 規劃書 §2.5）：請求英文而英文店名是空的，回應是回退的繁中時為 true；請求繁中恆為 false。</summary>
    public required bool IsFallbackLocale { get; init; }
    public string? Category { get; init; }
    public string? Region { get; init; }
    public string? Address { get; init; }

    /// <summary>緯度。<b><c>null</c>＝座標尚未確認</b>：App 地圖不顯示、不計距離、不排進「附近店家」。**不會**用 <c>0</c> 或 <c>(0,0)</c> 當替代值——
    /// 後台驗證與資料庫 <c>CK_partner_stores_coords</c> 都不允許（有值時必與經度成對、在 -90～90、且不是 (0,0)）。</summary>
    public decimal? Lat { get; init; }

    /// <summary>經度，規則同 <see cref="Lat"/>（-180～180）。</summary>
    public decimal? Lng { get; init; }
    public string? Phone { get; init; }
    public string? BusinessHours { get; init; }
    public string? OfferContent { get; init; }
    /// <summary><c>all</c>（所有會員）／<c>premium</c>（限尊尚層級）。</summary>
    public required string ApplicableTier { get; init; }
    public required string ApplicableTierLabel { get; init; }
    public string? MapUrl { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? ImageUrl { get; init; }

    /// <summary>圖片寬高（像素）與替代文字（當前語系，英文空白回退繁中）；沒有圖片時三者皆 <c>null</c>。</summary>
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public string? ImageAlt { get; init; }
    /// <summary>true＝兩隊共同的店家（<c>club_id</c> 為空）。</summary>
    public required bool IsShared { get; init; }
}

public sealed record PartnerStoreFiltersDto
{
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<string> Regions { get; init; }
}
