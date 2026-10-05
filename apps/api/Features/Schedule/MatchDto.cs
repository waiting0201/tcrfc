using Tcrfc.Api.Features.Seo;

namespace Tcrfc.Api.Features.Schedule;

/// <summary>賽程與賽果公開欄位。<c>matches</c> 不在受限欄位清單內（docs/12b-database-tables.md §8）。</summary>
public sealed record MatchDto
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
    public string? Opponent { get; init; }

    /// <summary>未翻譯標示（App 規劃書 §2.5「未翻譯 fallback 繁中並標示」、主站 G-01）：請求的是英文、而這筆的英文主要欄位（對手）是空的，回應內容是回退的繁中時為 true。
    /// 請求繁中時恆為 false。用戶端據此顯示「本內容尚無英文版本」。</summary>
    public required bool IsFallbackLocale { get; init; }
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
}
