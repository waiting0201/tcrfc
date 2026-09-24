namespace Tcrfc.Api.Features.AdminTeams;

/// <summary>球隊授權（J4，<c>admin_user_teams</c>）畫面用的下拉選單資料——前端 agent 回報缺口②。
/// 跨俱樂部（見 <c>AdminTeamsEndpoints</c> 檔頭說明），所以每一列都帶著俱樂部代碼與名稱，
/// 前端可以依俱樂部分組顯示，不必再逐一查詢俱樂部主檔。</summary>
public sealed record AdminTeamListItemDto
{
    public required Guid Id { get; init; }
    public required Guid ClubId { get; init; }
    public required string ClubCode { get; init; }
    public string? ClubNameZh { get; init; }
    public required string Code { get; init; }
    public required string Type { get; init; }
    public required string Gender { get; init; }
    public string? AgeBand { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
}
