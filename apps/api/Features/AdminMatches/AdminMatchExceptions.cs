namespace Tcrfc.Api.Features.AdminMatches;

public abstract class AdminMatchException(string message) : Exception(message);

public sealed class AdminMatchValidationException(string message) : AdminMatchException(message);

/// <summary>「場次編號同季同聯賽唯一」（主站規劃書 §4.3 C4）——<c>(club_id, season_id,
/// competition_id, match_no)</c> 在應用層檢查（<c>matches.match_no</c> 沒有 DB 唯一索引，
/// 見 <c>AdminMatchesRepository.EnsureMatchNoUniqueAsync</c> 上的說明）。</summary>
public sealed class AdminMatchNoConflictException(int matchNo)
    : AdminMatchException($"場次編號「{matchNo}」在這個賽季、這個賽事系列已經被使用，請確認或換一個編號。");
