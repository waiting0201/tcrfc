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

    /// <summary>活動類型顯示名稱（依 <c>lang</c>，缺譯回退中文）；未設類型為 <c>null</c>。B-6。</summary>
    public string? EventTypeName { get; init; }

    /// <summary>活動類型代表色（如 <c>#C8102E</c>），可為 <c>null</c>。</summary>
    public string? EventTypeColour { get; init; }

    /// <summary>活動類型圖示代碼，可為 <c>null</c>。</summary>
    public string? EventTypeIcon { get; init; }

    /// <summary>此活動是否設有重複規則（每週／每兩週／每月）。為 true 時同一個 <c>Id</c> 會出現多筆（每個發生日一筆）。</summary>
    public bool IsRecurring { get; init; }

    /// <summary>每個發生次數的唯一鍵（<c>{活動id}:{yyyyMMddHHmm}</c>），前台 <c>v-for</c> 的 key 用這個，
    /// 因為重複活動的 <c>Id</c> 會重複。match 來源恆為 <c>null</c>。</summary>
    public string? OccurrenceId { get; init; }
    public string? Description { get; init; }
    public string? CtaUrl { get; init; }
    public string? CoverKey { get; init; }

    /// <summary><see cref="CoverKey"/> 完整可公開存取網址（E-64 修正，2026-09-29），由
    /// <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/> 算出，比照
    /// <c>Features/Staff/StaffDto.PhotoUrl</c> 的既有慣例。<c>null</c>＝這則自建活動沒有封面圖。
    /// match 來源事件恆為 <c>null</c>（<c>matches</c> 沒有封面圖欄位）。</summary>
    public string? CoverUrl { get; init; }
}
