using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.AdminVenues;

/// <summary>
/// 全站共用場地主檔的唯讀清單（S1-12d 後續缺口補完，2026-09-29）。補的是
/// <c>apps/admin/README.md</c>「I：網站設定」與 <c>apps/admin/src/views/teams/MatchEditView.vue</c>
/// 檔頭都記過的同一個缺口：後台一直沒有任何「列出全部場地」的端點，導致兩個畫面都只能用自由文字
/// 或「編輯既有＋新增」，做不出真正的「從既有場地中選擇」下拉選單。
///
/// **只做唯讀清單，不做場地的新增／刪除管理**（任務範圍明文排除，規格也沒有要求）——新增場地仍然
/// 透過既有管道（例如 <c>Features/AdminSiteFacts</c> 的 <c>UpdateSiteFactVenueRequest</c> 省略
/// <c>Id</c> 即新增一筆）完成，本端點只負責「有哪些既有場地可以選」這個唯讀問題。
/// </summary>
public sealed class AdminVenuesRepository(ClubDbContext dbContext)
{
    public async Task<IReadOnlyList<AdminVenueListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Venues.AsNoTracking()
            .Select(v => new
            {
                v.Id,
                v.SortOrder,
                NameZh = v.VenuesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = v.VenuesI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                Address = v.VenuesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Address).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // 既有列的 SortOrder 目前全部是 0（從未被賦予有意義的排序值，見種子資料），單獨用它排序
        // 會落回資料庫回傳順序不保證的問題；用中文名稱做穩定的次要排序，讓下拉選單順序至少可預期。
        return rows
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.NameZh, StringComparer.Ordinal)
            .Select(r => new AdminVenueListItemDto
            {
                Id = r.Id,
                NameZh = r.NameZh ?? string.Empty,
                NameEn = r.NameEn,
                Address = r.Address,
            })
            .ToList();
    }
}
