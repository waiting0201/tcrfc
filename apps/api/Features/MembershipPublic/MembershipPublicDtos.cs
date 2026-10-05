namespace Tcrfc.Api.Features.MembershipPublic;

/// <summary>會籍方案（單人／家庭）。對應 8.2 Membership Plans 與會員中心升級頁。<c>Fee</c> 是整數元；App 首版依規劃書不顯示金額，由客戶端決定要不要顯示。</summary>
public sealed record MembershipPlanPublicDto
{
    public required string Code { get; init; }
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

public sealed record BenefitItemPublicDto
{
    public string? Name { get; init; }
    /// <summary>未翻譯標示（App 規劃書 §2.5）：請求英文而英文權益名稱是空的，回應是回退的繁中時為 true；請求繁中恆為 false。</summary>
    public required bool IsFallbackLocale { get; init; }

    public string? Description { get; init; }
    /// <summary>免費層對應值（「✓」「✗」或文字如「9 折」）。</summary>
    public string? FreeValue { get; init; }
    public string? PaidValue { get; init; }
}

public sealed record BenefitGroupPublicDto
{
    /// <summary><c>member_card</c>／<c>store_discount</c>／<c>jersey</c>／<c>event</c>。</summary>
    public required string Group { get; init; }
    public required string GroupLabel { get; init; }
    public required IReadOnlyList<BenefitItemPublicDto> Items { get; init; }
}

/// <summary>權益對照表（免費與付費逐條對照）：一份資料、多處使用（加入會員頁、8.2 球迷會頁、會員中心升級頁、App 升級頁），未登入即可讀。</summary>
public sealed record BenefitTablePublicDto
{
    public string? PlanCode { get; init; }
    public string? PlanName { get; init; }
    /// <summary>未翻譯標示（App 規劃書 §2.5）：請求英文而英文方案名稱是空的，回應是回退的繁中時為 true；請求繁中恆為 false。</summary>
    public required bool IsFallbackLocale { get; init; }

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
    /// <summary><c>all</c>（全會員適用）／<c>fan_club</c>（限付費會員）。</summary>
    public required string ApplicableTier { get; init; }
    public required string ApplicableTierLabel { get; init; }
    public string? MapUrl { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? ImageUrl { get; init; }
    /// <summary>true＝兩隊共同的店家（<c>club_id</c> 為空）。</summary>
    public required bool IsShared { get; init; }
}

public sealed record PartnerStoreFiltersDto
{
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<string> Regions { get; init; }
}
