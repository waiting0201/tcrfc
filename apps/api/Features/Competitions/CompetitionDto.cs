namespace Tcrfc.Api.Features.Competitions;

/// <summary>
/// 賽事系列（<c>Competition</c>，如「企業甲級聯賽」）的公開欄位——行動 App 規劃書 §9.2「賽事系列 列表」，供賽程第三層篩選
/// （App 規劃書 §3.2）。只回已發布（<c>status = published</c>）的系列。
/// 🔵 「系列已知但賽程未定」（缺口 A1）：系列由後台 J4 建立、與賽事無關，所以即使這個系列目前沒有任何賽事，這裡也會列出，
/// 用戶端據此顯示空狀態而不是從賽事去重推導。
/// </summary>
public sealed record CompetitionDto
{
    public required Guid Id { get; init; }

    /// <summary>賽事系列代碼，賽程 <c>GET /{club}/schedule?competition=</c> 的篩選值，也是 <c>MatchDto.competitionCode</c>。同一俱樂部內唯一（後台 J4 強制）。</summary>
    public required string Code { get; init; }

    public required string ClubCode { get; init; }
    public required string SeasonCode { get; init; }

    /// <summary>賽事類型（自由代碼，如 <c>league</c>、<c>cup</c>；沒填為 null）。</summary>
    public string? CompType { get; init; }

    public string? Name { get; init; }

    /// <summary>未翻譯標示（App 規劃書 §2.5「未翻譯 fallback 繁中並標示」、主站 G-01）：請求的是英文、而這筆的英文主要欄位（名稱）是空的，回應內容是回退的繁中時為 true。
    /// 請求繁中時恆為 false。用戶端據此顯示「本內容尚無英文版本」。</summary>
    public required bool IsFallbackLocale { get; init; }

    /// <summary>主辦單位名稱。</summary>
    public string? Organizer { get; init; }

    public required int SortOrder { get; init; }
}
