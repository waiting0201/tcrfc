using Tcrfc.Api.Features.Seo;

namespace Tcrfc.Api.Features.Schedule;

/// <summary>賽程與賽果公開欄位。<c>matches</c> 不在受限欄位清單內（docs/12b-database-tables.md §8）。</summary>
public record MatchDto
{
    public required Guid Id { get; init; }
    public required string SeasonCode { get; init; }
    public required string TeamCode { get; init; }

    /// <summary>所屬俱樂部代碼（等於請求路徑的 <c>{club}</c>）。App 的賽程把兩個俱樂部合在一起顯示時用它標註（缺口 A2）。</summary>
    public required string ClubCode { get; init; }

    /// <summary>賽事系列代碼（<c>competitions.code</c>，如 <c>CTFA-1</c>）。沒有掛賽事系列的賽事（只有自由文字 <see cref="CompetitionTag"/>）為 <c>null</c>。
    /// 對應 <c>GET /{club}/competitions</c> 的 <c>code</c>，也是賽程 <c>?competition=</c> 篩選值。</summary>
    public string? CompetitionCode { get; init; }
    public required DateOnly MatchOn { get; init; }
    public string? Kickoff { get; init; }

    /// <summary>開賽時刻（UTC，ISO 8601 帶 <c>Z</c>）：由 <see cref="MatchOn"/>＋<see cref="Kickoff"/>（台北當地牆上時間，<c>Asia/Taipei</c> UTC+8、無夏令時間）
    /// 換算的衍生欄位，**不是儲存值**（資料庫仍是 <c>date</c>＋<c>HH:mm</c>）。<see cref="Kickoff"/> 為空時為 <c>null</c>（不猜 00:00）。
    /// 用戶端要顯示或排提醒請用這個值（依裝置時區顯示），不必各自換算；<see cref="MatchOn"/>／<see cref="Kickoff"/> 保留給「當地牆上時間」呈現。</summary>
    public DateTime? KickoffAt { get; init; }
    public string? HomeAway { get; init; }
    /// <summary>對手名稱（已解析）：與 <see cref="OpponentName"/> 相同；保留這個欄位是相容舊用戶端。</summary>
    public string? Opponent { get; init; }

    /// <summary>對手球隊庫（<c>opponent_teams</c>）的 id；賽事沒有挑球隊庫時為 <c>null</c>（此時對手只有文字）。</summary>
    public Guid? OpponentTeamId { get; init; }

    /// <summary>對手名稱（請求語系）：有球隊庫就用庫內隊名（英文空白回退繁中），否則退回賽事上的對手文字。</summary>
    public string? OpponentName { get; init; }

    /// <summary>對手隊徽（各尺寸網址）；沒有球隊庫或庫內沒有隊徽為 <c>null</c>。</summary>
    public OpponentCrestDto? OpponentCrest { get; init; }

    /// <summary>未翻譯標示（App 規劃書 §2.5「未翻譯 fallback 繁中並標示」、主站 G-01）：請求的是英文、而這筆的英文主要欄位（對手）是空的，回應內容是回退的繁中時為 true。
    /// 請求繁中時恆為 false。用戶端據此顯示「本內容尚無英文版本」。</summary>
    public required bool IsFallbackLocale { get; init; }
    /// <summary>場地名稱（請求語系）：賽事有場地庫（<c>venue_id</c>）就用庫內名稱（英文空白回退繁中），否則退回賽事上的場地文字。</summary>
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

    /// <summary>延賽前的原定日期（v3.13）。只有 <see cref="Status"/> 為「延賽」（<c>postponed</c>）時才有值，
    /// 其餘狀態一律為 <c>null</c>——不是「賽程還沒排」的意思。對應 <c>matches.original_match_on</c>。</summary>
    public DateOnly? OriginalMatchOn { get; init; }

    /// <summary>延賽前的原定時間（v3.13）。與 <see cref="OriginalMatchOn"/> 同一組欄位，只有延賽時才有值，
    /// 格式與 <see cref="Kickoff"/> 相同。對應 <c>matches.original_kickoff</c>。</summary>
    public string? OriginalKickoff { get; init; }

    /// <summary>
    /// GEO-05／S1-12c：這筆賽事的資料是否足以輸出 <c>SportsEvent</c> Schema
    /// （<see cref="SchemaType.SportsEvent"/> 必填欄位齊全，見 <see cref="SchemaRequiredFields"/>
    /// 檔頭「必填欄位怎麼訂出來的」）。前台（<c>app/pages/zh/schedule.vue</c>）改讀這個欄位決定
    /// 輸不輸出該筆的 JSON-LD 節點，不再自己重新判斷一次「這六個欄位夠不夠」——判斷條件只在
    /// <see cref="SchemaRequiredFields"/> 宣告一次（E-39）。
    /// </summary>
    public required bool SchemaEligible { get; init; }

    /// <summary>我方進球者（排除烏龍球，依分鐘排序）。只有 <see cref="Status"/> 為 <c>played</c> 的賽事才填，其餘為空陣列。球員非現役時 <see cref="MatchScorerDto.PlayerName"/> 為 <c>null</c>（進球仍保留）。</summary>
    public IReadOnlyList<MatchScorerDto> Scorers { get; init; } = [];

    /// <summary>賽後報導：最新一篇已發布、分類為比賽報導（<c>match</c>）且在後台「關聯」選了這場賽事的文章；沒有為 <c>null</c>。</summary>
    public MatchReportDto? MatchReport { get; init; }
}

public sealed record MatchScorerDto
{
    public int? Minute { get; init; }
    public string? PlayerName { get; init; }

    /// <summary>進球類型代碼（<c>header</c>／<c>penalty</c>／<c>free_kick</c>／<c>other</c>）；一般進球為 <c>null</c>。</summary>
    public string? GoalType { get; init; }
}

/// <summary>對手隊徽。<see cref="Url"/> 是主檔（長邊上限 2560）；另有 1280／640／320 三種長邊的衍生檔（沒有儲存體網址設定時為 <c>null</c>）。寬高是主檔尺寸。</summary>
public sealed record OpponentCrestDto
{
    public required string Url { get; init; }
    public string? Url1280 { get; init; }
    public string? Url640 { get; init; }
    public string? Url320 { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
}

/// <summary>單場賽事詳情＝<see cref="MatchDto"/> 的全部欄位 ＋ 進球、牌、出賽名單、賽後報導。</summary>
public sealed record MatchDetailDto : MatchDto
{
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public MatchDetailDto(MatchDto source) : base(source)
    {
    }

    public IReadOnlyList<MatchGoalDto> Goals { get; init; } = [];
    public IReadOnlyList<MatchCardDto> Cards { get; init; } = [];
    public IReadOnlyList<MatchLineupDto> Lineup { get; init; } = [];
}

/// <summary>進球。<see cref="PlayerId"/>／<see cref="PlayerSlug"/>／<see cref="PlayerName"/>／<see cref="ShirtNumber"/> 在球員非現役（不公開）時為 <c>null</c>，但這筆進球仍保留。</summary>
public sealed record MatchGoalDto
{
    public int? Minute { get; init; }
    public Guid? PlayerId { get; init; }
    public string? PlayerSlug { get; init; }
    public string? PlayerName { get; init; }
    public int? ShirtNumber { get; init; }

    /// <summary>進球類型代碼：<c>header</c>／<c>penalty</c>／<c>free_kick</c>／<c>own_goal</c>／<c>other</c>；<c>null</c>＝一般進球。</summary>
    public string? GoalType { get; init; }

    /// <summary>這球算誰的：<c>club</c>＝本隊進球；<c>opponent</c>＝烏龍球（本隊球員打進自家球門，算對手進球）。</summary>
    public string? Side { get; init; }
}

public sealed record MatchCardDto
{
    public int? Minute { get; init; }
    public Guid? PlayerId { get; init; }
    public string? PlayerSlug { get; init; }
    public string? PlayerName { get; init; }

    /// <summary><c>yellow</c>／<c>red</c>。</summary>
    public required string CardType { get; init; }
}

/// <summary>出賽名單一列。只含現役球員。<see cref="CameOn"/> 只在替補有意義（先發恆為 <c>false</c>）；出賽＝<see cref="IsStarter"/> 或 <see cref="CameOn"/>。</summary>
public sealed record MatchLineupDto
{
    public required Guid PlayerId { get; init; }
    public string? PlayerSlug { get; init; }
    public string? PlayerName { get; init; }
    public int? ShirtNumber { get; init; }
    public string? Position { get; init; }
    public required bool IsStarter { get; init; }
    public required bool CameOn { get; init; }
}

public sealed record MatchReportDto
{
    public required string Slug { get; init; }
    public required string Title { get; init; }
}
