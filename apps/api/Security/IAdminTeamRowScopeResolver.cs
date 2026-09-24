namespace Tcrfc.Api.Security;

/// <summary>
/// <see cref="TeamRowScope"/> 的唯一產生者介面。呼叫時機：<see cref="IAdminClubAuthorizer.AuthorizeAsync"/>
/// 已經確認「這個人對這個俱樂部有沒有授權」與「有沒有這個權限碼」之後，**再多問一句**「這個權限碼
/// 對這個人是不是整個俱樂部隨便碰，還是被 <c>role_permissions.scope_type</c> 縮限到特定球隊」——
/// 是 <see cref="IAdminClubAuthorizer"/> 之後的第二道、更細的關卡，兩者不互相取代
/// （比照規劃書 §6「匯出先套資料範圍，再套受限欄位授權；兩道關卡不可互相取代」的兩階段精神）。
/// </summary>
public interface IAdminTeamRowScopeResolver
{
    /// <param name="scope">已通過 <see cref="IAdminClubAuthorizer"/> 驗證的俱樂部範圍。</param>
    /// <param name="permissionCode">要查的權限碼，例如 <c>team.match.update</c>——同一個人對不同權限碼
    /// 可能有不同的 <c>scope_type</c>（例如 <c>team.team.view</c> 是 <c>all</c>，但
    /// <c>team.match.update</c> 是 <c>academy_only</c>），故每次呼叫都要帶著具體的權限碼查。</param>
    Task<TeamRowScope> ResolveAsync(AdminClubScope scope, string permissionCode, CancellationToken cancellationToken);
}
