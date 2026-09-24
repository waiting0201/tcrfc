namespace Tcrfc.Api.Security;

/// <summary>
/// J1／J2／J4（俱樂部主檔與俱樂部授權）等全域（不含 <c>{club}</c> 路由段）後台端點的唯一入口。
/// 跟 <see cref="IAdminClubAuthorizer"/> 的差別只在於少了「這個俱樂部存不存在／對這個俱樂部有沒有
/// 授權」兩步——這些端點的操作對象本來就不是「某一個俱樂部的資料」（帳號、角色、俱樂部主檔本身、
/// 俱樂部授權關聯表），沒有「當下站在哪個俱樂部」這個概念可言。
/// </summary>
public interface IAdminSystemAuthorizer
{
    /// <exception cref="AdminUnauthenticatedException">沒有登入、權杖缺漏或無效。</exception>
    /// <exception cref="AdminForbiddenException">已登入，但帳號已停用／尚未完成強制改密或 2FA／沒有這項操作的權限碼。</exception>
    Task<AdminSystemScope> AuthorizeAsync(HttpContext httpContext, string permissionCode, CancellationToken cancellationToken);
}
