using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCharity;

/// <summary>
/// B5「事蹟紀錄」（規劃書 §4.2 B5、前台 11.3）：**三項必填**——公益團體、捐助內容（文字描述）、活動圖片（可多張）；
/// 另含日期、地點、簡述、所屬計畫（選填）。主圖存 <c>impact_records.image_key</c>（建立時必填，對應「活動圖片」必填），
/// 其他圖片走子表 <c>impact_record_images</c>（可多張）。事蹟沒有草稿狀態：建立即前台可見（規劃書沒有為事蹟定義發布流程）。
/// <c>club_id</c> 可為空：共同列只讀。
/// </summary>
public sealed class AdminImpactRecordsRepository(ClubDbContext dbContext, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    public async Task<PagedResult<AdminImpactRecordListItemDto>> ListAsync(
        AdminClubScope scope, Guid? charityId, Guid? programId, int? year, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.ImpactRecords.AsNoTracking().Where(r => r.ClubId == scope.ClubId || r.ClubId == null);
        if (charityId is Guid c)
        {
            query = query.Where(r => r.CharityId == c);
        }

        if (programId is Guid p)
        {
            query = query.Where(r => r.CharityProgramId == p);
        }

        if (year is int y)
        {
            query = query.Where(r => r.HappenedOn != null && r.HappenedOn.Value.Year == y);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(r => r.ImpactRecordsI18ns.Any(i =>
                (i.DonationContent != null && i.DonationContent.Contains(k)) || (i.Location != null && i.Location.Contains(k))
                || (i.BriefDescription != null && i.BriefDescription.Contains(k)))
                || r.Charity.CharitiesI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(r => r.IsPinned).ThenBy(r => r.SortOrder).ThenByDescending(r => r.HappenedOn).ThenByDescending(r => r.RowSeq)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, IsShared = r.ClubId == null, r.CharityId, r.CharityProgramId, r.HappenedOn, r.SortOrder, r.IsPinned, r.ImageKey, r.UpdatedAt,
                CharityNameZh = r.Charity.CharitiesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                ProgramNameZh = r.CharityProgram == null ? null
                    : r.CharityProgram.CharityProgramsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                DonationContentZh = r.ImpactRecordsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.DonationContent).FirstOrDefault(),
                LocationZh = r.ImpactRecordsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Location).FirstOrDefault(),
            }).ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminImpactRecordListItemDto
        {
            Id = r.Id, IsShared = r.IsShared, CharityId = r.CharityId, CharityNameZh = r.CharityNameZh,
            CharityProgramId = r.CharityProgramId, ProgramNameZh = r.ProgramNameZh, HappenedOn = r.HappenedOn,
            SortOrder = r.SortOrder, IsPinned = r.IsPinned, ImageKey = r.ImageKey, ImageUrl = imageUrls.Resolve(r.ImageKey),
            ImageThumbUrl = r.ImageKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(r.ImageKey)),
            DonationContentZh = r.DonationContentZh, LocationZh = r.LocationZh, UpdatedAt = r.UpdatedAt,
        }).ToList();
        return new PagedResult<AdminImpactRecordListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<AdminImpactRecordDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var record = await dbContext.ImpactRecords.AsNoTracking()
            .Include(r => r.ImpactRecordsI18ns).Include(r => r.ImpactRecordImages)
            .Include(r => r.Charity).ThenInclude(c => c.CharitiesI18ns)
            .Include(r => r.CharityProgram).ThenInclude(p => p!.CharityProgramsI18ns)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id && (r.ClubId == scope.ClubId || r.ClubId == null), cancellationToken);
        return record is null ? null : ToDetail(record);
    }

    public async Task<AdminImpactRecordDetailDto> CreateAsync(
        AdminClubScope scope, Guid id, UpsertAdminImpactRecordRequest request, UploadedImageInfo image, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        await EnsureRefsAsync(scope, request, cancellationToken);
        var now = DateTime.UtcNow;
        var record = new ImpactRecord
        {
            Id = id, ClubId = scope.ClubId, ImageKey = image.Key, ImageWidth = image.Width, ImageHeight = image.Height,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        Apply(record, request);
        dbContext.ImpactRecords.Add(record);
        SetI18n(record, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        return (await GetByIdAsync(scope, id, cancellationToken))!;
    }

    public async Task<AdminImpactRecordDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminImpactRecordRequest request, ImageFieldUpdate image, OrphanedObjects orphans,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        var record = await dbContext.ImpactRecords.Include(r => r.ImpactRecordsI18ns)
            .FirstOrDefaultAsync(r => r.Id == id && (r.ClubId == scope.ClubId || r.ClubId == null), cancellationToken);
        if (record is null)
        {
            return null;
        }

        if (record.ClubId is null)
        {
            throw new SharedContentReadOnlyException("事蹟紀錄");
        }

        await EnsureRefsAsync(scope, request, cancellationToken);
        if (image.Change)
        {
            if (image.Key is null)
            {
                throw new AdminValidationException("事蹟紀錄的活動圖片是必填的，不能移除；如要更換請直接上傳新圖片。", "image");
            }

            orphans.Image(record.ImageKey);
            record.ImageKey = image.Key;
            record.ImageWidth = image.Width;
            record.ImageHeight = image.Height;
        }

        Apply(record, request);
        record.UpdatedAt = DateTime.UtcNow;
        record.UpdatedBy = operatorId;
        SetI18n(record, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var record = await dbContext.ImpactRecords.Include(r => r.ImpactRecordImages)
            .FirstOrDefaultAsync(r => r.Id == id && (r.ClubId == scope.ClubId || r.ClubId == null), cancellationToken);
        if (record is null)
        {
            return false;
        }

        if (record.ClubId is null)
        {
            throw new SharedContentReadOnlyException("事蹟紀錄");
        }

        orphans.Image(record.ImageKey);
        foreach (var image in record.ImpactRecordImages)
        {
            orphans.Image(image.ImageKey);
        }

        dbContext.ImpactRecords.Remove(record);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        return true;
    }

    public async Task<AdminImpactRecordDetailDto?> AddImageAsync(
        AdminClubScope scope, Guid id, UploadedImageInfo image, Guid? operatorId, CancellationToken cancellationToken)
    {
        var record = await LoadOwnForGalleryAsync(scope, id, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        dbContext.ImpactRecordImages.Add(new ImpactRecordImage
        {
            Id = Guid.NewGuid(), ImpactRecordId = id, ImageKey = image.Key, CreatedAt = now, UpdatedAt = now,
            SortOrder = record.ImpactRecordImages.Count == 0 ? 0 : record.ImpactRecordImages.Max(i => i.SortOrder) + 1,
            CreatedBy = operatorId, UpdatedBy = operatorId,
        });
        record.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteImageAsync(AdminClubScope scope, Guid id, Guid imageId, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var record = await LoadOwnForGalleryAsync(scope, id, cancellationToken);
        var image = record?.ImpactRecordImages.FirstOrDefault(i => i.Id == imageId);
        if (image is null)
        {
            return false;
        }

        orphans.Image(image.ImageKey);
        dbContext.ImpactRecordImages.Remove(image);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        return true;
    }

    public async Task<AdminImpactRecordDetailDto?> ReorderImagesAsync(
        AdminClubScope scope, Guid id, IReadOnlyList<Guid> imageIds, CancellationToken cancellationToken)
    {
        var record = await LoadOwnForGalleryAsync(scope, id, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var byId = record.ImpactRecordImages.ToDictionary(i => i.Id);
        if (imageIds.Distinct().Count() != imageIds.Count || imageIds.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("圖片排序清單含有不存在或重複的圖片，請重新整理後再試。");
        }

        var order = imageIds.Concat(record.ImpactRecordImages.OrderBy(i => i.SortOrder).Select(i => i.Id).Where(i => !imageIds.Contains(i))).ToList();
        for (var i = 0; i < order.Count; i++)
        {
            byId[order[i]].SortOrder = i;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    private async Task<ImpactRecord?> LoadOwnForGalleryAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var record = await dbContext.ImpactRecords.Include(r => r.ImpactRecordImages)
            .FirstOrDefaultAsync(r => r.Id == id && (r.ClubId == scope.ClubId || r.ClubId == null), cancellationToken);
        if (record is not null && record.ClubId is null)
        {
            throw new SharedContentReadOnlyException("事蹟紀錄");
        }

        return record;
    }

    private static void Validate(UpsertAdminImpactRecordRequest request)
    {
        AdminInput.RequireText(request.Content.Zh.DonationContent, "中文捐助內容", 4000, "donationZh");
        AdminInput.OptionalText(request.Content.Zh.Location, "地點", 128, "locationZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.DonationContent))
        {
            AdminInput.OptionalText(request.Content.En.Location, "地點（英文）", 128, "locationEn");
        }
    }

    private async Task EnsureRefsAsync(AdminClubScope scope, UpsertAdminImpactRecordRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Charities.AsNoTracking().AnyAsync(c => c.Id == request.CharityId && (c.ClubId == scope.ClubId || c.ClubId == null), cancellationToken))
        {
            throw new AdminValidationException("找不到指定的公益團體，請重新選擇（公益團體名稱為必填）。", "charityId");
        }

        if (request.CharityProgramId is Guid programId
            && !await dbContext.CharityPrograms.AsNoTracking().AnyAsync(p => p.Id == programId && (p.ClubId == scope.ClubId || p.ClubId == null), cancellationToken))
        {
            throw new AdminValidationException("找不到指定的慈善計畫，請重新選擇。", "programId");
        }
    }

    private static void Apply(ImpactRecord record, UpsertAdminImpactRecordRequest request)
    {
        record.CharityId = request.CharityId;
        record.CharityProgramId = request.CharityProgramId;
        record.HappenedOn = request.HappenedOn;
        record.SortOrder = request.SortOrder;
        record.IsPinned = request.IsPinned;
    }

    private void SetI18n(ImpactRecord record, AdminImpactRecordContentInput content)
    {
        Upsert(record, RequestLocale.DefaultDbLocale, content.Zh);
        var en = record.ImpactRecordsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.DonationContent))
        {
            Upsert(record, "en", content.En);
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
        }
    }

    private void Upsert(ImpactRecord record, string locale, AdminImpactRecordLocaleContent content)
    {
        var row = record.ImpactRecordsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new ImpactRecordsI18n { ImpactRecordId = record.Id, Locale = locale };
            record.ImpactRecordsI18ns.Add(row);
            dbContext.ImpactRecordsI18ns.Add(row);
        }

        row.DonationContent = content.DonationContent.Trim();
        row.Location = string.IsNullOrWhiteSpace(content.Location) ? null : content.Location.Trim();
        row.BriefDescription = string.IsNullOrWhiteSpace(content.BriefDescription) ? null : content.BriefDescription;
    }

    private AdminImpactRecordDetailDto ToDetail(ImpactRecord record)
    {
        var zh = record.ImpactRecordsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = record.ImpactRecordsI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminImpactRecordDetailDto
        {
            Id = record.Id, IsShared = record.ClubId is null, CharityId = record.CharityId,
            CharityNameZh = record.Charity.CharitiesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
            CharityProgramId = record.CharityProgramId,
            ProgramNameZh = record.CharityProgram?.CharityProgramsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
            HappenedOn = record.HappenedOn, SortOrder = record.SortOrder, IsPinned = record.IsPinned,
            ImageKey = record.ImageKey, ImageUrl = imageUrls.Resolve(record.ImageKey), ImageWidth = record.ImageWidth, ImageHeight = record.ImageHeight,
            Zh = new AdminImpactRecordLocaleContent { DonationContent = zh?.DonationContent ?? "", Location = zh?.Location, BriefDescription = zh?.BriefDescription },
            En = en is null ? null : new AdminImpactRecordLocaleContent { DonationContent = en.DonationContent ?? "", Location = en.Location, BriefDescription = en.BriefDescription },
            Images = record.ImpactRecordImages.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).Select(i => new AdminGalleryImageDto
            {
                Id = i.Id, ImageKey = i.ImageKey, ImageUrl = imageUrls.Resolve(i.ImageKey),
                ThumbUrl = imageUrls.Resolve(ImageObjectKey.ForThumbnail(i.ImageKey)), SortOrder = i.SortOrder,
            }).ToList(),
            CreatedAt = record.CreatedAt, UpdatedAt = record.UpdatedAt,
        };
    }
}
