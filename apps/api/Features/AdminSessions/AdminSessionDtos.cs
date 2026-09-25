namespace Tcrfc.Api.Features.AdminSessions;

public sealed record AdminSessionListItemDto
{
    public required Guid Id { get; init; }
    public required Guid ProgramId { get; init; }
    public string? ProgramNameZh { get; init; }
    public Guid? VenueId { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public int? Capacity { get; init; }
    public required int EnrolledCount { get; init; }
    public int? Price { get; init; }
    public int? EarlyBirdPrice { get; init; }
    public DateOnly? EarlyBirdUntil { get; init; }
    public DateTime? SignupOpensAt { get; init; }
    public DateTime? SignupClosesAt { get; init; }
    public required string Status { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminSessionDetailDto
{
    public required Guid Id { get; init; }
    public required Guid ProgramId { get; init; }
    public Guid? VenueId { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }

    /// <summary>對應 <c>sessions.weekly_schedule</c>（SQL Server <c>json</c> 型別，週期時段表）。
    /// 只驗證語法合法性，不逐項驗證結構（規劃書只寫「上課時間表」，沒有定義逐週格式）。</summary>
    public string? WeeklySchedule { get; init; }
    public int? Capacity { get; init; }
    public required int EnrolledCount { get; init; }
    public int? Price { get; init; }
    public int? EarlyBirdPrice { get; init; }
    public DateOnly? EarlyBirdUntil { get; init; }
    public DateTime? SignupOpensAt { get; init; }
    public DateTime? SignupClosesAt { get; init; }
    public required string Status { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 🔴 <c>EnrolledCount</c>（<c>sessions.enrolled_count</c>）刻意不開放後台直接填寫——這個欄位由
/// 報名寫入路徑（<c>Features/AdminRegistrations</c>／<c>Features/Programs</c> 公開報名）維護，
/// 直接讓後台改這個數字會讓它跟 <c>registrations</c> 的實際列數脫勾，見
/// <see cref="AdminSessionsRepository"/> 檔頭「名額控管」段的完整說明。<see cref="Status"/> 省略時
/// 依 <see cref="Capacity"/>／<see cref="AdminSessionsRepository.EnrolledCount"/> 自動推定
/// （見 <see cref="AdminSessionsRepository.DeriveDefaultStatus"/>），提供時則是人工覆寫
/// （提前截止／延長開放等）。
/// </summary>
public sealed record CreateAdminSessionRequest
{
    public required Guid ProgramId { get; init; }
    public Guid? VenueId { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public string? WeeklySchedule { get; init; }
    public int? Capacity { get; init; }
    public int? Price { get; init; }
    public int? EarlyBirdPrice { get; init; }
    public DateOnly? EarlyBirdUntil { get; init; }
    public DateTime? SignupOpensAt { get; init; }
    public DateTime? SignupClosesAt { get; init; }
    public string? Status { get; init; }
}

public sealed record UpdateAdminSessionRequest
{
    public Guid? VenueId { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public string? WeeklySchedule { get; init; }
    public int? Capacity { get; init; }
    public int? Price { get; init; }
    public int? EarlyBirdPrice { get; init; }
    public DateOnly? EarlyBirdUntil { get; init; }
    public DateTime? SignupOpensAt { get; init; }
    public DateTime? SignupClosesAt { get; init; }
    public string? Status { get; init; }
}
