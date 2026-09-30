namespace Tcrfc.Api.Features.Calendar;

/// <summary>前台讀取用（公開）：行事曆預設顯示與可選隊別。</summary>
public sealed record PublicCalendarSettingsDto
{
    public required string DefaultView { get; init; }
    public required string DefaultRange { get; init; }
    public required string DefaultTeamCode { get; init; }
    public required IReadOnlyList<string> HomeTeamCodes { get; init; }
    public string? FirstTeamCode { get; init; }
    public required IReadOnlyList<PublicCalendarTeamDto> Teams { get; init; }
    public required IReadOnlyList<PublicCalendarEventTypeDto> EventTypes { get; init; }
}

public sealed record PublicCalendarTeamDto
{
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
    public string? Colour { get; init; }
    public required int SortOrder { get; init; }
}

public sealed record PublicCalendarEventTypeDto
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Colour { get; init; }
    public string? Icon { get; init; }
}
