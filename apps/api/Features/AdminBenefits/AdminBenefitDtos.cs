namespace Tcrfc.Api.Features.AdminBenefits;

public sealed record AdminBenefitLocaleContent
{
    /// <summary>條目名稱。</summary>
    public required string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>免費層對應值（「✓」「✗」或文字如「9 折」）。</summary>
    public string? FreeValue { get; init; }

    /// <summary>付費層對應值。</summary>
    public string? PaidValue { get; init; }
}

public sealed record AdminBenefitContentInput
{
    public required AdminBenefitLocaleContent Zh { get; init; }
    public AdminBenefitLocaleContent? En { get; init; }
}

/// <summary>新增與更新共用。<c>Group</c>：<c>member_card</c>（會員卡）／<c>store_discount</c>（店家折扣）／<c>jersey</c>（球衣）／<c>event</c>（活動）。
/// <c>Status</c>：<c>published</c>（上架）／<c>draft</c>（下架）。更新時 <c>PlanId</c> 不可變更（省略或相同）。
/// <c>SortOrder</c> 省略＝排在該方案最後。</summary>
public sealed record UpsertAdminBenefitRequest
{
    public required Guid PlanId { get; init; }
    public required string Group { get; init; }
    public int? SortOrder { get; init; }
    public string Status { get; init; } = "draft";
    public required AdminBenefitContentInput Content { get; init; }
}

public sealed record AdminBenefitListItemDto
{
    public required Guid Id { get; init; }
    public required Guid PlanId { get; init; }
    public required string PlanCode { get; init; }
    public string? PlanName { get; init; }
    public required string SeasonCode { get; init; }
    public required string Group { get; init; }
    public required string GroupLabel { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? FreeValueZh { get; init; }
    public string? PaidValueZh { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminBenefitDetailDto
{
    public required Guid Id { get; init; }
    public required Guid PlanId { get; init; }
    public required string PlanCode { get; init; }
    public string? PlanName { get; init; }
    public required string SeasonCode { get; init; }
    public required string Group { get; init; }
    public required string GroupLabel { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required AdminBenefitLocaleContent Zh { get; init; }
    public AdminBenefitLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminBenefitGroupDto
{
    public required string Code { get; init; }
    public required string Label { get; init; }
}

/// <summary>權益條目排序：<c>Ids</c> 必須都屬於同一個方案。</summary>
public sealed record ReorderBenefitsRequest
{
    public required Guid PlanId { get; init; }
    public required IReadOnlyList<Guid> Ids { get; init; }
}
