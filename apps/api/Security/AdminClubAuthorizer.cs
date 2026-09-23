using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

/// <summary>
/// <see cref="AdminClubScope"/> 的唯一產生者。四個檢查依序執行，任何一步失敗立刻丟例外——
/// 這是任務要求「四種擋下情境」在程式碼裡的單一落點，見 apps/api/README.md 的驗收紀錄。
/// </summary>
public sealed class AdminClubAuthorizer(ClubDbContext db, IClubResolver clubResolver) : IAdminClubAuthorizer
{
    public async Task<AdminClubScope> AuthorizeAsync(
        HttpContext httpContext, string clubCode, string permissionCode, CancellationToken cancellationToken)
    {
        // ① 有沒有登入：權杖缺漏、簽章錯誤、過期都會讓 JwtBearer 中介軟體不把 User 標成已驗證。
        var tokenIdentity = AdminIdentity.FromClaimsPrincipal(httpContext.User);
        if (tokenIdentity is null)
        {
            throw new AdminUnauthenticatedException();
        }

        // ② 俱樂部本身存不存在：沿用既有的公開端點驗證邏輯，行為完全不變（含快取）。
        var club = await clubResolver.ResolveAsync(clubCode, cancellationToken);

        // 帳號目前的真實狀態——⚠️ 一律重查資料庫，不信任 JWT 裡的 is_super_admin claim
        // （見 AdminIdentity 與 IPermissionChecker 上的說明：帳號可能在權杖簽發後被停用或降級）。
        var account = await db.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == tokenIdentity.Value.AdminUserId)
            .Select(u => new { u.Status, u.IsSuperAdmin, u.MustChangePassword, u.TwoFactorEnabled })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null || account.Status != "active")
        {
            throw new AdminForbiddenException("帳號已停用或不存在，請聯繫系統管理員。");
        }

        // 強制密碼更換與強制 2FA（docs/12b-database-tables.md §7.6：「§8 非功能性需求明訂後台強制
        // 2FA」；種子超管 must_change_password=true「首次登入強制更換」）——這兩項一律在這裡擋，
        // 不在個別端點各自檢查，確保沒有任何俱樂部範圍的寫入端點能繞過。/auth/change-password、
        // /auth/2fa/* 本身不是俱樂部範圍端點，不經過本方法，不受影響，見 Features/AdminAuth 的路由。
        if (account.MustChangePassword)
        {
            throw new AdminForbiddenException("首次登入須先更換密碼，請呼叫「變更密碼」端點。");
        }
        if (!account.TwoFactorEnabled)
        {
            throw new AdminForbiddenException("後台強制兩階段驗證，請先完成 2FA 設定。");
        }

        var identity = tokenIdentity.Value with { IsSuperAdmin = account.IsSuperAdmin };

        // ③ 資料範圍：系統管理員跳過整個範圍查詢（規劃書 §6「資料範圍規則」）。
        if (!identity.IsSuperAdmin)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var hasClubGrant = await db.AdminUserClubs
                .AsNoTracking()
                .AnyAsync(g => g.AdminUserId == identity.AdminUserId
                             && g.ClubId == club.ClubId
                             && g.IsActive
                             && (g.ExpiresOn == null || g.ExpiresOn >= today),
                    cancellationToken);

            if (!hasClubGrant)
            {
                throw new AdminForbiddenException($"你沒有被授權存取俱樂部「{clubCode}」的後台資料。");
            }
        }

        // ④ 這項操作的權限碼。
        var permissionChecker = new PermissionChecker(db);
        var hasPermission = await permissionChecker.HasPermissionAsync(
            identity.AdminUserId, identity.IsSuperAdmin, permissionCode, cancellationToken);

        if (!hasPermission)
        {
            throw new AdminForbiddenException($"你的角色沒有「{permissionCode}」這項操作的權限。");
        }

        return new AdminClubScope(club, identity);
    }
}
