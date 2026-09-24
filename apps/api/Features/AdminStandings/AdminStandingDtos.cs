namespace Tcrfc.Api.Features.AdminStandings;

/// <summary>
/// 積分榜（<c>standings</c>）——⚠️ **這張表沒有球隊維度**（<c>team_name</c> 是自由文字，不是
/// <c>Team</c> 外鍵，db/club-schema.sql 註解「對手隊名是自由文字，不是 Team」），一份積分榜代表
/// 「這個俱樂部、這個賽季的一張聯賽排名表」，同一張表裡本方球隊與對手球隊都用純文字列出。
/// **因此本模組不套用 <see cref="Tcrfc.Api.Security.TeamRowScope"/> 列級授權**——沒有結構化的
/// <c>team_id</c> 欄位可以拿來檢查「這一列屬於哪支本方球隊」，見
/// <c>AdminStandingsRepository</c> 檔頭「為什麼不套列級授權」的完整說明，已列入本次回報的
/// 「綱要缺口或待裁決」。
/// </summary>
public sealed record AdminStandingListItemDto
{
    public required Guid Id { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public required string TeamName { get; init; }
    public int? Rank { get; init; }
    public int? Played { get; init; }
    public int? Points { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminStandingDetailDto
{
    public required Guid Id { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public required string TeamName { get; init; }
    public int? Rank { get; init; }
    public int? Played { get; init; }
    public int? Points { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record CreateAdminStandingRequest
{
    public required Guid SeasonId { get; init; }
    public required string TeamName { get; init; }
    public int? Rank { get; init; }
    public int? Played { get; init; }
    public int? Points { get; init; }
}

public sealed record UpdateAdminStandingRequest
{
    public required Guid SeasonId { get; init; }
    public required string TeamName { get; init; }
    public int? Rank { get; init; }
    public int? Played { get; init; }
    public int? Points { get; init; }
}

public sealed record StandingCsvImportRowErrorDto
{
    public required int RowNumber { get; init; }
    public required string Reason { get; init; }
}

/// <summary>
/// 🔴 **CSV 匯入是「整季替換」，不是逐列 upsert**——見
/// <see cref="AdminStandingsRepository.ImportCsvAsync"/> 檔頭的完整說明。
/// <see cref="ReplacedCount"/> 是這個賽季匯入後的總列數（等於新檔案的資料列數），
/// <see cref="DeletedCount"/> 是匯入前先清掉的這個賽季既有列數，供畫面顯示「原本 N 筆被換成 M 筆」。
/// </summary>
public sealed record StandingCsvImportResultDto
{
    public required int ReplacedCount { get; init; }
    public required int DeletedCount { get; init; }
    public required IReadOnlyList<StandingCsvImportRowErrorDto> Errors { get; init; }
}
