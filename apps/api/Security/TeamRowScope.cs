namespace Tcrfc.Api.Security;

/// <summary>
/// 「這個已通過俱樂部授權與權限碼檢查的人，對某一筆特定球隊資料能不能寫」——`role_permissions.scope_type`
/// 的列級（row-level）判斷結果，由 <see cref="IAdminTeamRowScopeResolver"/> 針對「這個人 ＋ 這個權限碼」
/// 即時查庫算出。docs/12b-database-tables.md §7.4：`scope_type` 是矩陣裡「不是布林」的格子——
/// 同一個權限碼（例如 <c>team.match.update</c>），系統管理員與競技／球隊管理是 <c>all</c>（整個俱樂部
/// 隨便碰），學院／課程管理是 <c>academy_only</c>（只能碰 <c>team.type='academy'</c> 的球隊）。
///
/// 🔴🔴🔴 S1-8 新增（列級授權強制）：本型別與既有 <see cref="AdminClubScope"/>／
/// <see cref="AdminSystemScope"/> 是**同一套「型別層強制授權」設計**，套用完全相同的三個理由
/// （見 <see cref="AdminClubScope"/> 上的完整說明，這裡只講差異）：
/// ① `sealed class`（不是 `readonly struct`）——擋 `default`／`default(T)` 繞過建構子產生零值實例。
/// ② 建構子 `internal`，只有 <see cref="AdminTeamRowScopeResolver"/> 能建立實例。
/// ③ 已加入 <see cref="Tcrfc.Api.Tests.ArchitectureTests"/> 的 Roslyn 語意掃描允許清單。
///
/// **取捨（與 <see cref="AdminClubScope"/> 的差異，誠實說明為什麼還是做到同一等級）**：
/// <see cref="AdminClubScope"/> 擋的是「能不能碰這個俱樂部」——繞過去等於直接看到別的俱樂部資料，
/// 後果是資料外洩。本型別擋的是「同一個俱樂部裡，能不能碰特定球隊」——繞過去的後果是「學院管理者
/// 改到一線隊賽程」這種**權限逾越**，範圍比跨俱樂部外洩小，但主站規劃書 §6 明文把它當成矩陣裡一格
/// 具體的規則（「合作球隊管理」與「學院／課程管理」兩列都靠這個機制才成立），不是可有可無的細節，
/// 而且未來會被 C1（球隊）／C2（球員）／C3（教練）／C4（賽事）四個模組共用（見類別上「可重用」的
/// 設計目的）——**共用機制的正確性比單一端點重要，值得付同一筆型別層防護的成本**，因此選擇跟
/// <see cref="AdminClubScope"/> 同一套做法而不是退回到「repository 自己記得呼叫檢查方法」。
/// </summary>
public sealed class TeamRowScope
{
    private readonly bool _isUnrestricted;
    private readonly bool _allowsAcademyBlanket;
    private readonly IReadOnlySet<Guid> _ownTeamsGrantedIds;

    internal TeamRowScope(bool isUnrestricted, bool allowsAcademyBlanket, IReadOnlySet<Guid> ownTeamsGrantedIds)
    {
        _isUnrestricted = isUnrestricted;
        _allowsAcademyBlanket = allowsAcademyBlanket;
        _ownTeamsGrantedIds = ownTeamsGrantedIds;
    }

    /// <summary>整個俱樂部隨便碰——<c>scope_type = 'all'</c>（或系統管理員，跳過整個查詢）。</summary>
    public bool IsUnrestricted => _isUnrestricted;

    /// <summary>
    /// 這一筆**既有**球隊資料（<paramref name="teamId"/> 已經存在於資料庫，<paramref name="teamType"/>
    /// 是它目前的 <c>teams.type</c>）能不能寫。用於：C1 更新既有球隊本身（<paramref name="teamId"/> 即
    /// 該球隊自己的 id）、C2 球員的 <c>team_id</c>、C3 教練的 <c>staff_teams</c> 逐筆指派、
    /// C4 賽事的 <c>match_teams</c> 逐筆關聯。
    /// </summary>
    public bool Allows(Guid teamId, string teamType)
        => _isUnrestricted
        || (_allowsAcademyBlanket && teamType == "academy")
        || _ownTeamsGrantedIds.Contains(teamId);

    /// <summary>
    /// 一次檢查多筆關聯（C3 教練可能同時帶多個梯隊、C4 一場跨梯隊友誼賽可能同時掛多支球隊，
    /// 見主站規劃書 §3.13「每一筆賽事皆需指定所屬隊別（跨梯隊友誼賽可複選）」）——**任何一筆不通過，
    /// 整體就不通過**（不能因為挑得到一支允許的球隊，就放行其餘不允許的球隊被一起寫入）。
    /// **空集合視為不通過**（<c>_isUnrestricted</c> 除外）：一筆「不歸屬任何球隊」的資源
    /// （例如教練的球隊指派留空、代表沒有任何梯隊背書），範圍受限的帳號沒有理由能建立或維持這種
    /// 資源——fail-closed，不是遺漏。
    /// </summary>
    public bool AllowsAll(IReadOnlyCollection<(Guid TeamId, string TeamType)> teams)
        => _isUnrestricted || (teams.Count > 0 && teams.All(t => Allows(t.TeamId, t.TeamType)));

    /// <summary>
    /// C1「建立一支全新的球隊」專用——這時候還沒有既有的 <c>teams.id</c> 可以查
    /// <see cref="_ownTeamsGrantedIds"/>（<c>own_teams</c> 授權只可能指向已經存在的球隊，
    /// 不可能預先指向一支還沒建立的球隊），所以 <c>own_teams</c> 範圍的帳號**一律不能新建球隊**
    /// （現實對應：被個別指派特定梯隊的帳號，本來就不該有新建球隊這種俱樂部層級的操作）；
    /// <c>academy_only</c> 範圍的帳號則可以新建球隊，但只能建 <paramref name="newTeamType"/>
    /// 為 <c>academy</c> 的球隊。
    /// </summary>
    public bool AllowsCreatingTeamOfType(string newTeamType)
        => _isUnrestricted || (_allowsAcademyBlanket && newTeamType == "academy");
}
