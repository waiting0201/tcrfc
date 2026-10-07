using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPress;

/// <summary>
/// B6 媒體專區（主站規劃書 §4.2 B6，產出前台 7.8）：新聞稿、品牌識別包（Logo／CIS）、高解析圖三類資源。
/// 檔案與封面屬於該筆資源列（無媒體庫，§4.0）。<c>press_resources.club_id</c> 可為空：共同列只讀。
///
/// **檔案處理（本輪執行層決定）**：新聞稿與品牌識別包是 PDF／ZIP（<c>Documents</c> 公開容器，不轉檔）；
/// 「高解析圖」依 §4.0 圖片上傳通則走圖片管線（重新編碼為 WebP、長邊上限 2560px、去 EXIF／GPS），
/// 主檔即 <c>file_key</c>，封面直接用主檔的 640px 衍生檔（不另傳封面）。⚠️ 這讓「高解析」受 2560px 上限約束，
/// 是否對媒體專區例外保留原檔尺寸，規劃書沒寫，列入待裁決。
/// 下載次數（<c>download_count</c>）由前台下載端點累加，後台唯讀。
/// </summary>
public sealed class AdminPressRepository(ClubDbContext dbContext, IQueryCache cache, IImagePublicUrlResolver imageUrls, IDocumentPublicUrlResolver documentUrls)
{
    public const string CacheEntity = "press";
    public const string TypePressRelease = "press_release";
    public const string TypeBrandKit = "brand_kit";
    public const string TypeHiresImage = "hires_image";

    public static readonly HashSet<string> ResourceTypes = new(StringComparer.Ordinal) { TypePressRelease, TypeBrandKit, TypeHiresImage };
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "draft", "published" };
    private const int MaxBatchSize = 200;

    public static bool IsImageType(string resourceType) => resourceType == TypeHiresImage;

    public async Task<PagedResult<AdminPressListItemDto>> ListAsync(
        AdminClubScope scope, string? resourceType, string? status, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.PressResources.AsNoTracking().Where(r => r.ClubId == scope.ClubId || r.ClubId == null);
        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            AdminInput.OneOf(resourceType, ResourceTypes, "類別", "「新聞稿」「品牌識別包」或「高解析圖」");
            query = query.Where(r => r.ResourceType == resourceType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, Statuses, "狀態", "「草稿」或「已發布」");
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(r => r.Slug.Contains(k) || r.PressResourcesI18ns.Any(i =>
                (i.Title != null && i.Title.Contains(k)) || (i.Description != null && i.Description.Contains(k))));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(r => r.SortOrder).ThenByDescending(r => r.PublishedOn).ThenByDescending(r => r.RowSeq)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.Slug, IsShared = r.ClubId == null, r.ResourceType, r.Status, r.PublishedOn, r.SortOrder, r.DownloadCount,
                r.FileBytes, r.FileKey, r.CoverKey, r.UpdatedAt,
                TitleZh = r.PressResourcesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Title).FirstOrDefault(),
                TitleEn = r.PressResourcesI18ns.Where(i => i.Locale == "en").Select(i => i.Title).FirstOrDefault(),
            }).ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminPressListItemDto
        {
            Id = r.Id, Slug = r.Slug, IsShared = r.IsShared, ResourceType = r.ResourceType, Status = r.Status, PublishedOn = r.PublishedOn,
            SortOrder = r.SortOrder, DownloadCount = r.DownloadCount, FileBytes = r.FileBytes,
            CoverThumbUrl = CoverThumbUrl(r.ResourceType, r.FileKey, r.CoverKey), TitleZh = r.TitleZh, TitleEn = r.TitleEn, UpdatedAt = r.UpdatedAt,
        }).ToList();
        return new PagedResult<AdminPressListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<AdminPressDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var resource = await dbContext.PressResources.AsNoTracking().Include(r => r.PressResourcesI18ns)
            .FirstOrDefaultAsync(r => r.Id == id && (r.ClubId == scope.ClubId || r.ClubId == null), cancellationToken);
        return resource is null ? null : ToDetail(resource);
    }

    /// <summary>建立。<paramref name="file"/> 是已上傳的檔案（文件類為 <see cref="UploadedDocumentInfo"/>，圖片類為
    /// <see cref="UploadedImageInfo"/>，兩者擇一由呼叫端依類別上傳）。</summary>
    public async Task<AdminPressDetailDto> CreateAsync(
        AdminClubScope scope, Guid id, UpsertAdminPressRequest request, string fileKey, long fileBytes, UploadedImageInfo? cover,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        var slug = Validate(request) ?? AdminInput.GenerateSlug("press", request.Content.En?.Title);
        await EnsureSlugFreeAsync(scope, slug, null, cancellationToken);

        var now = DateTime.UtcNow;
        var resource = new PressResource
        {
            Id = id, ClubId = scope.ClubId, Slug = slug, FileKey = fileKey, FileBytes = ToInt(fileBytes),
            CoverKey = cover?.Key, CoverWidth = cover?.Width, CoverHeight = cover?.Height,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        Apply(resource, request);
        dbContext.PressResources.Add(resource);
        SetI18n(resource, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return (await GetByIdAsync(scope, id, cancellationToken))!;
    }

    /// <summary>更新。<paramref name="newFile"/> 非空＝換檔（舊檔登記到 <paramref name="orphans"/>）；類別在圖片類與文件類
    /// 之間切換時必須同時換檔（呼叫端驗證，這裡再擋一次）。</summary>
    public async Task<AdminPressDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminPressRequest request, (string Key, long Bytes)? newFile, ImageFieldUpdate cover,
        OrphanedObjects orphans, Guid? operatorId, CancellationToken cancellationToken)
    {
        var newSlug = Validate(request);
        var resource = await dbContext.PressResources.Include(r => r.PressResourcesI18ns)
            .FirstOrDefaultAsync(r => r.Id == id && (r.ClubId == scope.ClubId || r.ClubId == null), cancellationToken);
        if (resource is null)
        {
            return null;
        }

        if (resource.ClubId is null)
        {
            throw new SharedContentReadOnlyException("媒體資源");
        }

        if (IsImageType(resource.ResourceType) != IsImageType(request.ResourceType) && newFile is null)
        {
            throw new AdminValidationException("把類別改成「高解析圖」或從「高解析圖」改成其他類別時，必須同時重新上傳對應格式的檔案。", "file");
        }

        if (newSlug is not null && !string.Equals(newSlug, resource.Slug, StringComparison.Ordinal))
        {
            await EnsureSlugFreeAsync(scope, newSlug, id, cancellationToken);
            resource.Slug = newSlug;
        }

        if (newFile is { } nf)
        {
            RegisterOldFile(orphans, resource);
            resource.FileKey = nf.Key;
            resource.FileBytes = ToInt(nf.Bytes);
        }

        if (cover.Change)
        {
            orphans.Image(resource.CoverKey);
            resource.CoverKey = cover.Key;
            resource.CoverWidth = cover.Width;
            resource.CoverHeight = cover.Height;
        }

        Apply(resource, request);
        resource.UpdatedAt = DateTime.UtcNow;
        resource.UpdatedBy = operatorId;
        SetI18n(resource, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var resource = await dbContext.PressResources.FirstOrDefaultAsync(r => r.Id == id && (r.ClubId == scope.ClubId || r.ClubId == null), cancellationToken);
        if (resource is null)
        {
            return false;
        }

        if (resource.ClubId is null)
        {
            throw new SharedContentReadOnlyException("媒體資源");
        }

        RegisterOldFile(orphans, resource);
        orphans.Image(resource.CoverKey);
        dbContext.PressResources.Remove(resource);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return true;
    }

    public async Task ReorderAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count)
        {
            throw new AdminValidationException("排序清單不可為空，也不可重複。");
        }

        var rows = await dbContext.PressResources.Where(r => r.ClubId == scope.ClubId).OrderBy(r => r.SortOrder).ThenBy(r => r.RowSeq).ToListAsync(cancellationToken);
        var byId = rows.ToDictionary(r => r.Id);
        if (ids.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("排序清單含有不存在（或共用）的資源，請重新整理後再試。");
        }

        var order = ids.Concat(rows.Select(r => r.Id).Where(i => !ids.Contains(i))).ToList();
        var now = DateTime.UtcNow;
        for (var i = 0; i < order.Count; i++)
        {
            var r = byId[order[i]];
            if (r.SortOrder != i)
            {
                r.SortOrder = i;
                r.UpdatedAt = now;
                r.UpdatedBy = operatorId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
    }

    // ── 批次操作（規劃書 B6「批次改類別、批次顯示／隱藏」）────────────────────────

    public Task<BatchOperationResultDto> BatchSetStatusAsync(
        AdminClubScope scope, IReadOnlyList<Guid> ids, bool publish, Guid? operatorId, CancellationToken cancellationToken)
        => BatchAsync(scope, ids, (r, _) =>
        {
            r.Status = publish ? "published" : "draft";
            if (publish && r.PublishedOn is null)
            {
                r.PublishedOn = DateOnly.FromDateTime(DateTime.UtcNow);
            }

            return null;
        }, operatorId, cancellationToken);

    public Task<BatchOperationResultDto> BatchChangeTypeAsync(
        AdminClubScope scope, BatchChangePressTypeRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.ResourceType, ResourceTypes, "類別", "「新聞稿」「品牌識別包」或「高解析圖」");
        return BatchAsync(scope, request.Ids, (r, _) =>
        {
            if (IsImageType(r.ResourceType) != IsImageType(request.ResourceType))
            {
                return "高解析圖與其他類別的檔案格式不同，無法直接改類別，請個別編輯並重新上傳檔案。";
            }

            r.ResourceType = request.ResourceType;
            return null;
        }, operatorId, cancellationToken);
    }

    private async Task<BatchOperationResultDto> BatchAsync(
        AdminClubScope scope, IReadOnlyList<Guid> ids, Func<PressResource, int, string?> apply, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (ids.Count == 0 || ids.Count > MaxBatchSize)
        {
            throw new AdminValidationException($"批次操作一次需選擇 1 到 {MaxBatchSize} 筆資源。");
        }

        var skipped = new List<BatchSkippedItemDto>();
        var updated = 0;
        var now = DateTime.UtcNow;
        var distinct = ids.Distinct().ToList();
        var rows = await dbContext.PressResources.Where(r => distinct.Contains(r.Id) && (r.ClubId == scope.ClubId || r.ClubId == null)).ToListAsync(cancellationToken);
        var byId = rows.ToDictionary(r => r.Id);
        foreach (var id in distinct)
        {
            if (!byId.TryGetValue(id, out var row))
            {
                skipped.Add(new BatchSkippedItemDto { Id = id, Reason = "找不到這筆資源。" });
                continue;
            }

            if (row.ClubId is null)
            {
                skipped.Add(new BatchSkippedItemDto { Id = id, Reason = "這是兩隊共用的資源，僅系統管理員可以編輯。" });
                continue;
            }

            var reason = apply(row, updated);
            if (reason is not null)
            {
                skipped.Add(new BatchSkippedItemDto { Id = id, Reason = reason });
                continue;
            }

            row.UpdatedAt = now;
            row.UpdatedBy = operatorId;
            updated++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (updated > 0)
        {
            await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        }

        return new BatchOperationResultDto { UpdatedCount = updated, Skipped = skipped };
    }

    // ── 內部 ────────────────────────────────────────────────────────

    private void RegisterOldFile(OrphanedObjects orphans, PressResource resource)
    {
        if (IsImageType(resource.ResourceType))
        {
            orphans.Image(resource.FileKey);
        }
        else
        {
            orphans.Document(DocumentBucket.Public, resource.FileKey);
        }
    }

    private static string? Validate(UpsertAdminPressRequest request)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? null : AdminInput.Slug(request.Slug.Trim());
        AdminInput.OneOf(request.ResourceType, ResourceTypes, "類別", "「新聞稿」「品牌識別包」或「高解析圖」", "resourceType");
        AdminInput.OneOf(request.Status, Statuses, "狀態", "「草稿（隱藏）」或「顯示」", "status");
        AdminInput.RequireText(request.Content.Zh.Title, "中文標題", 200, "titleZh");
        AdminInput.OptionalText(request.Content.Zh.CoverAlt, "封面圖片替代文字（中文）", 200, "coverAltZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Title))
        {
            AdminInput.RequireText(request.Content.En.Title, "英文標題", 200, "titleEn");
            AdminInput.OptionalText(request.Content.En.CoverAlt, "封面圖片替代文字（英文）", 200, "coverAltEn");
        }

        return slug;
    }

    private static void Apply(PressResource resource, UpsertAdminPressRequest request)
    {
        resource.ResourceType = request.ResourceType;
        resource.Status = request.Status;
        resource.SortOrder = request.SortOrder;
        resource.PublishedOn = request.PublishedOn
            ?? (request.Status == "published" ? resource.PublishedOn ?? DateOnly.FromDateTime(DateTime.UtcNow) : resource.PublishedOn);
    }

    private void SetI18n(PressResource resource, AdminPressContentInput content)
    {
        Upsert(resource, RequestLocale.DefaultDbLocale, content.Zh);
        var en = resource.PressResourcesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Title))
        {
            Upsert(resource, "en", content.En);
        }
        else if (en is not null)
        {
            dbContext.Remove(en);
        }
    }

    private void Upsert(PressResource resource, string locale, AdminPressLocaleContent content)
    {
        var row = resource.PressResourcesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new PressResourcesI18n { PressResourceId = resource.Id, Locale = locale };
            resource.PressResourcesI18ns.Add(row);
            dbContext.PressResourcesI18ns.Add(row);
        }

        row.Title = content.Title.Trim();
        row.Description = string.IsNullOrWhiteSpace(content.Description) ? null : content.Description;
        row.CoverAlt = string.IsNullOrWhiteSpace(content.CoverAlt) ? null : content.CoverAlt.Trim();
    }

    private async Task EnsureSlugFreeAsync(AdminClubScope scope, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await dbContext.PressResources.AsNoTracking().AnyAsync(r => r.ClubId == scope.ClubId && r.Slug == slug && r.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經被這個俱樂部的其他媒體資源使用，請換一個。", "slug");
        }
    }

    private static int? ToInt(long bytes) => (int)Math.Min(bytes, int.MaxValue);

    /// <summary>高解析圖沒有獨立封面：以主檔的縮圖代替；其餘類別用自己上傳的封面縮圖。</summary>
    private string? CoverThumbUrl(string resourceType, string fileKey, string? coverKey)
        => IsImageType(resourceType)
            ? imageUrls.Resolve(ImageObjectKey.ForThumbnail(fileKey))
            : coverKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(coverKey));

    private AdminPressDetailDto ToDetail(PressResource resource)
    {
        var zh = resource.PressResourcesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = resource.PressResourcesI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminPressDetailDto
        {
            Id = resource.Id, Slug = resource.Slug, IsShared = resource.ClubId is null, ResourceType = resource.ResourceType,
            Status = resource.Status, PublishedOn = resource.PublishedOn, SortOrder = resource.SortOrder, DownloadCount = resource.DownloadCount,
            FileKey = resource.FileKey,
            FileUrl = IsImageType(resource.ResourceType) ? imageUrls.Resolve(resource.FileKey) : documentUrls.Resolve(resource.FileKey),
            FileBytes = resource.FileBytes, CoverKey = resource.CoverKey, CoverUrl = imageUrls.Resolve(resource.CoverKey),
            CoverWidth = resource.CoverWidth, CoverHeight = resource.CoverHeight,
            Zh = new AdminPressLocaleContent { Title = zh?.Title ?? "", Description = zh?.Description, CoverAlt = zh?.CoverAlt },
            En = en is null ? null : new AdminPressLocaleContent { Title = en.Title ?? "", Description = en.Description, CoverAlt = en.CoverAlt },
            CreatedAt = resource.CreatedAt, UpdatedAt = resource.UpdatedAt,
        };
    }
}
