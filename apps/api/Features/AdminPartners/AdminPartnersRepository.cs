using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPartners;

/// <summary>
/// E1 合作夥伴（主站規劃書 §4.5 E1，產出前台 09.1 夥伴 Logo 牆、首頁夥伴 Logo 牆、頁尾）。
/// <c>partners.club_id</c> 必填（v3.10 §5.4：50 張必填之一），所以**沒有共同列、沒有共同唯讀例外**——
/// 所有查詢一律 <c>club_id = scope.ClubId</c>，跨俱樂部的 id 一律 404（不洩漏存在與否）。
/// 快取：公開端點 <c>Features/Partners</c> 以 entity <c>partners</c> 快取，寫入後失效。
/// </summary>
public sealed class AdminPartnersRepository(ClubDbContext dbContext, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    public const string CacheEntity = "partners";

    /// <summary>規劃書 §3.9 9.1：策略夥伴／國際夥伴／訓練夥伴／教育夥伴／品牌夥伴。</summary>
    public static readonly IReadOnlyList<string> StandardTypes = ["策略夥伴", "國際夥伴", "訓練夥伴", "教育夥伴", "品牌夥伴"];

    public async Task<IReadOnlyList<AdminPartnerListItemDto>> ListAsync(
        AdminClubScope scope, string? partnerType, string? keyword, CancellationToken cancellationToken)
    {
        var query = dbContext.Partners.AsNoTracking().Where(p => p.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(partnerType))
        {
            query = query.Where(p => p.PartnerType == partnerType);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(p => p.Slug.Contains(k) || p.PartnersI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        var rows = await query
            .OrderBy(p => p.SortOrder).ThenBy(p => p.RowSeq)
            .Select(p => new
            {
                p.Id, p.Slug, p.PartnerType, p.Country, p.StartOn, p.EndOn, p.WebsiteUrl, p.ShowInFooter, p.ShowOnHome,
                p.SortOrder, p.LogoDarkKey, p.LogoLightKey, p.LogoDarkWidth, p.LogoDarkHeight, p.LogoLightWidth, p.LogoLightHeight, p.UpdatedAt,
                NameZh = p.PartnersI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = p.PartnersI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return rows.Select(r => new AdminPartnerListItemDto
        {
            Id = r.Id,
            Slug = r.Slug,
            PartnerType = r.PartnerType,
            Country = r.Country,
            StartOn = r.StartOn,
            EndOn = r.EndOn,
            WebsiteUrl = r.WebsiteUrl,
            ShowInFooter = r.ShowInFooter,
            ShowOnHome = r.ShowOnHome,
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
            IsActive = IsActive(r.StartOn, r.EndOn, today),
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminPartnerTypesDto> ListTypesAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var used = await dbContext.Partners.AsNoTracking()
            .Where(p => p.ClubId == scope.ClubId && p.PartnerType != null)
            .Select(p => p.PartnerType!).Distinct().OrderBy(t => t).ToListAsync(cancellationToken);
        return new AdminPartnerTypesDto { StandardTypes = StandardTypes, UsedTypes = used };
    }

    public async Task<AdminPartnerDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var partner = await dbContext.Partners.AsNoTracking()
            .Include(p => p.PartnersI18ns)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        return partner is null ? null : ToDetail(partner);
    }

    public async Task<AdminPartnerDetailDto> CreateAsync(
        AdminClubScope scope, Guid id, UpsertAdminPartnerRequest request,
        UploadedImageInfo? logoDark, UploadedImageInfo? logoLight, Guid? operatorId, CancellationToken cancellationToken)
    {
        var validated = Validate(request);
        var slug = validated.Slug ?? AdminInput.GenerateSlug("partner", request.Content.En?.Name);
        await EnsureSlugFreeAsync(scope, slug, null, cancellationToken);

        var now = DateTime.UtcNow;
        var partner = new Partner
        {
            Id = id,
            ClubId = scope.ClubId,
            Slug = slug,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
            LogoDarkKey = logoDark?.Key,
            LogoDarkWidth = logoDark?.Width,
            LogoDarkHeight = logoDark?.Height,
            LogoLightKey = logoLight?.Key,
            LogoLightWidth = logoLight?.Width,
            LogoLightHeight = logoLight?.Height,
        };
        Apply(partner, validated, request);
        dbContext.Partners.Add(partner);
        SetI18n(partner, request.Content);

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return (await GetByIdAsync(scope, id, cancellationToken))!;
    }

    public async Task<AdminPartnerDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminPartnerRequest request,
        ImageFieldUpdate logoDark, ImageFieldUpdate logoLight, OrphanedObjects orphans, Guid? operatorId, CancellationToken cancellationToken)
    {
        var validated = Validate(request);
        var partner = await dbContext.Partners.Include(p => p.PartnersI18ns)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (partner is null)
        {
            return null;
        }

        if (validated.Slug is not null && !string.Equals(validated.Slug, partner.Slug, StringComparison.Ordinal))
        {
            await EnsureSlugFreeAsync(scope, validated.Slug, id, cancellationToken);
            partner.Slug = validated.Slug;
        }

        if (logoDark.Change)
        {
            orphans.Image(partner.LogoDarkKey);
            partner.LogoDarkKey = logoDark.Key;
            partner.LogoDarkWidth = logoDark.Key is null ? null : logoDark.Width;
            partner.LogoDarkHeight = logoDark.Key is null ? null : logoDark.Height;
        }

        if (logoLight.Change)
        {
            orphans.Image(partner.LogoLightKey);
            partner.LogoLightKey = logoLight.Key;
            partner.LogoLightWidth = logoLight.Key is null ? null : logoLight.Width;
            partner.LogoLightHeight = logoLight.Key is null ? null : logoLight.Height;
        }

        Apply(partner, validated, request);
        partner.UpdatedAt = DateTime.UtcNow;
        partner.UpdatedBy = operatorId;
        SetI18n(partner, request.Content);

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var partner = await dbContext.Partners.FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (partner is null)
        {
            return false;
        }

        orphans.Image(partner.LogoDarkKey);
        orphans.Image(partner.LogoLightKey);
        // 側表與 program_partners／charity_program_partners 由資料庫 ON DELETE CASCADE 清掉。
        dbContext.Partners.Remove(partner);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return true;
    }

    /// <summary>依 <paramref name="ids"/> 的順序重排（第 0 個 sortOrder=0，依序遞增）。清單必須是這個俱樂部的夥伴 id，
    /// 不在其中的 id 一律 400（不默默略過，避免畫面以為排好了）。未列入清單的夥伴排在其後，相對順序不變。</summary>
    public async Task ReorderAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count)
        {
            throw new AdminValidationException("排序清單不可為空，也不可重複。");
        }

        var partners = await dbContext.Partners.Where(p => p.ClubId == scope.ClubId).OrderBy(p => p.SortOrder).ThenBy(p => p.RowSeq)
            .ToListAsync(cancellationToken);
        var byId = partners.ToDictionary(p => p.Id);
        if (ids.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("排序清單含有不存在的夥伴，請重新整理後再試。");
        }

        var order = ids.Concat(partners.Select(p => p.Id).Where(i => !ids.Contains(i))).ToList();
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

    /// <summary>夥伴頁與慈善計畫頁互相帶對方的資料（共同參與的公益計畫），兩邊快取都要失效。</summary>
    private async Task InvalidateAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        await cache.InvalidateAsync("charity", scope.ClubCode, cancellationToken);
    }

    private sealed record Validated(string? Slug, string PartnerType, string? Country, string? WebsiteUrl);

    private static Validated Validate(UpsertAdminPartnerRequest request)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : AdminInput.Slug(request.Slug.Trim());
        var type = AdminInput.RequireText(request.PartnerType, "夥伴類型", 32, "partnerType");
        var country = AdminInput.OptionalText(request.Country, "國家", 32, "country");
        var website = AdminInput.OptionalHttpUrl(request.WebsiteUrl, "官網連結", 500, "websiteUrl");
        AdminInput.DateRange(request.StartOn, request.EndOn, "合作期間", "endOn");
        AdminInput.RequireText(request.Content.Zh.Name, "中文名稱", 128, "nameZh");
        AdminInput.OptionalText(request.Content.Zh.LogoAlt, "標誌替代文字（中文）", 200, "logoAltZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文名稱", 128, "nameEn");
            AdminInput.OptionalText(request.Content.En.LogoAlt, "標誌替代文字（英文）", 200, "logoAltEn");
        }

        return new Validated(slug, type, country, website);
    }

    private static void Apply(Partner partner, Validated v, UpsertAdminPartnerRequest request)
    {
        partner.PartnerType = v.PartnerType;
        partner.Country = v.Country;
        partner.WebsiteUrl = v.WebsiteUrl;
        partner.StartOn = request.StartOn;
        partner.EndOn = request.EndOn;
        partner.ShowInFooter = request.ShowInFooter;
        partner.ShowOnHome = request.ShowOnHome;
        partner.SortOrder = request.SortOrder;
    }

    private void SetI18n(Partner partner, AdminPartnerContentInput content)
    {
        Upsert(partner, RequestLocale.DefaultDbLocale, content.Zh);
        var en = partner.PartnersI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(partner, "en", content.En);
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
        }
    }

    private void Upsert(Partner partner, string locale, AdminPartnerLocaleContent content)
    {
        var row = partner.PartnersI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new PartnersI18n { PartnerId = partner.Id, Locale = locale };
            partner.PartnersI18ns.Add(row);
            dbContext.PartnersI18ns.Add(row);
        }

        row.Name = content.Name.Trim();
        row.Content = string.IsNullOrWhiteSpace(content.Content) ? null : content.Content;
        row.LogoAlt = string.IsNullOrWhiteSpace(content.LogoAlt) ? null : content.LogoAlt.Trim();
    }

    private async Task EnsureSlugFreeAsync(AdminClubScope scope, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await dbContext.Partners.AsNoTracking().AnyAsync(
                p => p.ClubId == scope.ClubId && p.Slug == slug && p.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經被這個俱樂部的其他夥伴使用，請換一個。", "slug");
        }
    }

    private static bool IsActive(DateOnly? start, DateOnly? end, DateOnly today)
        => (start is null || start <= today) && (end is null || end >= today);

    private string? Thumb(string? key) => key is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(key));

    private AdminPartnerDetailDto ToDetail(Partner partner)
    {
        var zh = partner.PartnersI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = partner.PartnersI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminPartnerDetailDto
        {
            Id = partner.Id,
            Slug = partner.Slug,
            PartnerType = partner.PartnerType,
            Country = partner.Country,
            StartOn = partner.StartOn,
            EndOn = partner.EndOn,
            WebsiteUrl = partner.WebsiteUrl,
            ShowInFooter = partner.ShowInFooter,
            ShowOnHome = partner.ShowOnHome,
            SortOrder = partner.SortOrder,
            LogoDarkKey = partner.LogoDarkKey,
            LogoDarkUrl = imageUrls.Resolve(partner.LogoDarkKey),
            LogoLightKey = partner.LogoLightKey,
            LogoLightUrl = imageUrls.Resolve(partner.LogoLightKey),
            LogoDarkWidth = partner.LogoDarkKey is null ? null : partner.LogoDarkWidth,
            LogoDarkHeight = partner.LogoDarkKey is null ? null : partner.LogoDarkHeight,
            LogoLightWidth = partner.LogoLightKey is null ? null : partner.LogoLightWidth,
            LogoLightHeight = partner.LogoLightKey is null ? null : partner.LogoLightHeight,
            Zh = new AdminPartnerLocaleContent { Name = zh?.Name ?? "", Content = zh?.Content, LogoAlt = zh?.LogoAlt },
            En = en is null ? null : new AdminPartnerLocaleContent { Name = en.Name ?? "", Content = en.Content, LogoAlt = en.LogoAlt },
            CreatedAt = partner.CreatedAt,
            UpdatedAt = partner.UpdatedAt,
        };
    }
}
