using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Data;

namespace Tcrfc.Api.CharityPlatform.Security;

/// <summary>
/// 「能做什麼」：AdminUser → AdminUserRole → AdminRole → RolePermission → Permission 的聯集查詢
/// （慈善庫版本，與主站 <c>PermissionChecker</c> 同形）。
/// <c>sysadmin_only</c> 是獨立於角色指派之外的閘門：非系統管理員即使被（誤）指派了這類權限碼也查不到。
/// 🔴 慈善後台沒有 <c>scope_type</c> 的列級授權需求（沒有俱樂部／球隊維度），種子裡檢視者的
/// <c>masked</c> 與「個資預設遮罩、<c>reveal</c> 權限才看明文」是同一件事，後者以獨立權限碼實作，
/// 這裡不解讀 <c>scope_type</c>。
/// </summary>
public sealed class CharityPermissionChecker(CharityDbContext db)
{
    public async Task<bool> HasPermissionAsync(Guid adminUserId, bool isSuperAdmin, string permissionCode, CancellationToken cancellationToken)
    {
        if (isSuperAdmin)
        {
            return true;
        }

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

    /// <summary>這個帳號目前實際持有的全部權限碼（<c>/me</c> 回給前端決定要不要顯示按鈕；權限碼本身不得顯示給使用者）。
    /// 系統管理員回傳全部權限碼（含 <c>sysadmin_only</c>）。</summary>
    public async Task<IReadOnlyList<string>> GetAllHeldPermissionCodesAsync(
        Guid adminUserId, bool isSuperAdmin, CancellationToken cancellationToken)
    {
        if (isSuperAdmin)
        {
            return await db.Permissions.AsNoTracking().OrderBy(p => p.Code).Select(p => p.Code).ToListAsync(cancellationToken);
        }

        return await db.AdminUsers.AsNoTracking()
            .Where(u => u.Id == adminUserId)
            .SelectMany(u => u.AdminRoles)
            .SelectMany(r => r.RolePermissions)
            .Where(rp => !rp.Permission.SysadminOnly)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }
}
