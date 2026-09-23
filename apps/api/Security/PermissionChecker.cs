using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

public sealed class PermissionChecker(ClubDbContext db) : IPermissionChecker
{
    public async Task<bool> HasPermissionAsync(Guid adminUserId, bool isSuperAdmin, string permissionCode, CancellationToken cancellationToken)
    {
        if (isSuperAdmin)
        {
            // 規劃書 §6「系統管理員（is_super_admin）跳過整個資料範圍查詢」——這裡是權限版本，
            // 同一條規則的另一個落點（範圍版本在 AdminClubAuthorizer）。
            return true;
        }

        // admin_user_roles 是純關聯（只有兩個 FK 組成 PK，沒有其他欄位），EF Core scaffold
        // 把它建模成 AdminUser.AdminRoles／AdminRole.AdminUsers 的隱式多對多跳躍導覽，
        // 沒有獨立的 DbSet 可查——改用導覽屬性展開，語意等價於 INNER JOIN 三張表。
        return await db.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == adminUserId)
            .SelectMany(u => u.AdminRoles)
            .SelectMany(r => r.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .AnyAsync(code => code == permissionCode, cancellationToken);
    }
}
