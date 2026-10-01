using Tcrfc.Api.Security;

namespace Tcrfc.Api.CharityPlatform.Security;

/// <summary>
/// 慈善後台所有端點的<b>唯一</b>授權入口（<see cref="CharityAdminScope"/> 的唯一產生者見實作類別）。
/// 與主站的 <c>IAdminClubAuthorizer</c>／<c>IAdminSystemAuthorizer</c> 是同一設計的獨立版本：
/// 讀的是慈善資料庫的 <c>admin_users</c>／<c>role_permissions</c>，驗證的是慈善後台自己的 JWT 方案
/// （<see cref="CharityTokenService.SchemeName"/>），沒有任何一步碰到主站資料庫或主站權杖。
/// </summary>
public interface ICharityAdminAuthorizer
{
    /// <summary>只確認「有登入且帳號啟用中」，不檢查權限碼（<c>/me</c>、變更密碼等個人操作用）。</summary>
    /// <exception cref="AdminUnauthenticatedException">沒有登入、權杖缺漏或無效。</exception>
    /// <exception cref="AdminForbiddenException">帳號已停用或不存在。</exception>
    Task<CharityAdminIdentity> RequireSignedInAsync(HttpContext httpContext, CancellationToken cancellationToken);

    /// <exception cref="AdminUnauthenticatedException">沒有登入、權杖缺漏或無效。</exception>
    /// <exception cref="AdminForbiddenException">帳號已停用，或沒有這項操作的權限碼。</exception>
    Task<CharityAdminScope> AuthorizeAsync(HttpContext httpContext, string permissionCode, CancellationToken cancellationToken);

    /// <summary>清單中有任何一個權限碼通過就算過（同一個畫面多個角色各自持有不同細分權限碼的情境）。</summary>
    Task<CharityAdminScope> AuthorizeAnyAsync(
        HttpContext httpContext, IReadOnlyList<string> permissionCodes, CancellationToken cancellationToken);

    /// <summary>已通過授權的使用者，是否另外持有某個「加碼權限」（例如看個資明文的 <c>reveal</c>）。
    /// 每次都即時查庫，不快取。</summary>
    Task<bool> HasAdditionalPermissionAsync(CharityAdminScope scope, string permissionCode, CancellationToken cancellationToken);
}
