using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Geocoding;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPartnerStores;

/// <summary>
/// K4 特約店家（規劃書 §4.11 K4，產出前台 8.4 與 App「附近特約店家」）。<b>特約店家不是 Partner／Sponsor／DonationStore</b>
/// （五種商業對象各建各的，docs/14）：無金流、無分潤、不核銷。<c>partner_stores.club_id</c> 可為空＝兩隊共同（§5.4）：
/// 清單與詳情會一併列出共同店家（<c>isShared: true</c>），但<b>編輯與刪除只有系統管理員</b>（共同內容唯讀規則）；建立一律歸屬呼叫端俱樂部，
/// 系統管理員可用 <c>isShared</c> 建立共同店家。座標 <c>lat／lng</c> 由人工確認後儲存；「由地址定位」只在後台由管理者觸發（預覽按鈕或儲存時勾選自動定位，見 <c>ResolveCoordinatesAsync</c>），App 與訪客請求永遠不會觸發定位。
/// </summary>
public sealed class AdminPartnerStoresRepository(
    ClubDbContext db, IImagePublicUrlResolver imageUrls, IGeocoder geocoder, ILogger<AdminPartnerStoresRepository> logger)
{
    /// <summary>S2-5「由地址定位」的結果狀態（只出現在寫入回應）。<c>skipped</c>＝沒有要求定位，或已手動填座標（手動值優先）。</summary>
    public const string LocateSkipped = "skipped";
    public const string LocateLocated = "located";
    public const string LocateNotFound = "not_found";
    public const string LocateUnavailable = "unavailable";

    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "draft", "published" };
    private static readonly HashSet<string> Tiers = new(StringComparer.Ordinal) { "all", "fan_club" };

    private IQueryable<PartnerStore> Visible(AdminClubScope scope)
        => db.PartnerStores.Where(s => s.ClubId == scope.ClubId || s.ClubId == null);

    public async Task<IReadOnlyList<AdminPartnerStoreListItemDto>> ListAsync(
        AdminClubScope scope, string? category, string? region, string? status, string? tier, string? keyword, CancellationToken cancellationToken)
    {
        var query = Visible(scope).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(s => s.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            query = query.Where(s => s.Region == region);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, Statuses, "狀態", "「上架」或「下架」");
            query = query.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(tier))
        {
            AdminInput.OneOf(tier, Tiers, "適用層級", "「全會員」或「限付費」");
            query = query.Where(s => s.ApplicableTier == tier);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(s => s.Slug.Contains(k) || (s.Address != null && s.Address.Contains(k))
                || s.PartnerStoresI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        var rows = await query.OrderBy(s => s.SortOrder).ThenBy(s => s.RowSeq)
            .Select(s => new
            {
                s.Id, s.Slug, s.ClubId, s.Category, s.Region, s.Address, s.Lat, s.Lng, s.Phone, s.ApplicableTier, s.StartOn, s.EndOn,
                s.SortOrder, s.Status, s.ImageKey, s.UpdatedAt,
                NameZh = s.PartnerStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = s.PartnerStoresI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                OfferZh = s.PartnerStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.OfferContent).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return rows.Select(r => new AdminPartnerStoreListItemDto
        {
            Id = r.Id, Slug = r.Slug, IsShared = r.ClubId is null, Category = r.Category, Region = r.Region, Address = r.Address,
            Lat = r.Lat, Lng = r.Lng, Phone = r.Phone, ApplicableTier = r.ApplicableTier, ApplicableTierLabel = TierLabel(r.ApplicableTier),
            StartOn = r.StartOn, EndOn = r.EndOn, IsActive = IsActive(r.StartOn, r.EndOn, today), SortOrder = r.SortOrder, Status = r.Status,
            StatusLabel = StatusLabel(r.Status), ImageKey = r.ImageKey, ImageUrl = imageUrls.Resolve(r.ImageKey), ImageThumbUrl = Thumb(r.ImageKey),
            NameZh = r.NameZh, NameEn = r.NameEn, OfferZh = r.OfferZh, UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminPartnerStoreFiltersDto> ListFiltersAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var rows = await Visible(scope).AsNoTracking().Select(s => new { s.Category, s.Region }).ToListAsync(cancellationToken);
        return new AdminPartnerStoreFiltersDto
        {
            Categories = rows.Where(r => !string.IsNullOrWhiteSpace(r.Category)).Select(r => r.Category!).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList(),
            Regions = rows.Where(r => !string.IsNullOrWhiteSpace(r.Region)).Select(r => r.Region!).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList(),
        };
    }

    public async Task<AdminPartnerStoreDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var store = await Visible(scope).AsNoTracking().Include(s => s.PartnerStoresI18ns).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return store is null ? null : ToDetail(store);
    }

    public async Task<AdminPartnerStoreDetailDto> CreateAsync(
        AdminClubScope scope, Guid id, UpsertAdminPartnerStoreRequest request, UploadedImageInfo? image, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (request.IsShared && !scope.Identity.IsSuperAdmin)
        {
            throw new AdminForbiddenException("只有系統管理員可以建立兩隊共同的特約店家。");
        }

        var v = Validate(request);
        Guid? clubId = request.IsShared ? null : scope.ClubId;
        var slug = v.Slug ?? AdminInput.GenerateSlug("store", request.Content.En?.Name);
        await EnsureSlugFreeAsync(clubId, slug, null, cancellationToken);

        var now = DateTime.UtcNow;
        var store = new PartnerStore
        {
            Id = id, ClubId = clubId, Slug = slug, ImageKey = image?.Key, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        var (lat, lng, locateStatus) = await ResolveCoordinatesAsync(request, cancellationToken);
        Apply(store, request, v, lat, lng);
        db.PartnerStores.Add(store);
        SetI18n(store, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(scope, id, cancellationToken))! with { AutoLocateStatus = locateStatus };
    }

    public async Task<AdminPartnerStoreDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminPartnerStoreRequest request, ImageFieldUpdate image, OrphanedObjects orphans,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        var v = Validate(request);
        var store = await Visible(scope).Include(s => s.PartnerStoresI18ns).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (store is null)
        {
            return null;
        }

        RequireWritable(scope, store);
        if (v.Slug is not null && !string.Equals(v.Slug, store.Slug, StringComparison.Ordinal))
        {
            await EnsureSlugFreeAsync(store.ClubId, v.Slug, id, cancellationToken);
            store.Slug = v.Slug;
        }

        if (image.Change)
        {
            orphans.Image(store.ImageKey);
            store.ImageKey = image.Key;
        }

        var (lat, lng, locateStatus) = await ResolveCoordinatesAsync(request, cancellationToken);
        Apply(store, request, v, lat, lng);
        store.UpdatedAt = DateTime.UtcNow;
        store.UpdatedBy = operatorId;
        SetI18n(store, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        var updated = await GetByIdAsync(scope, id, cancellationToken);
        return updated is null ? null : updated with { AutoLocateStatus = locateStatus };
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var store = await Visible(scope).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (store is null)
        {
            return false;
        }

        RequireWritable(scope, store);
        orphans.Image(store.ImageKey);
        db.PartnerStores.Remove(store); // 側表由資料庫串聯刪除
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>依 <paramref name="ids"/> 順序重排本俱樂部的店家（共同店家不參與，其排序由系統管理員在編輯時設定）。</summary>
    public async Task ReorderAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count)
        {
            throw new AdminValidationException("排序清單不可為空，也不可重複。");
        }

        var stores = await db.PartnerStores.Where(s => s.ClubId == scope.ClubId).OrderBy(s => s.SortOrder).ThenBy(s => s.RowSeq).ToListAsync(cancellationToken);
        var byId = stores.ToDictionary(s => s.Id);
        if (ids.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("排序清單含有不存在的店家（或是兩隊共同的店家），請重新整理後再試。");
        }

        var order = ids.Concat(stores.Select(s => s.Id).Where(i => !ids.Contains(i))).ToList();
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

        await db.SaveChangesAsync(cancellationToken);
    }

    // ── 內部 ───────────────────────────────────────────

    private static void RequireWritable(AdminClubScope scope, PartnerStore store)
    {
        if (store.ClubId is null && !scope.Identity.IsSuperAdmin)
        {
            throw new SharedContentReadOnlyException("特約店家");
        }
    }

    private sealed record Validated(string? Slug, string? Category, string? Region, string? Phone, string? MapUrl, string? WebsiteUrl, string? Hours);

    private static Validated Validate(UpsertAdminPartnerStoreRequest request)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : AdminInput.Slug(request.Slug.Trim());
        var category = AdminInput.OptionalText(request.Category, "類別", 32);
        var region = AdminInput.OptionalText(request.Region, "地區", 32);
        var phone = AdminInput.OptionalText(request.Phone, "電話", 32);
        var mapUrl = AdminInput.OptionalHttpUrl(request.MapUrl, "地圖連結");
        var website = AdminInput.OptionalHttpUrl(request.WebsiteUrl, "官網或社群連結");
        var hours = AdminInput.OptionalText(request.BusinessHours, "營業時間", 500);
        AdminInput.OneOf(request.ApplicableTier, Tiers, "適用層級", "「全會員」或「限付費」");
        AdminInput.OneOf(request.Status, Statuses, "狀態", "「上架」或「下架」");
        AdminInput.DateRange(request.StartOn, request.EndOn, "合作期間");
        if ((request.Lat is null) != (request.Lng is null))
        {
            throw new AdminValidationException("緯度與經度必須一起填寫，或兩個都留空。");
        }

        if (request.Lat is < -90 or > 90 || request.Lng is < -180 or > 180)
        {
            throw new AdminValidationException("座標超出範圍：緯度須在 -90 到 90 之間，經度須在 -180 到 180 之間。");
        }

        AdminInput.RequireText(request.Content.Zh.Name, "中文店名", 128);
        AdminInput.OptionalText(request.Content.Zh.Address, "中文地址", 500);
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文店名", 128);
            AdminInput.OptionalText(request.Content.En.Address, "英文地址", 500);
        }

        return new Validated(slug, category, region, phone, mapUrl, website, hours);
    }

    /// <summary>
    /// 座標決定規則（S2-5）：<b>手動填的座標永遠優先</b>（可覆寫任何自動結果）；兩者都沒填且 <c>AutoLocate</c> 為 true、
    /// 又有中文地址時，才呼叫 <see cref="IGeocoder"/>。定位失敗（查無、未啟用、供應商故障）<b>一律不阻擋存檔</b>，
    /// 座標留空並以狀態回報，讓後台提示「請手動輸入座標」。規劃書要求「人工確認後儲存」，所以自動定位只在管理者明確勾選
    /// 「儲存時由地址定位」時才發生；單純預覽請用 <c>POST …/partner-stores/locate</c>，不寫入任何資料。
    /// </summary>
    private async Task<(decimal? Lat, decimal? Lng, string Status)> ResolveCoordinatesAsync(
        UpsertAdminPartnerStoreRequest request, CancellationToken cancellationToken)
    {
        if (request.Lat is not null || !request.AutoLocate)
        {
            return (request.Lat, request.Lng, LocateSkipped);
        }

        var address = request.Content.Zh.Address?.Trim();
        if (string.IsNullOrEmpty(address))
        {
            return (null, null, LocateNotFound);
        }

        try
        {
            var result = await geocoder.GeocodeAsync(address, cancellationToken);
            return result is null
                ? (null, null, LocateNotFound)
                : (result.Lat, result.Lng, LocateLocated);
        }
        catch (FeatureNotConfiguredException)
        {
            return (null, null, LocateUnavailable);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException && !cancellationToken.IsCancellationRequested)
        {
            // 🔴 不記地址（個資性質）；只記例外型別。
            logger.LogWarning("特約店家由地址定位失敗（供應商錯誤 {ExceptionType}），已略過自動定位。", ex.GetType().Name);
            return (null, null, LocateUnavailable);
        }
    }

    /// <summary>後台「由地址定位」預覽按鈕（只回候選座標，<b>不寫入任何資料</b>）。查無回 <c>null</c>；未啟用拋 <see cref="FeatureNotConfiguredException"/>。</summary>
    public async Task<GeocodeResult?> LocateAsync(string? address, CancellationToken cancellationToken)
    {
        var trimmed = AdminInput.RequireText(address, "地址", 500);
        return await geocoder.GeocodeAsync(trimmed, cancellationToken);
    }

    private static void Apply(PartnerStore store, UpsertAdminPartnerStoreRequest request, Validated v, decimal? lat, decimal? lng)
    {
        store.Category = v.Category;
        store.Region = v.Region;
        store.Address = string.IsNullOrWhiteSpace(request.Content.Zh.Address) ? null : request.Content.Zh.Address.Trim();
        store.Lat = lat;
        store.Lng = lng;
        store.Phone = v.Phone;
        store.BusinessHours = JsonColumn.WrapText(v.Hours); // json 欄位只收物件或陣列：自由文字包成 {"text":"…"}（docs/18 E-111）
        store.MapUrl = v.MapUrl;
        store.WebsiteUrl = v.WebsiteUrl;
        store.ApplicableTier = request.ApplicableTier;
        store.StartOn = request.StartOn;
        store.EndOn = request.EndOn;
        store.SortOrder = request.SortOrder;
        store.Status = request.Status;
    }

    private void SetI18n(PartnerStore store, AdminStoreContentInput content)
    {
        Upsert(store, RequestLocale.DefaultDbLocale, content.Zh, includeAddress: false);
        var en = store.PartnerStoresI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(store, "en", content.En, includeAddress: true);
        }
        else if (en is not null)
        {
            db.Remove(en);
        }
    }

    private void Upsert(PartnerStore store, string locale, AdminStoreLocaleContent content, bool includeAddress)
    {
        var row = store.PartnerStoresI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new PartnerStoresI18n { PartnerStoreId = store.Id, Locale = locale };
            store.PartnerStoresI18ns.Add(row);
            db.PartnerStoresI18ns.Add(row);
        }

        row.Name = content.Name.Trim();
        row.OfferContent = string.IsNullOrWhiteSpace(content.OfferContent) ? null : content.OfferContent;
        row.Address = includeAddress && !string.IsNullOrWhiteSpace(content.Address) ? content.Address.Trim() : null;
    }

    private async Task EnsureSlugFreeAsync(Guid? clubId, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await db.PartnerStores.AsNoTracking().AnyAsync(s => s.ClubId == clubId && s.Slug == slug && s.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經被其他特約店家使用，請換一個。");
        }
    }

    private static bool IsActive(DateOnly? start, DateOnly? end, DateOnly today)
        => (start is null || start <= today) && (end is null || end >= today);

    public static string StatusLabel(string status) => status == "published" ? "上架" : "下架";

    public static string TierLabel(string tier) => tier == "fan_club" ? "限付費會員" : "全會員";

    private string? Thumb(string? key) => key is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(key));

    private static string? ReadHours(string? stored) => JsonColumn.UnwrapText(stored);

    private AdminPartnerStoreDetailDto ToDetail(PartnerStore store)
    {
        var zh = store.PartnerStoresI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = store.PartnerStoresI18ns.FirstOrDefault(i => i.Locale == "en");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new AdminPartnerStoreDetailDto
        {
            Id = store.Id, Slug = store.Slug, IsShared = store.ClubId is null, Category = store.Category, Region = store.Region,
            Address = store.Address, Lat = store.Lat, Lng = store.Lng, Phone = store.Phone, BusinessHours = ReadHours(store.BusinessHours),
            MapUrl = store.MapUrl, WebsiteUrl = store.WebsiteUrl, ApplicableTier = store.ApplicableTier, ApplicableTierLabel = TierLabel(store.ApplicableTier),
            StartOn = store.StartOn, EndOn = store.EndOn, IsActive = IsActive(store.StartOn, store.EndOn, today), SortOrder = store.SortOrder,
            Status = store.Status, StatusLabel = StatusLabel(store.Status), ImageKey = store.ImageKey, ImageUrl = imageUrls.Resolve(store.ImageKey),
            Zh = new AdminStoreLocaleContent { Name = zh?.Name ?? "", Address = store.Address, OfferContent = zh?.OfferContent },
            En = en is null ? null : new AdminStoreLocaleContent { Name = en.Name ?? "", Address = en.Address, OfferContent = en.OfferContent },
            CreatedAt = store.CreatedAt, UpdatedAt = store.UpdatedAt,
        };
    }
}
