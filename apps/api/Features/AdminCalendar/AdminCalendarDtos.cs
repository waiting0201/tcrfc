namespace Tcrfc.Api.Features.AdminCalendar;

/// <summary>
/// L1 行事曆總覽的合併讀取結果——彙整 <c>matches</c>（C4，來源模組）與 <c>calendar_custom_events</c>
/// （L2，行事曆唯一自有資料）兩種來源，逐字比照主站規劃書 §4.12「資料一致性原則：行事曆是彙整層
/// 而非資料源」。<see cref="SourceType"/> 為 <c>match</c> 或 <c>custom</c>，欄位依來源各自有意義，
/// 沒有意義的一律 <c>null</c>（例如 <see cref="Status"/> 只有 <c>match</c> 有值）。
///
/// 🔴 沒有套用 <see cref="Tcrfc.Api.Security.TeamRowScope"/>——見
/// <c>AdminCalendarOverviewRepository</c> 檔頭「為什麼讀取端不套列級授權」的完整說明：賽事本身
/// 已經是 13 前台任何人都能看到的公開資訊，行事曆總覽只是換一種畫面呈現同一份資料，不因此變成
/// 需要列級限制的敏感資料。
/// </summary>
public sealed record AdminCalendarEventDto
{
    public required string SourceType { get; init; }
    public required Guid SourceId { get; init; }
    public required DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public required bool IsAllDay { get; init; }
    public required string Title { get; init; }
    public required IReadOnlyList<string> TeamCodes { get; init; }
    public string? VenueName { get; init; }

    /// <summary>僅 <c>custom</c> 有值——<c>event_types.code</c>（記者會／簽名會……）。</summary>
    public string? EventTypeCode { get; init; }

    /// <summary>僅 <c>match</c> 有值：<c>scheduled</c>／<c>live</c>／<c>played</c>／<c>postponed</c>／<c>cancelled</c>。</summary>
    public string? Status { get; init; }

    /// <summary>僅 <c>match</c> 有值：<c>主場</c>／<c>客場</c>。</summary>
    public string? HomeAway { get; init; }

    /// <summary>僅 <c>custom</c> 有值：是否公開於前台。</summary>
    public bool? IsPublic { get; init; }

    /// <summary>S2-6：場地 id（分軌檢視、衝突偵測、依場地篩選用）。</summary>
    public Guid? VenueId { get; init; }

    /// <summary>S2-6：僅 <c>match</c> 有值——開賽時間（<c>HH:mm</c>，當地時間）。<c>StartsAt</c> 對賽事只有日期，時間在這裡。</summary>
    public string? Kickoff { get; init; }

    /// <summary>S2-6：僅 <c>match</c> 有值——賽事類型（<c>league</c>／<c>cup</c>／<c>friendly</c>／<c>other</c>）。</summary>
    public string? CompetitionTag { get; init; }
}

/// <summary>L3 賽事／活動類型。<c>GET calendar/event-types</c>（L2 選單用）與 L3 維護畫面共用這個形狀；
/// S2-6 起補上 <c>IsPublic</c>／<c>SortOrder</c>／<c>UsageCount</c>（有自建事件使用時不能刪除）。</summary>
public sealed record AdminEventTypeDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public string? Colour { get; init; }
    public string? Icon { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public bool IsPublic { get; init; } = true;
    public int SortOrder { get; init; }
    public int UsageCount { get; init; }
}

public sealed record AdminCalendarEventLocaleContent
{
    public required string Title { get; init; }
    public string? Description { get; init; }

    /// <summary>封面圖片替代文字（§4.0 圖片欄位組，逐語系）。對應 <c>calendar_custom_events_i18n.cover_alt</c>。</summary>
    public string? CoverAlt { get; init; }
}

public sealed record AdminCalendarEventContentInput
{
    public required AdminCalendarEventLocaleContent Zh { get; init; }
    public AdminCalendarEventLocaleContent? En { get; init; }
}

public sealed record AdminCalendarCustomEventListItemDto
{
    public required Guid Id { get; init; }
    public required DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public required bool IsAllDay { get; init; }
    public string? RepeatRule { get; init; }
    public required bool IsPublic { get; init; }
    public string? CoverKey { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbUrl { get; init; }
    public int? CoverWidth { get; init; }
    public int? CoverHeight { get; init; }
    public required IReadOnlyList<string> TeamCodes { get; init; }
    public string? EventTypeCode { get; init; }
    public string? TitleZh { get; init; }
    public string? TitleEn { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminCalendarCustomEventDetailDto
{
    public required Guid Id { get; init; }
    public Guid? EventTypeId { get; init; }
    public Guid? VenueId { get; init; }
    public required DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public required bool IsAllDay { get; init; }
    public string? RepeatRule { get; init; }
    public DateOnly? RepeatUntil { get; init; }
    public required IReadOnlyList<DateOnly> ExceptionDates { get; init; }
    public required bool IsPublic { get; init; }
    public string? CoverKey { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbUrl { get; init; }
    public int? CoverWidth { get; init; }
    public int? CoverHeight { get; init; }
    public string? CtaUrl { get; init; }
    public required IReadOnlyList<Guid> TeamIds { get; init; }
    public required IReadOnlyList<string> TeamCodes { get; init; }
    public required AdminCalendarEventLocaleContent Zh { get; init; }
    public AdminCalendarEventLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 🔴 建立一律歸屬呼叫端當下的俱樂部（<c>calendar_custom_events.club_id = scope.ClubId</c>）——
/// <c>calendar_custom_events</c> 是 club_id 必填表，不像 <c>staff</c>／<c>articles</c> 有「兩隊共同」
/// 的可為空情境。<see cref="RepeatRule"/> 值域見
/// <see cref="Tcrfc.Api.Common.RecurrenceExpander.AllowedRepeatRules"/>（<c>weekly</c>／
/// <c>biweekly</c>／<c>monthly</c>，<c>null</c>＝不重複）；<see cref="RepeatUntil"/> 只有
/// <see cref="RepeatRule"/> 非空時才有意義。<see cref="TeamIds"/> 省略或空陣列＝「俱樂部活動」
/// （規劃書 L2「所屬隊別（可複選，或選『俱樂部活動』）」），不強制至少一支，跟 C4 賽事的
/// <c>TeamIds</c>（至少一支本方球隊）語意不同。
/// </summary>
public sealed record CreateAdminCalendarCustomEventRequest
{
    public Guid? EventTypeId { get; init; }
    public Guid? VenueId { get; init; }
    public required DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public bool IsAllDay { get; init; }
    public string? RepeatRule { get; init; }
    public DateOnly? RepeatUntil { get; init; }
    public IReadOnlyList<DateOnly>? ExceptionDates { get; init; }
    public bool IsPublic { get; init; } = true;
    public string? CtaUrl { get; init; }
    public IReadOnlyList<Guid>? TeamIds { get; init; }
    public required AdminCalendarEventContentInput Content { get; init; }
}

public sealed record UpdateAdminCalendarCustomEventRequest
{
    public Guid? EventTypeId { get; init; }
    public Guid? VenueId { get; init; }
    public required DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public bool IsAllDay { get; init; }
    public string? RepeatRule { get; init; }
    public DateOnly? RepeatUntil { get; init; }

    /// <summary>省略＝維持既有例外日期不變、提供（含空陣列）＝整份取代——比照
    /// <c>UpdateAdminMatchRequest.Goals</c> 等既有「省略＝維持不變、空陣列＝清空」語意。</summary>
    public IReadOnlyList<DateOnly>? ExceptionDates { get; init; }
    public bool IsPublic { get; init; } = true;
    public string? CtaUrl { get; init; }

    /// <summary>省略＝維持不變、空陣列＝清空為「俱樂部活動」。</summary>
    public IReadOnlyList<Guid>? TeamIds { get; init; }
    public required AdminCalendarEventContentInput Content { get; init; }

    /// <summary>true＝移除目前的封面圖，不接受同時夾帶新檔案（比照 <c>UpdateAdminProgramRequest.RemoveCover</c>）。</summary>
    public bool RemoveCover { get; init; }
}
