using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminHomeSections;

/// <summary>
/// B3「首頁各區塊開關與排序、精選內容指定」。九個區塊代碼固定（見 <see cref="HomeSectionCatalog"/>），
/// 只有 Update，沒有 Create／Delete——區塊本身在種子階段就已經為每個俱樂部各種好一列
/// （<c>db/seed/generate-club-seed-sql.py</c>），理由見 <see cref="HomeSectionCatalog"/> 檔頭。
/// </summary>
public sealed class AdminHomeSectionsRepository(ClubDbContext dbContext, IQueryCache cache)
{
    private const string PublicEntity = "home-sections";

    public async Task<IReadOnlyList<AdminHomeSectionDto>> ListAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var rows = await dbContext.HomeSections.AsNoTracking()
            .Where(s => s.ClubId == scope.ClubId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    /// <summary>依區塊代碼更新。回傳 <c>null</c>＝這個俱樂部沒有這個區塊代碼的列（正常情況下不會
    /// 發生——九個代碼在建站種子階段就已經逐一種好，見 <see cref="HomeSectionCatalog"/> 檔頭；
    /// 唯一可能發生的情境是「新俱樂部上線但種子腳本忘了幫它種九個區塊」，這種情況下呼叫端會
    /// 拿到 404，不是靜默失敗）。</summary>
    public async Task<AdminHomeSectionDto?> UpdateAsync(
        AdminClubScope scope, string sectionCode, UpdateHomeSectionRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var section = await dbContext.HomeSections
            .FirstOrDefaultAsync(s => s.ClubId == scope.ClubId && s.SectionCode == sectionCode, cancellationToken);

        if (section is null)
        {
            return null;
        }

        if (request.FeaturedBannerId is Guid bannerId)
        {
            if (sectionCode != "hero")
            {
                throw new AdminHomeSectionValidationException(
                    "只有「Hero 輪播」區塊可以指定精選輪播，其餘區塊目前沒有可以指定的欄位（見 apps/api/README.md 綱要缺口）。");
            }

            var bannerBelongsToClub = await dbContext.Banners.AsNoTracking()
                .AnyAsync(b => b.Id == bannerId && b.ClubId == scope.ClubId, cancellationToken);
            if (!bannerBelongsToClub)
            {
                throw new AdminHomeSectionValidationException("指定的精選輪播不存在，或不屬於這個俱樂部。");
            }
        }

        section.IsEnabled = request.IsEnabled;
        section.SortOrder = request.SortOrder;
        section.FeaturedBannerId = request.FeaturedBannerId;
        section.UpdatedAt = DateTime.UtcNow;
        section.UpdatedBy = operatorId;

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(PublicEntity, scope.ClubCode, cancellationToken);

        return ToDto(section);
    }

    private static AdminHomeSectionDto ToDto(Data.EfEntities.HomeSection section)
    {
        var catalogEntry = HomeSectionCatalog.Find(section.SectionCode);
        return new AdminHomeSectionDto
        {
            Id = section.Id,
            SectionCode = section.SectionCode,
            NameZh = catalogEntry?.NameZh ?? section.SectionCode,
            NameEn = catalogEntry?.NameEn ?? section.SectionCode,
            IsEnabled = section.IsEnabled,
            SortOrder = section.SortOrder,
            FeaturedBannerId = section.FeaturedBannerId,
            UpdatedAt = section.UpdatedAt,
        };
    }
}
