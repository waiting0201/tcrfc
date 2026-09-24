using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminBanners;

/// <summary>
/// B3「Hero 輪播管理」（規劃書 §4.2 B3「排序、圖／影片、標題、CTA、上架期間」——本輪只做圖片，
/// 不做影片，理由見 apps/api/README.md）。俱樂部範圍（<c>banners.club_id</c> 必填，跟
/// <c>Competition</c> 同一類，沒有「共用內容」這件事）、沒有樂觀並行控制（理由同
/// <c>Features/AdminFaqs/AdminFaqsRepository.cs</c> 檔頭）、沒有草稿／發布狀態（規劃書沒有給
/// 輪播圖獨立的狀態欄位，只有「上架期間」，`banners` 表本身也確實沒有 <c>status</c> 欄位）。
/// </summary>
public sealed class AdminBannersRepository(ClubDbContext dbContext, IQueryCache cache, IImageStorageService imageStorage)
{
    private const string PublicEntity = "banners";

    public async Task<IReadOnlyList<AdminBannerListItemDto>> ListAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Banners.AsNoTracking()
            .Where(b => b.ClubId == scope.ClubId)
            .OrderBy(b => b.SortOrder)
            .Select(b => new
            {
                b.Id,
                b.ImageKey,
                b.StartAt,
                b.EndAt,
                b.SortOrder,
                b.UpdatedAt,
                TitleZh = b.BannersI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Title).FirstOrDefault(),
                TitleEn = b.BannersI18ns.Where(i => i.Locale == "en").Select(i => i.Title).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminBannerListItemDto
        {
            Id = r.Id,
            ImageKey = r.ImageKey,
            StartAt = r.StartAt,
            EndAt = r.EndAt,
            SortOrder = r.SortOrder,
            UpdatedAt = r.UpdatedAt,
            TitleZh = r.TitleZh,
            TitleEn = r.TitleEn,
        }).ToList();
    }

    public async Task<AdminBannerDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var banner = await dbContext.Banners.AsNoTracking()
            .Include(b => b.BannersI18ns)
            .FirstOrDefaultAsync(b => b.Id == id && b.ClubId == scope.ClubId, cancellationToken);

        return banner is null ? null : ToDetailDto(banner);
    }

    /// <summary><paramref name="imageKey"/> 是呼叫端（<see cref="AdminBannersEndpoints"/>）已經上傳
    /// 成功的物件鍵——本方法完全不碰物件儲存的上傳，只負責寫資料列。</summary>
    public async Task<AdminBannerDetailDto> CreateAsync(
        AdminClubScope scope, Guid bannerId, CreateBannerRequest request, string imageKey, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateDateRange(request.StartAt, request.EndAt);

        var now = DateTime.UtcNow;
        var banner = new Banner
        {
            Id = bannerId,
            ClubId = scope.ClubId,
            ImageKey = imageKey,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            SortOrder = request.SortOrder,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Banners.Add(banner);
        AddOrReplaceI18n(banner, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(banner, "en", request.Content.En);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(PublicEntity, scope.ClubCode, cancellationToken);

        return (await GetByIdAsync(scope, banner.Id, cancellationToken))!;
    }

    /// <summary><paramref name="newImageKey"/> 為 <c>null</c>＝這次請求沒有換圖，維持原圖；
    /// 有值＝呼叫端已經上傳成功的新物件鍵，這裡負責寫回資料列，並在整筆更新成功後刪除舊物件
    /// （規劃書 §4.0「新圖寫入成功後才刪舊物件」，理由與寫法逐字對應
    /// <c>AdminArticlesRepository.UpdateAsync</c> 換封面圖那一段）。</summary>
    public async Task<AdminBannerDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateBannerRequest request, string? newImageKey, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateDateRange(request.StartAt, request.EndAt);

        var banner = await dbContext.Banners
            .Include(b => b.BannersI18ns)
            .FirstOrDefaultAsync(b => b.Id == id && b.ClubId == scope.ClubId, cancellationToken);

        if (banner is null)
        {
            return null;
        }

        var previousImageKey = banner.ImageKey;
        var effectiveImageKey = newImageKey ?? banner.ImageKey;

        banner.ImageKey = effectiveImageKey;
        banner.StartAt = request.StartAt;
        banner.EndAt = request.EndAt;
        banner.SortOrder = request.SortOrder;
        banner.UpdatedAt = DateTime.UtcNow;
        banner.UpdatedBy = operatorId;

        AddOrReplaceI18n(banner, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = banner.BannersI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(banner, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(PublicEntity, scope.ClubCode, cancellationToken);

        if (!string.Equals(previousImageKey, effectiveImageKey, StringComparison.Ordinal))
        {
            await imageStorage.DeleteAsync(previousImageKey, cancellationToken); // fail-open
        }

        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>回傳 <c>null</c>＝找不到（含跨俱樂部）。刪除資料列成功後才刪圖片物件
    /// （規劃書 §4.0「刪資料列一併刪圖」，fail-open，不因刪圖失敗讓整個請求失敗）。</summary>
    public async Task<bool?> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var banner = await dbContext.Banners.FirstOrDefaultAsync(b => b.Id == id && b.ClubId == scope.ClubId, cancellationToken);
        if (banner is null)
        {
            return null;
        }

        var imageKey = banner.ImageKey;
        dbContext.Banners.Remove(banner);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(PublicEntity, scope.ClubCode, cancellationToken);

        await imageStorage.DeleteAsync(imageKey, cancellationToken); // fail-open
        return true;
    }

    private void AddOrReplaceI18n(Banner banner, string locale, AdminBannerLocaleContent content)
    {
        var existing = banner.BannersI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new BannersI18n { BannerId = banner.Id, Locale = locale };
            banner.BannersI18ns.Add(existing);
            dbContext.BannersI18ns.Add(existing);
        }

        existing.Title = content.Title;
        existing.Subtitle = content.Subtitle;
        existing.Cta1Label = content.Cta1Label;
        existing.Cta1Url = content.Cta1Url;
        existing.Cta2Label = content.Cta2Label;
        existing.Cta2Url = content.Cta2Url;
    }

    private static void ValidateDateRange(DateTime? startAt, DateTime? endAt)
    {
        if (startAt is DateTime s && endAt is DateTime e && s >= e)
        {
            throw new AdminBannerValidationException("上架時間必須早於下架時間。");
        }
    }

    private static AdminBannerDetailDto ToDetailDto(Banner banner)
    {
        var zh = banner.BannersI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = banner.BannersI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminBannerDetailDto
        {
            Id = banner.Id,
            ImageKey = banner.ImageKey,
            StartAt = banner.StartAt,
            EndAt = banner.EndAt,
            SortOrder = banner.SortOrder,
            UpdatedAt = banner.UpdatedAt,
            Zh = ToLocaleContent(zh),
            En = en is null ? null : ToLocaleContent(en),
        };
    }

    private static AdminBannerLocaleContent ToLocaleContent(BannersI18n? i18n) => new()
    {
        Title = i18n?.Title,
        Subtitle = i18n?.Subtitle,
        Cta1Label = i18n?.Cta1Label,
        Cta1Url = i18n?.Cta1Url,
        Cta2Label = i18n?.Cta2Label,
        Cta2Url = i18n?.Cta2Url,
    };
}
