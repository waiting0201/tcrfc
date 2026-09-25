using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class CalendarCustomEvent
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid? EventTypeId { get; set; }

    public Guid? VenueId { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public bool IsAllDay { get; set; }

    public string? RepeatRule { get; set; }

    /// <summary>重複規則的結束日期（S1-11 新增）——主站規劃書 §4.12 L2「可設定結束日期與例外
    /// 日期」明文要求，原始 DDL 只有 <see cref="RepeatRule"/> 與例外日期表
    /// （<c>calendar_event_exceptions</c>），沒有承接「結束日期」的欄位，屬綱要落差補齊，
    /// 見 docs/12-database-schema.md §12。<c>RepeatRule</c> 為 <c>null</c>（不重複）時本欄無意義，
    /// 一律留空。</summary>
    public DateOnly? RepeatUntil { get; set; }

    public bool IsPublic { get; set; }

    public string? CoverKey { get; set; }

    public string? CtaUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<CalendarCustomEventsI18n> CalendarCustomEventsI18ns { get; set; } = new List<CalendarCustomEventsI18n>();

    public virtual ICollection<CalendarEventException> CalendarEventExceptions { get; set; } = new List<CalendarEventException>();

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual EventType? EventType { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual Venue? Venue { get; set; }
}
