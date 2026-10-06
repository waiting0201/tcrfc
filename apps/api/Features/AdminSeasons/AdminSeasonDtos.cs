namespace Tcrfc.Api.Features.AdminSeasons;

/// <summary>賽季（<c>seasons</c>）清單與詳情共用。<see cref="Code"/> 同俱樂部唯一（如 <c>2026/27</c>）；
/// <see cref="Usage"/> 列出被哪些資料使用（空陣列＝可刪除），<see cref="InUse"/> 為其摘要，畫面可據此停用刪除鈕。</summary>
public sealed record AdminSeasonDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required DateOnly StartOn { get; init; }
    public required DateOnly EndOn { get; init; }
    public required bool InUse { get; init; }
    public required IReadOnlyList<AdminSeasonUsageDto> Usage { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>某類資料引用這個賽季的筆數。<see cref="Label"/> 是日常中文（賽事系列、賽事、積分榜、榮譽、球員賽季數據、會籍、會籍方案）。</summary>
public sealed record AdminSeasonUsageDto
{
    public required string Label { get; init; }
    public required int Count { get; init; }
}

public sealed record CreateAdminSeasonRequest
{
    public required string Code { get; init; }
    public required DateOnly StartOn { get; init; }
    public required DateOnly EndOn { get; init; }
}

public sealed record UpdateAdminSeasonRequest
{
    public required string Code { get; init; }
    public required DateOnly StartOn { get; init; }
    public required DateOnly EndOn { get; init; }
}
