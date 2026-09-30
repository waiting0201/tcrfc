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
/// B5「公益團體資料」（規劃書 §4.2 B5：名稱、簡介、Logo 或代表圖、官網連結、聯絡窗口、合作紀錄；可重複引用於多筆事蹟）。
/// <c>charities.club_id</c> 可為空（9 張可為空表之一）：共同列在清單與詳情中可見（<c>IsShared=true</c>），
/// 但透過俱樂部範圍端點一律唯讀（<see cref="SharedContentReadOnlyException"/>，docs/14「共同內容只有超管能建立與修改」）。
/// 建立一律歸屬呼叫端當下的俱樂部。「合作紀錄」是唯讀彙整（這個團體受贈的計畫與事蹟），不是另存欄位。
/// </summary>
public sealed class AdminCharityOrgsRepository(ClubDbContext dbContext, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    public const string CacheEntity = "charity";

    public async Task<IReadOnlyList<AdminCharityOrgListItemDto>> ListAsync(AdminClubScope scope, string? keyword, CancellationToken cancellationToken)
    {
        var query = dbContext.Charities.AsNoTracking().Where(c => c.ClubId == scope.ClubId || c.ClubId == null);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(c => c.Slug.Contains(k) || c.CharitiesI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        var rows = await query.OrderBy(c => c.ClubId == null ? 1 : 0).ThenBy(c => c.RowSeq)
            .Select(c => new
            {
                c.Id, c.Slug, IsShared = c.ClubId == null, c.WebsiteUrl, c.ContactName, c.ContactPhone, c.LogoKey, c.UpdatedAt,
                NameZh = c.CharitiesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = c.CharitiesI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                ProgramCount = c.CharityPrograms.Count, RecordCount = c.ImpactRecords.Count,
            }).ToListAsync(cancellationToken);

        return rows.Select(r => new AdminCharityOrgListItemDto
        {
            Id = r.Id, Slug = r.Slug, IsShared = r.IsShared, WebsiteUrl = r.WebsiteUrl, ContactName = r.ContactName,
            ContactPhone = r.ContactPhone, LogoKey = r.LogoKey, LogoUrl = imageUrls.Resolve(r.LogoKey),
            LogoThumbUrl = r.LogoKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(r.LogoKey)),
            NameZh = r.NameZh, NameEn = r.NameEn, ProgramCount = r.ProgramCount, RecordCount = r.RecordCount, UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminCharityOrgDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var charity = await dbContext.Charities.AsNoTracking().Include(c => c.CharitiesI18ns)
            .FirstOrDefaultAsync(c => c.Id == id && (c.ClubId == scope.ClubId || c.ClubId == null), cancellationToken);
        if (charity is null)
        {
            return null;
        }

        var programs = await dbContext.CharityPrograms.AsNoTracking()
            .Where(p => p.CharityId == id && (p.ClubId == scope.ClubId || p.ClubId == null))
            .OrderBy(p => p.SortOrder).ThenByDescending(p => p.RowSeq)
            .Select(p => new AdminCharityOrgProgramRefDto
            {
                Id = p.Id, Status = p.Status,
                NameZh = p.CharityProgramsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        var records = await dbContext.ImpactRecords.AsNoTracking()
            .Where(r => r.CharityId == id && (r.ClubId == scope.ClubId || r.ClubId == null))
            .OrderByDescending(r => r.HappenedOn).ThenByDescending(r => r.RowSeq)
            .Select(r => new AdminCharityOrgRecordRefDto
            {
                Id = r.Id, HappenedOn = r.HappenedOn,
                DonationContentZh = r.ImpactRecordsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.DonationContent).FirstOrDefault(),
            }).ToListAsync(cancellationToken);

        var zh = charity.CharitiesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = charity.CharitiesI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminCharityOrgDetailDto
        {
            Id = charity.Id, Slug = charity.Slug, IsShared = charity.ClubId is null, WebsiteUrl = charity.WebsiteUrl,
            ContactName = charity.ContactName, ContactPhone = charity.ContactPhone, LogoKey = charity.LogoKey,
            LogoUrl = imageUrls.Resolve(charity.LogoKey),
            Zh = new AdminCharityOrgLocaleContent { Name = zh?.Name ?? "", Intro = zh?.Intro },
            En = en is null ? null : new AdminCharityOrgLocaleContent { Name = en.Name ?? "", Intro = en.Intro },
            Programs = programs, Records = records, CreatedAt = charity.CreatedAt, UpdatedAt = charity.UpdatedAt,
        };
    }

    public async Task<AdminCharityOrgDetailDto> CreateAsync(
        AdminClubScope scope, Guid id, UpsertAdminCharityOrgRequest request, UploadedImageInfo? logo, Guid? operatorId, CancellationToken cancellationToken)
    {
        var slug = Validate(request) ?? AdminInput.GenerateSlug("org", request.Content.En?.Name);
        await EnsureSlugFreeAsync(scope, slug, null, cancellationToken);

        var now = DateTime.UtcNow;
        var charity = new Charity
        {
            Id = id, ClubId = scope.ClubId, Slug = slug, LogoKey = logo?.Key, CreatedAt = now, UpdatedAt = now,
            CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        Apply(charity, request);
        dbContext.Charities.Add(charity);
        SetI18n(charity, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return (await GetByIdAsync(scope, id, cancellationToken))!;
    }

    public async Task<AdminCharityOrgDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminCharityOrgRequest request, ImageFieldUpdate logo, OrphanedObjects orphans,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        var newSlug = Validate(request);
        var charity = await dbContext.Charities.Include(c => c.CharitiesI18ns)
            .FirstOrDefaultAsync(c => c.Id == id && (c.ClubId == scope.ClubId || c.ClubId == null), cancellationToken);
        if (charity is null)
        {
            return null;
        }

        if (charity.ClubId is null)
        {
            throw new SharedContentReadOnlyException("公益團體資料");
        }

        if (newSlug is not null && !string.Equals(newSlug, charity.Slug, StringComparison.Ordinal))
        {
            await EnsureSlugFreeAsync(scope, newSlug, id, cancellationToken);
            charity.Slug = newSlug;
        }

        if (logo.Change)
        {
            orphans.Image(charity.LogoKey);
            charity.LogoKey = logo.Key;
        }

        Apply(charity, request);
        charity.UpdatedAt = DateTime.UtcNow;
        charity.UpdatedBy = operatorId;
        SetI18n(charity, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var charity = await dbContext.Charities
            .FirstOrDefaultAsync(c => c.Id == id && (c.ClubId == scope.ClubId || c.ClubId == null), cancellationToken);
        if (charity is null)
        {
            return false;
        }

        if (charity.ClubId is null)
        {
            throw new SharedContentReadOnlyException("公益團體資料");
        }

        var programCount = await dbContext.CharityPrograms.CountAsync(p => p.CharityId == id, cancellationToken);
        var recordCount = await dbContext.ImpactRecords.CountAsync(r => r.CharityId == id, cancellationToken);
        if (programCount + recordCount > 0)
        {
            throw new AdminConflictException(
                "團體仍被引用",
                $"這個公益團體仍被 {programCount} 個慈善計畫與 {recordCount} 筆事蹟紀錄使用，請先移除或改選其他團體後再刪除。");
        }

        orphans.Image(charity.LogoKey);
        dbContext.Charities.Remove(charity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return true;
    }

    private static string? Validate(UpsertAdminCharityOrgRequest request)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : AdminInput.Slug(request.Slug.Trim());
        AdminInput.OptionalHttpUrl(request.WebsiteUrl, "官網連結");
        AdminInput.OptionalText(request.ContactName, "聯絡窗口姓名", 64);
        AdminInput.OptionalText(request.ContactPhone, "聯絡窗口電話", 32);
        AdminInput.RequireText(request.Content.Zh.Name, "中文團體名稱", 128);
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文團體名稱", 128);
        }

        return slug;
    }

    private static void Apply(Charity charity, UpsertAdminCharityOrgRequest request)
    {
        charity.WebsiteUrl = AdminInput.OptionalHttpUrl(request.WebsiteUrl, "官網連結");
        charity.ContactName = AdminInput.OptionalText(request.ContactName, "聯絡窗口姓名", 64);
        charity.ContactPhone = AdminInput.OptionalText(request.ContactPhone, "聯絡窗口電話", 32);
    }

    private void SetI18n(Charity charity, AdminCharityOrgContentInput content)
    {
        Upsert(charity, RequestLocale.DefaultDbLocale, content.Zh);
        var en = charity.CharitiesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(charity, "en", content.En);
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
        }
    }

    private void Upsert(Charity charity, string locale, AdminCharityOrgLocaleContent content)
    {
        var row = charity.CharitiesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new CharitiesI18n { CharityId = charity.Id, Locale = locale };
            charity.CharitiesI18ns.Add(row);
            dbContext.CharitiesI18ns.Add(row);
        }

        row.Name = content.Name.Trim();
        row.Intro = string.IsNullOrWhiteSpace(content.Intro) ? null : content.Intro;
    }

    private async Task EnsureSlugFreeAsync(AdminClubScope scope, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await dbContext.Charities.AsNoTracking().AnyAsync(c => c.ClubId == scope.ClubId && c.Slug == slug && c.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經被這個俱樂部的其他公益團體使用，請換一個。");
        }
    }
}
