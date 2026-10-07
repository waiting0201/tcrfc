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
/// B5「慈善計畫」（規劃書 §4.2 B5、前台 11.2）：名稱、封面、對象、期間、狀態、緣起與內容（區塊編輯）、受贈公益團體、
/// 捐助內容、圖集、贊助夥伴（E1／E2）、關聯報導（7.7）。<c>charity_programs.club_id</c> 可為空：共同列只讀。
/// 「進行中／已完成」是顯示狀態，由期間推導（沒填結束日或結束日尚未到＝進行中）；資料庫的 <c>status</c> 是
/// 發布狀態（草稿／已發布）。列表排序＝置頂優先、再依排序值、再依開始日（新到舊）。
/// </summary>
public sealed class AdminCharityProgramsRepository(ClubDbContext dbContext, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "draft", "published" };

    public async Task<PagedResult<AdminCharityProgramListItemDto>> ListAsync(
        AdminClubScope scope, string? status, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.CharityPrograms.AsNoTracking().Where(p => p.ClubId == scope.ClubId || p.ClubId == null);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, Statuses, "狀態", "「草稿」或「發布」");
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(p => p.Slug.Contains(k) || p.CharityProgramsI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(p => p.IsPinned).ThenBy(p => p.SortOrder).ThenByDescending(p => p.StartOn).ThenByDescending(p => p.RowSeq)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Slug, IsShared = p.ClubId == null, p.Status, p.StartOn, p.EndOn, p.SortOrder, p.IsPinned, p.CharityId, p.CoverKey, p.UpdatedAt,
                NameZh = p.CharityProgramsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = p.CharityProgramsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                CharityNameZh = p.Charity.CharitiesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            }).ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = rows.Select(r => new AdminCharityProgramListItemDto
        {
            Id = r.Id, Slug = r.Slug, IsShared = r.IsShared, Status = r.Status, Progress = ComputeProgress(r.EndOn, today),
            StartOn = r.StartOn, EndOn = r.EndOn, SortOrder = r.SortOrder, IsPinned = r.IsPinned, CharityId = r.CharityId,
            CharityNameZh = r.CharityNameZh, CoverKey = r.CoverKey, CoverUrl = imageUrls.Resolve(r.CoverKey),
            CoverThumbUrl = r.CoverKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(r.CoverKey)),
            NameZh = r.NameZh, NameEn = r.NameEn, UpdatedAt = r.UpdatedAt,
        }).ToList();
        return new PagedResult<AdminCharityProgramListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<AdminCharityProgramDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var program = await dbContext.CharityPrograms.AsNoTracking()
            .Include(p => p.CharityProgramsI18ns).Include(p => p.CharityProgramImages)
            .Include(p => p.Partners).ThenInclude(x => x.PartnersI18ns)
            .Include(p => p.Sponsors).ThenInclude(x => x.SponsorsI18ns)
            .Include(p => p.CharityProgramArticles).ThenInclude(a => a.Article).ThenInclude(a => a.ArticlesI18ns)
            .Include(p => p.Charity).ThenInclude(c => c.CharitiesI18ns)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && (p.ClubId == scope.ClubId || p.ClubId == null), cancellationToken);
        return program is null ? null : ToDetail(program);
    }

    public async Task<AdminCharityProgramDetailDto> CreateAsync(
        AdminClubScope scope, Guid id, UpsertAdminCharityProgramRequest request, UploadedImageInfo? cover, Guid? operatorId, CancellationToken cancellationToken)
    {
        var slug = Validate(request) ?? AdminInput.GenerateSlug("program", request.Content.En?.Name);
        await EnsureSlugFreeAsync(scope, slug, null, cancellationToken);
        await EnsureCharityAsync(scope, request.CharityId, cancellationToken);
        var partners = await ResolvePartnersAsync(scope, request.PartnerIds ?? [], cancellationToken);
        var sponsors = await ResolveSponsorsAsync(scope, request.SponsorIds ?? [], cancellationToken);
        var articles = await ResolveArticlesAsync(scope, request.ArticleIds ?? [], cancellationToken);

        var now = DateTime.UtcNow;
        var program = new CharityProgram
        {
            Id = id, ClubId = scope.ClubId, Slug = slug, CoverKey = cover?.Key, CreatedAt = now, UpdatedAt = now,
            CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        Apply(program, request);
        dbContext.CharityPrograms.Add(program);
        SetI18n(program, request.Content);
        foreach (var p in partners) { program.Partners.Add(p); }
        foreach (var s in sponsors) { program.Sponsors.Add(s); }
        SetArticles(program, articles);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return (await GetByIdAsync(scope, id, cancellationToken))!;
    }

    public async Task<AdminCharityProgramDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminCharityProgramRequest request, ImageFieldUpdate cover, OrphanedObjects orphans,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        var newSlug = Validate(request);
        var program = await dbContext.CharityPrograms
            .Include(p => p.CharityProgramsI18ns).Include(p => p.Partners).Include(p => p.Sponsors).Include(p => p.CharityProgramArticles)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && (p.ClubId == scope.ClubId || p.ClubId == null), cancellationToken);
        if (program is null)
        {
            return null;
        }

        if (program.ClubId is null)
        {
            throw new SharedContentReadOnlyException("慈善計畫");
        }

        if (program.CharityId != request.CharityId)
        {
            await EnsureCharityAsync(scope, request.CharityId, cancellationToken);
        }

        if (newSlug is not null && !string.Equals(newSlug, program.Slug, StringComparison.Ordinal))
        {
            await EnsureSlugFreeAsync(scope, newSlug, id, cancellationToken);
            program.Slug = newSlug;
        }

        if (cover.Change)
        {
            orphans.Image(program.CoverKey);
            program.CoverKey = cover.Key;
        }

        Apply(program, request);
        program.UpdatedAt = DateTime.UtcNow;
        program.UpdatedBy = operatorId;
        SetI18n(program, request.Content);

        if (request.PartnerIds is not null)
        {
            var partners = await ResolvePartnersAsync(scope, request.PartnerIds, cancellationToken);
            program.Partners.Clear();
            foreach (var p in partners) { program.Partners.Add(p); }
        }

        if (request.SponsorIds is not null)
        {
            var sponsors = await ResolveSponsorsAsync(scope, request.SponsorIds, cancellationToken);
            program.Sponsors.Clear();
            foreach (var s in sponsors) { program.Sponsors.Add(s); }
        }

        if (request.ArticleIds is not null)
        {
            var articles = await ResolveArticlesAsync(scope, request.ArticleIds, cancellationToken);
            dbContext.CharityProgramArticles.RemoveRange(program.CharityProgramArticles);
            program.CharityProgramArticles.Clear();
            SetArticles(program, articles);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var program = await dbContext.CharityPrograms.Include(p => p.CharityProgramImages)
            .FirstOrDefaultAsync(p => p.Id == id && (p.ClubId == scope.ClubId || p.ClubId == null), cancellationToken);
        if (program is null)
        {
            return false;
        }

        if (program.ClubId is null)
        {
            throw new SharedContentReadOnlyException("慈善計畫");
        }

        var recordCount = await dbContext.ImpactRecords.CountAsync(r => r.CharityProgramId == id, cancellationToken);
        var metricCount = await dbContext.ImpactMetrics.CountAsync(m => m.CharityProgramId == id, cancellationToken);
        if (recordCount + metricCount > 0)
        {
            throw new AdminConflictException(
                "計畫仍被引用",
                $"這個慈善計畫仍被 {recordCount} 筆事蹟紀錄與 {metricCount} 個影響力數據使用，請先移除關聯後再刪除。");
        }

        orphans.Image(program.CoverKey);
        foreach (var image in program.CharityProgramImages)
        {
            orphans.Image(image.ImageKey);
        }

        dbContext.CharityPrograms.Remove(program);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return true;
    }

    // ── 圖集 ─────────────────────────────────────────────────────────

    public async Task<AdminCharityProgramDetailDto?> AddImageAsync(
        AdminClubScope scope, Guid id, UploadedImageInfo image, Guid? operatorId, CancellationToken cancellationToken)
    {
        var program = await LoadOwnForGalleryAsync(scope, id, cancellationToken);
        if (program is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        dbContext.CharityProgramImages.Add(new CharityProgramImage
        {
            Id = Guid.NewGuid(), CharityProgramId = id, ImageKey = image.Key, CreatedAt = now, UpdatedAt = now,
            SortOrder = program.CharityProgramImages.Count == 0 ? 0 : program.CharityProgramImages.Max(i => i.SortOrder) + 1,
            CreatedBy = operatorId, UpdatedBy = operatorId,
        });
        program.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteImageAsync(AdminClubScope scope, Guid id, Guid imageId, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var program = await LoadOwnForGalleryAsync(scope, id, cancellationToken);
        var image = program?.CharityProgramImages.FirstOrDefault(i => i.Id == imageId);
        if (image is null)
        {
            return false;
        }

        orphans.Image(image.ImageKey);
        dbContext.CharityProgramImages.Remove(image);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return true;
    }

    public async Task<AdminCharityProgramDetailDto?> ReorderImagesAsync(
        AdminClubScope scope, Guid id, IReadOnlyList<Guid> imageIds, CancellationToken cancellationToken)
    {
        var program = await LoadOwnForGalleryAsync(scope, id, cancellationToken);
        if (program is null)
        {
            return null;
        }

        var byId = program.CharityProgramImages.ToDictionary(i => i.Id);
        if (imageIds.Distinct().Count() != imageIds.Count || imageIds.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("圖片排序清單含有不存在或重複的圖片，請重新整理後再試。");
        }

        var order = imageIds.Concat(program.CharityProgramImages.OrderBy(i => i.SortOrder).Select(i => i.Id).Where(i => !imageIds.Contains(i))).ToList();
        for (var i = 0; i < order.Count; i++)
        {
            byId[order[i]].SortOrder = i;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>圖集寫入：計畫必須是本俱樂部自己的（共同計畫拋 403，找不到回傳 <c>null</c>）。</summary>
    private async Task<CharityProgram?> LoadOwnForGalleryAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var program = await dbContext.CharityPrograms.Include(p => p.CharityProgramImages)
            .FirstOrDefaultAsync(p => p.Id == id && (p.ClubId == scope.ClubId || p.ClubId == null), cancellationToken);
        if (program is not null && program.ClubId is null)
        {
            throw new SharedContentReadOnlyException("慈善計畫");
        }

        return program;
    }

    /// <summary>夥伴頁與贊助商頁帶了「共同參與的公益計畫」，計畫異動時三邊的公開快取都要失效。</summary>
    private async Task InvalidateAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        await cache.InvalidateAsync(AdminCharityOrgsRepository.CacheEntity, scope.ClubCode, cancellationToken);
        await cache.InvalidateAsync("partners", scope.ClubCode, cancellationToken);
        await cache.InvalidateAsync("sponsors", scope.ClubCode, cancellationToken);
    }

    internal static string ComputeProgress(DateOnly? endOn, DateOnly today) => endOn is not null && endOn < today ? "completed" : "ongoing";

    private static string? Validate(UpsertAdminCharityProgramRequest request)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : AdminInput.Slug(request.Slug.Trim());
        AdminInput.OneOf(request.Status, Statuses, "狀態", "「草稿」或「發布」", "status");
        AdminInput.DateRange(request.StartOn, request.EndOn, "計畫期間", "endOn");
        AdminInput.RequireText(request.Content.Zh.Name, "中文計畫名稱", 128, "nameZh");
        AdminInput.OptionalText(request.Content.Zh.TargetAudience, "計畫對象", 200, "audienceZh");
        AdminInput.OptionalJson(request.Content.Zh.Content, "中文緣起與內容", "contentZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文計畫名稱", 128, "nameEn");
            AdminInput.OptionalText(request.Content.En.TargetAudience, "計畫對象（英文）", 200, "audienceEn");
            AdminInput.OptionalJson(request.Content.En.Content, "英文緣起與內容", "contentEn");
        }

        return slug;
    }

    private static void Apply(CharityProgram program, UpsertAdminCharityProgramRequest request)
    {
        program.CharityId = request.CharityId;
        program.StartOn = request.StartOn;
        program.EndOn = request.EndOn;
        program.Status = request.Status;
        program.SortOrder = request.SortOrder;
        program.IsPinned = request.IsPinned;
    }

    private void SetI18n(CharityProgram program, AdminCharityProgramContentInput content)
    {
        Upsert(program, RequestLocale.DefaultDbLocale, content.Zh);
        var en = program.CharityProgramsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(program, "en", content.En);
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
        }
    }

    private void Upsert(CharityProgram program, string locale, AdminCharityProgramLocaleContent content)
    {
        var row = program.CharityProgramsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new CharityProgramsI18n { CharityProgramId = program.Id, Locale = locale };
            program.CharityProgramsI18ns.Add(row);
            dbContext.CharityProgramsI18ns.Add(row);
        }

        row.Name = content.Name.Trim();
        row.TargetAudience = string.IsNullOrWhiteSpace(content.TargetAudience) ? null : content.TargetAudience.Trim();
        row.Content = string.IsNullOrWhiteSpace(content.Content) ? null : content.Content;
        row.DonationContent = string.IsNullOrWhiteSpace(content.DonationContent) ? null : content.DonationContent;
    }

    private async Task EnsureCharityAsync(AdminClubScope scope, Guid charityId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Charities.AsNoTracking().AnyAsync(c => c.Id == charityId && (c.ClubId == scope.ClubId || c.ClubId == null), cancellationToken))
        {
            throw new AdminValidationException("找不到指定的受贈公益團體，請重新選擇。", "charityId");
        }
    }

    private async Task<List<Partner>> ResolvePartnersAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.Partners.Where(p => p.ClubId == scope.ClubId && distinct.Contains(p.Id)).ToListAsync(cancellationToken);
        return rows.Count == distinct.Count ? rows : throw new AdminValidationException("贊助夥伴清單含有不存在的夥伴，請重新整理後再試。", "partnerIds");
    }

    private async Task<List<Sponsor>> ResolveSponsorsAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.Sponsors.Where(s => s.ClubId == scope.ClubId && distinct.Contains(s.Id)).ToListAsync(cancellationToken);
        return rows.Count == distinct.Count ? rows : throw new AdminValidationException("贊助商清單含有不存在的贊助商，請重新整理後再試。", "sponsorIds");
    }

    private async Task<List<Article>> ResolveArticlesAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.Articles.AsNoTracking()
            .Where(a => distinct.Contains(a.Id) && (a.ClubId == scope.ClubId || a.ClubId == null)).ToListAsync(cancellationToken);
        if (rows.Count != distinct.Count)
        {
            throw new AdminValidationException("關聯報導清單含有找不到的文章，請重新整理後再試。", "articleIds");
        }

        return distinct.Select(i => rows.First(a => a.Id == i)).ToList();
    }

    private static void SetArticles(CharityProgram program, List<Article> articles)
    {
        for (var i = 0; i < articles.Count; i++)
        {
            program.CharityProgramArticles.Add(new CharityProgramArticle { CharityProgramId = program.Id, ArticleId = articles[i].Id, SortOrder = i });
        }
    }

    private async Task EnsureSlugFreeAsync(AdminClubScope scope, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await dbContext.CharityPrograms.AsNoTracking().AnyAsync(p => p.ClubId == scope.ClubId && p.Slug == slug && p.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經被這個俱樂部的其他慈善計畫使用，請換一個。", "slug");
        }
    }

    private AdminCharityProgramDetailDto ToDetail(CharityProgram program)
    {
        var zh = program.CharityProgramsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = program.CharityProgramsI18ns.FirstOrDefault(i => i.Locale == "en");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new AdminCharityProgramDetailDto
        {
            Id = program.Id, Slug = program.Slug, IsShared = program.ClubId is null, Status = program.Status,
            Progress = ComputeProgress(program.EndOn, today), StartOn = program.StartOn, EndOn = program.EndOn,
            SortOrder = program.SortOrder, IsPinned = program.IsPinned, CharityId = program.CharityId,
            CharityNameZh = program.Charity.CharitiesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
            CoverKey = program.CoverKey, CoverUrl = imageUrls.Resolve(program.CoverKey), CoverThumbUrl = imageUrls.ResolveThumbnail(program.CoverKey),
            Zh = new AdminCharityProgramLocaleContent { Name = zh?.Name ?? "", TargetAudience = zh?.TargetAudience, Content = zh?.Content, DonationContent = zh?.DonationContent },
            En = en is null ? null : new AdminCharityProgramLocaleContent { Name = en.Name ?? "", TargetAudience = en.TargetAudience, Content = en.Content, DonationContent = en.DonationContent },
            Partners = program.Partners.OrderBy(p => p.SortOrder).Select(p => new AdminCharityLinkRefDto
            {
                Id = p.Id, Slug = p.Slug, Title = p.PartnersI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
            }).ToList(),
            Sponsors = program.Sponsors.OrderBy(s => s.SortOrder).Select(s => new AdminCharityLinkRefDto
            {
                Id = s.Id, Slug = s.Slug, Title = s.SponsorsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
            }).ToList(),
            Articles = program.CharityProgramArticles.OrderBy(a => a.SortOrder).Select(a => new AdminCharityLinkRefDto
            {
                Id = a.ArticleId, Slug = a.Article.Slug, Title = a.Article.ArticlesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Title,
            }).ToList(),
            Images = program.CharityProgramImages.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).Select(i => new AdminGalleryImageDto
            {
                Id = i.Id, ImageKey = i.ImageKey, ImageUrl = imageUrls.Resolve(i.ImageKey),
                ThumbUrl = imageUrls.Resolve(ImageObjectKey.ForThumbnail(i.ImageKey)), SortOrder = i.SortOrder,
            }).ToList(),
            CreatedAt = program.CreatedAt, UpdatedAt = program.UpdatedAt,
        };
    }
}
