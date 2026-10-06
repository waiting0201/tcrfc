using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Geocoding;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminVenues;

/// <summary>
/// 全站共用場地主檔（規劃書 §4.9 I5「場地管理：場地名稱、地址、經緯度、交通說明、照片（供 5.1 訓練地點與 Location &amp; Map 使用）」）。
/// 原本只有唯讀清單（S1-12d 後續缺口補完）；2026-10-02 補上完整 CRUD、照片上傳（圖片欄位組：<c>photo_key</c>／<c>photo_width</c>／<c>photo_height</c>＋
/// <c>venues_i18n.photo_alt</c>）與「由地址定位」預覽。
///
/// <c>Venue</c> 刻意不帶 <c>club_id</c>（兩隊共用同一座球場，docs/12 §4.7）：寫入權限碼 <c>site.venue.*</c> 因此是 sysadmin_only，
/// 不存在「只能改自己俱樂部的場地」這種範圍。刪除前檢查所有引用（賽事、梯次、試訓、行事曆自建事件、球迷會活動，以及任何俱樂部的主場登記）；
/// 仍被引用一律 409，不做連帶刪除或自動解除引用。
/// </summary>
public sealed class AdminVenuesRepository(ClubDbContext dbContext, IImagePublicUrlResolver imageUrls, IGeocoder geocoder, IQueryCache cache)
{
    private const string HomeVenueSettingKey = "site.home_venue_ids";

    public async Task<IReadOnlyList<AdminVenueListItemDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Venues.AsNoTracking()
            .Select(v => new
            {
                v.Id,
                v.SortOrder,
                v.Lat,
                v.Lng,
                v.PhotoKey,
                NameZh = v.VenuesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = v.VenuesI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                Address = v.VenuesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Address).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // 既有列的 SortOrder 目前全部是 0（從未被賦予有意義的排序值，見種子資料），單獨用它排序
        // 會落回資料庫回傳順序不保證的問題；用中文名稱做穩定的次要排序，讓下拉選單順序至少可預期。
        return rows
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.NameZh, StringComparer.Ordinal)
            .Select(r => new AdminVenueListItemDto
            {
                Id = r.Id,
                NameZh = r.NameZh ?? string.Empty,
                NameEn = r.NameEn,
                Address = r.Address,
                Lat = r.Lat,
                Lng = r.Lng,
                PhotoUrl = imageUrls.Resolve(r.PhotoKey),
            })
            .ToList();
    }

    public async Task<AdminVenueDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var venue = await dbContext.Venues.AsNoTracking().Include(v => v.VenuesI18ns).FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        return venue is null ? null : await ToDetailAsync(venue, cancellationToken);
    }

    /// <summary>建立：<paramref name="id"/> 由端點先產生（照片物件鍵的路徑要用到）。</summary>
    public async Task<AdminVenueDetailDto> CreateAsync(
        Guid id, UpsertAdminVenueRequest request, ImageFieldUpdate photo, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (zh, en, lat, lng) = Validate(request);
        var now = DateTime.UtcNow;
        var venue = new Venue
        {
            Id = id, Lat = lat, Lng = lng, SortOrder = request.SortOrder, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        if (photo.Change)
        {
            venue.PhotoKey = photo.Key;
            venue.PhotoWidth = photo.Width;
            venue.PhotoHeight = photo.Height;
        }

        dbContext.Venues.Add(venue);
        SetI18n(venue, RequestLocale.DefaultDbLocale, zh);
        SetI18n(venue, "en", en);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateSiteFactsAsync(cancellationToken);
        return await ToDetailAsync(venue, cancellationToken);
    }

    public async Task<AdminVenueDetailDto?> UpdateAsync(
        Guid id, UpsertAdminVenueRequest request, ImageFieldUpdate photo, OrphanedObjects orphans, Guid? operatorId, CancellationToken cancellationToken)
    {
        var venue = await dbContext.Venues.Include(v => v.VenuesI18ns).FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (venue is null)
        {
            return null;
        }

        var (zh, en, lat, lng) = Validate(request);
        venue.Lat = lat;
        venue.Lng = lng;
        venue.SortOrder = request.SortOrder;
        if (photo.Change)
        {
            orphans.Image(venue.PhotoKey);
            venue.PhotoKey = photo.Key;
            venue.PhotoWidth = photo.Width;
            venue.PhotoHeight = photo.Height;
        }

        SetI18n(venue, RequestLocale.DefaultDbLocale, zh);
        SetI18n(venue, "en", en);
        venue.UpdatedAt = DateTime.UtcNow;
        venue.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateSiteFactsAsync(cancellationToken);
        return await ToDetailAsync(venue, cancellationToken);
    }

    /// <summary>刪除；仍被引用丟 <see cref="AdminConflictException"/>。回傳 <c>null</c>＝找不到。成功時把照片物件鍵登記到 <paramref name="orphans"/>。</summary>
    public async Task<bool> DeleteAsync(Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var venue = await dbContext.Venues.Include(v => v.VenuesI18ns).FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (venue is null)
        {
            return false;
        }

        var (usage, isHome) = await CountUsageAsync(id, cancellationToken);
        if (usage > 0 || isHome)
        {
            throw new AdminConflictException("場地仍在使用中", isHome && usage == 0
                ? "這個場地被登記為某個俱樂部的主場，請先到網站設定移除主場登記，再刪除場地。"
                : "這個場地仍被賽事、梯次、試訓或活動使用，請先改掉那些資料的地點，再刪除場地。");
        }

        orphans.Image(venue.PhotoKey);
        dbContext.VenuesI18ns.RemoveRange(venue.VenuesI18ns.ToList());
        dbContext.Venues.Remove(venue);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateSiteFactsAsync(cancellationToken);
        return true;
    }

    /// <summary>目前的照片物件鍵（端點在建立失敗時不需要；更新／刪除的孤兒清理由 <see cref="OrphanedObjects"/> 處理）。</summary>
    public async Task<string?> GetPhotoKeyAsync(Guid id, CancellationToken cancellationToken)
        => await dbContext.Venues.AsNoTracking().Where(v => v.Id == id).Select(v => v.PhotoKey).FirstOrDefaultAsync(cancellationToken);

    /// <summary>「由地址定位」預覽（只回候選座標，不寫入任何資料），行為同特約店家的定位按鈕。</summary>
    public async Task<GeocodeResult?> LocateAsync(string? address, CancellationToken cancellationToken)
    {
        var trimmed = AdminInput.RequireText(address, "地址", 500);
        try
        {
            return await geocoder.GeocodeAsync(trimmed, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException)
        {
            throw new FeatureNotConfiguredException("定位服務暫時無法使用，請稍後再試，或直接輸入緯度與經度。", "geocoder_unavailable");
        }
    }

    private async Task<(int Usage, bool IsHome)> CountUsageAsync(Guid id, CancellationToken cancellationToken)
    {
        var usage = await dbContext.Matches.CountAsync(m => m.VenueId == id, cancellationToken)
                    + await dbContext.Sessions.CountAsync(s => s.VenueId == id, cancellationToken)
                    + await dbContext.Trials.CountAsync(t => t.VenueId == id, cancellationToken)
                    + await dbContext.CalendarCustomEvents.CountAsync(e => e.VenueId == id, cancellationToken)
                    + await dbContext.FanEvents.CountAsync(e => e.VenueId == id, cancellationToken);
        var idText = id.ToString();
        var isHome = await dbContext.Settings.AsNoTracking()
            .AnyAsync(s => s.SettingKey == HomeVenueSettingKey && s.SettingValue != null && s.SettingValue.Contains(idText), cancellationToken);
        return (usage, isHome);
    }

    private async Task<AdminVenueDetailDto> ToDetailAsync(Venue venue, CancellationToken cancellationToken)
    {
        var (usage, isHome) = await CountUsageAsync(venue.Id, cancellationToken);
        var zh = venue.VenuesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = venue.VenuesI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminVenueDetailDto
        {
            Id = venue.Id,
            Lat = venue.Lat,
            Lng = venue.Lng,
            PhotoUrl = imageUrls.Resolve(venue.PhotoKey),
            PhotoWidth = venue.PhotoKey is null ? null : venue.PhotoWidth,
            PhotoHeight = venue.PhotoKey is null ? null : venue.PhotoHeight,
            SortOrder = venue.SortOrder,
            Zh = ToContent(zh) ?? new AdminVenueLocaleContent { Name = string.Empty },
            En = ToContent(en),
            UsageCount = usage,
            IsHomeVenue = isHome,
            UpdatedAt = venue.UpdatedAt,
        };
    }

    private static AdminVenueLocaleContent? ToContent(VenuesI18n? row)
        => row is null ? null : new AdminVenueLocaleContent
        {
            Name = row.Name ?? string.Empty, Address = row.Address, Directions = row.Directions, PhotoAlt = row.PhotoAlt,
        };

    private static (AdminVenueLocaleContent Zh, AdminVenueLocaleContent? En, decimal? Lat, decimal? Lng) Validate(UpsertAdminVenueRequest request)
    {
        if (request.Zh is null)
        {
            throw new AdminValidationException("場地名稱（繁中）為必填欄位。", "nameZh");
        }

        var zh = Clean(request.Zh, "繁中", "Zh", requireName: true)!;
        var en = request.En is null || string.IsNullOrWhiteSpace(request.En.Name) ? null : Clean(request.En, "英文", "En", requireName: true);
        if (request.SortOrder < 0)
        {
            throw new AdminValidationException("排序不可為負數。", "sortOrder");
        }

        if ((request.Lat is null) != (request.Lng is null))
        {
            throw new AdminValidationException("緯度與經度要同時填寫，或同時留白。", request.Lat is null ? "lat" : "lng");
        }

        if (request.Lat is { } lat && (lat < -90 || lat > 90))
        {
            throw new AdminValidationException("緯度必須介於 -90 到 90 之間。", "lat");
        }

        if (request.Lng is { } lng && (lng < -180 || lng > 180))
        {
            throw new AdminValidationException("經度必須介於 -180 到 180 之間。", "lng");
        }

        // decimal(9,6)：小數最多 6 位，超過會被資料庫靜默四捨五入，這裡先明確四捨五入。
        return (zh, en, request.Lat is null ? null : Math.Round(request.Lat.Value, 6), request.Lng is null ? null : Math.Round(request.Lng.Value, 6));
    }

    private static AdminVenueLocaleContent? Clean(AdminVenueLocaleContent content, string languageLabel, string suffix, bool requireName)
        => new()
        {
            Name = requireName ? AdminInput.RequireText(content.Name, $"場地名稱（{languageLabel}）", 128, "name" + suffix) : content.Name,
            Address = AdminInput.OptionalText(content.Address, $"地址（{languageLabel}）", 255, "address" + suffix),
            Directions = AdminInput.OptionalText(content.Directions, $"交通說明（{languageLabel}）", 5000, "directions" + suffix),
            PhotoAlt = AdminInput.OptionalText(content.PhotoAlt, $"照片替代文字（{languageLabel}）", 200, "photoAlt" + suffix),
        };

    private void SetI18n(Venue venue, string locale, AdminVenueLocaleContent? content)
    {
        var row = venue.VenuesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (content is null)
        {
            if (row is not null)
            {
                dbContext.VenuesI18ns.Remove(row);
                venue.VenuesI18ns.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new VenuesI18n { VenueId = venue.Id, Locale = locale };
            venue.VenuesI18ns.Add(row);
            dbContext.VenuesI18ns.Add(row);
        }

        row.Name = content.Name;
        row.Address = content.Address;
        row.Directions = content.Directions;
        row.PhotoAlt = content.PhotoAlt;
    }

    /// <summary>站台事實（<c>Features/SiteFacts</c>）的公開讀取會快取主場名稱與地址；場地變更後讓所有俱樂部的該項快取失效。</summary>
    private async Task InvalidateSiteFactsAsync(CancellationToken cancellationToken)
    {
        foreach (var code in await dbContext.Clubs.AsNoTracking().Select(c => c.Code).ToListAsync(cancellationToken))
        {
            await cache.InvalidateAsync("site-facts", code, cancellationToken);
        }
    }
}
