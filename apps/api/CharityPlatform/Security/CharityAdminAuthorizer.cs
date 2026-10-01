using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.CharityPlatform.Security;

/// <summary>
/// <see cref="CharityAdminScope"/> 的唯一產生者。依序：① 用慈善後台自己的 JWT 方案驗證權杖
/// （<see cref="AuthenticationHttpContextExtensions.AuthenticateAsync(HttpContext, string?)"/>，不讀 <c>HttpContext.User</c>——
/// 那是主站預設方案的結果，主站的權杖在這裡一律當作沒登入）② 重查資料庫確認帳號存在且啟用，並以資料庫的
/// <c>is_super_admin</c> 為準（不信任 JWT 裡的舊 claim）③ 權限碼。任何一步失敗立刻丟例外。
/// </summary>
public sealed class CharityAdminAuthorizer(CharityDbContext db) : ICharityAdminAuthorizer
{
    private readonly CharityPermissionChecker _permissions = new(db);

    public async Task<CharityAdminIdentity> RequireSignedInAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var authResult = await httpContext.AuthenticateAsync(CharityTokenService.SchemeName);
        var tokenIdentity = authResult.Succeeded ? CharityAdminIdentity.FromClaimsPrincipal(authResult.Principal) : null;
        if (tokenIdentity is null)
        {
            throw new AdminUnauthenticatedException();
        }

        var account = await db.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == tokenIdentity.Value.AdminUserId)
            .Select(u => new { u.Status, u.IsSuperAdmin })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null || account.Status != "active")
        {
            throw new AdminForbiddenException("帳號已停用或不存在，請聯繫系統管理員。");
        }

        return tokenIdentity.Value with { IsSuperAdmin = account.IsSuperAdmin };
    }

    public async Task<CharityAdminScope> AuthorizeAsync(
        HttpContext httpContext, string permissionCode, CancellationToken cancellationToken)
    {
        var identity = await RequireSignedInAsync(httpContext, cancellationToken);

        if (!await _permissions.HasPermissionAsync(identity.AdminUserId, identity.IsSuperAdmin, permissionCode, cancellationToken))
        {
            throw new AdminForbiddenException("你的角色沒有這項操作的權限，請洽系統管理員。"); // 不得內插權限碼（規劃書 §4.0）
        }

        return new CharityAdminScope(identity, permissionCode);
    }

    public async Task<CharityAdminScope> AuthorizeAnyAsync(
        HttpContext httpContext, IReadOnlyList<string> permissionCodes, CancellationToken cancellationToken)
    {
        var identity = await RequireSignedInAsync(httpContext, cancellationToken);

        var held = await _permissions.GetHeldPermissionCodesAsync(identity.AdminUserId, identity.IsSuperAdmin, permissionCodes, cancellationToken);
        if (held.Count == 0)
        {
            throw new AdminForbiddenException("你的角色沒有這項操作的權限，請洽系統管理員。");
        }

        return new CharityAdminScope(identity, permissionCodes.First(held.Contains));
    }

    public Task<bool> HasAdditionalPermissionAsync(CharityAdminScope scope, string permissionCode, CancellationToken cancellationToken)
        => _permissions.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, permissionCode, cancellationToken);
}
