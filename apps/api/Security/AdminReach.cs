using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

/// <summary>
/// 「這個帳號能碰到哪些俱樂部」——規劃書 §6「有效範圍 ＝ AdminUserClub 中啟用且未到期的俱樂部集合」，系統管理員跳過。
/// 給需要跨俱樂部彙整的清單使用（K1 會員名單：帳號層跨俱樂部，但只看得到自己有授權的俱樂部的會籍列）。
/// 單一俱樂部的端點不需要它——<see cref="AdminClubScope"/> 已經是通過授權的單一俱樂部。
/// </summary>
public static class AdminReach
{
    /// <summary><c>null</c> 代表不受限（系統管理員，可看全部俱樂部）；否則為授權集合（至少包含目前操作的俱樂部）。</summary>
    public static async Task<IReadOnlySet<Guid>?> GetAuthorizedClubIdsAsync(
        ClubDbContext db, AdminClubScope scope, CancellationToken cancellationToken)
    {
        if (scope.Identity.IsSuperAdmin)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var ids = await db.AdminUserClubs.AsNoTracking()
            .Where(g => g.AdminUserId == scope.Identity.AdminUserId && g.IsActive && (g.ExpiresOn == null || g.ExpiresOn >= today))
            .Select(g => g.ClubId)
            .ToListAsync(cancellationToken);
        var set = new HashSet<Guid>(ids) { scope.ClubId };
        return set;
    }
}
