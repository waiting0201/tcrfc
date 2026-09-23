using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Tcrfc.Api.Security;

/// <summary>已通過存取權杖驗證的後台使用者身分（來自 JWT claims，不查資料庫）。
/// ⚠️ 這只證明「這個人通過身分驗證」，不證明「這個人有權做這件事」——
/// 後者一律由 <see cref="IAdminClubAuthorizer"/>／<see cref="IPermissionChecker"/> 即時查庫判斷，
/// 不從 token 內容推斷（token 不帶權限與俱樂部範圍，理由見 AdminTokenService 上的說明）。</summary>
public readonly record struct AdminIdentity(Guid AdminUserId, string Username, bool IsSuperAdmin)
{
    /// <summary>從已驗證的 <see cref="ClaimsPrincipal"/> 讀出身分；未驗證或 claims 不完整回傳 <c>null</c>。</summary>
    public static AdminIdentity? FromClaimsPrincipal(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var subClaim = user.FindFirst(JwtRegisteredClaimNames.Sub) ?? user.FindFirst(ClaimTypes.NameIdentifier);
        var usernameClaim = user.FindFirst("username");
        var isSuperClaim = user.FindFirst("is_super_admin");

        if (subClaim is null || !Guid.TryParse(subClaim.Value, out var adminUserId) || usernameClaim is null)
        {
            return null;
        }

        return new AdminIdentity(adminUserId, usernameClaim.Value, isSuperClaim?.Value == "true");
    }
}
