using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSponsors;

/// <summary>
/// E2「贊助方案管理（9.4）」（規劃書 §4.5 E2）：方案內容、權益清單、價格區間（可設定不公開）、排序，產出前台 09.4
/// 「9 種贊助方案卡片」。九種方案是規劃書列的**內容**，不是資料庫約束——不硬性限制筆數，後台自行維護。
/// <c>sponsor_packages.club_id</c> 必填，無共同列。價格區間不公開（<c>is_price_public = 0</c>）時，公開端點
/// 完全不輸出價格（<c>Features/Sponsors</c>）。
/// </summary>
public sealed class AdminSponsorPackagesRepository(ClubDbContext dbContext, IQueryCache cache)
{
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "draft", "published" };

    public async Task<IReadOnlyList<AdminSponsorPackageListItemDto>> ListAsync(AdminClubScope scope, string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.SponsorPackages.AsNoTracking().Where(p => p.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, Statuses, "狀態", "「draft」或「published」");
            query = query.Where(p => p.Status == status);
        }

        var rows = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.RowSeq)
            .Select(p => new
            {
                p.Id, p.Slug, p.PriceMin, p.PriceMax, p.IsPricePublic, p.SortOrder, p.Status, p.UpdatedAt,
                NameZh = p.SponsorPackagesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = p.SponsorPackagesI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                SponsorCount = p.Sponsors.Count,
            }).ToListAsync(cancellationToken);

        return rows.Select(r => new AdminSponsorPackageListItemDto
        {
            Id = r.Id, Slug = r.Slug, PriceMin = r.PriceMin, PriceMax = r.PriceMax, IsPricePublic = r.IsPricePublic,
            SortOrder = r.SortOrder, Status = r.Status, NameZh = r.NameZh, NameEn = r.NameEn,
            SponsorCount = r.SponsorCount, UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminSponsorPackageDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var package = await dbContext.SponsorPackages.AsNoTracking().Include(p => p.SponsorPackagesI18ns)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        return package is null ? null : ToDetail(package);
    }

    public async Task<AdminSponsorPackageDetailDto> CreateAsync(
        AdminClubScope scope, UpsertAdminSponsorPackageRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var slug = Validate(request) ?? AdminInput.GenerateSlug("package", request.Content.En?.Name);
        await EnsureSlugFreeAsync(scope, slug, null, cancellationToken);

        var now = DateTime.UtcNow;
        var package = new SponsorPackage
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, Slug = slug, CreatedAt = now, UpdatedAt = now,
            CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        Apply(package, request);
        dbContext.SponsorPackages.Add(package);
        SetI18n(package, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return (await GetByIdAsync(scope, package.Id, cancellationToken))!;
    }

    public async Task<AdminSponsorPackageDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminSponsorPackageRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var newSlug = Validate(request);
        var package = await dbContext.SponsorPackages.Include(p => p.SponsorPackagesI18ns)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (package is null)
        {
            return null;
        }

        if (newSlug is not null && !string.Equals(newSlug, package.Slug, StringComparison.Ordinal))
        {
            await EnsureSlugFreeAsync(scope, newSlug, id, cancellationToken);
            package.Slug = newSlug;
        }

        Apply(package, request);
        package.UpdatedAt = DateTime.UtcNow;
        package.UpdatedBy = operatorId;
        SetI18n(package, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var package = await dbContext.SponsorPackages.FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (package is null)
        {
            return false;
        }

        // sponsor_package_links 與側表由 ON DELETE CASCADE 清掉。
        dbContext.SponsorPackages.Remove(package);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return true;
    }

    public async Task ReorderAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count)
        {
            throw new AdminValidationException("排序清單不可為空，也不可重複。");
        }

        var packages = await dbContext.SponsorPackages.Where(p => p.ClubId == scope.ClubId).OrderBy(p => p.SortOrder).ThenBy(p => p.RowSeq)
            .ToListAsync(cancellationToken);
        var byId = packages.ToDictionary(p => p.Id);
        if (ids.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("排序清單含有不存在的贊助方案，請重新整理後再試。");
        }

        var order = ids.Concat(packages.Select(p => p.Id).Where(i => !ids.Contains(i))).ToList();
        var now = DateTime.UtcNow;
        for (var i = 0; i < order.Count; i++)
        {
            var p = byId[order[i]];
            if (p.SortOrder != i)
            {
                p.SortOrder = i;
                p.UpdatedAt = now;
                p.UpdatedBy = operatorId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
    }

    private static string? Validate(UpsertAdminSponsorPackageRequest request)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : AdminInput.Slug(request.Slug.Trim());
        AdminInput.OneOf(request.Status, Statuses, "狀態", "「draft」（不顯示）或「published」（顯示）");
        AdminInput.OptionalNonNegative(request.PriceMin, "價格下限");
        AdminInput.OptionalNonNegative(request.PriceMax, "價格上限");
        if (request.PriceMin is not null && request.PriceMax is not null && request.PriceMax < request.PriceMin)
        {
            throw new AdminValidationException("價格上限不可低於價格下限。");
        }

        AdminInput.RequireText(request.Content.Zh.Name, "中文名稱", 128);
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文名稱", 128);
        }

        AdminInput.OptionalText(request.Content.Zh.Audience, "適合對象", 128);
        AdminInput.OptionalText(request.Content.En?.Audience, "適合對象（英文）", 128);
        return slug;
    }

    private static void Apply(SponsorPackage package, UpsertAdminSponsorPackageRequest request)
    {
        package.PriceMin = request.PriceMin;
        package.PriceMax = request.PriceMax;
        package.IsPricePublic = request.IsPricePublic;
        package.SortOrder = request.SortOrder;
        package.Status = request.Status;
    }

    private void SetI18n(SponsorPackage package, AdminSponsorPackageContentInput content)
    {
        Upsert(package, RequestLocale.DefaultDbLocale, content.Zh);
        var en = package.SponsorPackagesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(package, "en", content.En);
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
        }
    }

    private void Upsert(SponsorPackage package, string locale, AdminSponsorPackageLocaleContent content)
    {
        var row = package.SponsorPackagesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new SponsorPackagesI18n { SponsorPackageId = package.Id, Locale = locale };
            package.SponsorPackagesI18ns.Add(row);
            dbContext.SponsorPackagesI18ns.Add(row);
        }

        row.Name = content.Name.Trim();
        row.Content = string.IsNullOrWhiteSpace(content.Content) ? null : content.Content;
        row.BenefitList = string.IsNullOrWhiteSpace(content.BenefitList) ? null : content.BenefitList;
        row.Audience = string.IsNullOrWhiteSpace(content.Audience) ? null : content.Audience.Trim();
    }

    private async Task EnsureSlugFreeAsync(AdminClubScope scope, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await dbContext.SponsorPackages.AsNoTracking().AnyAsync(
                p => p.ClubId == scope.ClubId && p.Slug == slug && p.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經被這個俱樂部的其他贊助方案使用，請換一個。");
        }
    }

    private Task InvalidateAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => cache.InvalidateAsync(AdminSponsorsRepository.CacheEntity, scope.ClubCode, cancellationToken);

    private static AdminSponsorPackageDetailDto ToDetail(SponsorPackage package)
    {
        var zh = package.SponsorPackagesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = package.SponsorPackagesI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminSponsorPackageDetailDto
        {
            Id = package.Id, Slug = package.Slug, PriceMin = package.PriceMin, PriceMax = package.PriceMax,
            IsPricePublic = package.IsPricePublic, SortOrder = package.SortOrder, Status = package.Status,
            Zh = new AdminSponsorPackageLocaleContent { Name = zh?.Name ?? "", Content = zh?.Content, BenefitList = zh?.BenefitList, Audience = zh?.Audience },
            En = en is null ? null : new AdminSponsorPackageLocaleContent { Name = en.Name ?? "", Content = en.Content, BenefitList = en.BenefitList, Audience = en.Audience },
            CreatedAt = package.CreatedAt, UpdatedAt = package.UpdatedAt,
        };
    }
}
