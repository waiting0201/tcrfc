namespace Tcrfc.Api.Security;

/// <summary>
/// 「能做什麼」：AdminUser → AdminUserRole → AdminRole → RolePermission → Permission
/// （docs/12b-database-tables.md §7.1）的聯集查詢。與「對誰做」（<see cref="IAdminClubAuthorizer"/>
/// 的 AdminUserClub 檢查）是兩個獨立的問句，缺一不可——這正是規劃書「匯出先套資料範圍，
/// 再套受限欄位授權；兩道關卡不可互相取代」在一般寫入操作上的對應版本。
///
/// ⚠️ 本次只實作「有沒有這個權限碼」的布林判斷，**不實作 <c>role_permissions.scope_type</c>
/// 的細粒度限制**（own_teams／academy_only／masked／translate_only）——那是欄位層級與列層級的
/// 篩選規則，牽涉到每個模組各自的資料形狀，留給實作對應模組寫入端點時一併處理，見
/// apps/api/README.md「本輪沒做的部分」。
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// <paramref name="isSuperAdmin"/> 一律由呼叫端傳入「剛從資料庫查到的最新值」，
    /// 不接受呼叫端傳入 JWT 裡的舊 claim——避免帳號在權杖簽發後被降級，仍在權杖效期內
    /// 誤判為超管（見 <see cref="AdminClubAuthorizer"/> 的呼叫方式）。
    /// </summary>
    Task<bool> HasPermissionAsync(Guid adminUserId, bool isSuperAdmin, string permissionCode, CancellationToken cancellationToken);
}
