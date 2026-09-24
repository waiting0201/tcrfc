namespace Tcrfc.Api.Features.Teams;

/// <summary>球隊公開欄位（C1）。<c>teams</c> 不在 docs/12b-database-tables.md §8 受限欄位清單內——
/// 球隊主檔（代號、類型、性別、代表色）是球隊官網例行公開的資訊。S1-7 新增：既有的 S0-7b 唯讀端點
/// 只做了球員／教練與職員／新聞／賽程／俱樂部主檔五組，球隊本身沒有獨立的公開清單端點——
/// 前台的球隊選單、行事曆分類（13）、學院年齡層頁需要這份清單，本輪補上，是新增端點不是契約變更。</summary>
public sealed record TeamDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Type { get; init; }
    public required string Gender { get; init; }
    public string? AgeBand { get; init; }
    public string? TeamColor { get; init; }
    public string? HeroKey { get; init; }
    public string? Name { get; init; }
    public string? Intro { get; init; }
}
