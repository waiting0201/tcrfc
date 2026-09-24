using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.AdminTeams;

/// <summary>J4「球隊授權」（<c>admin_user_teams</c>）畫面的唯讀支援資料——只有一個查詢，
/// 不做球隊本身的 CRUD（C1 球隊管理尚未接真實授權，本次任務範圍只做前端回報的唯讀缺口）。</summary>
public sealed class AdminTeamsRepository(ClubDbContext dbContext)
{
    /// <summary>不分俱樂部列出全部球隊——見 <c>AdminTeamsEndpoints</c> 檔頭「跨俱樂部」的說明。
    /// <paramref name="clubCode"/> 給定時縮小到單一俱樂部（畫面已經選定俱樂部時可以少拉一點資料，
    /// 不給就是完整跨俱樂部清單）。</summary>
    public async Task<IReadOnlyList<AdminTeamListItemDto>> ListAsync(string? clubCode, CancellationToken cancellationToken)
    {
        var query = dbContext.Teams.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(clubCode))
        {
            query = query.Where(t => t.Club.Code == clubCode);
        }

        var rows = await query
            .OrderBy(t => t.Club.SortOrder).ThenBy(t => t.SortOrder).ThenBy(t => t.Code)
            .Select(t => new
            {
                t.Id,
                t.ClubId,
                ClubCode = t.Club.Code,
                ClubNameZh = t.Club.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                t.Code,
                t.Type,
                t.Gender,
                t.AgeBand,
                NameZh = t.TeamsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = t.TeamsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminTeamListItemDto
        {
            Id = r.Id,
            ClubId = r.ClubId,
            ClubCode = r.ClubCode,
            ClubNameZh = r.ClubNameZh,
            Code = r.Code,
            Type = r.Type,
            Gender = r.Gender,
            AgeBand = r.AgeBand,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
        }).ToList();
    }
}
