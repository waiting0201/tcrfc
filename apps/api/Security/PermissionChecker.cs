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
        //
        // 🔴 本輪新增（S1-3 J1／J2／J4）：多比對一個 `!p.SysadminOnly` 條件——
        // docs/12b-database-tables.md §7.3「sysadmin_only：僅系統管理員」是一道獨立於角色指派
        // 之外的閘門，不能只靠「seed 資料沒有把這個權限碼指派給非超管角色」來保證，因為 J2
        // 本輪新增了「角色權限指派」端點，一旦有人（誤）把 sysadmin_only 權限碼指派給某個角色，
        // 若這裡不擋，非超管帳號就能實際取得該權限。is_super_admin=true 的呼叫在上面已經
        // 提前 return true，不會走到這裡，所以這個條件只影響「非超管」查得到的集合。
        return await db.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == adminUserId)
            .SelectMany(u => u.AdminRoles)
            .SelectMany(r => r.RolePermissions)
            .Select(rp => rp.Permission)
            .AnyAsync(p => p.Code == permissionCode && !p.SysadminOnly, cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetHeldPermissionCodesAsync(
        Guid adminUserId, bool isSuperAdmin, IReadOnlyList<string> candidateCodes, CancellationToken cancellationToken)
    {
        if (isSuperAdmin)
        {
            return new HashSet<string>(candidateCodes, StringComparer.Ordinal);
        }

        var held = await db.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == adminUserId)
            .SelectMany(u => u.AdminRoles)
            .SelectMany(r => r.RolePermissions)
            .Select(rp => rp.Permission)
            .Where(p => candidateCodes.Contains(p.Code) && !p.SysadminOnly)
            .Select(p => p.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new HashSet<string>(held, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetAllHeldPermissionsAsync(
        Guid adminUserId, bool isSuperAdmin, CancellationToken cancellationToken)
    {
        if (isSuperAdmin)
        {
            // 規劃書 §6「系統管理員跳過整個資料範圍查詢」——這裡回傳全部權限碼（含 sysadmin_only），
            // 不查 role_permissions：系統管理員的權限不是靠角色指派來的，是身分本身賦予的。
            var allCodes = await db.Permissions.AsNoTracking()
                .Select(p => p.Code)
                .ToListAsync(cancellationToken);
            return allCodes.ToDictionary(c => c, IReadOnlyList<string> (_) => ["all"]);
        }

        var rows = await db.AdminUsers.AsNoTracking()
            .Where(u => u.Id == adminUserId)
            .SelectMany(u => u.AdminRoles)
            .SelectMany(r => r.RolePermissions)
            .Where(rp => !rp.Permission.SysadminOnly)
            .Select(rp => new { rp.Permission.Code, rp.ScopeType })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.Code, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                IReadOnlyList<string> (g) => g.Select(r => r.ScopeType).Distinct(StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);
    }
}
