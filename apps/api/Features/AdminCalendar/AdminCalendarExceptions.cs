namespace Tcrfc.Api.Features.AdminCalendar;

public abstract class AdminCalendarException(string message) : Exception(message);

public sealed class AdminCalendarValidationException(string message) : AdminCalendarException(message);

/// <summary>圖片欄位插槽把「封面圖」對到 <c>calendar_custom_events.cover_key</c> 三態，形狀比照
/// <c>Features/AdminPrograms/ProgramCoverKeyUpdate</c>。</summary>
public readonly record struct CalendarEventCoverKeyUpdate(bool Change, string? NewKey)
{
    public static readonly CalendarEventCoverKeyUpdate Keep = new(false, null);
    public static CalendarEventCoverKeyUpdate Set(string? newKey) => new(true, newKey);
}
