namespace Tcrfc.Api.Security;

/// <summary>
/// 後台寫入／後台專用讀取端點的唯一入口：一次做完「有沒有登入」「這個俱樂部存不存在」
/// 「這個人對這個俱樂部有沒有權限」「這個人有沒有這項操作的權限碼」四件事，
/// 回傳的 <see cref="AdminClubScope"/> 才能被後台 repository 接受。
/// </summary>
public interface IAdminClubAuthorizer
{
    /// <exception cref="AdminUnauthenticatedException">沒有登入、權杖缺漏或無效。</exception>
    /// <exception cref="Tcrfc.Api.Security.ClubNotFoundException">俱樂部代碼查無資料或非啟用。</exception>
    /// <exception cref="AdminForbiddenException">已登入，但帳號已停用／對這個俱樂部沒有授權／沒有這項操作的權限碼。</exception>
    Task<AdminClubScope> AuthorizeAsync(
        HttpContext httpContext, string clubCode, string permissionCode, CancellationToken cancellationToken);

    /// <summary>
    /// S1-10 新增：放寬版本——只要角色持有 <paramref name="permissionCodes"/> 清單中「任一個」
    /// 就算通過，用於「同一個操作、不同角色拿到不同細分權限碼」的情境（例：G2 詢問收件匣，
    /// 系統管理員／客服拿 <c>enquiry.inbox.view</c>，學院／課程管理只拿範圍較窄的
    /// <c>enquiry.course.view</c>）。四步檢查中的①②③（帳號、俱樂部存在、俱樂部授權）只做一次，
    /// 第④步改成「清單中有一個權限碼通過就算過」而不是要求指定的單一權限碼通過。
    /// 通過後呼叫端仍需自行（例如透過 <see cref="IPermissionChecker.GetHeldPermissionCodesAsync"/>）
    /// 判斷實際持有哪幾個碼，藉此決定要套用哪一種列級過濾——本方法只負責「能不能進來」，
    /// 不負責「進來後能看到多少」。
    /// </summary>
    /// <exception cref="AdminUnauthenticatedException">沒有登入、權杖缺漏或無效。</exception>
    /// <exception cref="Tcrfc.Api.Security.ClubNotFoundException">俱樂部代碼查無資料或非啟用。</exception>
    /// <exception cref="AdminForbiddenException">已登入，但帳號已停用／對這個俱樂部沒有授權／清單中沒有任何一個權限碼持有。</exception>
    Task<AdminClubScope> AuthorizeAnyAsync(
        HttpContext httpContext, string clubCode, IReadOnlyList<string> permissionCodes, CancellationToken cancellationToken);
}
