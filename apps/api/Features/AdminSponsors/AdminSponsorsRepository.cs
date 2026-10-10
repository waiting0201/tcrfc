using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSponsors;

/// <summary>
/// E2 贊助商（主站規劃書 §4.5 E2，產出前台 09.2 贊助商）。<c>sponsors.club_id</c> 必填，無共同列。
/// 「贊助故事」＝ <c>sponsor_articles</c>（贊助商 ↔ 文章），文章必須是本俱樂部或共同的文章。
/// 贊助商聯絡窗口（姓名／電話／Email）是商務資料，只出現在後台，公開端點不輸出（見 <c>Features/Sponsors</c>）。
/// </summary>
public sealed class AdminSponsorsRepository(ClubDbContext dbContext, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    public const string CacheEntity = "sponsors";
    private static readonly HashSet<string> Tiers = new(StringComparer.Ordinal) { "主贊助", "官方贊助", "支持夥伴" };
    private static readonly HashSet<string> ContractFilters = new(StringComparer.Ordinal) { "alert", "expired", "active" };

    public async Task<IReadOnlyList<AdminSponsorListItemDto>> ListAsync(
        AdminClubScope scope, string? tier, string? contractStatus, string? keyword, CancellationToken cancellationToken)
    {
        if (contractStatus is not null && !ContractFilters.Contains(contractStatus))
        {
            throw new AdminValidationException("合約狀態篩選只能是「即將到期提醒」「已到期」或「進行中」。");
        }

        var query = dbContext.Sponsors.AsNoTracking().Where(s => s.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(tier))
        {
            query = query.Where(s => s.Tier == tier);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(s => s.Slug.Contains(k) || s.SponsorsI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        var rows = await query.OrderBy(s => s.SortOrder).ThenBy(s => s.RowSeq)
            .Select(s => new
            {
                s.Id, s.Slug, s.Tier, s.ContractStartOn, s.ContractEndOn, s.ExpiryAlertOn, s.ContactName, s.ContactPhone,
                s.ContactEmail, s.SortOrder, s.LogoDarkKey, s.LogoLightKey, s.LogoDarkWidth, s.LogoDarkHeight, s.LogoLightWidth, s.LogoLightHeight, s.UpdatedAt,
                NameZh = s.SponsorsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = s.SponsorsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                PackageCount = s.SponsorPackages.Count,
                ActivationCount = s.SponsorActivations.Count,
            }).ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = rows.Select(r => new AdminSponsorListItemDto
        {
            Id = r.Id,
            Slug = r.Slug,
            Tier = r.Tier,
            ContractStartOn = r.ContractStartOn,
            ContractEndOn = r.ContractEndOn,
            ExpiryAlertOn = r.ExpiryAlertOn,
            ContractStatus = ComputeContractStatus(r.ContractEndOn, r.ExpiryAlertOn, today),
            ContactName = r.ContactName,
            ContactPhone = r.ContactPhone,
            ContactEmail = r.ContactEmail,
            SortOrder = r.SortOrder,
            LogoDarkKey = r.LogoDarkKey,
            LogoDarkUrl = imageUrls.Resolve(r.LogoDarkKey),
            LogoDarkThumbUrl = Thumb(r.LogoDarkKey),
            LogoLightKey = r.LogoLightKey,
            LogoLightUrl = imageUrls.Resolve(r.LogoLightKey),
            LogoLightThumbUrl = Thumb(r.LogoLightKey),
            LogoDarkWidth = r.LogoDarkKey is null ? null : r.LogoDarkWidth,
            LogoDarkHeight = r.LogoDarkKey is null ? null : r.LogoDarkHeight,
            LogoLightWidth = r.LogoLightKey is null ? null : r.LogoLightWidth,
            LogoLightHeight = r.LogoLightKey is null ? null : r.LogoLightHeight,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            PackageCount = r.PackageCount,
            ActivationCount = r.ActivationCount,
            UpdatedAt = r.UpdatedAt,
        });

        if (contractStatus is not null)
        {
            items = items.Where(i => contractStatus == "active" ? i.ContractStatus is "active" or "alert" : i.ContractStatus == contractStatus);
        }

        return items.ToList();
    }

    public async Task<AdminSponsorDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var sponsor = await dbContext.Sponsors.AsNoTracking()
            .Include(s => s.SponsorsI18ns)
            .Include(s => s.SponsorPackages).ThenInclude(p => p.SponsorPackagesI18ns)
            .Include(s => s.SponsorArticles).ThenInclude(a => a.Article).ThenInclude(a => a.ArticlesI18ns)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);
        return sponsor is null ? null : ToDetail(sponsor);
    }

    public async Task<AdminSponsorDetailDto> CreateAsync(
        AdminClubScope scope, Guid id, UpsertAdminSponsorRequest request,
        UploadedImageInfo? logoDark, UploadedImageInfo? logoLight, Guid? operatorId, CancellationToken cancellationToken)
    {
        var slug = ValidateAndSlug(request);
        slug ??= AdminInput.GenerateSlug("sponsor", request.Content.En?.Name);
        await EnsureSlugFreeAsync(scope, slug, null, cancellationToken);
        var packages = await ResolvePackagesAsync(scope, request.PackageIds ?? [], cancellationToken);
        var articles = await ResolveArticlesAsync(scope, request.ArticleIds ?? [], cancellationToken);

        var now = DateTime.UtcNow;
        var sponsor = new Sponsor
        {
            Id = id, ClubId = scope.ClubId, Slug = slug, CreatedAt = now, UpdatedAt = now,
            CreatedBy = operatorId, UpdatedBy = operatorId,
            LogoDarkKey = logoDark?.Key, LogoLightKey = logoLight?.Key,
            LogoDarkWidth = logoDark?.Width, LogoDarkHeight = logoDark?.Height, LogoLightWidth = logoLight?.Width, LogoLightHeight = logoLight?.Height,
        };
        Apply(sponsor, request);
        dbContext.Sponsors.Add(sponsor);
        SetI18n(sponsor, request.Content);
        foreach (var package in packages)
        {
            sponsor.SponsorPackages.Add(package);
        }

        SetArticles(sponsor, articles);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return (await GetByIdAsync(scope, id, cancellationToken))!;
    }

    public async Task<AdminSponsorDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminSponsorRequest request,
        ImageFieldUpdate logoDark, ImageFieldUpdate logoLight, OrphanedObjects orphans, Guid? operatorId, CancellationToken cancellationToken)
    {
        var newSlug = ValidateAndSlug(request);
        var sponsor = await dbContext.Sponsors
            .Include(s => s.SponsorsI18ns).Include(s => s.SponsorPackages).Include(s => s.SponsorArticles)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);
        if (sponsor is null)
        {
            return null;
        }

        if (newSlug is not null && !string.Equals(newSlug, sponsor.Slug, StringComparison.Ordinal))
        {
            await EnsureSlugFreeAsync(scope, newSlug, id, cancellationToken);
            sponsor.Slug = newSlug;
        }

        if (logoDark.Change)
        {
            orphans.Image(sponsor.LogoDarkKey);
            sponsor.LogoDarkKey = logoDark.Key;
            sponsor.LogoDarkWidth = logoDark.Key is null ? null : logoDark.Width;
            sponsor.LogoDarkHeight = logoDark.Key is null ? null : logoDark.Height;
        }

        if (logoLight.Change)
        {
            orphans.Image(sponsor.LogoLightKey);
            sponsor.LogoLightKey = logoLight.Key;
            sponsor.LogoLightWidth = logoLight.Key is null ? null : logoLight.Width;
            sponsor.LogoLightHeight = logoLight.Key is null ? null : logoLight.Height;
        }

        Apply(sponsor, request);
        sponsor.UpdatedAt = DateTime.UtcNow;
        sponsor.UpdatedBy = operatorId;
        SetI18n(sponsor, request.Content);

        if (request.PackageIds is not null)
        {
            var packages = await ResolvePackagesAsync(scope, request.PackageIds, cancellationToken);
            sponsor.SponsorPackages.Clear();
            foreach (var package in packages)
            {
                sponsor.SponsorPackages.Add(package);
            }
        }

        if (request.ArticleIds is not null)
        {
            var articles = await ResolveArticlesAsync(scope, request.ArticleIds, cancellationToken);
            dbContext.SponsorArticles.RemoveRange(sponsor.SponsorArticles);
            sponsor.SponsorArticles.Clear();
            SetArticles(sponsor, articles);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var sponsor = await dbContext.Sponsors.FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);
        if (sponsor is null)
        {
            return false;
        }

        // 贊助活動的圖集圖片要一併刪物件（資料列由 ON DELETE CASCADE 清）。
        var activationImageKeys = await dbContext.SponsorActivationImages
            .Where(i => i.SponsorActivation.SponsorId == id).Select(i => i.ImageKey).ToListAsync(cancellationToken);
        foreach (var key in activationImageKeys)
        {
            orphans.Image(key);
        }

        orphans.Image(sponsor.LogoDarkKey);
        orphans.Image(sponsor.LogoLightKey);
        dbContext.Sponsors.Remove(sponsor);
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

        var sponsors = await dbContext.Sponsors.Where(s => s.ClubId == scope.ClubId).OrderBy(s => s.SortOrder).ThenBy(s => s.RowSeq)
            .ToListAsync(cancellationToken);
        var byId = sponsors.ToDictionary(s => s.Id);
        if (ids.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("排序清單含有不存在的贊助商，請重新整理後再試。");
        }

        var order = ids.Concat(sponsors.Select(s => s.Id).Where(i => !ids.Contains(i))).ToList();
        var now = DateTime.UtcNow;
        for (var i = 0; i < order.Count; i++)
        {
            var s = byId[order[i]];
            if (s.SortOrder != i)
            {
                s.SortOrder = i;
                s.UpdatedAt = now;
                s.UpdatedBy = operatorId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
    }

    internal static string ComputeContractStatus(DateOnly? end, DateOnly? alertOn, DateOnly today)
    {
        if (end is null)
        {
            return "none";
        }

        if (end < today)
        {
            return "expired";
        }

        return alertOn is not null && alertOn <= today ? "alert" : "active";
    }

    private static string? ValidateAndSlug(UpsertAdminSponsorRequest request)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : AdminInput.Slug(request.Slug.Trim());
        AdminInput.OneOf(request.Tier, Tiers, "贊助等級", "「主贊助」「官方贊助」或「支持夥伴」", "tier");
        AdminInput.DateRange(request.ContractStartOn, request.ContractEndOn, "合約期間", "contractEndOn");
        if (request.ExpiryAlertOn is not null && request.ContractEndOn is not null && request.ExpiryAlertOn > request.ContractEndOn)
        {
            throw new AdminValidationException("到期提醒日期不可晚於合約結束日期。", "expiryAlertOn");
        }

        AdminInput.OptionalText(request.ContactName, "聯絡人姓名", 64, "contactName");
        AdminInput.OptionalText(request.ContactPhone, "聯絡電話", 32, "contactPhone");
        AdminInput.OptionalEmail(request.ContactEmail, "聯絡 Email", "contactEmail");
        AdminInput.RequireText(request.Content.Zh.Name, "中文名稱", 128, "nameZh");
        AdminInput.OptionalText(request.Content.Zh.LogoAlt, "標誌替代文字（中文）", 200, "logoAltZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文名稱", 128, "nameEn");
            AdminInput.OptionalText(request.Content.En.LogoAlt, "標誌替代文字（英文）", 200, "logoAltEn");
        }

        return slug;
    }

    private static void Apply(Sponsor sponsor, UpsertAdminSponsorRequest request)
    {
        sponsor.Tier = request.Tier;
        sponsor.ContractStartOn = request.ContractStartOn;
        sponsor.ContractEndOn = request.ContractEndOn;
        sponsor.ExpiryAlertOn = request.ExpiryAlertOn;
        sponsor.ContactName = AdminInput.OptionalText(request.ContactName, "聯絡人姓名", 64, "contactName");
        sponsor.ContactPhone = AdminInput.OptionalText(request.ContactPhone, "聯絡電話", 32, "contactPhone");
        sponsor.ContactEmail = AdminInput.OptionalEmail(request.ContactEmail, "聯絡 Email", "contactEmail");
        sponsor.SortOrder = request.SortOrder;
    }

    private void SetI18n(Sponsor sponsor, AdminSponsorContentInput content)
    {
        Upsert(sponsor, RequestLocale.DefaultDbLocale, content.Zh);
        var en = sponsor.SponsorsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(sponsor, "en", content.En);
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
        }
    }

    private void Upsert(Sponsor sponsor, string locale, AdminSponsorLocaleContent content)
    {
        var row = sponsor.SponsorsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new SponsorsI18n { SponsorId = sponsor.Id, Locale = locale };
            sponsor.SponsorsI18ns.Add(row);
            dbContext.SponsorsI18ns.Add(row);
        }

        row.Name = content.Name.Trim();
        row.Content = string.IsNullOrWhiteSpace(content.Content) ? null : content.Content;
        row.LogoAlt = string.IsNullOrWhiteSpace(content.LogoAlt) ? null : content.LogoAlt.Trim();
    }

    private async Task<List<SponsorPackage>> ResolvePackagesAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return [];
        }

        var packages = await dbContext.SponsorPackages.Where(p => p.ClubId == scope.ClubId && distinct.Contains(p.Id)).ToListAsync(cancellationToken);
        if (packages.Count != distinct.Count)
        {
            throw new AdminValidationException("贊助方案清單含有不存在的方案，請重新整理後再試。", "packageIds");
        }

        return packages;
    }

    private async Task<List<Article>> ResolveArticlesAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return [];
        }

        var articles = await dbContext.Articles.AsNoTracking()
            .Where(a => distinct.Contains(a.Id) && (a.ClubId == scope.ClubId || a.ClubId == null)).ToListAsync(cancellationToken);
        if (articles.Count != distinct.Count)
        {
            throw new AdminValidationException("贊助故事清單含有找不到的文章，請重新整理後再試。", "articleIds");
        }

        // 保持呼叫端給的順序。
        return distinct.Select(i => articles.First(a => a.Id == i)).ToList();
    }

    private void SetArticles(Sponsor sponsor, List<Article> articles)
    {
        for (var i = 0; i < articles.Count; i++)
        {
            var link = new SponsorArticle { SponsorId = sponsor.Id, ArticleId = articles[i].Id, SortOrder = i };
            sponsor.SponsorArticles.Add(link);
        }
    }

    private async Task EnsureSlugFreeAsync(AdminClubScope scope, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await dbContext.Sponsors.AsNoTracking().AnyAsync(
                s => s.ClubId == scope.ClubId && s.Slug == slug && s.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經被這個俱樂部的其他贊助商使用，請換一個。", "slug");
        }
    }

    private async Task InvalidateAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        // 贊助商與夥伴頁的「共同參與的公益計畫」有關聯，慈善公開端點也快取了關聯清單。
        await cache.InvalidateAsync("charity", scope.ClubCode, cancellationToken);
    }

    private string? Thumb(string? key) => key is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(key));

    private AdminSponsorDetailDto ToDetail(Sponsor sponsor)
    {
        var zh = sponsor.SponsorsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = sponsor.SponsorsI18ns.FirstOrDefault(i => i.Locale == "en");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new AdminSponsorDetailDto
        {
            Id = sponsor.Id,
            Slug = sponsor.Slug,
            Tier = sponsor.Tier,
            ContractStartOn = sponsor.ContractStartOn,
            ContractEndOn = sponsor.ContractEndOn,
            ExpiryAlertOn = sponsor.ExpiryAlertOn,
            ContractStatus = ComputeContractStatus(sponsor.ContractEndOn, sponsor.ExpiryAlertOn, today),
            ContactName = sponsor.ContactName,
            ContactPhone = sponsor.ContactPhone,
            ContactEmail = sponsor.ContactEmail,
            SortOrder = sponsor.SortOrder,
            LogoDarkKey = sponsor.LogoDarkKey,
            LogoDarkUrl = imageUrls.Resolve(sponsor.LogoDarkKey),
            LogoLightKey = sponsor.LogoLightKey,
            LogoLightUrl = imageUrls.Resolve(sponsor.LogoLightKey),
            LogoDarkWidth = sponsor.LogoDarkKey is null ? null : sponsor.LogoDarkWidth,
            LogoDarkHeight = sponsor.LogoDarkKey is null ? null : sponsor.LogoDarkHeight,
            LogoLightWidth = sponsor.LogoLightKey is null ? null : sponsor.LogoLightWidth,
            LogoLightHeight = sponsor.LogoLightKey is null ? null : sponsor.LogoLightHeight,
            Zh = new AdminSponsorLocaleContent { Name = zh?.Name ?? "", Content = zh?.Content, LogoAlt = zh?.LogoAlt },
            En = en is null ? null : new AdminSponsorLocaleContent { Name = en.Name ?? "", Content = en.Content, LogoAlt = en.LogoAlt },
            Packages = sponsor.SponsorPackages.OrderBy(p => p.SortOrder).ThenBy(p => p.RowSeq).Select(p => new AdminSponsorPackageRefDto
            {
                Id = p.Id,
                Slug = p.Slug,
                NameZh = p.SponsorPackagesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
            }).ToList(),
            Articles = sponsor.SponsorArticles.OrderBy(a => a.SortOrder).Select(a => new AdminSponsorArticleRefDto
            {
                Id = a.ArticleId,
                Slug = a.Article.Slug,
                TitleZh = a.Article.ArticlesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Title,
                Status = a.Article.Status,
            }).ToList(),
            CreatedAt = sponsor.CreatedAt,
            UpdatedAt = sponsor.UpdatedAt,
        };
    }
}
