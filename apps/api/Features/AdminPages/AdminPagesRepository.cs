using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPages;

/// <summary>
/// 後台頁面管理（B1）寫入與後台專用讀取。形狀比照
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
public sealed class AdminPagesRepository(ClubDbContext dbContext, IQueryCache cache, IImageStorageService imageStorage)
{
    /// <summary>公開讀取 API 用的 entity 名稱，跟 <see cref="Features.Pages.PagesRepository"/>、
    /// <see cref="Features.News.ScheduledPublishRunner"/> 三處字面值必須完全一致（docs/17 §4
    /// write-invalidate，字串比對版本號命名空間）。</summary>
    public const string PublicDetailEntity = "page-detail";

    // ───────────────────────────── 讀取（後台專用，含全部狀態） ─────────────────────────────

    public async Task<PagedResult<AdminPageListItemDto>> ListAsync(
        AdminClubScope scope, string? status, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Pages.AsNoTracking().Where(p => p.ClubId == scope.ClubId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p => p.Slug.Contains(keyword)
                || p.PagesI18ns.Any(i => i.SeoTitle != null && i.SeoTitle.Contains(keyword)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.RowSeq)
            .Skip((page - 1) * pageSize).Take(pageSize)
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

        var items = rows.Select(r => new AdminPageListItemDto
        {
            Id = r.Id,
            Slug = r.Slug,
            Status = r.Status,
            PublishedAt = r.PublishedAt,
            UpdatedAt = r.UpdatedAt,
            SeoTitleZh = r.SeoTitleZh,
            SeoTitleEn = r.SeoTitleEn,
        }).ToList();

        return new PagedResult<AdminPageListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    /// <summary>跨俱樂部（真的存在但屬於別的俱樂部）與真的不存在一律回傳 <c>null</c>（404），
    /// 不透露這個 id 存在於別的俱樂部——與 <c>AdminArticlesRepository</c> 對共用內容以外的行為一致，
    /// 差別是頁面沒有「共同內容」這個第三種情況（<c>pages.club_id</c> 必填）。</summary>
    public async Task<AdminPageDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var page = await dbContext.Pages.AsNoTracking()
            .Include(p => p.PagesI18ns)
            .Include(p => p.PageBlocks)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (page is null || page.ClubId != scope.ClubId)
        {
            return null;
        }

        var latestVersion = await dbContext.PageVersions.AsNoTracking()
            .Where(v => v.PageId == id)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

        return ToDetailDto(page, latestVersion);
    }

    // ───────────────────────────── 寫入 ─────────────────────────────

    /// <summary>
    /// <paramref name="pageId"/> 由呼叫端（<see cref="AdminPagesEndpoints"/>）先產生——理由與
    /// <c>AdminArticlesRepository.CreateAsync</c> 完全相同：區塊裡的圖片物件鍵路徑
    /// （<c>{club}/pages/{pageId}/blocks/{blockIndex}/{path}</c>）需要知道「這張圖屬於哪一筆
    /// 將要建立的資料列」，但這裡的上傳其實發生在本方法內部（<see cref="ResolveBlocksAsync"/>），
    /// 不是像新聞封面那樣先在端點層上傳完才呼叫進來——因為頁面的圖片藏在區塊 JSON 的任意位置，
    /// 只有驗證流程本身知道「這個區塊的這個路徑是不是待上傳」。因此本方法自己負責失敗時的補償刪除
    /// （E-47：一律用 <see cref="CancellationToken.None"/>，不沿用觸發失敗的請求 token）。
    /// </summary>
    public async Task<AdminPageDetailDto> CreateAsync(
        AdminClubScope scope, Guid pageId, CreatePageRequest request, IFormFileCollection files, Guid? operatorId, CancellationToken cancellationToken)
    {
        PageSlugPolicy.Validate(request.Slug);

        if (await dbContext.Pages.AsNoTracking().AnyAsync(p => p.ClubId == scope.ClubId && p.Slug == request.Slug, cancellationToken))
        {
            throw new PageSlugConflictException(request.Slug);
        }

        var uploadedKeys = new List<string>();
        try
        {
            var blocksContent = await ResolveBlocksAsync(scope, pageId, request.Blocks, files, uploadedKeys, cancellationToken);

            var now = DateTime.UtcNow;
            var page = new Page
            {
                Id = pageId,
                ClubId = scope.ClubId,
                Slug = request.Slug,
                Status = "draft", // 🔴 一律從草稿開始，狀態轉換是獨立端點（Publish／Schedule），比照 Article
                PublishedAt = null,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = operatorId,
                UpdatedBy = operatorId,
            };

            dbContext.Pages.Add(page);
            ApplySeo(page, request.Seo);
            ReplaceBlocks(page, blocksContent, operatorId, now);
            await AddVersionSnapshotAsync(page, request.Seo, blocksContent, operatorId, now, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await InvalidatePublicCacheAsync(scope, cancellationToken);

            return (await GetByIdAsync(scope, page.Id, cancellationToken))!;
        }
        catch
        {
            // 補償交易：區塊裡已經真的上傳成功的圖片，若資料列最終沒有寫入成功（slug 重複、
            // 區塊內容驗證失敗、SaveChanges 失敗……），不留下孤兒物件。E-47：一律
            // CancellationToken.None，不沿用可能已經被取消的請求 token。
            foreach (var key in uploadedKeys)
            {
                await imageStorage.DeleteAsync(key, CancellationToken.None);
            }

            throw;
        }
    }

    /// <summary>整份取代語意：<paramref name="request"/> 的 <c>Blocks</c> 是這個頁面之後應有的
    /// 完整清單，省略的既有區塊視為刪除；換掉／移除的圖片在成功寫入後才刪除舊物件
    /// （規劃書 §4.0「換圖與刪除」），失敗時新上傳的物件走跟 <see cref="CreateAsync"/> 相同的補償刪除。</summary>
    public async Task<AdminPageDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdatePageRequest request, IFormFileCollection files, Guid? operatorId, CancellationToken cancellationToken)
    {
        PageSlugPolicy.Validate(request.Slug);

        var page = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (page is null)
        {
            return null;
        }

        if (!string.Equals(page.Slug, request.Slug, StringComparison.Ordinal)
            && await dbContext.Pages.AsNoTracking().AnyAsync(p => p.ClubId == scope.ClubId && p.Slug == request.Slug && p.Id != id, cancellationToken))
        {
            throw new PageSlugConflictException(request.Slug);
        }

        ApplyConcurrencyToken(page, request.ExpectedUpdatedAt);

        var previousImageKeys = ExtractPageImageKeys(page);

        var uploadedKeys = new List<string>();
        try
        {
            var blocksContent = await ResolveBlocksAsync(scope, id, request.Blocks, files, uploadedKeys, cancellationToken);

            var now = DateTime.UtcNow;
            page.Slug = request.Slug;
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

            return await GetByIdAsync(scope, id, cancellationToken);
        }
        catch
        {
            foreach (var key in uploadedKeys)
            {
                await imageStorage.DeleteAsync(key, CancellationToken.None);
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
            throw new AdminPageValidationException("排程發布時間必須晚於現在。");
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

    /// <summary>回傳 <c>null</c>＝找不到（含跨俱樂部），<c>true</c>＝刪除成功。DB 的
    /// <c>ON DELETE CASCADE</c>（<c>pages_i18n</c>／<c>page_blocks</c>／<c>page_versions</c>）
    /// 負責關聯列，這裡只需要另外處理圖片物件（規劃書 §4.0「刪除資料列一併刪除其圖片物件」，
    /// 資料庫沒有能力連帶刪除物件儲存裡的檔案）。</summary>
    public async Task<bool?> DeleteAsync(AdminClubScope scope, Guid id, DateTime expectedUpdatedAt, CancellationToken cancellationToken)
    {
        var page = await LoadTrackedForWriteAsync(scope, id, cancellationToken);
        if (page is null)
        {
            return null;
        }

        ApplyConcurrencyToken(page, expectedUpdatedAt);
        var imageKeys = ExtractPageImageKeys(page);

        dbContext.Pages.Remove(page);

        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        await InvalidatePublicCacheAsync(scope, cancellationToken);

        foreach (var key in imageKeys)
        {
            await imageStorage.DeleteAsync(key, cancellationToken);
        }

        return true;
    }

    // ───────────────────────────── 版本歷程與還原 ─────────────────────────────

    public async Task<PagedResult<AdminPageVersionListItemDto>?> ListVersionsAsync(
        AdminClubScope scope, Guid pageId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var owns = await dbContext.Pages.AsNoTracking().AnyAsync(p => p.Id == pageId && p.ClubId == scope.ClubId, cancellationToken);
        if (!owns)
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
        var owns = await dbContext.Pages.AsNoTracking().AnyAsync(p => p.Id == pageId && p.ClubId == scope.ClubId, cancellationToken);
        if (!owns)
        {
            return null;
        }

        var version = await dbContext.PageVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.PageId == pageId && v.VersionNo == versionNo, cancellationToken);

        return version is null ? null : ToVersionDetailDto(version);
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

        if (page is null || page.ClubId != scope.ClubId)
        {
            // ⛔ 沒有「共同內容唯讀」這個分支——pages.club_id 必填，不像 articles 可為 NULL
            // （docs/14-invariants.md：50 張必填／9 張可為空，pages 在必填的 50 張之列）。
            return null;
        }

        return page;
    }

    private async Task<List<(string BlockType, JsonNode Content)>> ResolveBlocksAsync(
        AdminClubScope scope, Guid pageId, IReadOnlyList<AdminPageBlockInput> blocks, IFormFileCollection files,
        List<string> uploadedKeys, CancellationToken cancellationToken)
    {
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

    private static AdminPageDetailDto ToDetailDto(Page page, PageVersion? latestVersion)
    {
        var zh = page.PagesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = page.PagesI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminPageDetailDto
        {
            Id = page.Id,
            Slug = page.Slug,
            Status = page.Status,
            PublishedAt = page.PublishedAt,
            UpdatedAt = page.UpdatedAt,
            Zh = new AdminPageSeoLocaleContent { SeoTitle = zh?.SeoTitle, SeoDescription = zh?.SeoDescription },
            En = en is null ? null : new AdminPageSeoLocaleContent { SeoTitle = en.SeoTitle, SeoDescription = en.SeoDescription },
            Blocks = page.PageBlocks.OrderBy(b => b.SortOrder).Select(ToBlockDto).ToList(),
            LatestVersionNo = latestVersion?.VersionNo ?? 0,
            PreviewToken = latestVersion?.PreviewToken,
        };
    }

    private static AdminPageBlockDto ToBlockDto(PageBlock block) => new()
    {
        Id = block.Id,
        BlockType = block.BlockType,
        Content = ParseContentElement(block.Content),
        SortOrder = block.SortOrder,
    };

    private static AdminPageVersionDetailDto ToVersionDetailDto(PageVersion version)
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
                    Content = content is null ? default : JsonDocument.Parse(content.ToJsonString()).RootElement.Clone(),
                    SortOrder = blocks.Count,
                });
            }
        }

        return new AdminPageVersionDetailDto
        {
            VersionNo = version.VersionNo,
            CreatedAt = version.CreatedAt,
            CreatedBy = version.CreatedBy,
            PreviewToken = version.PreviewToken,
            Zh = new AdminPageSeoLocaleContent { SeoTitle = Str(zhNode?["seoTitle"]), SeoDescription = Str(zhNode?["seoDescription"]) },
            En = enNode is null ? null : new AdminPageSeoLocaleContent { SeoTitle = Str(enNode["seoTitle"]), SeoDescription = Str(enNode["seoDescription"]) },
            Blocks = blocks,
        };
    }

    private static JsonElement ParseContentElement(string? content)
        => string.IsNullOrWhiteSpace(content) ? default : JsonDocument.Parse(content).RootElement.Clone();

    private async Task InvalidatePublicCacheAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => await cache.InvalidateAsync(PublicDetailEntity, scope.ClubCode, cancellationToken);
}
