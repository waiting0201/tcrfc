using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

/// <summary>
/// <see cref="AdminClubScope"/> 的唯一產生者。四個檢查依序執行，任何一步失敗立刻丟例外——
/// 這是任務要求「四種擋下情境」在程式碼裡的單一落點，見 apps/api/README.md 的驗收紀錄。
/// </summary>
public sealed class AdminClubAuthorizer(ClubDbContext db, IClubResolver clubResolver) : IAdminClubAuthorizer
{
    public async Task<AdminClubScope> AuthorizeAsync(
        HttpContext httpContext, string clubCode, string permissionCode, CancellationToken cancellationToken)
    {
        // ①＋帳號本身狀態（存在、啟用、已改密、已完成 2FA）：抽到 AdminAccountGate 共用
        // （本輪新增，供 AdminSystemAuthorizer 共用同一組判斷，見該檔案上的說明）。
        var identity = await AdminAccountGate.RequireActiveAccountAsync(db, httpContext, cancellationToken);

        // ② 俱樂部本身存不存在：沿用既有的公開端點驗證邏輯，行為完全不變（含快取）。
        var club = await clubResolver.ResolveAsync(clubCode, cancellationToken);

        // ③ 資料範圍：系統管理員跳過整個範圍查詢（規劃書 §6「資料範圍規則」）。
        if (!identity.IsSuperAdmin)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var hasClubGrant = await db.AdminUserClubs
                .AsNoTracking()
                .AnyAsync(g => g.AdminUserId == identity.AdminUserId
                             && g.ClubId == club.ClubId
                             && g.IsActive
                             && (g.ExpiresOn == null || g.ExpiresOn >= today),
                    cancellationToken);

            if (!hasClubGrant)
            {
                throw new AdminForbiddenException($"你沒有被授權存取俱樂部「{clubCode}」的後台資料。");
            }
        }

        // ④ 這項操作的權限碼。
        var permissionChecker = new PermissionChecker(db);
        var hasPermission = await permissionChecker.HasPermissionAsync(
            identity.AdminUserId, identity.IsSuperAdmin, permissionCode, cancellationToken);

        if (!hasPermission)
        {
            throw new AdminForbiddenException($"你的角色沒有「{permissionCode}」這項操作的權限。");
        }

        return new AdminClubScope(club, identity);
    }
}
