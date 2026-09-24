namespace Tcrfc.Api.Features.AdminCompetitions;

public sealed record AdminCompetitionLocaleContent
{
    public required string Name { get; init; }
    public string? Organizer { get; init; }
}

public sealed record AdminCompetitionContentInput
{
    public required AdminCompetitionLocaleContent Zh { get; init; }
    public AdminCompetitionLocaleContent? En { get; init; }
}

public sealed record AdminCompetitionListItemDto
{
    public required Guid Id { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public required string Code { get; init; }
    public string? CompType { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminCompetitionDetailDto
{
    public required Guid Id { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public required string Code { get; init; }
    public string? CompType { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required AdminCompetitionLocaleContent Zh { get; init; }
    public AdminCompetitionLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 🔴 <c>Status</c> 只接受 <c>draft</c>／<c>published</c>——<c>competitions.status</c> 的 CHECK
/// 約束允許 <c>scheduled</c>，但這張表**沒有 <c>published_at</c> 欄位**（docs/14-invariants.md
/// 「S0-7g」段已裁決：規劃書只在 B1／B2 給了排程發布，其餘表不補欄位、且「不得提供排程選項」）。
/// 這裡在寫入層直接擋掉 <c>scheduled</c>，不是漏做，是照已拍板的不變量。
/// </summary>
public sealed record CreateAdminCompetitionRequest
{
    public required Guid SeasonId { get; init; }
    public required string Code { get; init; }
    public string? CompType { get; init; }
    public int SortOrder { get; init; }
    public string Status { get; init; } = "draft";
    public required AdminCompetitionContentInput Content { get; init; }
}

public sealed record UpdateAdminCompetitionRequest
{
    public required Guid SeasonId { get; init; }
    public required string Code { get; init; }
    public string? CompType { get; init; }
    public int SortOrder { get; init; }
    public required string Status { get; init; }
    public required AdminCompetitionContentInput Content { get; init; }
}
