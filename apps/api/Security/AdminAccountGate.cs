using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

/// <summary>
/// 帳號本身的閘門檢查——「這個存取權杖對應的帳號，現在還是不是一個可以操作後台的活躍帳號」。
/// 抽成共用方法給 <see cref="AdminClubAuthorizer"/>（俱樂部範圍端點）與
/// <see cref="AdminSystemAuthorizer"/>（本輪新增，J1／J2／J4 全域端點）共用——兩者都需要一字不差
/// 的同一組判斷（帳號存在且啟用、未被要求強制改密、已完成強制 2FA），若各自維護一份，
/// 日後改一邊（例如新增鎖定條件）很容易忘記改另一邊，見 docs/18-work-errors.md 對
/// 「同一條規則兩處實作」類錯誤的一貫要求。
///
/// ⚠️ 這裡只驗證「帳號本身能不能動」，不驗證「對哪個俱樂部」或「有沒有哪個操作的權限碼」——
/// 後兩者分別是 <see cref="IAdminClubAuthorizer"/> 多出來的步驟，與 <see cref="IPermissionChecker"/>。
/// </summary>
internal static class AdminAccountGate
{
    /// <exception cref="AdminUnauthenticatedException">沒有登入、權杖缺漏或無效。</exception>
    /// <exception cref="AdminForbiddenException">帳號已停用／不存在、尚未完成強制改密、尚未完成強制 2FA。</exception>
    public static async Task<AdminIdentity> RequireActiveAccountAsync(
        ClubDbContext db, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var tokenIdentity = AdminIdentity.FromClaimsPrincipal(httpContext.User);
        if (tokenIdentity is null)
        {
            throw new AdminUnauthenticatedException();
        }

        // ⚠️ 一律重查資料庫，不信任 JWT 裡的 is_super_admin claim——帳號可能在權杖簽發後
        // 被停用或降級，權杖在效期內仍會被拿來用（見 AdminClubAuthorizer 原本的說明）。
        var account = await db.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == tokenIdentity.Value.AdminUserId)
            .Select(u => new { u.Status, u.IsSuperAdmin, u.MustChangePassword, u.TwoFactorEnabled })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null || account.Status != "active")
        {
            throw new AdminForbiddenException("帳號已停用或不存在，請聯繫系統管理員。");
        }

        if (account.MustChangePassword)
        {
            throw new AdminForbiddenException("首次登入須先更換密碼，請呼叫「變更密碼」端點。");
        }

        if (!account.TwoFactorEnabled)
        {
            throw new AdminForbiddenException("後台強制兩階段驗證，請先完成 2FA 設定。");
        }

        return tokenIdentity.Value with { IsSuperAdmin = account.IsSuperAdmin };
    }
}
