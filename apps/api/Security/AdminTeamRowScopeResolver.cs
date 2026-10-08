using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

/// <summary><see cref="TeamRowScope"/> 的唯一產生者。見該型別與 <see cref="IAdminTeamRowScopeResolver"/>
/// 上的完整說明。</summary>
public sealed class AdminTeamRowScopeResolver(ClubDbContext db) : IAdminTeamRowScopeResolver
{
    public async Task<TeamRowScope> ResolveAsync(AdminClubScope scope, string permissionCode, CancellationToken cancellationToken)
    {
        if (scope.Identity.IsSuperAdmin)
        {
            // 規劃書 §6「系統管理員（is_super_admin）跳過整個資料範圍查詢」——列級版本，
            // 跟 AdminClubAuthorizer（俱樂部版本）／PermissionChecker（權限碼版本）同一條規則。
            return new TeamRowScope(isUnrestricted: true, allowsAcademyBlanket: false);
        }

        // 同一個人可能有多個角色，每個角色對同一個權限碼可能給不同的 scope_type——
        // 「有效權限＝所有角色的權限聯集」（docs/12b §7.1）在列級的對應版本：
        // 只要有任何一個角色是 'all'，整體就是不限；否則看有沒有 academy_only
        // （見下方組裝邏輯），不是取交集或只認第一筆。
        var scopeTypes = await db.AdminUsers.AsNoTracking()
            .Where(u => u.Id == scope.Identity.AdminUserId)
            .SelectMany(u => u.AdminRoles)
            .SelectMany(r => r.RolePermissions)
            .Where(rp => rp.Permission.Code == permissionCode && !rp.Permission.SysadminOnly)
            .Select(rp => rp.ScopeType)
            .Distinct()
            .ToListAsync(cancellationToken);

        // 🔴 "own_clubs" 視同不限（跟 "all" 同一個分支）：docs/12b-database-tables.md §7.1
        // 「RolePermission: scope_type 加值 own_clubs」與 §7.4「scope_type 是矩陣裡不是布林的
        // 格子」兩段合看——§7.4 那張表只列「衍生自矩陣特殊格子」的值（academy_only／
        // masked／translate_only），"own_clubs" 跟 "all" 一樣是「這個角色在這個權限碼上沒有列級
        // 限制」的基準值，差別只在於它額外標記「這個角色的資料範圍被 AdminRole.scope_mode 限制在
        // 自己的俱樂部」——但那件事本來就已經由 IAdminClubAuthorizer 的 AdminUserClub 檢查在更上
        // 一層擋住了（合作球隊管理帳號一開始就進不了別的俱樂部），不需要 TeamRowScope 在同一個
        // 俱樂部**內部**再對球隊做二次窄化。既有種子腳本（`db/seed/generate-club-seed-sql.py`）
        // 對 `partner_club_manager` 的既有指派全部用 "own_clubs"，若這裡不特別處理，會被下面
        // fail-closed 分支誤判為「查無有效 scope_type」而整批拒絕，讓合作球隊管理角色形同無法
        // 操作任何球隊資料——已列入本次回報的文件缺口（§7.1／§7.4 用詞不一致，建議 system-analyst
        // 日後把 "own_clubs" 也正式收進 §7.4 的表格）。
        if (scopeTypes.Contains("all") || scopeTypes.Contains("own_clubs"))
        {
            return new TeamRowScope(isUnrestricted: true, allowsAcademyBlanket: false);
        }

        if (scopeTypes.Count == 0)
        {
            // fail-closed：呼叫端理應已經先透過 IAdminClubAuthorizer.AuthorizeAsync 確認過
            // 「這個人至少有一個角色持有這個權限碼」才會走到這裡；這裡查不到任何 scope_type
            // 代表資料不一致（例如權限碼字串兩處打得不一樣），寧可整批拒絕也不要預設放行——
            // 跟 docs/18 E-50「個資輸出用白名單」同一種 fail-closed 精神。
            return new TeamRowScope(isUnrestricted: false, allowsAcademyBlanket: false);
        }

        var allowsAcademyBlanket = scopeTypes.Contains("academy_only");

        return new TeamRowScope(isUnrestricted: false, allowsAcademyBlanket: allowsAcademyBlanket);
    }
}
