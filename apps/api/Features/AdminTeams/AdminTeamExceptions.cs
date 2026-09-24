namespace Tcrfc.Api.Features.AdminTeams;

public abstract class AdminTeamException(string message) : Exception(message);

public sealed class AdminTeamValidationException(string message) : AdminTeamException(message);

/// <summary><c>teams.code</c> 全站唯一（<c>UQ_teams_code</c>，主站規劃書 §4.3 C1：
/// 「隊別代號需全站唯一，作為行事曆分類與訂閱網址的識別鍵」）已被使用。對應 409。</summary>
public sealed class AdminTeamCodeConflictException(string code)
    : AdminTeamException($"隊別代號「{code}」已經被使用（隊別代號全站唯一，不分俱樂部），請換一個。");

/// <summary><c>type = first_team</c> 在同一俱樂部只能有一筆（主站規劃書 §4.3 C1）。對應 409。</summary>
public sealed class AdminTeamFirstTeamAlreadyExistsException(string existingTeamCode)
    : AdminTeamException($"這個俱樂部已經有一線隊（{existingTeamCode}），同一俱樂部只能設定一支一線隊。");

/// <summary>圖片欄位插槽把「主視覺」對到 <c>teams.hero_key</c> 三態（維持不變／清空／換成新值），
/// 形狀比照 <c>Features/AdminNews/CoverKeyUpdate.cs</c> 的說明。</summary>
public readonly record struct HeroKeyUpdate(bool Change, string? NewKey)
{
    public static readonly HeroKeyUpdate Keep = new(false, null);
    public static HeroKeyUpdate Set(string? newKey) => new(true, newKey);
}
