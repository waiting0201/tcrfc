namespace Tcrfc.Api.Features.AdminMatches;

/// <summary>
/// 賽事的雙語內容——**跟其餘模組的 <c>*ContentInput</c> 形狀不同**：<c>matches</c> 沒有
/// <c>matches_i18n</c> 的 zh-Hant 列（<c>opponent</c>／<c>venue</c> 是**基礎表**上的中文欄位，
/// 不是側表），<c>matches_i18n</c> 只在需要覆寫英文（或其他語系）時才有列——逐字比照既有
/// <c>Features/Schedule/MatchesRepository.cs</c> 檔頭「對手／場地的英文回退規則與其他實體不同」
/// 的既有說明。因此這裡的中文（<see cref="Opponent"/>／<see cref="Venue"/>）是必填的一般欄位，
/// 英文（<see cref="OpponentEn"/>／<see cref="VenueEn"/>）是可省略的覆寫欄位，不是
/// <c>Zh</c>／<c>En</c> 兩個巢狀物件。
/// </summary>
public sealed record AdminMatchListItemDto
{
    public required Guid Id { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public Guid? CompetitionId { get; init; }
    public string? CompetitionCode { get; init; }
    public required IReadOnlyList<Guid> TeamIds { get; init; }
    public required IReadOnlyList<string> TeamCodes { get; init; }
    public required DateOnly MatchOn { get; init; }
    public string? Kickoff { get; init; }
    public string? HomeAway { get; init; }
    public string? Opponent { get; init; }
    public string? CompetitionTag { get; init; }
    public string? Status { get; init; }
    public int? ScoreHome { get; init; }
    public int? ScoreAway { get; init; }
    public int? RoundNo { get; init; }
    public int? MatchNo { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminMatchDetailDto
{
    public required Guid Id { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public Guid? CompetitionId { get; init; }
    public string? CompetitionCode { get; init; }
    public Guid? VenueId { get; init; }
    public required IReadOnlyList<Guid> TeamIds { get; init; }
    public required IReadOnlyList<string> TeamCodes { get; init; }
    public required DateOnly MatchOn { get; init; }
    public string? Kickoff { get; init; }
    public string? HomeAway { get; init; }
    public string? Opponent { get; init; }
    public string? OpponentEn { get; init; }
    public string? Venue { get; init; }
    public string? VenueEn { get; init; }
    public required IReadOnlyList<AdminMatchGoalDto> Goals { get; init; }
    public required IReadOnlyList<AdminMatchCardDto> Cards { get; init; }
    public required IReadOnlyList<AdminMatchLineupDto> Lineups { get; init; }

    /// <summary><c>matches.competition</c> 自由文字標籤（<c>league</c>／<c>cup</c>／<c>friendly</c>／
    /// <c>other</c>），跟結構化的 <see cref="CompetitionId"/>（賽事系列）是兩回事，
    /// 逐字比照既有公開端點 <c>Features/Schedule/MatchDto.CompetitionTag</c> 的既有註解。</summary>
    public string? CompetitionTag { get; init; }
    public required string Status { get; init; }
    public int? ScoreHome { get; init; }
    public int? ScoreAway { get; init; }
    public int? RoundNo { get; init; }
    public int? MatchNo { get; init; }
    public DateOnly? OriginalMatchOn { get; init; }
    public string? OriginalKickoff { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 🔴 <see cref="Status"/> 值域（本輪 API 層第一次定案，docs/12d-field-audit.md 行 292 一類的
/// 「規劃書沒有給列舉代碼」缺口）：<c>scheduled</c>（未開始）／<c>live</c>（進行中）／
/// <c>played</c>（已結束）／<c>postponed</c>（延賽）——**逐字沿用種子腳本與既有測試已經在用的
/// 三個真實字串**（<c>db/seed/generate-club-seed-sql.py</c> 的 <c>scheduled</c>／<c>played</c>，
/// <c>Tcrfc.Api.Tests/ScheduleOriginalDateTests.cs</c> 的 <c>postponed</c>），只新增
/// <c>live</c>（規劃書兩處都有「進行中」，種子資料還沒有這個狀態的真實案例，此前無從得知它會被
/// 拼成什麼樣的字串，選擇跟其餘三個同一種簡短英文單字風格）。
/// ⚠️ **主站規劃書 §3.13（前台賽事卡片版型）另外列了「取消」，但 §4.3 C4（後台欄位定義）只列
/// 這四個值，不含取消**——兩處不一致，本輪照 C4（後台權威定義）為準，不多加「取消」這個第五個值
/// （CLAUDE.md「沒寫的不自創使用者可見功能」），已列入回報請人工裁決是否要回頭同步 §3.13。
/// </summary>
public sealed record CreateAdminMatchRequest
{
    public required Guid SeasonId { get; init; }
    public Guid? CompetitionId { get; init; }
    public Guid? VenueId { get; init; }

    /// <summary>本方參賽隊——至少一支，跨梯隊友誼賽可複選（主站規劃書 §3.13「每一筆賽事皆需指定
    /// 所屬隊別（跨梯隊友誼賽可複選）」）。</summary>
    public required IReadOnlyList<Guid> TeamIds { get; init; }

    public required DateOnly MatchOn { get; init; }
    public string? Kickoff { get; init; }
    public string? HomeAway { get; init; }
    public required string Opponent { get; init; }
    public string? OpponentEn { get; init; }
    public string? Venue { get; init; }
    public string? VenueEn { get; init; }
    public string? CompetitionTag { get; init; }
    public string Status { get; init; } = "scheduled";
    public int? ScoreHome { get; init; }
    public int? ScoreAway { get; init; }
    public int? RoundNo { get; init; }
    public int? MatchNo { get; init; }

    /// <summary>僅 <see cref="Status"/> 為 <c>postponed</c> 時必填——見
    /// <c>AdminMatchesRepository.ValidatePostponedFields</c> 上的說明。</summary>
    public DateOnly? OriginalMatchOn { get; init; }
    public string? OriginalKickoff { get; init; }

    /// <summary>省略（<c>null</c>）＝這場賽事沒有任何進球／卡牌／出賽名單資料（新建時的預設情境，
    /// 賽事還沒開打）；提供空陣列與提供 <c>null</c> 效果相同（建立時沒有「既有資料」可以保留）。</summary>
    public IReadOnlyList<AdminMatchGoalInput>? Goals { get; init; }
    public IReadOnlyList<AdminMatchCardInput>? Cards { get; init; }
    public IReadOnlyList<AdminMatchLineupInput>? Lineups { get; init; }
}

public sealed record UpdateAdminMatchRequest
{
    public required Guid SeasonId { get; init; }
    public Guid? CompetitionId { get; init; }
    public Guid? VenueId { get; init; }
    public required IReadOnlyList<Guid> TeamIds { get; init; }
    public required DateOnly MatchOn { get; init; }
    public string? Kickoff { get; init; }
    public string? HomeAway { get; init; }
    public required string Opponent { get; init; }
    public string? OpponentEn { get; init; }
    public string? Venue { get; init; }
    public string? VenueEn { get; init; }
    public string? CompetitionTag { get; init; }
    public required string Status { get; init; }
    public int? ScoreHome { get; init; }
    public int? ScoreAway { get; init; }
    public int? RoundNo { get; init; }
    public int? MatchNo { get; init; }
    public DateOnly? OriginalMatchOn { get; init; }
    public string? OriginalKickoff { get; init; }

    /// <summary>省略（<c>null</c>）＝維持既有內容不變；提供陣列（含空陣列）＝整份取代——逐字比照
    /// <c>AdminStaffRepository.UpdateAsync</c> 對 <c>Teams</c> 的既有「省略＝維持不變、空陣列＝
    /// 清空」語意（apps/api/README.md S1-5 段）。</summary>
    public IReadOnlyList<AdminMatchGoalInput>? Goals { get; init; }
    public IReadOnlyList<AdminMatchCardInput>? Cards { get; init; }
    public IReadOnlyList<AdminMatchLineupInput>? Lineups { get; init; }
}

/// <summary>
/// 進球（<c>match_goals</c>）——主站規劃書 §4.3 C4「結果：比分、進球者與時間……」。
/// <see cref="PlayerId"/> 必須是這場賽事其中一支所屬球隊（<c>TeamIds</c>）底下的球員——見
/// <c>AdminMatchesRepository.ResolveMatchPlayerAsync</c> 上的說明，這同時是資料正確性檢查，
/// 也順帶讓球員一定落在已經通過列級授權的球隊範圍內，不需要對球員另開一次
/// <see cref="Tcrfc.Api.Security.TeamRowScope"/> 檢查。
/// </summary>
public sealed record AdminMatchGoalInput
{
    public required Guid PlayerId { get; init; }
    public int? Minute { get; init; }
    public string? GoalType { get; init; }
}

public sealed record AdminMatchGoalDto
{
    public required Guid Id { get; init; }
    public required Guid PlayerId { get; init; }
    public string? PlayerName { get; init; }
    public int? Minute { get; init; }
    public string? GoalType { get; init; }
}

/// <summary>黃紅牌（<c>match_cards</c>）。<see cref="CardType"/> 值域：<c>yellow</c>／<c>red</c>
/// （規劃書「卡牌」原文只講「黃紅牌」，沒有給英文代碼，本輪比照 <c>matches.status</c> 同一套
/// 「挑最直白的英文單字」風格定案）。</summary>
public sealed record AdminMatchCardInput
{
    public required Guid PlayerId { get; init; }
    public required string CardType { get; init; }
    public int? Minute { get; init; }
}

public sealed record AdminMatchCardDto
{
    public required Guid Id { get; init; }
    public required Guid PlayerId { get; init; }
    public string? PlayerName { get; init; }
    public required string CardType { get; init; }
    public int? Minute { get; init; }
}

/// <summary>出賽名單（<c>match_lineups</c>）。<see cref="IsStarter"/>：<c>true</c>＝先發，
/// <c>false</c>＝替補（規劃書「出賽名單」原文沒有細分先發／替補的用詞，沿用 <c>match_lineups</c>
/// 既有的 <c>is_starter</c> 欄位語意）。</summary>
public sealed record AdminMatchLineupInput
{
    public required Guid PlayerId { get; init; }
    public required bool IsStarter { get; init; }
}

public sealed record AdminMatchLineupDto
{
    public required Guid Id { get; init; }
    public required Guid PlayerId { get; init; }
    public string? PlayerName { get; init; }
    public required bool IsStarter { get; init; }
}

/// <summary>CSV 整批匯入的單列錯誤——逐字比照 <c>Features/AdminFaqs/AdminFaqDtos.FaqCsvImportRowErrorDto</c>。</summary>
public sealed record MatchCsvImportRowErrorDto
{
    public required int RowNumber { get; init; }
    public required string Reason { get; init; }
}

public sealed record MatchCsvImportResultDto
{
    public required int ImportedCount { get; init; }
    public required IReadOnlyList<MatchCsvImportRowErrorDto> Errors { get; init; }
}
