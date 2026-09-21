namespace Tcrfc.Api.Features.Schedule;

/// <summary>賽程與賽果公開欄位。<c>matches</c> 不在受限欄位清單內（docs/12b-database-tables.md §8）。</summary>
public sealed record MatchDto
{
    public required Guid Id { get; init; }
    public required string SeasonCode { get; init; }
    public required string TeamCode { get; init; }
    public required DateOnly MatchOn { get; init; }
    public string? Kickoff { get; init; }
    public string? HomeAway { get; init; }
    public string? Opponent { get; init; }
    public string? Venue { get; init; }

    /// <summary><c>matches.competition</c> 自由文字標籤（如 <c>league</c>），不是 <see cref="CompetitionName"/>。</summary>
    public string? CompetitionTag { get; init; }
    public string? CompetitionName { get; init; }
    public string? Status { get; init; }
    public int? ScoreHome { get; init; }
    public int? ScoreAway { get; init; }
    public int? RoundNo { get; init; }

    /// <summary>聯賽官方場次編號（同賽季同聯賽內唯一，可為空），與 <see cref="RoundNo"/>（第幾輪）是兩回事。
    /// 前台用它重建 mockup 原本的賽事卡片錨點 id（<c>fx-{日期}-{h|a}-{match_no}</c>）。</summary>
    public int? MatchNo { get; init; }
}
