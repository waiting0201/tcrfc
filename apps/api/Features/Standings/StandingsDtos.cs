namespace Tcrfc.Api.Features.Standings;

public sealed record SeasonRefDto
{
    public required string Code { get; init; }
    public required DateOnly StartOn { get; init; }
    public required DateOnly EndOn { get; init; }
}

public sealed record StandingRowDto
{
    public int? Rank { get; init; }
    public required string TeamName { get; init; }

    /// <summary>未翻譯標示：請求語系不是繁中、且該隊在請求語系沒有名稱（<see cref="TeamName"/> 已回退成繁中）時為 true。前台據此標示「尚無英文版」。</summary>
    public required bool IsFallbackLocale { get; init; }
    public int? Played { get; init; }
    public int? Points { get; init; }
}

/// <summary>積分榜。<see cref="Season"/> 為 null 代表這個俱樂部還沒有任何球季資料。</summary>
public sealed record StandingsDto
{
    public SeasonRefDto? Season { get; init; }

    /// <summary>有積分榜資料的球季代碼（新→舊），供前台的球季下拉選單。</summary>
    public required IReadOnlyList<string> Seasons { get; init; }
    public required IReadOnlyList<StandingRowDto> Items { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record PlayerSeasonStatDto
{
    public required Guid PlayerId { get; init; }
    public string? Name { get; init; }

    /// <summary>未翻譯標示：請求語系不是繁中、且該球員在請求語系沒有名稱（<see cref="Name"/> 已回退成繁中）時為 true。</summary>
    public required bool IsFallbackLocale { get; init; }
    public required string TeamCode { get; init; }
    public int? ShirtNo { get; init; }
    public string? Position { get; init; }

    /// <summary>肖像同意未到位者為 null（同球員名單的 fail-closed 規則）。</summary>
    public string? PhotoUrl { get; init; }

    /// <summary>照片寬高（像素）與替代文字（當前語系，英文空白回退繁中）；與 <see cref="PhotoUrl"/> 同一套肖像同意規則，沒有照片時三者皆 <c>null</c>。</summary>
    public int? PhotoWidth { get; init; }
    public int? PhotoHeight { get; init; }
    public string? PhotoAlt { get; init; }
    public required int Appearances { get; init; }
    public required int Goals { get; init; }

    /// <summary>助攻。<b>賽事紀錄沒有助攻資料</b>：自動彙總時為 null，只有後台手動輸入的球季數據才有值。</summary>
    public int? Assists { get; init; }
    public required int YellowCards { get; init; }
    public required int RedCards { get; init; }

    /// <summary><c>auto</c>＝由已結束賽事的進球／黃紅牌／先發名單自動彙總；<c>manual</c>＝後台手動輸入的球季數據（有手動數據時以手動為準）。</summary>
    public required string Source { get; init; }
}

public sealed record PlayerStatsDto
{
    public SeasonRefDto? Season { get; init; }
    public required IReadOnlyList<string> Seasons { get; init; }
    public required IReadOnlyList<PlayerSeasonStatDto> Items { get; init; }
}

/// <summary>單一球員逐季數據（球員詳情頁「生涯數據、本季出賽」）。</summary>
public sealed record PlayerCareerStatDto
{
    public required string SeasonCode { get; init; }
    public required int Appearances { get; init; }
    public required int Goals { get; init; }
    public int? Assists { get; init; }
    public required int YellowCards { get; init; }
    public required int RedCards { get; init; }
    public required string Source { get; init; }
}

public sealed record PlayerCareerStatsDto
{
    public required Guid PlayerId { get; init; }
    public required IReadOnlyList<PlayerCareerStatDto> Seasons { get; init; }
}
