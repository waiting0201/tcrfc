namespace Tcrfc.Api.Features.Calendar;

/// <summary>
/// 13 賽事行事曆的公開合併讀取欄位——彙整 <c>matches</c>（C4）與**公開**的 <c>calendar_custom_events</c>
/// （L2，<c>is_public = 1</c>）兩種來源。<c>SourceType</c> 為 <c>match</c> 或 <c>custom</c>，欄位依
/// 來源各自有意義，沒有意義的一律 <c>null</c>（比照既有 <c>Features/Schedule/MatchDto</c> 的既有寫法）。
/// </summary>
public sealed record PublicCalendarEventDto
{
    public required string SourceType { get; init; }
    public required Guid Id { get; init; }
    public required DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public required bool IsAllDay { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<string> TeamCodes { get; init; }
    public string? VenueName { get; init; }

    // ── match-only 欄位（比照既有 Features/Schedule/MatchDto，本端點沒有取代它，是給行事曆頁
    //    合併月曆／「全部」分頁使用的另一種形狀）──────────────────────────────────────
    public string? SeasonCode { get; init; }
    public string? CompetitionTag { get; init; }
    public string? CompetitionName { get; init; }
    public string? Status { get; init; }
    public string? HomeAway { get; init; }
    public int? ScoreHome { get; init; }
    public int? ScoreAway { get; init; }
    public int? RoundNo { get; init; }
    public int? MatchNo { get; init; }
    public DateOnly? OriginalMatchOn { get; init; }
    public string? OriginalKickoff { get; init; }

    // ── custom-only 欄位 ─────────────────────────────────────────────────
    public string? EventTypeCode { get; init; }
    public string? Description { get; init; }
    public string? CtaUrl { get; init; }
    public string? CoverKey { get; init; }
}
