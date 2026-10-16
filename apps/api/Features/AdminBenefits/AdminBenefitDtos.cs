namespace Tcrfc.Api.Features.AdminBenefits;

public sealed record AdminBenefitLocaleContent
{
    /// <summary>條目名稱。</summary>
    public required string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>該方案的對應值（「✓」「✗」或文字如「10%」「一件」）。權益對照表以方案為欄，每個方案各有自己的條目與對應值（v3.29）。</summary>
    public string? Value { get; init; }
}

public sealed record AdminBenefitContentInput
{
    public required AdminBenefitLocaleContent Zh { get; init; }
    public AdminBenefitLocaleContent? En { get; init; }
}

/// <summary>新增與更新共用。<c>Group</c>：<c>member_card</c>（會員卡）／<c>discount</c>（折扣與優惠）／<c>gift</c>（禮品）／<c>event</c>（活動）／<c>content</c>（內容與溝通）。
/// <c>Code</c>＝條目代碼：同一俱樂部各方案以代碼對齊成權益對照表，同一方案內唯一；<b>新增時省略＝由伺服器產生</b>（<c>item_</c>＋8 碼），
/// 要在另一個方案放「同一條權益」時，沿用既有條目的代碼；<b>更新時不可變更</b>（省略或相同）。小寫英數與底線，1–64 字元。
/// <c>Status</c>：<c>published</c>（上架）／<c>draft</c>（下架）。更新時 <c>PlanId</c> 不可變更（省略或相同）。
/// <c>SortOrder</c> 省略＝排在該方案最後。</summary>
public sealed record UpsertAdminBenefitRequest
{
    public required Guid PlanId { get; init; }
    public string? Code { get; init; }
    public required string Group { get; init; }
    public int? SortOrder { get; init; }
    public string Status { get; init; } = "draft";
    public required AdminBenefitContentInput Content { get; init; }
}

public sealed record AdminBenefitListItemDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
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
    public string? ValueZh { get; init; }
    public string? ValueEn { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminBenefitDetailDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
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
