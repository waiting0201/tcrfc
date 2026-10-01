using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Tcrfc.Api.CharityPlatform.Security;

/// <summary>已通過<b>慈善後台</b>存取權杖驗證的後台使用者身分（來自 JWT claims，不查資料庫）。
/// 只證明「這個人通過身分驗證」，不證明「這個人有權做這件事」——後者一律由
/// <see cref="ICharityAdminAuthorizer"/> 即時查庫判斷（權杖不帶權限，理由見 <see cref="CharityTokenService"/>）。</summary>
public readonly record struct CharityAdminIdentity(Guid AdminUserId, string Username, bool IsSuperAdmin)
{
    public static CharityAdminIdentity? FromClaimsPrincipal(ClaimsPrincipal? user)
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

        return new CharityAdminIdentity(adminUserId, usernameClaim.Value, isSuperClaim?.Value == "true");
    }
}
