namespace Tcrfc.Api.Features.AdminCalendar;

// ═══════════════════ L3 分類與顯示設定 ═══════════════════

public sealed record AdminCalendarTeamSettingDto
{
    public required Guid TeamId { get; init; }
    public required string Code { get; init; }
    public required string Type { get; init; }

    /// <summary>球隊本身的中文名稱（C1）。</summary>
    public string? TeamNameZh { get; init; }

    /// <summary>前台顯示名稱（中／英）。沒填＝沿用球隊名稱。</summary>
    public string? DisplayNameZh { get; init; }
    public string? DisplayNameEn { get; init; }

    /// <summary>行事曆上的代表色覆寫；沒填＝沿用球隊代表色（<c>EffectiveColour</c>）。</summary>
    public string? Colour { get; init; }
    public string? EffectiveColour { get; init; }
    public int? SortOrder { get; init; }
    public required int EffectiveSortOrder { get; init; }
    public required bool IsPublic { get; init; }
}

public sealed record AdminCalendarSettingsDto
{
    /// <summary><c>list</c>（列表）或 <c>month</c>（月曆）。</summary>
    public required string DefaultView { get; init; }

    /// <summary><c>upcoming</c>（即將到來）／<c>this_month</c>（本月）／<c>next_30_days</c>（未來 30 天）／<c>season</c>（整個球季）。</summary>
    public required string DefaultRange { get; init; }

    /// <summary>預設選取的隊別代碼；<c>all</c>＝全部。</summary>
    public required string DefaultTeamCode { get; init; }

    /// <summary>首頁「近期賽事」嵌入元件顯示哪些隊別（空＝全部公開隊別）。</summary>
    public required IReadOnlyList<string> HomeTeamCodes { get; init; }

    /// <summary>一線隊頁嵌入元件固定顯示的隊別代碼（各梯隊頁自動顯示自己那一隊，不需設定）。</summary>
    public string? FirstTeamCode { get; init; }

    /// <summary>試訓是否同步至行事曆（預設關閉）。</summary>
    public required bool SyncTrials { get; init; }
    public required IReadOnlyList<AdminCalendarTeamSettingDto> Teams { get; init; }
    public required IReadOnlyList<AdminEventTypeDto> EventTypes { get; init; }
}

public sealed record UpdateCalendarSettingsRequest
{
    public required string DefaultView { get; init; }
    public required string DefaultRange { get; init; }
    public string? DefaultTeamCode { get; init; }
    public IReadOnlyList<string>? HomeTeamCodes { get; init; }
    public string? FirstTeamCode { get; init; }
    public required bool SyncTrials { get; init; }
}

public sealed record CalendarTeamSettingInput
{
    public required Guid TeamId { get; init; }
    public string? DisplayNameZh { get; init; }
    public string? DisplayNameEn { get; init; }

    /// <summary><c>#RRGGBB</c>；省略＝沿用球隊代表色。</summary>
    public string? Colour { get; init; }
    public int? SortOrder { get; init; }
    public bool IsPublic { get; init; } = true;
}

/// <summary>整批更新隊別分類設定（沒列在裡面的球隊維持不變）。</summary>
public sealed record UpdateCalendarTeamSettingsRequest
{
    public required IReadOnlyList<CalendarTeamSettingInput> Teams { get; init; }
}

/// <summary>賽事／活動類型（兩隊共用，不帶俱樂部）：只有系統管理員能新增、修改、刪除。<c>Icon</c> 必須從系統預設圖示集選
/// （<c>GET calendar/event-types/icons</c>），不是上傳圖片。</summary>
public sealed record UpsertEventTypeRequest
{
    /// <summary>類型代碼：小寫英文字母、數字與底線／連字號，全站不可重複。更新時不可變更（省略或相同）。</summary>
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? Colour { get; init; }
    public string? Icon { get; init; }
    public bool IsPublic { get; init; } = true;
    public int SortOrder { get; init; }
}

public sealed record AdminEventTypeIconDto
{
    public required string Code { get; init; }
    public required string Label { get; init; }
}

// ═══════════════════ L1 進階：分軌、衝突、改期 ═══════════════════

/// <summary>衝突涉及的一場事件（賽事或自建活動）。</summary>
public sealed record AdminCalendarConflictEventDto
{
    public required string SourceType { get; init; }
    public required Guid SourceId { get; init; }
    public required string Title { get; init; }
    public required DateTime StartsAt { get; init; }
    public string? Kickoff { get; init; }
    public required IReadOnlyList<string> TeamCodes { get; init; }
    public string? VenueName { get; init; }
}

/// <summary>一組時間重疊的衝突。<c>Reasons</c>：<c>venue</c>（同一場地）、<c>team</c>（同一梯隊），可能同時成立。</summary>
public sealed record AdminCalendarConflictDto
{
    public required IReadOnlyList<string> Reasons { get; init; }
    public required string Description { get; init; }
    public string? VenueName { get; init; }
    public required IReadOnlyList<string> SharedTeamCodes { get; init; }
    public required AdminCalendarConflictEventDto First { get; init; }
    public required AdminCalendarConflictEventDto Second { get; init; }
}

public sealed record AdminCalendarTrackDto
{
    public required Guid TeamId { get; init; }
    public required string TeamCode { get; init; }
    public required string Name { get; init; }
    public string? Colour { get; init; }
    public required int SortOrder { get; init; }
    public required IReadOnlyList<AdminCalendarEventDto> Events { get; init; }
}

/// <summary>隊別分軌檢視：每支公開隊別一條軌道並排（一場跨隊賽事會同時出現在它涉及的每條軌道），
/// 沒有隊別的自建活動與同步的試訓放在 <c>ClubEvents</c>。<c>Conflicts</c> 是這個區間的全部衝突。</summary>
public sealed record AdminCalendarTracksDto
{
    public required DateOnly From { get; init; }
    public required DateOnly ToExclusive { get; init; }
    public required IReadOnlyList<AdminCalendarTrackDto> Tracks { get; init; }
    public required IReadOnlyList<AdminCalendarEventDto> ClubEvents { get; init; }
    public required IReadOnlyList<AdminCalendarConflictDto> Conflicts { get; init; }
}

/// <summary>拖曳改期（賽事）。<c>MatchOn</c> 必填；<c>Kickoff</c> 省略＝維持原開賽時間、空字串＝清除、<c>HH:mm</c>＝改成新時間。
/// <c>MarkAsPostponed</c>＝同時把賽事標為延賽並記下原定日期／時間；預設 false（只是改日期，例如更正輸入）。
/// 若新時段與其他事件衝突，未帶 <c>AcknowledgeConflicts=true</c> 會回 409 與衝突清單（不寫入）。</summary>
public sealed record RescheduleMatchRequest
{
    public required DateOnly MatchOn { get; init; }
    public string? Kickoff { get; init; }
    public bool MarkAsPostponed { get; init; }
    public bool AcknowledgeConflicts { get; init; }
}

/// <summary>拖曳改期（自建活動）。時間為 UTC；重複規則的活動改的是整個系列的起始時間。</summary>
public sealed record MoveCustomEventRequest
{
    public required DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public bool? IsAllDay { get; init; }
    public bool AcknowledgeConflicts { get; init; }
}

public sealed record AdminCalendarRescheduleResultDto
{
    /// <summary>false＝有衝突而且沒有帶 <c>acknowledgeConflicts</c>，<b>沒有寫入</b>（HTTP 409），請畫面警示後讓使用者確認再重送。</summary>
    public required bool Saved { get; init; }
    public required string SourceType { get; init; }
    public required Guid SourceId { get; init; }
    public required DateTime StartsAt { get; init; }
    public string? Kickoff { get; init; }
    public string? Status { get; init; }
    public DateOnly? OriginalMatchOn { get; init; }

    /// <summary>已寫入時為空（已確認過的衝突不再列出）。</summary>
    public required IReadOnlyList<AdminCalendarConflictDto> Conflicts { get; init; }

    /// <summary>改期通知：本系統目前沒有可用的通知通路（EmailLog 只是紀錄表，App 推播尚未開發），這個值恆為 false。</summary>
    public required bool NotificationSent { get; init; }
}

// ═══════════════════ L4 訂閱與匯出 ═══════════════════

public sealed record AdminCalendarSubscriptionDto
{
    /// <summary><c>all</c>（全站）或隊別代碼。</summary>
    public required string FeedKey { get; init; }
    public required string Label { get; init; }
    public required string HttpsUrl { get; init; }
    public required string WebcalUrl { get; init; }

    /// <summary>最近 30 天內不同的訂閱來源數（估計值，見 <c>README</c>：Google 行事曆由 Google 伺服器代抓，多位訂閱者會被算成同一個來源）。</summary>
    public required int Subscribers30d { get; init; }
    public required int Subscribers7d { get; init; }
    public DateOnly? LastFetchedOn { get; init; }
    public required bool IsPublic { get; init; }
}

public sealed record AdminCalendarSubscriptionsDto
{
    public required IReadOnlyList<AdminCalendarSubscriptionDto> Feeds { get; init; }
    public required string StatsNote { get; init; }
}
