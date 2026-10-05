namespace Tcrfc.Api.Features.AdminTrials;

public sealed record AdminTrialLocaleContent
{
    /// <summary>對象說明（例：「U15 男足，2011–2012 年出生」）。</summary>
    public required string Audience { get; init; }
}

public sealed record AdminTrialContentInput
{
    public required AdminTrialLocaleContent Zh { get; init; }
    public AdminTrialLocaleContent? En { get; init; }
}

/// <summary>新增與更新共用。<c>Status</c> 省略時：新增為「開放」、更新為維持不變；值只能是「開放」「額滿」「候補」「已結束」
/// （報名達名額上限時系統會自動由「開放」轉為「額滿」）。名額省略＝不限。
/// 是否同步到行事曆由 L3 的全站開關決定（新場次沿用目前的開關值），這裡不逐場設定。</summary>
public sealed record UpsertAdminTrialRequest
{
    /// <summary>試訓對象的球隊（可省略＝俱樂部整體的試訓）。</summary>
    public Guid? TeamId { get; init; }
    public Guid? VenueId { get; init; }
    public required DateOnly TrialOn { get; init; }
    public int? Capacity { get; init; }
    public DateOnly? DeadlineOn { get; init; }
    public string? Status { get; init; }
    public required AdminTrialContentInput Content { get; init; }
}

public sealed record AdminTrialListItemDto
{
    public required Guid Id { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamCode { get; init; }
    public string? TeamName { get; init; }
    public Guid? VenueId { get; init; }
    public string? VenueName { get; init; }
    public required DateOnly TrialOn { get; init; }
    public int? Capacity { get; init; }
    public required int EnrolledCount { get; init; }
    public DateOnly? DeadlineOn { get; init; }
    public required string Status { get; init; }

    /// <summary>前台是否還接受報名：狀態為「開放」且尚未超過報名截止日、試訓日尚未過。</summary>
    public required bool IsSignupOpen { get; init; }
    public required bool SyncToCalendar { get; init; }
    public string? AudienceZh { get; init; }
    public string? AudienceEn { get; init; }

    /// <summary>候補中的人數（供「候補遞補」提醒）。</summary>
    public required int WaitlistCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminTrialDetailDto
{
    public required Guid Id { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamCode { get; init; }
    public string? TeamName { get; init; }
    public Guid? VenueId { get; init; }
    public string? VenueName { get; init; }
    public required DateOnly TrialOn { get; init; }
    public int? Capacity { get; init; }
    public required int EnrolledCount { get; init; }
    public DateOnly? DeadlineOn { get; init; }
    public required string Status { get; init; }
    public required bool IsSignupOpen { get; init; }
    public required bool SyncToCalendar { get; init; }
    public required AdminTrialLocaleContent Zh { get; init; }
    public AdminTrialLocaleContent? En { get; init; }
    public required int WaitlistCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

// ═════════════ 試訓報名 ═════════════

public sealed record AdminTrialRegistrationListItemDto
{
    public required Guid Id { get; init; }
    public required string RegistrationNo { get; init; }
    public Guid? MemberId { get; init; }
    public required bool IsMember { get; init; }
    public required string ApplicantName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateOnly? BirthOn { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhone { get; init; }
    public string? Note { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record AdminTrialRegistrationDetailDto
{
    public required Guid Id { get; init; }
    public required string RegistrationNo { get; init; }
    public required Guid TrialId { get; init; }
    public Guid? MemberId { get; init; }
    public required string ApplicantName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateOnly? BirthOn { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhone { get; init; }
    public string? HealthDeclaration { get; init; }
    public string? Note { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    /// <summary>這筆報名所屬的試訓場次目前是否「報名人數已超過名額」（後台代填、遞補不擋超額，由承辦判斷；前台用這個旗標顯示警示）。名額未設定視為否。</summary>
    public bool IsOverCapacity { get; init; }
}

/// <summary>後台代填試訓報名（電話／現場）。狀態省略＝待確認。額滿後可直接填「候補」；系統不會替你擋下超額（後台是人為判斷）。</summary>
public sealed record CreateAdminTrialRegistrationRequest
{
    public Guid? MemberId { get; init; }
    public required string ApplicantName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateOnly? BirthOn { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhone { get; init; }
    public string? HealthDeclaration { get; init; }
    public string? Note { get; init; }
    public string? Status { get; init; }
}

/// <summary>處理試訓報名（確認／取消／加入候補／備註）：整份覆寫，狀態必填。</summary>
public sealed record UpdateAdminTrialRegistrationRequest
{
    public Guid? MemberId { get; init; }
    public required string ApplicantName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateOnly? BirthOn { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhone { get; init; }
    public string? HealthDeclaration { get; init; }
    public string? Note { get; init; }
    public required string Status { get; init; }
}

/// <summary>簽到表資料（畫面直接列印，後端不產生 PDF）：不含健康聲明與備註（資料最小化）。
/// 只列會到場的人：待確認、已確認、已繳費、完成；候補與取消不列入。</summary>
public sealed record AdminTrialSignInSheetDto
{
    public required Guid TrialId { get; init; }
    public required DateOnly TrialOn { get; init; }
    public string? TeamName { get; init; }
    public string? VenueName { get; init; }
    public string? AudienceZh { get; init; }
    public required DateTime GeneratedAt { get; init; }
    public required IReadOnlyList<AdminSignInRowDto> Rows { get; init; }
}

public sealed record AdminSignInRowDto
{
    public required int No { get; init; }
    public required string RegistrationNo { get; init; }
    public required string ApplicantName { get; init; }
    public string? Phone { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhone { get; init; }
    public required string Status { get; init; }
}
