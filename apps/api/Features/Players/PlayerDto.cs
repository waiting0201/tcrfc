namespace Tcrfc.Api.Features.Players;

/// <summary>
/// 球員名單公開欄位。<c>players</c> 不在 docs/12b-database-tables.md §8 受限欄位清單內——
/// 球員名冊（含生日／慣用腳等）是球隊官網例行公開的競技資訊，不是一般會員個資。
/// </summary>
public sealed record PlayerDto
{
    public required Guid Id { get; init; }
    public required string TeamCode { get; init; }
    public int? ShirtNo { get; init; }
    public string? Position { get; init; }
    public DateOnly? BirthOn { get; init; }
    public int? HeightCm { get; init; }
    public int? WeightKg { get; init; }
    public string? Nationality { get; init; }
    public string? PreferredFoot { get; init; }
    public string? PhotoKey { get; init; }
    public string? Name { get; init; }
    public string? Bio { get; init; }
}
