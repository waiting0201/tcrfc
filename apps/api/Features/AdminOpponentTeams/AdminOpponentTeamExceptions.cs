using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.AdminOpponentTeams;

/// <summary>對手球隊的衝突（409）。<see cref="Code"/> 是機器可讀代碼（ProblemDetails 的 <c>code</c>）：
/// <c>opponent_team_in_use</c>（仍被賽事或積分榜引用，不能刪）、<c>opponent_team_name_duplicate</c>（同俱樂部隊名重複）。</summary>
public sealed class AdminOpponentTeamConflictException(string title, string message, string code, string? field = null)
    : Exception(message), ICodedApiException, IFieldApiException
{
    public string Title { get; } = title;
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, string> FieldErrors { get; } = FieldKey.Single(field, message);
}
