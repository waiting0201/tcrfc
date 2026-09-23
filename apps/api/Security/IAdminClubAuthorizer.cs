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
}
