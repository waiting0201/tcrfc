namespace Tcrfc.Api.Features.AdminOpponentTeams;

/// <summary>對手球隊庫清單項目（C4，主站規劃書 v3.31）。<see cref="UsageCount"/>＝被賽事與積分榜引用的總數（&gt; 0 不能刪）。</summary>
public sealed record AdminOpponentTeamListItemDto
{
    public required Guid Id { get; init; }

    /// <summary>隊名（繁中）。</summary>
    public required string Name { get; init; }

    /// <summary>隊名（英文）；未填為 <c>null</c>，前台回退繁中。</summary>
    public string? NameEn { get; init; }

    /// <summary>隊徽主檔網址（沒有隊徽為 <c>null</c>）。</summary>
    public string? CrestUrl { get; init; }

    /// <summary>隊徽 160px 方形縮圖網址（後台列表用）。</summary>
    public string? CrestThumbUrl { get; init; }

    public required int SortOrder { get; init; }
    public required int UsageCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminOpponentTeamDetailDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? NameEn { get; init; }
    public string? CrestUrl { get; init; }
    public string? CrestThumbUrl { get; init; }
    public int? CrestWidth { get; init; }
    public int? CrestHeight { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>被多少場賽事引用。</summary>
    public required int MatchCount { get; init; }

    /// <summary>被多少筆積分榜列引用。</summary>
    public required int StandingCount { get; init; }

    public required int UsageCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 建立／更新對手球隊。以 <c>multipart/form-data</c> 送出：<c>payload</c>（本型別的 JSON）＋選填檔案欄位 <c>crest</c>（隊徽）。
/// 更新是整份取代；沒帶檔案且 <see cref="RemoveCrest"/> 為 false＝維持原隊徽。英文隊名空白＝刪除英文版。
/// </summary>
public sealed record UpsertAdminOpponentTeamRequest
{
    public string? Name { get; init; }
    public string? NameEn { get; init; }
    public int SortOrder { get; init; }
    public bool RemoveCrest { get; init; }
}
