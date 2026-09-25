namespace Tcrfc.Api.Features.AdminRegistrations;

public sealed record AdminRegistrationListItemDto
{
    public required Guid Id { get; init; }
    public required string RegistrationNo { get; init; }
    public Guid? SessionId { get; init; }
    public string? ProgramNameZh { get; init; }
    public Guid? TrialId { get; init; }
    public Guid? MemberId { get; init; }
    public required bool IsMember { get; init; }
    public required string ApplicantName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// 🔴 <c>HealthDeclaration</c>（健康聲明）維持現行 DDL 的明文欄位（<c>nvarchar(max)</c>），
/// **未做加密、未做欄位級遮罩**——docs/12b-database-tables.md §8「受限與加密欄位盤點」明文標注
/// <c>Registration.health_declaration</c> 是「🔐 建議，⚠️ 待法務確認」，真正待確認的是《個資法》
/// §6 特種個資的蒐集要件與保存期限，不是儲存方式本身（見該檔案「真正要確認的是能不能蒐集、要不要
/// 蒐集、保存多久」段）。這一層法務判斷超出本次任務邊界，本檔僅依現行決定（明文欄位、由
/// <c>program.registration.view</c> 權限碼控管可見範圍）處理，不自行加密或建立覈實流程，
/// 見任務回報「規劃書沒寫清楚、自行判斷」。
/// </summary>
public sealed record AdminRegistrationDetailDto
{
    public required Guid Id { get; init; }
    public required string RegistrationNo { get; init; }
    public Guid? SessionId { get; init; }
    public Guid? TrialId { get; init; }
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
}

/// <summary>後台代填報名（電話／現場報名）。<c>P4</c>（試訓）不在本次範圍，故本檔只服務
/// <c>session_id</c>，不接受 <c>trial_id</c>——見 <c>db/club-schema.sql</c>
/// <c>CK_registrations_session_or_trial</c>（兩者恰有一個非空）與任務指示「P4 不在本次範圍」。</summary>
public sealed record CreateAdminRegistrationRequest
{
    public required Guid SessionId { get; init; }
    public Guid? MemberId { get; init; }
    public required string ApplicantName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public DateOnly? BirthOn { get; init; }
    public string? GuardianName { get; init; }
    public string? GuardianPhone { get; init; }
    public string? HealthDeclaration { get; init; }
    public string? Note { get; init; }

    /// <summary>省略時預設 <c>待確認</c>（比照 <c>registrations.status</c> 的 DDL 預設值）。</summary>
    public string? Status { get; init; }
}

/// <summary>
/// 報名處理（規劃書 §4.4 P3「操作：確認／取消、轉梯次、加入候補、備註」）：<see cref="Status"/>
/// 涵蓋確認／取消／加入候補／標記已繳費／完成；<see cref="SessionId"/> 非空且與目前不同即為
/// 「轉梯次」，會連動調整新舊兩個梯次的 <c>enrolled_count</c>，見
/// <see cref="AdminRegistrationsRepository"/> 檔頭「名額連動」段。學員資料／家長聯絡／健康聲明／
/// 備註採整份覆寫（跟 <c>CreateAdminRegistrationRequest</c> 同一組欄位），不是逐欄 patch——
/// 後台編輯頁預期會先讀出整筆再送出整份，比照本專案既有模組的更新語意。
/// </summary>
public sealed record UpdateAdminRegistrationRequest
{
    public required Guid SessionId { get; init; }
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
