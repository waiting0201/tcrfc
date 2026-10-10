namespace Tcrfc.Api.Features.AdminCalendar;

public abstract class AdminCalendarException(string message) : Exception(message);

public sealed class AdminCalendarValidationException(string message, string? field = null) : AdminCalendarException(message), Tcrfc.Api.Common.IFieldApiException
{
    public IReadOnlyDictionary<string, string> FieldErrors { get; } = Tcrfc.Api.Common.FieldKey.Single(field, message);
}

/// <summary>圖片欄位插槽把「封面圖」對到 <c>calendar_custom_events.cover_key</c> 三態，形狀比照
/// <c>Features/AdminPrograms/ProgramCoverKeyUpdate</c>。</summary>
public readonly record struct CalendarEventCoverKeyUpdate(bool Change, string? NewKey, int? Width = null, int? Height = null)
{
    public static readonly CalendarEventCoverKeyUpdate Keep = new(false, null);

    /// <summary>換圖時帶主檔縮小後的寬高；清空時 <paramref name="newKey"/> 傳 null，寬高一併清成 null。</summary>
    public static CalendarEventCoverKeyUpdate Set(string? newKey, int? width = null, int? height = null) => new(true, newKey, width, height);
}
