namespace Tcrfc.Api.Features.Honors;

/// <summary>02 關於：榮譽（年份、賽事、名次、關聯球隊）。</summary>
public sealed record AchievementDto
{
    public required Guid Id { get; init; }
    public int? Year { get; init; }
    public string? SeasonCode { get; init; }
    public required string TeamCode { get; init; }
    public string? TeamName { get; init; }
    public string? CompetitionName { get; init; }
    public string? Placing { get; init; }
}

/// <summary>02 關於：里程碑時間軸（只列後台標為「顯示於時間軸」的項目，依日期由舊到新）。</summary>
public sealed record MilestoneDto
{
    public required Guid Id { get; init; }
    public required DateOnly HappenedOn { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageAlt { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
}
