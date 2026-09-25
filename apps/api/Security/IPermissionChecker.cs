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

    /// <summary>
    /// S1-10 新增：一次查出 <paramref name="candidateCodes"/> 裡「這個人實際持有哪幾個」。
    /// 用於 G2 詢問收件匣這種「同一個操作，不同角色只拿到不同細分權限碼」的情境
    /// （<c>enquiry.inbox.*</c> 全權限 vs. <c>enquiry.course.*</c>／<c>enquiry.partnership.*</c>／
    /// <c>enquiry.media.*</c> 類別限定），呼叫端依回傳集合決定要不要加上
    /// <c>WHERE form_code IN (...)</c> 的過濾條件，見 <c>Features/AdminEnquiries/AdminEnquiriesRepository.cs</c>。
    /// <paramref name="isSuperAdmin"/> 為 <c>true</c> 時直接回傳整個 <paramref name="candidateCodes"/>
    /// （系統管理員跳過整個範圍查詢，跟 <see cref="HasPermissionAsync"/> 同一條規則）。
    /// </summary>
    Task<IReadOnlySet<string>> GetHeldPermissionCodesAsync(
        Guid adminUserId, bool isSuperAdmin, IReadOnlyList<string> candidateCodes, CancellationToken cancellationToken);

    /// <summary>
    /// S1-10 修正（2026-09-25）新增：這個帳號**目前實際持有的全部**權限碼，供
    /// <c>GET /admin/auth/me</c> 回傳給前端，取代「每個模組各自手寫一份角色→操作對照表、跟種子
    /// 腳本手動同步」的既有做法（E-39 同類風險——已經在 <c>useProgramPermissions</c>／
    /// <c>useFormsPermissions</c> 發生過兩次，見 apps/api/README.md「S1-10」段回報）。
    ///
    /// 回傳形狀是 <c>Code → 這個人對這個權限碼持有的 scope_type 集合</c>（一個人可能透過多個角色
    /// 持有同一個權限碼、各自帶不同 <c>scope_type</c>，例如同時是「學院／課程管理」與「合作球隊
    /// 管理」）——**不做「多個 scope_type 該如何合併成單一有效值」的商業判斷**（那件事留給
    /// <see cref="TeamRowScope"/>／<see cref="AdminTeamRowScopeResolver"/> 這種已經為特定資源類型
    /// 定義過合併規則的型別，例如「'all' 或 'own_clubs' 視同不限」），這裡只忠實回報資料庫裡的
    /// 原始集合，避免發明一個新的、只有這個端點在用的合併規則。
    ///
    /// <paramref name="isSuperAdmin"/> 為 <c>true</c> 時**回傳系統裡全部權限碼**（含
    /// <c>sysadmin_only</c>），每個都標記 <c>["all"]</c>——系統管理員跳過整個 <c>role_permissions</c>
    /// 查詢直接視為持有一切，跟 <see cref="HasPermissionAsync"/>／<see cref="TeamRowScope.IsUnrestricted"/>
    /// 同一條規則的第三個落點。**權限碼只給程式判斷用，前端不得顯示**（主站規劃書 §4.0「介面一律
    /// 日常中文……不顯示模組代號、權限碼」）。
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetAllHeldPermissionsAsync(
        Guid adminUserId, bool isSuperAdmin, CancellationToken cancellationToken);
}
