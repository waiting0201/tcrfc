namespace Tcrfc.Api.Features.Programs;

/// <summary>05 課程與活動列表卡片（5.1–5.5，主站規劃書 §3.5）。刻意不含梯次明細——
/// 前台清單頁只需要知道「有沒有開放中的梯次」，逐梯次的日期／名額／費用留給詳情頁一次撈齊，
/// 避免清單頁一次把所有課程的全部梯次都送下去。</summary>
public sealed record ProgramListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? ProgramType { get; init; }
    public string? Audience { get; init; }
    public int? AgeMin { get; init; }
    public int? AgeMax { get; init; }
    public string? CoverKey { get; init; }
    public string? Name { get; init; }
    public string? Intro { get; init; }

    /// <summary>true＝至少有一個梯次目前狀態是「開放」或「候補」（還收得到報名，不論是正常
    /// 報名還是候補）——前台依此決定卡片顯示「立即報名」還是「已截止」，不需要整包梯次資料。</summary>
    public required bool HasOpenSession { get; init; }
}

public sealed record ProgramSessionDto
{
    public required Guid Id { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public string? WeeklySchedule { get; init; }
    public int? Capacity { get; init; }
    public required int EnrolledCount { get; init; }
    public int? Price { get; init; }
    public int? EarlyBirdPrice { get; init; }
    public DateOnly? EarlyBirdUntil { get; init; }
    public DateTime? SignupOpensAt { get; init; }
    public DateTime? SignupClosesAt { get; init; }
    public required string Status { get; init; }
    public Guid? VenueId { get; init; }
    public string? VenueName { get; init; }
    public string? VenueAddress { get; init; }
    public decimal? VenueLat { get; init; }
    public decimal? VenueLng { get; init; }
}

/// <summary>教練團僅回傳姓名，**不含照片**——docs/12 §12 第 32 點與 S1-7a 已確認「全系統只有
/// <c>Features/Players</c>／<c>Features/Staff</c> 兩支公開端點會依肖像同意白名單輸出球員／教練
/// 照片」，本端點若另外夾帶 <c>photo_key</c> 會繞過那道白名單、變成第三個出口，故本輪刻意不做，
/// 前台如需教練完整資料（含已同意的照片）應另外呼叫 <c>GET /api/v1/{club}/staff</c>。</summary>
public sealed record ProgramStaffSummaryDto
{
    public required Guid Id { get; init; }
    public string? Name { get; init; }
}

public sealed record ProgramPartnerSummaryDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? Name { get; init; }
    public string? LogoDarkKey { get; init; }
    public string? LogoLightKey { get; init; }
    public string? WebsiteUrl { get; init; }
}

public sealed record ProgramDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? ProgramType { get; init; }
    public string? Audience { get; init; }
    public int? AgeMin { get; init; }
    public int? AgeMax { get; init; }
    public string? CoverKey { get; init; }
    public string? Name { get; init; }
    public string? Intro { get; init; }
    public string? Content { get; init; }
    public required IReadOnlyList<ProgramStaffSummaryDto> Staff { get; init; }
    public required IReadOnlyList<ProgramPartnerSummaryDto> Partners { get; init; }
    public required IReadOnlyList<ProgramSessionDto> Sessions { get; init; }
}

/// <summary>05 課程報名表單送出（前台報名流程，主站規劃書 3.5 行 389／`docs/02-frontend-spec.md`
/// 行 102：「填寫學員資料 → 家長／緊急聯絡人 → 健康聲明與同意條款 → 送出 → 產生報名編號」）。
/// 只服務 <c>session_id</c>——<c>P4</c> 試訓報名不在本次範圍，見
/// <c>Features/AdminRegistrations/CreateAdminRegistrationRequest</c> 上的相同說明。</summary>
public sealed record SubmitProgramRegistrationRequest
{
    public required string ApplicantName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateOnly? BirthOn { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhone { get; init; }
    public string? HealthDeclaration { get; init; }
    public string? Note { get; init; }
}

public sealed record ProgramRegistrationSubmittedDto
{
    public required string RegistrationNo { get; init; }
    public required string Status { get; init; }
}
