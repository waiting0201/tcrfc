namespace Tcrfc.Api.Features.AdminCompetitions;

public abstract class AdminCompetitionException(string message) : Exception(message);

public sealed class AdminCompetitionValidationException(string message) : AdminCompetitionException(message);

/// <summary><c>(club_id, code)</c> 唯一（<c>UQ_competitions_club_code</c>）已被使用。對應 409。</summary>
public sealed class AdminCompetitionCodeConflictException(string code)
    : AdminCompetitionException($"賽事系列代號「{code}」在這個俱樂部已經被使用，請換一個。");
