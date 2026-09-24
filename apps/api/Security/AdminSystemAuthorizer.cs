using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

/// <summary><see cref="AdminSystemScope"/> 的唯一產生者。見該型別與 <see cref="IAdminSystemAuthorizer"/> 上的說明。</summary>
public sealed class AdminSystemAuthorizer(ClubDbContext db, IPermissionChecker permissionChecker) : IAdminSystemAuthorizer
{
    public async Task<AdminSystemScope> AuthorizeAsync(HttpContext httpContext, string permissionCode, CancellationToken cancellationToken)
    {
        var identity = await AdminAccountGate.RequireActiveAccountAsync(db, httpContext, cancellationToken);

        var hasPermission = await permissionChecker.HasPermissionAsync(
            identity.AdminUserId, identity.IsSuperAdmin, permissionCode, cancellationToken);

        if (!hasPermission)
        {
            throw new AdminForbiddenException($"你的角色沒有「{permissionCode}」這項操作的權限。");
        }

        return new AdminSystemScope(identity);
    }
}
