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
/// E2「贊助活動（Activations）」：活動名稱、日期、圖集、成效摘要，掛在贊助商底下（規劃書 §4.5 E2），
/// 產出前台 09.2「贊助活動紀錄」。圖集是子表 <c>sponsor_activation_images</c>（每列一組圖片欄位＋排序）。
/// 所有操作先確認贊助商屬於 <c>scope.ClubId</c>，跨俱樂部一律 404。
/// </summary>
public sealed class AdminSponsorActivationsRepository(ClubDbContext dbContext, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    public async Task<IReadOnlyList<AdminActivationDto>?> ListAsync(AdminClubScope scope, Guid sponsorId, CancellationToken cancellationToken)
    {
        if (!await SponsorExistsAsync(scope, sponsorId, cancellationToken))
        {
            return null;
        }

        var rows = await dbContext.SponsorActivations.AsNoTracking()
            .Include(a => a.SponsorActivationsI18ns).Include(a => a.SponsorActivationImages)
            .Where(a => a.SponsorId == sponsorId && a.ClubId == scope.ClubId)
            .OrderByDescending(a => a.HappenedOn).ThenBy(a => a.SortOrder).ThenBy(a => a.RowSeq)
            .AsSplitQuery().ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<AdminActivationDto?> GetAsync(AdminClubScope scope, Guid sponsorId, Guid id, CancellationToken cancellationToken)
    {
        var activation = await LoadAsync(scope, sponsorId, id, tracking: false, cancellationToken);
        return activation is null ? null : ToDto(activation);
    }

    public async Task<AdminActivationDto?> CreateAsync(
        AdminClubScope scope, Guid sponsorId, UpsertAdminActivationRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (!await SponsorExistsAsync(scope, sponsorId, cancellationToken))
        {
            return null;
        }

        Validate(request);
        var now = DateTime.UtcNow;
        var activation = new SponsorActivation
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, SponsorId = sponsorId, CreatedAt = now, UpdatedAt = now,
            CreatedBy = operatorId, UpdatedBy = operatorId, HappenedOn = request.HappenedOn, SortOrder = request.SortOrder,
        };
        dbContext.SponsorActivations.Add(activation);
        SetI18n(activation, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetAsync(scope, sponsorId, activation.Id, cancellationToken);
    }

    public async Task<AdminActivationDto?> UpdateAsync(
        AdminClubScope scope, Guid sponsorId, Guid id, UpsertAdminActivationRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        var activation = await LoadAsync(scope, sponsorId, id, tracking: true, cancellationToken);
        if (activation is null)
        {
            return null;
        }

        activation.HappenedOn = request.HappenedOn;
        activation.SortOrder = request.SortOrder;
        activation.UpdatedAt = DateTime.UtcNow;
        activation.UpdatedBy = operatorId;
        SetI18n(activation, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetAsync(scope, sponsorId, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid sponsorId, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var activation = await LoadAsync(scope, sponsorId, id, tracking: true, cancellationToken);
        if (activation is null)
        {
            return false;
        }

        foreach (var image in activation.SponsorActivationImages)
        {
            orphans.Image(image.ImageKey);
        }

        dbContext.SponsorActivations.Remove(activation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return true;
    }

    public async Task<AdminActivationDto?> AddImageAsync(
        AdminClubScope scope, Guid sponsorId, Guid id, UploadedImageInfo image, Guid? operatorId, CancellationToken cancellationToken)
    {
        var activation = await LoadAsync(scope, sponsorId, id, tracking: true, cancellationToken);
        if (activation is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        dbContext.SponsorActivationImages.Add(new SponsorActivationImage
        {
            Id = Guid.NewGuid(), SponsorActivationId = id, ImageKey = image.Key, ImageWidth = image.Width, ImageHeight = image.Height,
            SortOrder = activation.SponsorActivationImages.Count == 0 ? 0 : activation.SponsorActivationImages.Max(i => i.SortOrder) + 1,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetAsync(scope, sponsorId, id, cancellationToken);
    }

    public async Task<bool> DeleteImageAsync(
        AdminClubScope scope, Guid sponsorId, Guid id, Guid imageId, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var activation = await LoadAsync(scope, sponsorId, id, tracking: true, cancellationToken);
        var image = activation?.SponsorActivationImages.FirstOrDefault(i => i.Id == imageId);
        if (image is null)
        {
            return false;
        }

        orphans.Image(image.ImageKey);
        dbContext.SponsorActivationImages.Remove(image);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return true;
    }

    public async Task<AdminActivationDto?> ReorderImagesAsync(
        AdminClubScope scope, Guid sponsorId, Guid id, IReadOnlyList<Guid> imageIds, CancellationToken cancellationToken)
    {
        var activation = await LoadAsync(scope, sponsorId, id, tracking: true, cancellationToken);
        if (activation is null)
        {
            return null;
        }

        var byId = activation.SponsorActivationImages.ToDictionary(i => i.Id);
        if (imageIds.Distinct().Count() != imageIds.Count || imageIds.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("圖片排序清單含有不存在或重複的圖片，請重新整理後再試。");
        }

        var order = imageIds.Concat(activation.SponsorActivationImages.OrderBy(i => i.SortOrder).Select(i => i.Id).Where(i => !imageIds.Contains(i))).ToList();
        for (var i = 0; i < order.Count; i++)
        {
            byId[order[i]].SortOrder = i;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetAsync(scope, sponsorId, id, cancellationToken);
    }

    private Task<bool> SponsorExistsAsync(AdminClubScope scope, Guid sponsorId, CancellationToken cancellationToken)
        => dbContext.Sponsors.AsNoTracking().AnyAsync(s => s.Id == sponsorId && s.ClubId == scope.ClubId, cancellationToken);

    private Task<SponsorActivation?> LoadAsync(AdminClubScope scope, Guid sponsorId, Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = dbContext.SponsorActivations.Include(a => a.SponsorActivationsI18ns).Include(a => a.SponsorActivationImages).AsSplitQuery();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(a => a.Id == id && a.SponsorId == sponsorId && a.ClubId == scope.ClubId, cancellationToken);
    }

    private static void Validate(UpsertAdminActivationRequest request)
    {
        AdminInput.RequireText(request.Content.Zh.Title, "中文活動名稱", 200, "titleZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Title))
        {
            AdminInput.RequireText(request.Content.En.Title, "英文活動名稱", 200, "titleEn");
        }
    }

    private void SetI18n(SponsorActivation activation, AdminActivationContentInput content)
    {
        Upsert(activation, RequestLocale.DefaultDbLocale, content.Zh);
        var en = activation.SponsorActivationsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Title))
        {
            Upsert(activation, "en", content.En);
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
        }
    }

    private void Upsert(SponsorActivation activation, string locale, AdminActivationLocaleContent content)
    {
        var row = activation.SponsorActivationsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new SponsorActivationsI18n { SponsorActivationId = activation.Id, Locale = locale };
            activation.SponsorActivationsI18ns.Add(row);
            dbContext.SponsorActivationsI18ns.Add(row);
        }

        row.Title = content.Title.Trim();
        row.ResultSummary = string.IsNullOrWhiteSpace(content.ResultSummary) ? null : content.ResultSummary;
    }

    private Task InvalidateAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => cache.InvalidateAsync(AdminSponsorsRepository.CacheEntity, scope.ClubCode, cancellationToken);

    private AdminActivationDto ToDto(SponsorActivation activation)
    {
        var zh = activation.SponsorActivationsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = activation.SponsorActivationsI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminActivationDto
        {
            Id = activation.Id,
            SponsorId = activation.SponsorId,
            HappenedOn = activation.HappenedOn,
            SortOrder = activation.SortOrder,
            Zh = new AdminActivationLocaleContent { Title = zh?.Title ?? "", ResultSummary = zh?.ResultSummary },
            En = en is null ? null : new AdminActivationLocaleContent { Title = en.Title ?? "", ResultSummary = en.ResultSummary },
            Images = activation.SponsorActivationImages.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).Select(i => new AdminActivationImageDto
            {
                Id = i.Id,
                ImageKey = i.ImageKey,
                ImageUrl = imageUrls.Resolve(i.ImageKey),
                ThumbUrl = imageUrls.Resolve(ImageObjectKey.ForThumbnail(i.ImageKey)),
                ImageWidth = i.ImageWidth,
                ImageHeight = i.ImageHeight,
                SortOrder = i.SortOrder,
            }).ToList(),
            UpdatedAt = activation.UpdatedAt,
        };
    }
}
