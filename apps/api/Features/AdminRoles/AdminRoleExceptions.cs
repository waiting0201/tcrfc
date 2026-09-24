namespace Tcrfc.Api.Features.AdminRoles;

public abstract class AdminRoleException(string message) : Exception(message);

/// <summary>輸入不合法（角色代碼格式、<c>scope_mode</c>／<c>scope_type</c> 不在值域內、
/// 權限碼不存在……）。對應 400。</summary>
public sealed class AdminRoleValidationException(string message) : AdminRoleException(message);

/// <summary><c>admin_roles.code</c> 全域唯一已被使用。對應 409。</summary>
public sealed class AdminRoleCodeConflictException(string code)
    : AdminRoleException($"角色代碼「{code}」已經被使用，請換一個。");

/// <summary>種子角色（<c>is_system = true</c>）不可刪除，但權限可調（docs/12b-database-tables.md §7.2）。對應 403。</summary>
public sealed class AdminRoleSystemDeleteException()
    : AdminRoleException("這是系統內建角色，不可刪除；如需調整可修改它的權限指派。");

/// <summary>角色仍有帳號指派中，刪除前必須先移除所有指派——避免刪除後這些帳號的權限判斷憑空消失
/// 卻沒有任何明確訊息。對應 409。</summary>
public sealed class AdminRoleInUseException(int assignedAccountCount)
    : AdminRoleException($"這個角色目前指派給 {assignedAccountCount} 個帳號，請先移除這些帳號的指派後再刪除。");

/// <summary>
/// 🔴 <c>sysadmin_only</c> 權限碼不透過角色指派管理——docs/12b-database-tables.md §7.3
/// 「sysadmin_only：僅系統管理員」是帳號層級的閘門（<c>AdminUser.IsSuperAdmin</c>），不是
/// 「指派給某個角色」的東西；<see cref="Security.PermissionChecker"/> 即使真的指派了也一律
/// 對非超管帳號回傳沒有權限（見該檔案的說明），為避免介面上出現「勾了但其實不會生效」的
/// 誤導狀態，這裡直接在寫入時擋下。對應 400。
/// </summary>
public sealed class AdminRoleSysadminOnlyPermissionException(IReadOnlyList<string> codes)
    : AdminRoleException($"這些權限碼僅供系統管理員使用，不透過角色指派：{string.Join("、", codes)}。");
