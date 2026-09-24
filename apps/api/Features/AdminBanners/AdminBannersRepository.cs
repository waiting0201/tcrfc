using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;
using Tcrfc.Api.Videos;

namespace Tcrfc.Api.Features.AdminBanners;

/// <summary>
/// B3「Hero 輪播管理」（規劃書 §4.2 B3「排序、圖／影片、標題、CTA、上架期間」）。俱樂部範圍
/// （<c>banners.club_id</c> 必填，跟 <c>Competition</c> 同一類，沒有「共用內容」這件事）、
/// 沒有樂觀並行控制（理由同 <c>Features/AdminFaqs/AdminFaqsRepository.cs</c> 檔頭）。
///
/// 🔴 **草稿／發布（v3.14 新增，<c>banners.status</c>）**：新增或上傳後一律是 <c>draft</c>，
/// 發布後依既有 <see cref="Banner.StartAt"/>／<see cref="Banner.EndAt"/>（上架期間）自動顯示
/// 與下架——這是查詢時的區間過濾，不是排程轉態，<c>banners</c> 不接
/// <c>ScheduledPublishRunner</c>、不加 <c>scheduled</c> 值（docs/12 §12 第 35 點）。
/// 沒有 <c>ExpectedUpdatedAt</c> 並行權杖（跟本類別其餘寫入方法一致），透過
/// <see cref="PublishAsync"/>／<see cref="UnpublishAsync"/> 兩支專用方法切換，**沿用既有
/// <c>content.banner.update</c> 權限碼**，不是規劃書要求的新權限碼（任務指示明文「沿用
/// content.banner.* 權限碼」）。
///
/// 影片素材：見本類別 <see cref="ValidateMediaType"/>——v3.14 起已開放 <c>video</c>，格式／
/// 大小限制見 <c>Tcrfc.Api.Videos.VideoUploadOptions</c>、docs/17-deployment.md §6
/// 「Hero 輪播影片上傳」。
/// </summary>
public sealed class AdminBannersRepository(
    ClubDbContext dbContext, IQueryCache cache, IImageStorageService imageStorage, IVideoStorageService videoStorage)
{
    private const string PublicEntity = "banners";

    /// <summary><c>banners.media_type</c> 的 DB CHECK 允許 <c>image</c>／<c>video</c> 兩態
    /// （S1-7a），v3.14 起應用層也開放兩者——影片格式／大小上限見
    /// <c>Tcrfc.Api.Videos.VideoUploadOptions</c>。<see cref="ValidateMediaType"/> 設為
    /// <c>internal</c>，讓 <c>AdminBannersEndpoints</c> 能在上傳檔案之前就先驗證這個欄位，
    /// 不必等到把圖片／影片都傳上物件儲存後才發現值不合法（fail fast，避免浪費一次上傳）。</summary>
    private static readonly HashSet<string> AllowedMediaTypes = new(StringComparer.Ordinal) { "image", "video" };

    public async Task<IReadOnlyList<AdminBannerListItemDto>> ListAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Banners.AsNoTracking()
            .Where(b => b.ClubId == scope.ClubId)
            .OrderBy(b => b.SortOrder)
            .Select(b => new
            {
                b.Id,
                b.MediaType,
                b.ImageKey,
                b.ImageWidth,
                b.ImageHeight,
                b.VideoKey,
                b.Status,
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
            MediaType = r.MediaType,
            ImageKey = r.ImageKey,
            ImageWidth = r.ImageWidth,
            ImageHeight = r.ImageHeight,
            VideoKey = r.VideoKey,
            Status = r.Status,
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
    /// 成功的物件鍵——本方法完全不碰物件儲存的上傳，只負責寫資料列。<paramref name="imageWidth"/>／
    /// <paramref name="imageHeight"/> 由上傳結果自動填入（<c>UploadedImageInfo</c>，比照
    /// articles 封面同一組欄位的填法），不是呼叫端可以自訂的輸入值。<paramref name="videoKey"/>
    /// 同理是呼叫端已上傳成功的影片物件鍵，僅 <c>mediaType="video"</c> 時應該有值——本方法仍會
    /// 依 <see cref="Banner.MediaType"/> 正規化後的值決定是否真的採用（防禦性檢查，
    /// <see cref="AdminBannersEndpoints"/> 理論上已經先擋過欄位互斥，這裡是第二層）。
    /// 🔴 新增一律是 <c>draft</c>（v3.14「新增或上傳後為草稿」）——不接受呼叫端指定初始狀態。</summary>
    public async Task<AdminBannerDetailDto> CreateAsync(
        AdminClubScope scope, Guid bannerId, CreateBannerRequest request, string imageKey, int imageWidth, int imageHeight,
        string? videoKey, Guid? operatorId, CancellationToken cancellationToken)
    {
        var mediaType = ValidateMediaType(request.MediaType);
        ValidateDateRange(request.StartAt, request.EndAt);
        ValidateVideoKeyConsistency(mediaType, videoKey);

        var now = DateTime.UtcNow;
        var banner = new Banner
        {
            Id = bannerId,
            ClubId = scope.ClubId,
            MediaType = mediaType,
            ImageKey = imageKey,
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            VideoKey = mediaType == "video" ? videoKey : null,
            Status = "draft",
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
    /// <c>AdminArticlesRepository.UpdateAsync</c> 換封面圖那一段）。<paramref name="newImageWidth"/>／
    /// <paramref name="newImageHeight"/> 只在 <paramref name="newImageKey"/> 有值（這次請求真的
    /// 換了圖）時才會被採用，理由同 <see cref="CreateAsync"/>。
    ///
    /// <paramref name="newVideoKey"/> 同理是「這次請求有沒有換影片」，但**有效值的推導比圖片複雜**
    /// （v3.14 新增）：
    /// - <c>mediaType="video"</c> 且 <paramref name="newVideoKey"/> 有值——換成新影片。
    /// - <c>mediaType="video"</c> 且 <paramref name="newVideoKey"/> 為 <c>null</c>——維持既有影片
    ///   （<see cref="Banner.VideoKey"/>）；若既有值也是 <c>null</c>（例如剛從 <c>image</c> 切成
    ///   <c>video</c> 卻沒有這次上傳影片），視為驗證錯誤——影片模式必須有一支影片，不能懸空。
    /// - <c>mediaType="image"</c>——不論 <paramref name="newVideoKey"/> 是否有值（理論上呼叫端
    ///   不該在圖片模式送影片檔，<see cref="AdminBannersEndpoints"/> 已先擋過，這裡是防禦性
    ///   第二層），有效影片鍵一律清成 <c>null</c>，並在整筆更新成功後刪除舊影片物件（跟圖片
    ///   「換圖刪舊圖」同一種善後邏輯，只是換影片。</summary>
    public async Task<AdminBannerDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateBannerRequest request,
        string? newImageKey, int? newImageWidth, int? newImageHeight, string? newVideoKey,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        var mediaType = ValidateMediaType(request.MediaType);
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

        var previousVideoKey = banner.VideoKey;
        string? effectiveVideoKey;
        if (mediaType == "video")
        {
            effectiveVideoKey = newVideoKey ?? banner.VideoKey;
            if (effectiveVideoKey is null)
            {
                throw new AdminBannerValidationException("素材種類為「影片」時，必須先前已上傳過影片，或這次請求一併上傳影片檔案。");
            }
        }
        else
        {
            effectiveVideoKey = null;
        }

        banner.MediaType = mediaType;
        banner.ImageKey = effectiveImageKey;
        if (newImageKey is not null)
        {
            banner.ImageWidth = newImageWidth;
            banner.ImageHeight = newImageHeight;
        }
        banner.VideoKey = effectiveVideoKey;
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

        if (previousVideoKey is not null && !string.Equals(previousVideoKey, effectiveVideoKey, StringComparison.Ordinal))
        {
            await videoStorage.DeleteAsync(previousVideoKey, cancellationToken); // fail-open
        }

        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>回傳 <c>null</c>＝找不到（含跨俱樂部）。刪除資料列成功後才刪圖片／影片物件
    /// （規劃書 §4.0「刪資料列一併刪圖」，fail-open，不因刪圖失敗讓整個請求失敗；影片同理）。</summary>
    public async Task<bool?> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var banner = await dbContext.Banners.FirstOrDefaultAsync(b => b.Id == id && b.ClubId == scope.ClubId, cancellationToken);
        if (banner is null)
        {
            return null;
        }

        var imageKey = banner.ImageKey;
        var videoKey = banner.VideoKey;
        dbContext.Banners.Remove(banner);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(PublicEntity, scope.ClubCode, cancellationToken);

        await imageStorage.DeleteAsync(imageKey, cancellationToken); // fail-open
        await videoStorage.DeleteAsync(videoKey, cancellationToken); // fail-open
        return true;
    }

    /// <summary>發布（v3.14）：<c>status</c> → <c>published</c>。冪等——已經是 <c>published</c>
    /// 再呼叫一次不報錯，直接回傳目前狀態（比照 <c>AdminFaqCategoriesRepository</c> 對
    /// <c>IsEnabled</c> 這類簡單開關的既有寬鬆處理，不像 <c>Article</c> 的狀態機那樣限制
    /// 「只有草稿或排程中才能發布」——輪播沒有複雜的狀態轉換規則，發布後也允許改回草稿再發布）。
    /// 回傳 <c>null</c>＝找不到（含跨俱樂部）。</summary>
    public Task<AdminBannerDetailDto?> PublishAsync(AdminClubScope scope, Guid id, Guid? operatorId, CancellationToken cancellationToken)
        => SetStatusAsync(scope, id, "published", operatorId, cancellationToken);

    /// <summary>改回草稿（v3.14）：<c>status</c> → <c>draft</c>。同 <see cref="PublishAsync"/>，冪等。</summary>
    public Task<AdminBannerDetailDto?> UnpublishAsync(AdminClubScope scope, Guid id, Guid? operatorId, CancellationToken cancellationToken)
        => SetStatusAsync(scope, id, "draft", operatorId, cancellationToken);

    private async Task<AdminBannerDetailDto?> SetStatusAsync(
        AdminClubScope scope, Guid id, string status, Guid? operatorId, CancellationToken cancellationToken)
    {
        var banner = await dbContext.Banners.FirstOrDefaultAsync(b => b.Id == id && b.ClubId == scope.ClubId, cancellationToken);
        if (banner is null)
        {
            return null;
        }

        if (banner.Status != status)
        {
            banner.Status = status;
            banner.UpdatedAt = DateTime.UtcNow;
            banner.UpdatedBy = operatorId;
            await dbContext.SaveChangesAsync(cancellationToken);
            await cache.InvalidateAsync(PublicEntity, scope.ClubCode, cancellationToken);
        }

        return await GetByIdAsync(scope, id, cancellationToken);
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
        existing.ImageAlt = content.ImageAlt;
        existing.Cta1Label = content.Cta1Label;
        existing.Cta1Url = content.Cta1Url;
        existing.Cta2Label = content.Cta2Label;
        existing.Cta2Url = content.Cta2Url;
    }

    /// <summary>回傳正規化後的 <c>media_type</c>（省略即回退 <c>image</c>）。
    /// 🔴 <c>internal</c>（不是 <c>private</c>）：<see cref="AdminBannersEndpoints"/> 需要在
    /// 上傳任何檔案**之前**就先驗證這個欄位是否合法，fail fast，避免對一個註定會被拒絕的請求
    /// 白白做了圖片／影片上傳（見類別檔頭「影片素材」段）。</summary>
    internal static string ValidateMediaType(string? mediaType)
    {
        var normalized = mediaType ?? "image";
        if (!AllowedMediaTypes.Contains(normalized))
        {
            throw new AdminBannerValidationException("素材種類只能是「image」（圖片）或「video」（影片）。");
        }
        return normalized;
    }

    /// <summary>防禦性第二層檢查（<see cref="AdminBannersEndpoints"/> 理論上已經先擋過）：
    /// <c>image</c> 模式不該帶影片鍵，<c>video</c> 模式在建立時必須帶影片鍵（更新時的「維持既有
    /// 影片」邏輯在 <see cref="UpdateAsync"/> 內另外處理，不經過這個方法）。</summary>
    private static void ValidateVideoKeyConsistency(string mediaType, string? videoKey)
    {
        if (mediaType == "video" && videoKey is null)
        {
            throw new AdminBannerValidationException("素材種類為「影片」時，必須上傳影片檔案。");
        }
        if (mediaType == "image" && videoKey is not null)
        {
            throw new AdminBannerValidationException("素材種類為「圖片」時，不可上傳影片檔案。");
        }
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
            MediaType = banner.MediaType,
            ImageKey = banner.ImageKey,
            ImageWidth = banner.ImageWidth,
            ImageHeight = banner.ImageHeight,
            VideoKey = banner.VideoKey,
            Status = banner.Status,
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
        ImageAlt = i18n?.ImageAlt,
        Cta1Label = i18n?.Cta1Label,
        Cta1Url = i18n?.Cta1Url,
        Cta2Label = i18n?.Cta2Label,
        Cta2Url = i18n?.Cta2Url,
    };
}
