using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Features.AdminRoles;

/// <summary>J2 角色與權限：角色 CRUD、角色的權限碼指派、權限碼字典查詢。</summary>
public sealed class AdminRolesRepository(ClubDbContext dbContext)
{
    private static readonly HashSet<string> ValidScopeModes = new(StringComparer.Ordinal) { "all_clubs", "own_clubs" };

    private static readonly HashSet<string> ValidScopeTypes = new(StringComparer.Ordinal)
        { "all", "own_teams", "academy_only", "masked", "translate_only", "own_clubs" };

    // ───────────────────────────── 權限碼字典 ─────────────────────────────

    public async Task<IReadOnlyList<AdminPermissionDto>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        var permissions = await dbContext.Permissions.AsNoTracking()
            .OrderBy(p => p.ModuleCode).ThenBy(p => p.SubmoduleCode).ThenBy(p => p.SortOrder).ThenBy(p => p.Code)
            .ToListAsync(cancellationToken);

        return permissions.Select(ToPermissionDto).ToList();
    }

    // ───────────────────────────── 角色 ─────────────────────────────

    public async Task<IReadOnlyList<AdminRoleListItemDto>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.AdminRoles.AsNoTracking()
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Code)
            .Select(r => new { r.Id, r.Code, r.NameZh, r.NameEn, r.ScopeMode, r.IsSystem, r.SortOrder, AssignedAccountCount = r.AdminUsers.Count })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminRoleListItemDto
        {
            Id = r.Id,
            Code = r.Code,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            ScopeMode = r.ScopeMode,
            IsSystem = r.IsSystem,
            SortOrder = r.SortOrder,
            AssignedAccountCount = r.AssignedAccountCount,
        }).ToList();
    }

    public async Task<AdminRoleDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await dbContext.AdminRoles.AsNoTracking()
            .Include(r => r.AdminUsers)
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return role is null ? null : ToDetailDto(role);
    }

    public async Task<AdminRoleDetailDto> CreateAsync(CreateAdminRoleRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateCode(request.Code);
        ValidateScopeMode(request.ScopeMode);

        if (await dbContext.AdminRoles.AsNoTracking().AnyAsync(r => r.Code == request.Code, cancellationToken))
        {
            throw new AdminRoleCodeConflictException(request.Code);
        }

        var maxSortOrder = await dbContext.AdminRoles.AsNoTracking().Select(r => (int?)r.SortOrder).MaxAsync(cancellationToken) ?? -1;

        var now = DateTime.UtcNow;
        var role = new AdminRole
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            NameZh = request.NameZh,
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn,
            ScopeMode = request.ScopeMode,
            // 🔴 只有 db/seed 灌入的十個角色是 is_system=true——本端點建立的一律是可刪除的自訂角色
            // （docs/12b-database-tables.md §7.2「客戶可再自建更多角色」）。
            IsSystem = false,
            SortOrder = maxSortOrder + 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.AdminRoles.Add(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(role.Id, cancellationToken))!;
    }

    public async Task<AdminRoleDetailDto?> UpdateAsync(Guid id, UpdateAdminRoleRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateScopeMode(request.ScopeMode);

        var role = await dbContext.AdminRoles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return null;
        }

        role.NameZh = request.NameZh;
        role.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn;
        role.ScopeMode = request.ScopeMode;
        role.UpdatedAt = DateTime.UtcNow;
        role.UpdatedBy = operatorId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>回傳 <c>null</c>＝找不到，<c>true</c>＝刪除成功。系統角色與仍有帳號指派的角色一律擋下。</summary>
    public async Task<bool?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await dbContext.AdminRoles
            .Include(r => r.AdminUsers)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (role is null)
        {
            return null;
        }

        if (role.IsSystem)
        {
            throw new AdminRoleSystemDeleteException();
        }

        if (role.AdminUsers.Count > 0)
        {
            throw new AdminRoleInUseException(role.AdminUsers.Count);
        }

        dbContext.AdminRoles.Remove(role);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// 整份取代這個角色「非 <c>sysadmin_only</c>」的權限指派。<c>sysadmin_only</c> 權限碼的既有
    /// 指派（目前只有種子資料裡的系統管理員角色）維持原樣不受這次呼叫影響——見
    /// <see cref="AdminRoleSysadminOnlyPermissionException"/> 與 DTO 上的說明。
    /// </summary>
    public async Task<AdminRoleDetailDto?> ReplacePermissionsAsync(
        Guid roleId, ReplaceRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var role = await dbContext.AdminRoles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

        if (role is null)
        {
            return null;
        }

        var distinctCodes = request.Permissions.Select(p => p.PermissionCode).Distinct().ToList();
        var permissions = await dbContext.Permissions
            .Where(p => distinctCodes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code, cancellationToken);

        var missing = distinctCodes.Except(permissions.Keys).ToList();
        if (missing.Count > 0)
        {
            throw new AdminRoleValidationException($"找不到權限碼：{string.Join("、", missing)}。");
        }

        var sysadminOnlyCodes = permissions.Values.Where(p => p.SysadminOnly).Select(p => p.Code).ToList();
        if (sysadminOnlyCodes.Count > 0)
        {
            throw new AdminRoleSysadminOnlyPermissionException(sysadminOnlyCodes);
        }

        foreach (var input in request.Permissions)
        {
            ValidateScopeType(input.ScopeType);
        }

        // 只移除「這次呼叫負責管理」的既有列（非 sysadmin_only），sysadmin_only 的既有列原封不動保留。
        var removable = role.RolePermissions.Where(rp => !rp.Permission.SysadminOnly).ToList();
        foreach (var rp in removable)
        {
            dbContext.RolePermissions.Remove(rp);
        }

        foreach (var input in request.Permissions)
        {
            dbContext.RolePermissions.Add(new RolePermission
            {
                AdminRoleId = roleId,
                PermissionId = permissions[input.PermissionCode].Id,
                ScopeType = input.ScopeType,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(roleId, cancellationToken);
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private static void ValidateCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new AdminRoleValidationException("角色代碼為必填欄位。");
        }
        if (code.Length > 64)
        {
            throw new AdminRoleValidationException("角色代碼長度不能超過 64 個字元。");
        }
        if (!code.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_'))
        {
            throw new AdminRoleValidationException("角色代碼只能使用小寫英文字母、數字與底線（_）組成。");
        }
    }

    private static void ValidateScopeMode(string scopeMode)
    {
        if (!ValidScopeModes.Contains(scopeMode))
        {
            throw new AdminRoleValidationException(
                $"資料範圍模式「{scopeMode}」不合法，只能是「all_clubs」（跨俱樂部）或「own_clubs」（僅授權範圍內）。");
        }
    }

    private static void ValidateScopeType(string scopeType)
    {
        if (!ValidScopeTypes.Contains(scopeType))
        {
            throw new AdminRoleValidationException(
                $"權限範圍「{scopeType}」不合法，只能是：{string.Join("、", ValidScopeTypes)}。");
        }
    }

    private static AdminPermissionDto ToPermissionDto(Permission p) => new()
    {
        Id = p.Id,
        Code = p.Code,
        ModuleCode = p.ModuleCode,
        SubmoduleCode = p.SubmoduleCode,
        Domain = p.Domain,
        Action = p.Action,
        IsClubScoped = p.IsClubScoped,
        IsRestricted = p.IsRestricted,
        SysadminOnly = p.SysadminOnly,
        NameZh = p.NameZh,
        NameEn = p.NameEn,
    };

    private static AdminRoleDetailDto ToDetailDto(AdminRole role) => new()
    {
        Id = role.Id,
        Code = role.Code,
        NameZh = role.NameZh,
        NameEn = role.NameEn,
        ScopeMode = role.ScopeMode,
        IsSystem = role.IsSystem,
        SortOrder = role.SortOrder,
        AssignedAccountCount = role.AdminUsers.Count,
        Permissions = role.RolePermissions
            .OrderBy(rp => rp.Permission.ModuleCode).ThenBy(rp => rp.Permission.Code)
            .Select(rp => new AdminRolePermissionAssignmentDto
            {
                PermissionCode = rp.Permission.Code,
                NameZh = rp.Permission.NameZh,
                ScopeType = rp.ScopeType,
            }).ToList(),
    };
}
