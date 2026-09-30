using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

/// <summary>
/// 帳號本身的閘門檢查——「這個存取權杖對應的帳號，現在還是不是一個可以操作後台的活躍帳號」。
/// 抽成共用方法給 <see cref="AdminClubAuthorizer"/>（俱樂部範圍端點）與
/// <see cref="AdminSystemAuthorizer"/>（本輪新增，J1／J2／J4 全域端點）共用——兩者都需要一字不差
/// 的同一組判斷（帳號存在且啟用），若各自維護一份，
/// 日後改一邊（例如新增鎖定條件）很容易忘記改另一邊，見 docs/18-work-errors.md 對
/// 「同一條規則兩處實作」類錯誤的一貫要求。
///
/// 🔴 2026-09-30 使用者裁決：後台登入**不再強制**首次改密與啟用 2FA（正式環境亦同）。
/// <c>must_change_password</c>／<c>two_factor_enabled</c> 只是帳號屬性，不再擋端點；已啟用 2FA 的帳號
/// 仍須在登入時輸入驗證碼（那是登入流程 <c>totp_required</c>，不在這個閘門）。
///
/// ⚠️ 這裡只驗證「帳號本身能不能動」，不驗證「對哪個俱樂部」或「有沒有哪個操作的權限碼」——
/// 後兩者分別是 <see cref="IAdminClubAuthorizer"/> 多出來的步驟，與 <see cref="IPermissionChecker"/>。
/// </summary>
internal static class AdminAccountGate
{
    /// <exception cref="AdminUnauthenticatedException">沒有登入、權杖缺漏或無效。</exception>
    /// <exception cref="AdminForbiddenException">帳號已停用或不存在。</exception>
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
            .Select(u => new { u.Status, u.IsSuperAdmin })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null || account.Status != "active")
        {
            throw new AdminForbiddenException("帳號已停用或不存在，請聯繫系統管理員。");
        }

        return tokenIdentity.Value with { IsSuperAdmin = account.IsSuperAdmin };
    }
}
