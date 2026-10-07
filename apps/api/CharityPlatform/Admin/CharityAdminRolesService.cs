using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.CharityPlatform.Admin;

/// <summary>
/// 慈善後台「角色與權限」（對應主站 J2 <c>AdminRolesRepository</c>）。規則與主站相同：種子角色（<c>is_system</c>）不可刪、權限可調；
/// 仍有帳號指派的角色不可刪；<c>sysadmin_only</c> 權限碼不透過角色指派（寫入層擋下，既有指派原封不動）。
/// 🔴 慈善庫沒有 <c>scope_mode</c>／<c>sort_order</c>／<c>is_club_scoped</c> 欄位：回應分別補固定值 <c>all_clubs</c>、以 <c>seq</c> 為序、<c>false</c>。
/// 🔴 寫入要 <see cref="CharityAdminScope"/>，且與資料變更同一次 <c>SaveChanges</c> 寫稽核；稽核摘要只含筆數，不列權限碼（規劃書 §4.0）。
/// </summary>
public sealed class CharityAdminRolesService(CharityDbContext db, CharityAuditLogger audit)
{
    private const string FixedScopeMode = "all_clubs";

    private static readonly HashSet<string> ValidScopeTypes = new(StringComparer.Ordinal)
        { "all", "own_teams", "academy_only", "masked", "translate_only", "own_clubs" };

    public async Task<IReadOnlyList<CharityAdminPermissionDto>> ListPermissionsAsync(CancellationToken ct)
    {
        var rows = await db.Permissions.AsNoTracking()
            .OrderBy(p => p.ModuleCode).ThenBy(p => p.SubmoduleCode).ThenBy(p => p.Seq).ThenBy(p => p.Code)
            .ToListAsync(ct);
        return rows.Select(p => new CharityAdminPermissionDto
        {
            Id = p.Id, Code = p.Code, ModuleCode = p.ModuleCode, SubmoduleCode = p.SubmoduleCode, Domain = p.Domain,
            Action = p.Action, AppliesToClub = false, IsRestricted = p.IsRestricted, SysadminOnly = p.SysadminOnly,
            NameZh = p.NameZh, NameEn = p.NameEn,
        }).ToList();
    }

    public async Task<IReadOnlyList<CharityAdminRoleListItemDto>> ListRolesAsync(CancellationToken ct)
    {
        var rows = await db.AdminRoles.AsNoTracking().OrderBy(r => r.Seq)
            .Select(r => new { r.Id, r.Code, r.NameZh, r.NameEn, r.IsSystem, r.Seq, Count = r.AdminUsers.Count })
            .ToListAsync(ct);
        return rows.Select(r => new CharityAdminRoleListItemDto
        {
            Id = r.Id, Code = r.Code, NameZh = r.NameZh, NameEn = r.NameEn, ScopeMode = FixedScopeMode,
            IsSystem = r.IsSystem, SortOrder = (int)r.Seq, AssignedAccountCount = r.Count,
        }).ToList();
    }

    public async Task<CharityAdminRoleDetailDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var role = await db.AdminRoles.AsNoTracking()
            .Include(r => r.AdminUsers)
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        return role is null ? null : ToDetail(role);
    }

    public async Task<CharityAdminRoleDetailDto> CreateAsync(
        CharityAdminScope scope, CreateCharityAdminRoleRequest request, string? sourceIp, CancellationToken ct)
    {
        ValidateCode(request.Code);
        ValidateName(request.NameZh);

        if (await db.AdminRoles.AsNoTracking().AnyAsync(r => r.Code == request.Code, ct))
        {
            throw new CharityConflictException("角色代碼重複", $"角色代碼「{request.Code}」已經被使用，請換一個。", "code");
        }

        var now = DateTime.UtcNow;
        var role = new AdminRole
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            NameZh = request.NameZh.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim(),
            IsSystem = false, // 只有種子的九個角色是系統角色；本端點建立的一律可刪除。
            CreatedAt = now, UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };

        db.AdminRoles.Add(role);
        audit.Stage(scope, CharityAuditActions.AdminRoleCreate, CharityAuditTargets.AdminRole, role.Id, "建立自訂角色", null, sourceIp);
        await db.SaveChangesAsync(ct);
        return (await GetByIdAsync(role.Id, ct))!;
    }

    public async Task<CharityAdminRoleDetailDto?> UpdateAsync(
        CharityAdminScope scope, Guid id, UpdateCharityAdminRoleRequest request, string? sourceIp, CancellationToken ct)
    {
        ValidateName(request.NameZh);
        var role = await db.AdminRoles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null)
        {
            return null;
        }

        role.NameZh = request.NameZh.Trim();
        role.NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim();
        role.UpdatedAt = DateTime.UtcNow;
        role.UpdatedBy = scope.Identity.AdminUserId;

        audit.Stage(scope, CharityAuditActions.AdminRoleUpdate, CharityAuditTargets.AdminRole, role.Id, "更新角色名稱", null, sourceIp);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    /// <summary>回傳 <c>null</c>＝找不到，<c>true</c>＝刪除成功。</summary>
    public async Task<bool?> DeleteAsync(CharityAdminScope scope, Guid id, string? sourceIp, CancellationToken ct)
    {
        var role = await db.AdminRoles.Include(r => r.AdminUsers).Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null)
        {
            return null;
        }

        if (role.IsSystem)
        {
            throw new CharityApiException(StatusCodes.Status403Forbidden, "系統角色不可刪除", "這是系統內建角色，不可刪除；如需調整可修改它的權限指派。");
        }
        if (role.AdminUsers.Count > 0)
        {
            throw new CharityConflictException("角色仍在使用中", $"這個角色目前指派給 {role.AdminUsers.Count} 個帳號，請先移除這些帳號的指派後再刪除。");
        }

        db.RolePermissions.RemoveRange(role.RolePermissions);
        db.AdminRoles.Remove(role);
        audit.Stage(scope, CharityAuditActions.AdminRoleDelete, CharityAuditTargets.AdminRole, role.Id, "刪除自訂角色", null, sourceIp);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>整份取代這個角色「非 sysadmin_only」的權限指派；sysadmin_only 的既有指派維持原樣。</summary>
    public async Task<CharityAdminRoleDetailDto?> ReplacePermissionsAsync(
        CharityAdminScope scope, Guid roleId, ReplaceCharityRolePermissionsRequest request, string? sourceIp, CancellationToken ct)
    {
        var role = await db.AdminRoles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null)
        {
            return null;
        }

        var inputs = request.Permissions ?? [];
        var codes = inputs.Select(p => p.PermissionCode).Distinct().ToList();
        var permissions = await db.Permissions.Where(p => codes.Contains(p.Code)).ToDictionaryAsync(p => p.Code, ct);

        var missing = codes.Except(permissions.Keys).ToList();
        if (missing.Count > 0)
        {
            // 只回報筆數，不內插原始權限碼（規劃書 §4.0、E-52）。
            throw new CharityRolePermissionUnknownException(missing);
        }

        var sysadminOnly = permissions.Values.Where(p => p.SysadminOnly).Select(p => p.Code).ToList();
        if (sysadminOnly.Count > 0)
        {
            throw new CharityRolePermissionSysadminOnlyException(sysadminOnly);
        }

        foreach (var input in inputs)
        {
            if (!ValidScopeTypes.Contains(input.ScopeType))
            {
                throw new AdminValidationException($"權限範圍「{input.ScopeType}」不合法，只能是：{string.Join("、", ValidScopeTypes)}。", "permissions");
            }
        }

        foreach (var rp in role.RolePermissions.Where(rp => !rp.Permission.SysadminOnly).ToList())
        {
            db.RolePermissions.Remove(rp);
        }

        // 同一個權限碼重複送出時以最後一筆為準（複合主鍵不允許兩列）。
        foreach (var input in inputs.GroupBy(i => i.PermissionCode).Select(g => g.Last()))
        {
            db.RolePermissions.Add(new RolePermission
            {
                AdminRoleId = roleId,
                PermissionId = permissions[input.PermissionCode].Id,
                ScopeType = input.ScopeType,
            });
        }

        role.UpdatedAt = DateTime.UtcNow;
        role.UpdatedBy = scope.Identity.AdminUserId;
        audit.Stage(scope, CharityAuditActions.AdminRolePermissions, CharityAuditTargets.AdminRole, role.Id,
            $"調整角色權限，共 {codes.Count} 項", null, sourceIp);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(roleId, ct);
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private static void ValidateCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new AdminValidationException("角色代碼為必填欄位。", "code");
        }
        if (code.Length > 64)
        {
            throw new AdminValidationException("角色代碼長度不能超過 64 個字元。", "code");
        }
        if (!code.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_'))
        {
            throw new AdminValidationException("角色代碼只能使用小寫英文字母、數字與底線（_）組成。", "code");
        }
    }

    private static void ValidateName(string? nameZh)
    {
        if (string.IsNullOrWhiteSpace(nameZh))
        {
            throw new AdminValidationException("角色名稱為必填欄位。", "nameZh");
        }
        if (nameZh.Trim().Length > 50)
        {
            throw new AdminValidationException("角色名稱長度不能超過 50 個字元。", "nameZh");
        }
    }

    private static CharityAdminRoleDetailDto ToDetail(AdminRole role) => new()
    {
        Id = role.Id, Code = role.Code, NameZh = role.NameZh, NameEn = role.NameEn, ScopeMode = FixedScopeMode,
        IsSystem = role.IsSystem, SortOrder = (int)role.Seq, AssignedAccountCount = role.AdminUsers.Count,
        Permissions = role.RolePermissions
            .OrderBy(rp => rp.Permission.ModuleCode).ThenBy(rp => rp.Permission.Code)
            .Select(rp => new CharityAdminRolePermissionAssignmentDto
            {
                PermissionCode = rp.Permission.Code, NameZh = rp.Permission.NameZh, ScopeType = rp.ScopeType ?? "all",
            }).ToList(),
    };
}

/// <summary>送出的權限碼有查無資料者。400。🔴 只回報筆數，不內插原始權限碼（規劃書 §4.0、E-52）。</summary>
public sealed class CharityRolePermissionUnknownException(IReadOnlyList<string> codes)
    : CharityApiException(StatusCodes.Status400BadRequest, "輸入內容有誤", $"有 {codes.Count} 項權限查無資料，請重新整理權限清單後再試一次。", "permissions");

/// <summary>送出的權限碼含 <c>sysadmin_only</c> 者（不透過角色指派）。400。只回報筆數。</summary>
public sealed class CharityRolePermissionSysadminOnlyException(IReadOnlyList<string> codes)
    : CharityApiException(StatusCodes.Status400BadRequest, "輸入內容有誤", $"有 {codes.Count} 項權限僅供系統管理員使用，不透過角色指派，請重新選擇。", "permissions");
