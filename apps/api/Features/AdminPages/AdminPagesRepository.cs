using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPages;

/// <summary>
/// 後台頁面管理（B1）寫入與後台專用讀取（「固定頁＋固定欄位」：頁面清單與區塊結構由
/// <see cref="PageTemplates"/> 決定，沒有新增／刪除頁面）。形狀比照
/// <c>Features/AdminNews/AdminArticlesRepository.cs</c>（樂觀並行、雙語側表 upsert、俱樂部範圍、
/// 狀態轉換），差異集中在三處，理由見 apps/api/README.md「B1 頁面管理」：
/// ① <c>pages.club_id</c> 必填（不像 <c>articles.club_id</c> 可為空）——**沒有「共同內容唯讀」這件事**，
/// 每個頁面都明確屬於一個俱樂部；② 區塊清單整份取代＋版本快照＋還原；③ 圖片欄位活在區塊 JSON 裡，
/// 一次請求可能有多張圖，見 <see cref="PageBlockContentProcessor"/>。
///
/// ⚠️ 寫入一律走 EF Core（<see cref="ClubDbContext"/>），唯讀查詢仍是 Dapper 的
/// <see cref="Features.Pages.PagesRepository"/>（公開端點），兩者刻意分開，跟既有新聞模組同一個設計。
/// 本類別每個公開方法一律收 <see cref="AdminClubScope"/>，不收 <see cref="ClubScope"/>
/// （理由見 <c>AdminArticlesRepository</c> 檔頭與 apps/api/README.md「新增後台端點的必要形狀」）。
/// </summary>
public sealed class AdminPagesRepository(
    ClubDbContext dbContext, IQueryCache cache, IImageStorageService imageStorage, IImagePublicUrlResolver imageUrlResolver,
    IPageTemplateCatalog catalog)
{
    /// <summary>公開讀取 API 用的 entity 名稱，跟 <see cref="Features.Pages.PagesRepository"/>、
    /// <see cref="Features.News.ScheduledPublishRunner"/> 三處字面值必須完全一致（docs/17 §4
    /// write-invalidate，字串比對版本號命名空間）。</summary>
    public const string PublicDetailEntity = "page-detail";

    // ───────────────────────────── 讀取（後台專用，含全部狀態） ─────────────────────────────

    /// <summary>
    /// 清單＝這個俱樂部的版型清單（順序依版型）合併實際頁面的狀態，<b>保證每個版型都有一列</b>。
    /// 穩健做法的選擇：版型缺頁時（例：新增版型後、舊資料庫尚未灌種子）由
    /// <see cref="EnsureTemplatePagesAsync"/> 當場補建一份<b>草稿骨架頁</b>（冪等、以 <c>(club_id, slug)</c> 唯一鍵擋並行重複），
    /// 而不是回傳沒有 id 的虛擬列——這樣畫面永遠拿得到可編輯的 <c>id</c>、狀態與預覽權杖，也不需要「首次編輯才建立」的第二條流程。
    /// 不在版型內的舊頁面（例：已退場的測試頁）不顯示、也無法編輯（404）。
    /// 分頁／關鍵字／狀態篩選保留（沿用既有信封），在記憶體內完成（清單最多十餘列）。
    /// </summary>
    public async Task<PagedResult<AdminPageListItemDto>> ListAsync(
        AdminClubScope scope, string? status, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        await EnsureTemplatePagesAsync(scope, cancellationToken);

        var templates = catalog.ForClub(scope.ClubCode);
        var slugs = templates.Select(t => t.Slug).ToList();

        var rows = await dbContext.Pages.AsNoTracking()
            .Where(p => p.ClubId == scope.ClubId && slugs.Contains(p.Slug))
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.Status,
                p.PublishedAt,
                p.UpdatedAt,
                SeoTitleZh = p.PagesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.SeoTitle).FirstOrDefault(),
                SeoTitleEn = p.PagesI18ns.Where(i => i.Locale == "en").Select(i => i.SeoTitle).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var bySlug = rows.ToDictionary(r => r.Slug, StringComparer.Ordinal);
        var items = new List<AdminPageListItemDto>();
        foreach (var template in templates)
        {
            if (!bySlug.TryGetValue(template.Slug, out var r))
            {
                continue; // 並行補建失敗的極端情況；下一次清單會再補
            }

            if (!string.IsNullOrWhiteSpace(status) && r.Status != status)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(keyword)
                && !(template.Slug.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || template.TitleZh.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || (template.TitleEn?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (r.SeoTitleZh?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (r.SeoTitleEn?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)))
            {
                continue;
            }

            items.Add(new AdminPageListItemDto
            {
                Id = r.Id,
                Slug = r.Slug,
                TemplateKey = template.Slug,
                TitleZh = template.TitleZh,
                TitleEn = template.TitleEn,
                Status = r.Status,
                PublishedAt = r.PublishedAt,
                UpdatedAt = r.UpdatedAt,
                SeoTitleZh = r.SeoTitleZh,
                SeoTitleEn = r.SeoTitleEn,
            });
        }

        var totalCount = items.Count;
        var paged = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<AdminPageListItemDto> { Items = paged, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    /// <summary>為這個俱樂部缺頁的版型補建草稿骨架頁（冪等）。骨架內容不通過內容驗證（中文必填），
    /// 編輯者必須填完才能存檔；它是草稿，公開端點看不到。<c>created_by</c> 為 <c>null</c>（系統補建，不是某位管理員的操作）。</summary>
    public async Task EnsureTemplatePagesAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var templates = catalog.ForClub(scope.ClubCode);
        var slugs = templates.Select(t => t.Slug).ToList();
        var existing = (await dbContext.Pages.AsNoTracking()
            .Where(p => p.ClubId == scope.ClubId && slugs.Contains(p.Slug))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        foreach (var template in templates.Where(t => t.ProvisionWhenMissing && !existing.Contains(t.Slug)))
        {
            var now = DateTime.UtcNow;
            var seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent() };
            var blocks = template.Blocks.Select(b => (b.BlockType, Content: PageTemplates.BuildSkeleton(b))).ToList();

            var page = new Page
            {
                Id = Guid.NewGuid(),
                ClubId = scope.ClubId,
                Slug = template.Slug,
                Status = "draft",
                CreatedAt = now,
                UpdatedAt = now,
            };

            try
            {
                dbContext.Pages.Add(page);
                ApplySeo(page, seo);
                ReplaceBlocks(page, blocks, null, now);
                await AddVersionSnapshotAsync(page, seo, blocks, null, now, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // 並行請求已補建同一頁（UQ_pages_club_slug）：放掉這次的追蹤項目，結果以資料庫現況為準。
                dbContext.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>跨俱樂部（真的存在但屬於別的俱樂部）、真的不存在、以及不在版型內的舊頁面，一律回傳 <c>null</c>（404）。</summary>
    public async Task<AdminPageDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var page = await dbContext.Pages.AsNoTracking()
            .Include(p => p.PagesI18ns)
            .Include(p => p.PageBlocks)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (page is null || page.ClubId != scope.ClubId || catalog.Find(scope.ClubCode, page.Slug) is not { } template)
        {
            return null;
        }

        var latestVersion = await dbContext.PageVersions.AsNoTracking()
            .Where(v => v.PageId == id)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

        return ToDetailDto(page, template, latestVersion);
    }

    // ───────────────────────────── 寫入 ─────────────────────────────

    /// <summary>整份取代語意：<paramref name="request"/> 的 <c>Blocks</c> 是這個頁面之後應有的
    /// 完整清單，省略的既有區塊視為刪除；換掉／移除的圖片在成功寫入後才刪除舊物件
    /// （規劃書 §4.0「換圖與刪除」），失敗時新上傳的物件一律補償刪除（E-47：用 <see cref="CancellationToken.None"/>）。</summary>
    public async Task<AdminPageDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdatePageRequest request, IFormFileCollection files,
        ImageFieldUpdate ogImageUpdate, Guid? operatorId, CancellationToken cancellationToken)
    {
        var page = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (page is null)
        {
            return null;
        }

        var template = catalog.Find(scope.ClubCode, page.Slug)!;

        ApplyConcurrencyToken(page, request.ExpectedUpdatedAt);

        var previousImageKeys = ExtractPageImageKeys(page);

        // S1-12 新增：OG 圖片同一套「換圖成功才刪舊物件」邏輯，獨立於區塊圖片的差集清理之外。
        var previousOgImageKey = page.OgImageKey;

        var uploadedKeys = new List<string>();
        try
        {
            // 網址名稱不可變更（固定頁）：有帶就必須相同。放在 try 內，失敗時一併補償刪除已上傳的 OG 圖片。
            if (request.Slug is not null && !string.Equals(request.Slug, page.Slug, StringComparison.Ordinal))
            {
                throw new AdminPageValidationException("固定頁面的網址名稱不能變更。", "slug");
            }

            var blocksContent = await ResolveBlocksAsync(scope, id, template, request.Blocks, files, uploadedKeys, cancellationToken);

            var now = DateTime.UtcNow;
            page.CanonicalPath = string.IsNullOrWhiteSpace(request.CanonicalPath) ? null : request.CanonicalPath;
            page.IsNoindex = request.IsNoindex;
            page.IsExcludedFromSitemap = request.IsExcludedFromSitemap;
            if (ogImageUpdate.Change)
            {
                page.OgImageKey = ogImageUpdate.Key;
                page.OgImageWidth = ogImageUpdate.Width;
                page.OgImageHeight = ogImageUpdate.Height;
            }

            ApplySeo(page, request.Seo);
            ReplaceBlocks(page, blocksContent, operatorId, now);
            page.UpdatedAt = now;
            page.UpdatedBy = operatorId;
            await AddVersionSnapshotAsync(page, request.Seo, blocksContent, operatorId, now, cancellationToken);

            await SaveWithConcurrencyHandlingAsync(cancellationToken);
            await InvalidatePublicCacheAsync(scope, cancellationToken);

            var newImageKeys = blocksContent
                .SelectMany(b => PageBlockContentProcessor.ExtractImageKeys(b.BlockType, b.Content))
                .ToHashSet(StringComparer.Ordinal);

            // fail-open（跟 AdminArticlesRepository 一致）：清理舊物件不用 try/catch 再包一層，
            // IImageStorageService.DeleteAsync 內部自己吞例外並記警告。這裡用請求本身的
            // cancellationToken——這不是失敗後的補償，是成功寫入後的例行清理，跟建立/更新失敗時
            // 的補償刪除（上面 catch 區塊）是兩種不同性質的刪除，故意用不同的 token。
            foreach (var oldKey in previousImageKeys.Except(newImageKeys, StringComparer.Ordinal))
            {
                await imageStorage.DeleteAsync(oldKey, cancellationToken);
            }

            if (!string.Equals(previousOgImageKey, page.OgImageKey, StringComparison.Ordinal))
            {
                await imageStorage.DeleteAsync(previousOgImageKey, cancellationToken);
            }

            return await GetByIdAsync(scope, id, cancellationToken);
        }
        catch
        {
            foreach (var key in uploadedKeys)
            {
                await imageStorage.DeleteAsync(key, CancellationToken.None);
            }

            if (ogImageUpdate.Key is not null)
            {
                await imageStorage.DeleteAsync(ogImageUpdate.Key, CancellationToken.None);
            }

            throw;
        }
    }

    public async Task<AdminPageDetailDto?> PublishAsync(
        AdminClubScope scope, Guid id, PublishPageRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var page = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (page is null)
        {
            return null;
        }

        if (page.Status is not ("draft" or "scheduled"))
        {
            throw new PageInvalidStatusTransitionException(
                $"目前狀態是「{StatusLabel(page.Status)}」，只有草稿或排程發布中的頁面可以發布。");
        }

        ApplyConcurrencyToken(page, request.ExpectedUpdatedAt);

        // 🔴 用資料庫自己的「現在」，不是應用程式行程的 DateTime.UtcNow——理由見
        // Common/DatabaseClock.cs 檔頭（2026-09-24 排查 PagesPublicEndpointTests 間歇性失敗的根因）：
        // 公開讀取用 published_at <= SYSUTCDATETIME() 判斷「已到發布時間」，若這裡寫入的時間戳
        // 來自另一個時鐘（應用程式行程的作業系統時鐘），兩個時鐘只要有任何飄移，剛發布的內容就可能
        // 暫時被判定為「還沒到發布時間」。
        var dbNow = await DatabaseClock.GetUtcNowAsync(dbContext, cancellationToken);
        page.Status = "published";
        page.PublishedAt = dbNow;
        page.UpdatedAt = dbNow;
        page.UpdatedBy = operatorId;

        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<AdminPageDetailDto?> ScheduleAsync(
        AdminClubScope scope, Guid id, SchedulePageRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (request.PublishAt <= DateTime.UtcNow)
        {
            throw new AdminPageValidationException("排程發布時間必須晚於現在。", "publishAt");
        }

        var page = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (page is null)
        {
            return null;
        }

        if (page.Status is not ("draft" or "scheduled"))
        {
            throw new PageInvalidStatusTransitionException(
                $"目前狀態是「{StatusLabel(page.Status)}」，只有草稿或排程發布中的頁面可以重新排程。");
        }

        ApplyConcurrencyToken(page, request.ExpectedUpdatedAt);

        page.Status = "scheduled";
        page.PublishedAt = request.PublishAt;
        page.UpdatedAt = DateTime.UtcNow;
        page.UpdatedBy = operatorId;

        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        return await GetByIdAsync(scope, id, cancellationToken);
    }

    // ───────────────────────────── 版本歷程與還原 ─────────────────────────────

    public async Task<PagedResult<AdminPageVersionListItemDto>?> ListVersionsAsync(
        AdminClubScope scope, Guid pageId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!await OwnsTemplatePageAsync(scope, pageId, cancellationToken))
        {
            return null;
        }

        var query = dbContext.PageVersions.AsNoTracking().Where(v => v.PageId == pageId);
        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(v => v.VersionNo)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new AdminPageVersionListItemDto
            {
                VersionNo = v.VersionNo,
                CreatedAt = v.CreatedAt,
                CreatedBy = v.CreatedBy,
                PreviewToken = v.PreviewToken,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminPageVersionListItemDto> { Items = rows, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<AdminPageVersionDetailDto?> GetVersionAsync(
        AdminClubScope scope, Guid pageId, int versionNo, CancellationToken cancellationToken)
    {
        var template = await FindTemplateOfPageAsync(scope, pageId, cancellationToken);
        if (template is null)
        {
            return null;
        }

        var version = await dbContext.PageVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.PageId == pageId && v.VersionNo == versionNo, cancellationToken);

        return version is null ? null : ToVersionDetailDto(version, template);
    }

    /// <summary>
    /// 🔴 規劃書只寫「版本歷程與還原」，沒有定義還原後的行為（是否產生新版本、是否改變發布狀態）。
    /// 本次的執行層判斷（見 apps/api/README.md「我的判斷」）：**還原＝以舊版內容產生一個新版本**，
    /// 不是把時間倒轉回去覆蓋掉中間的版本——舊版本列本身不變動、不刪除，版本歷程只會往前累加。
    /// **不改變頁面目前的發布狀態**：若目前是 <c>published</c>，還原後的內容立即對外可見（跟
    /// <c>UpdateAsync</c> 的行為一致，「編輯不需要重新送審」），若這不是預期行為，需要另外決定
    /// 「還原已發布頁面時要不要先退回草稿」——規劃書沒有這個開關，本次不擅自新增。
    /// </summary>
    public async Task<AdminPageDetailDto?> RestoreVersionAsync(
        AdminClubScope scope, Guid pageId, int versionNo, RestorePageVersionRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var page = await LoadTrackedForWriteAsync(scope, pageId, cancellationToken);
        if (page is null)
        {
            return null;
        }

        var targetVersion = await dbContext.PageVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.PageId == pageId && v.VersionNo == versionNo, cancellationToken);
        if (targetVersion is null)
        {
            throw new PageVersionNotFoundException(versionNo);
        }

        ApplyConcurrencyToken(page, request.ExpectedUpdatedAt);

        var snapshot = JsonNode.Parse(targetVersion.Snapshot ?? "{}") as JsonObject ?? new JsonObject();
        var seoNode = snapshot["seo"] as JsonObject;
        var zhNode = seoNode?["zh"] as JsonObject;
        var enNode = seoNode?["en"] as JsonObject;

        var restoredSeo = new AdminPageSeoInput
        {
            Zh = new AdminPageSeoLocaleContent { SeoTitle = Str(zhNode?["seoTitle"]), SeoDescription = Str(zhNode?["seoDescription"]) },
            En = enNode is null ? null : new AdminPageSeoLocaleContent { SeoTitle = Str(enNode["seoTitle"]), SeoDescription = Str(enNode["seoDescription"]) },
        };

        var restoredBlocks = new List<(string BlockType, JsonNode Content)>();
        if (snapshot["blocks"] is JsonArray blockArray)
        {
            foreach (var node in blockArray)
            {
                if (node is not JsonObject blockObj)
                {
                    continue;
                }

                var blockType = Str(blockObj["blockType"]) ?? string.Empty;
                var content = blockObj["content"]?.DeepClone() ?? new JsonObject();
                restoredBlocks.Add((blockType, content));
            }
        }

        // 🔴 固定頁：快照的區塊結構（數量、類型、固定列數）若與現行版型不同（例：版型改版前留下的舊版本），
        // 還原會讓頁面長出版型之外的區塊，一律擋下（400，欄位鍵 versionNo），舊版本仍可閱覽。
        var template = catalog.Find(scope.ClubCode, page.Slug)!;
        if (!PageTemplates.Matches(template, restoredBlocks.Select(b => (b.BlockType, (JsonNode?)b.Content)).ToList()))
        {
            throw new AdminPageValidationException(
                $"版本 {versionNo} 的內容結構與目前的頁面版型不同（版型已調整），無法還原。請改以手動編輯，或還原到較新的版本。", "versionNo");
        }

        var previousImageKeys = ExtractPageImageKeys(page);

        var now = DateTime.UtcNow;
        page.UpdatedAt = now;
        page.UpdatedBy = operatorId;
        ApplySeo(page, restoredSeo);
        ReplaceBlocks(page, restoredBlocks, operatorId, now);
        await AddVersionSnapshotAsync(page, restoredSeo, restoredBlocks, operatorId, now, cancellationToken);

        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        var newImageKeys = restoredBlocks
            .SelectMany(b => PageBlockContentProcessor.ExtractImageKeys(b.BlockType, b.Content))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var oldKey in previousImageKeys.Except(newImageKeys, StringComparer.Ordinal))
        {
            await imageStorage.DeleteAsync(oldKey, cancellationToken);
        }

        return await GetByIdAsync(scope, pageId, cancellationToken);
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private async Task<Page?> LoadTrackedForWriteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var page = await dbContext.Pages
            .Include(p => p.PagesI18ns)
            .Include(p => p.PageBlocks)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (page is null || page.ClubId != scope.ClubId || catalog.Find(scope.ClubCode, page.Slug) is null)
        {
            // 不在版型內的舊頁面一律視為不存在（固定頁：只有版型內的頁面可編輯）。
            // ⛔ 沒有「共同內容唯讀」這個分支——pages.club_id 必填，不像 articles 可為 NULL
            // （docs/14-invariants.md：50 張必填／9 張可為空，pages 在必填的 50 張之列）。
            return null;
        }

        return page;
    }

    private async Task<List<(string BlockType, JsonNode Content)>> ResolveBlocksAsync(
        AdminClubScope scope, Guid pageId, PageTemplate template, IReadOnlyList<AdminPageBlockInput> blocks, IFormFileCollection files,
        List<string> uploadedKeys, CancellationToken cancellationToken)
    {
        // 結構（數量／類型／順序）先於內容驗證與圖片上傳檢查：結構錯的請求不應該先花時間處理圖片。
        PageTemplates.ValidateStructure(template, blocks.Select(b => (b.BlockType, b.Key)).ToList());

        var result = new List<(string, JsonNode)>();

        for (var i = 0; i < blocks.Count; i++)
        {
            var blockIndex = i;
            var block = blocks[i];
            var content = block.Content.DeepClone();

            async Task<UploadedImageInfo?> ResolveUploadAsync(string imagePath, CancellationToken ct)
            {
                var fieldName = AdminPageRequestForm.FileFieldName(blockIndex, imagePath);
                var file = files[fieldName];
                if (file is null)
                {
                    return null;
                }

                if (file.Length == 0)
                {
                    throw new EmptyImageException();
                }

                if (file.Length > ImageUploadOptions.MaxUploadBytes)
                {
                    throw new ImageTooLargeException();
                }

                byte[] rawBytes;
                using (var buffer = new MemoryStream())
                {
                    await file.CopyToAsync(buffer, ct);
                    rawBytes = buffer.ToArray();
                }

                // 物件鍵前綴含俱樂部代碼／頁面 id／區塊索引／圖片路徑，確保「一張圖只屬於一筆資料列」
                // 的精神延伸到「一張圖只屬於一個區塊的一個圖片欄位」。
                var safePath = imagePath.Replace(':', '-');
                var objectKeyPrefix = $"{scope.ClubCode}/pages/{pageId}/blocks/{blockIndex}/{safePath}";
                var uploaded = await imageStorage.UploadAsync(rawBytes, objectKeyPrefix, ct);
                uploadedKeys.Add(uploaded.Key);
                return uploaded;
            }

            await PageBlockContentProcessor.ValidateAndResolveAsync(blockIndex, block.BlockType, content, ResolveUploadAsync, cancellationToken);
            PageTemplates.ValidateRows(template.Blocks[blockIndex], blockIndex, content);
            result.Add((block.BlockType, content));
        }

        return result;
    }

    private void ReplaceBlocks(Page page, IReadOnlyList<(string BlockType, JsonNode Content)> blocks, Guid? operatorId, DateTime now)
    {
        foreach (var existing in page.PageBlocks.ToList())
        {
            dbContext.PageBlocks.Remove(existing);
        }

        page.PageBlocks.Clear();

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = new PageBlock
            {
                Id = Guid.NewGuid(),
                PageId = page.Id,
                BlockType = blocks[i].BlockType,
                Content = blocks[i].Content.ToJsonString(),
                SortOrder = i,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = operatorId,
                UpdatedBy = operatorId,
            };

            page.PageBlocks.Add(block);
            dbContext.PageBlocks.Add(block);
        }
    }

    private async Task AddVersionSnapshotAsync(
        Page page, AdminPageSeoInput seo, IReadOnlyList<(string BlockType, JsonNode Content)> blocks,
        Guid? operatorId, DateTime now, CancellationToken cancellationToken)
    {
        var nextVersionNo = 1 + (await dbContext.PageVersions.AsNoTracking()
            .Where(v => v.PageId == page.Id)
            .Select(v => (int?)v.VersionNo)
            .MaxAsync(cancellationToken) ?? 0);

        var version = new PageVersion
        {
            Id = Guid.NewGuid(),
            PageId = page.Id,
            VersionNo = nextVersionNo,
            Snapshot = BuildSnapshotJson(seo, blocks),
            PreviewToken = GeneratePreviewToken(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        page.PageVersions.Add(version);
        dbContext.PageVersions.Add(version);
    }

    private static string BuildSnapshotJson(AdminPageSeoInput seo, IReadOnlyList<(string BlockType, JsonNode Content)> blocks)
    {
        var seoNode = new JsonObject
        {
            ["zh"] = new JsonObject { ["seoTitle"] = seo.Zh.SeoTitle, ["seoDescription"] = seo.Zh.SeoDescription },
        };

        if (seo.En is not null)
        {
            seoNode["en"] = new JsonObject { ["seoTitle"] = seo.En.SeoTitle, ["seoDescription"] = seo.En.SeoDescription };
        }

        var blocksArray = new JsonArray();
        foreach (var block in blocks)
        {
            blocksArray.Add(new JsonObject { ["blockType"] = block.BlockType, ["content"] = block.Content });
        }

        var root = new JsonObject { ["seo"] = seoNode, ["blocks"] = blocksArray };
        return root.ToJsonString();
    }

    private void ApplySeo(Page page, AdminPageSeoInput seo)
    {
        AddOrReplaceI18n(page, RequestLocale.DefaultDbLocale, seo.Zh);

        var existingEn = page.PagesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (seo.En is not null)
        {
            AddOrReplaceI18n(page, "en", seo.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
            page.PagesI18ns.Remove(existingEn);
        }
    }

    private void AddOrReplaceI18n(Page page, string locale, AdminPageSeoLocaleContent content)
    {
        var existing = page.PagesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new PagesI18n { PageId = page.Id, Locale = locale };
            page.PagesI18ns.Add(existing);
            dbContext.PagesI18ns.Add(existing);
        }

        existing.SeoTitle = content.SeoTitle;
        existing.SeoDescription = content.SeoDescription;
        existing.SeoKeywords = content.SeoKeywords;
        existing.OgImageAlt = content.OgImageAlt;
    }

    private void ApplyConcurrencyToken(Page page, DateTime expectedUpdatedAt)
        => dbContext.Entry(page).Property(p => p.UpdatedAt).OriginalValue = expectedUpdatedAt;

    private async Task SaveWithConcurrencyHandlingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PageConcurrencyConflictException();
        }
    }

    private static HashSet<string> ExtractPageImageKeys(Page page)
        => page.PageBlocks
            .SelectMany(b => PageBlockContentProcessor.ExtractImageKeys(b.BlockType, ParseContentNode(b.Content)))
            .ToHashSet(StringComparer.Ordinal);

    private static JsonNode? ParseContentNode(string? content) => string.IsNullOrWhiteSpace(content) ? null : JsonNode.Parse(content);

    private static string GeneratePreviewToken()
    {
        // 256-bit 亂數 → Base64Url，約 43 字元，落在 page_versions.preview_token nvarchar(64) 內。
        // ⚠️ 已知缺口（回報，不是本次任務範圍）：DB 沒有這個欄位的 UNIQUE 索引（新增索引屬於綱要
        // 異動，任務指示明訂遇到就停下回報、不逕自加 migration），256-bit 熵值下碰撞機率可忽略但
        // 沒有資料庫層的保證；也沒有到期或撤銷欄位，權杖一旦核發即永久有效（見 README「已知缺口」）。
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string? Str(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string StatusLabel(string status) => status switch
    {
        "draft" => "草稿",
        "published" => "已發布",
        "scheduled" => "排程發布中",
        _ => status,
    };

    private async Task<bool> OwnsTemplatePageAsync(AdminClubScope scope, Guid pageId, CancellationToken cancellationToken)
        => await FindTemplateOfPageAsync(scope, pageId, cancellationToken) is not null;

    private async Task<PageTemplate?> FindTemplateOfPageAsync(AdminClubScope scope, Guid pageId, CancellationToken cancellationToken)
    {
        var slug = await dbContext.Pages.AsNoTracking()
            .Where(p => p.Id == pageId && p.ClubId == scope.ClubId)
            .Select(p => p.Slug)
            .FirstOrDefaultAsync(cancellationToken);
        return slug is null ? null : catalog.Find(scope.ClubCode, slug);
    }

    private AdminPageDetailDto ToDetailDto(Page page, PageTemplate template, PageVersion? latestVersion)
    {
        var zh = page.PagesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = page.PagesI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminPageDetailDto
        {
            Id = page.Id,
            Slug = page.Slug,
            Template = AdminPageTemplateDto.From(template),
            Status = page.Status,
            PublishedAt = page.PublishedAt,
            UpdatedAt = page.UpdatedAt,
            CanonicalPath = page.CanonicalPath,
            IsNoindex = page.IsNoindex,
            IsExcludedFromSitemap = page.IsExcludedFromSitemap,
            OgImageUrl = imageUrlResolver.Resolve(page.OgImageKey),
            OgImageWidth = page.OgImageWidth,
            OgImageHeight = page.OgImageHeight,
            Zh = new AdminPageSeoLocaleContent { SeoTitle = zh?.SeoTitle, SeoDescription = zh?.SeoDescription, SeoKeywords = zh?.SeoKeywords, OgImageAlt = zh?.OgImageAlt },
            En = en is null ? null : new AdminPageSeoLocaleContent { SeoTitle = en.SeoTitle, SeoDescription = en.SeoDescription, SeoKeywords = en.SeoKeywords, OgImageAlt = en.OgImageAlt },
            Blocks = page.PageBlocks.OrderBy(b => b.SortOrder).Select((b, i) => ToBlockDto(b, i < template.Blocks.Count ? template.Blocks[i] : null)).ToList(),
            LatestVersionNo = latestVersion?.VersionNo ?? 0,
            PreviewToken = latestVersion?.PreviewToken,
        };
    }

    private AdminPageBlockDto ToBlockDto(PageBlock block, PageTemplateBlock? def) => new()
    {
        Id = block.Id,
        Key = def is not null && def.BlockType == block.BlockType ? def.Key : null,
        LabelZh = def is not null && def.BlockType == block.BlockType ? def.LabelZh : null,
        BlockType = block.BlockType,
        Content = ParseContentElement(block.BlockType, block.Content),
        SortOrder = block.SortOrder,
    };

    private AdminPageVersionDetailDto ToVersionDetailDto(PageVersion version, PageTemplate template)
    {
        var snapshot = JsonNode.Parse(version.Snapshot ?? "{}") as JsonObject ?? new JsonObject();
        var seoNode = snapshot["seo"] as JsonObject;
        var zhNode = seoNode?["zh"] as JsonObject;
        var enNode = seoNode?["en"] as JsonObject;

        var blocks = new List<AdminPageBlockDto>();
        if (snapshot["blocks"] is JsonArray blockArray)
        {
            foreach (var node in blockArray)
            {
                if (node is not JsonObject blockObj)
                {
                    continue;
                }

                var blockType = Str(blockObj["blockType"]) ?? string.Empty;
                var content = blockObj["content"];
                blocks.Add(new AdminPageBlockDto
                {
                    // 快照沒有保留原本的 page_blocks.id（還原後 ReplaceBlocks 一律產生新的 id），
                    // 版本詳情頁只是唯讀預覽，用 Guid.Empty 代表「這不是一筆真正存在的區塊列」。
                    Id = Guid.Empty,
                    BlockType = blockType,
                    Content = content is null ? default : ParseContentElement(blockType, content.ToJsonString()),
                    SortOrder = blocks.Count,
                });
            }
        }

        var matches = PageTemplates.Matches(
            template, blocks.Select(b => (b.BlockType, b.Content.ValueKind == JsonValueKind.Undefined ? null : JsonNode.Parse(b.Content.GetRawText()))).ToList());

        return new AdminPageVersionDetailDto
        {
            StructureMatchesTemplate = matches,
            VersionNo = version.VersionNo,
            CreatedAt = version.CreatedAt,
            CreatedBy = version.CreatedBy,
            PreviewToken = version.PreviewToken,
            Zh = new AdminPageSeoLocaleContent { SeoTitle = Str(zhNode?["seoTitle"]), SeoDescription = Str(zhNode?["seoDescription"]) },
            En = enNode is null ? null : new AdminPageSeoLocaleContent { SeoTitle = Str(enNode["seoTitle"]), SeoDescription = Str(enNode["seoDescription"]) },
            Blocks = blocks,
        };
    }

    /// <summary>解析區塊內容並補上圖片的可顯示網址（<c>url</c>／<c>thumbUrl</c>，只用於輸出，見
    /// <see cref="PageBlockContentProcessor.AttachImageUrls"/>）。</summary>
    private JsonElement ParseContentElement(string blockType, string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        var node = JsonNode.Parse(content);
        PageBlockContentProcessor.AttachImageUrls(blockType, node, imageUrlResolver.Resolve, imageUrlResolver.ResolveThumbnail);
        return JsonDocument.Parse(node!.ToJsonString()).RootElement.Clone();
    }

    private async Task InvalidatePublicCacheAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => await cache.InvalidateAsync(PublicDetailEntity, scope.ClubCode, cancellationToken);
}
