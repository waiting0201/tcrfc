namespace Tcrfc.Api.Features.MembershipPublic;

/// <summary>會籍方案（單人／家庭）。對應 8.2 Membership Plans 與會員中心升級頁。<c>Fee</c> 是整數元；App 首版依規劃書不顯示金額，由客戶端決定要不要顯示。</summary>
public sealed record MembershipPlanPublicDto
{
    public required string Code { get; init; }
    public string? Name { get; init; }
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
    public required IReadOnlyList<BenefitGroupPublicDto> Groups { get; init; }
}

public sealed record PartnerStorePublicDto
{
    public required string Slug { get; init; }
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string? Region { get; init; }
    public string? Address { get; init; }
    public decimal? Lat { get; init; }
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
