namespace Tcrfc.Api.Features.AdminRoles;

/// <summary>權限碼字典（<c>permissions</c> 表）一列，供角色權限指派畫面組出勾選清單。</summary>
public sealed record AdminPermissionDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string ModuleCode { get; init; }
    public string? SubmoduleCode { get; init; }
    public string? Domain { get; init; }
    public string? Action { get; init; }
    public required bool IsClubScoped { get; init; }
    public required bool IsRestricted { get; init; }
    public required bool SysadminOnly { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
}

/// <summary>角色底下一筆權限指派（<c>role_permissions</c>）。</summary>
public sealed record AdminRolePermissionAssignmentDto
{
    public required string PermissionCode { get; init; }
    public required string NameZh { get; init; }

    /// <summary>值域：<c>all</c>／<c>own_teams</c>／<c>academy_only</c>／<c>masked</c>／
    /// <c>translate_only</c>／<c>own_clubs</c>（<c>role_permissions.scope_type</c> 的 CHECK 約束）。</summary>
    public required string ScopeType { get; init; }
}

public sealed record AdminRoleListItemDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
    public required string ScopeMode { get; init; }
    public required bool IsSystem { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>目前有幾個帳號指派這個角色——刪除前的參考資訊，不需要另外打一支端點查。</summary>
    public required int AssignedAccountCount { get; init; }
}

public sealed record AdminRoleDetailDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
    public required string ScopeMode { get; init; }
    public required bool IsSystem { get; init; }
    public required int SortOrder { get; init; }
    public required int AssignedAccountCount { get; init; }
    public required IReadOnlyList<AdminRolePermissionAssignmentDto> Permissions { get; init; }
}

public sealed record CreateAdminRoleRequest
{
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }

    /// <summary>值域 <c>all_clubs</c>／<c>own_clubs</c>。</summary>
    public required string ScopeMode { get; init; }
}

public sealed record UpdateAdminRoleRequest
{
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
    public required string ScopeMode { get; init; }
}

public sealed record RolePermissionInput
{
    public required string PermissionCode { get; init; }
    public required string ScopeType { get; init; }
}

/// <summary>
/// 整份取代這個角色的權限指派（不含 <c>sysadmin_only</c> 權限碼——那些一律被擋在寫入層，
/// 見 <see cref="AdminRoleSysadminOnlyPermissionException"/>；種子資料裡系統管理員角色仍種有
/// 這些權限碼，本端點只換掉「非 sysadmin_only」的那個子集，不會動到它們，見 repository 的說明）。
/// </summary>
public sealed record ReplaceRolePermissionsRequest
{
    public required IReadOnlyList<RolePermissionInput> Permissions { get; init; }
}
