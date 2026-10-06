using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminComics;

/// <summary>
/// F1 漫畫管理（主站規劃書 §4.6 F1）：企劃設定（8.1 世界觀說明）、角色、集數與內頁圖。
/// 🔴 <b>台中藍鯨不設漫畫</b>（藍鯨規劃書 v1.9 §1.3／§2.1）：所有端點在授權之後一律先過 <see cref="EnsureSupported"/>，
/// 對藍鯨回 403，系統管理員也一樣（不是權限問題，是這個俱樂部沒有這個功能）。
/// 集數「是否為最新集數」由系統自動判定（已發布且發布日不晚於今天的最大集數），每次異動後重算，不是人工勾選。
/// 全部集數免費公開閱讀，本模組不設付費牆欄位。寫入走 EF Core；漫畫尚無公開讀取端點，因此不需要快取失效。
/// </summary>
public sealed class AdminComicsRepository(ClubDbContext db, IImagePublicUrlResolver imageUrls, ClubTextSettings texts)
{
    internal const string SettingsGroup = "comic";
    internal const string KeyAboutTitle = "comic.about_title";
    internal const string KeyAboutBody = "comic.about_body";
    private static readonly string[] AboutKeys = [KeyAboutTitle, KeyAboutBody];

    public static readonly IReadOnlyDictionary<string, string> StatusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["draft"] = "草稿",
        ["published"] = "已發布",
    };

    /// <summary>藍鯨不設漫畫。以俱樂部代碼判斷（<c>bw</c>），其餘俱樂部一律可用。</summary>
    public static void EnsureSupported(AdminClubScope scope)
    {
        if (string.Equals(scope.ClubCode, "bw", StringComparison.OrdinalIgnoreCase))
        {
            throw new FeatureNotAvailableException("台中藍鯨不設漫畫，這個功能只在台中磐石使用。");
        }
    }

    // ═════════════ 企劃設定 ═════════════

    public async Task<AdminComicAboutDto> GetAboutAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var map = await db.Settings.AsNoTracking().Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && AboutKeys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, StringComparer.Ordinal, cancellationToken);
        return BuildAbout(map);
    }

    public async Task<AdminComicAboutDto> UpdateAboutAsync(AdminClubScope scope, UpdateAdminComicAboutRequest request, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var titleZh = AdminInput.OptionalText(request.Zh.Title, "中文標題", 200, "titleZh");
        var bodyZh = AdminInput.OptionalText(request.Zh.Body, "中文世界觀說明", 20000, "bodyZh");
        var titleEn = AdminInput.OptionalText(request.En?.Title, "英文標題", 200, "titleEn");
        var bodyEn = AdminInput.OptionalText(request.En?.Body, "英文世界觀說明", 20000, "bodyEn");
        var map = await texts.LoadAsync(scope.ClubId, AboutKeys, cancellationToken);
        texts.SetText(map, scope.ClubId, KeyAboutTitle, SettingsGroup, titleZh, titleEn, scope.Identity.AdminUserId);
        texts.SetText(map, scope.ClubId, KeyAboutBody, SettingsGroup, bodyZh, bodyEn, scope.Identity.AdminUserId);
        await db.SaveChangesAsync(cancellationToken);
        return BuildAbout(map);
    }

    private static AdminComicAboutDto BuildAbout(IReadOnlyDictionary<string, Setting> map)
    {
        var (titleZh, titleEn) = ClubTextSettings.Get(map, KeyAboutTitle);
        var (bodyZh, bodyEn) = ClubTextSettings.Get(map, KeyAboutBody);
        return new AdminComicAboutDto
        {
            Zh = new AdminComicAboutLocaleContent { Title = titleZh, Body = bodyZh },
            En = titleEn is null && bodyEn is null ? null : new AdminComicAboutLocaleContent { Title = titleEn, Body = bodyEn },
            UpdatedAt = ClubTextSettings.LatestUpdate(map),
        };
    }

    // ═════════════ 角色 ═════════════

    public async Task<IReadOnlyList<AdminComicCharacterDto>> ListCharactersAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var rows = await db.ComicCharacters.AsNoTracking().Include(c => c.ComicCharactersI18ns)
            .Where(c => c.ClubId == scope.ClubId).OrderBy(c => c.SortOrder).ThenBy(c => c.RowSeq).ToListAsync(cancellationToken);
        var names = await PlayerNamesAsync(rows.Select(r => r.PlayerId).OfType<Guid>().Distinct().ToList(), cancellationToken);
        return rows.Select(r => ToDto(r, names)).ToList();
    }

    public async Task<AdminComicCharacterDto?> GetCharacterAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var row = await db.ComicCharacters.AsNoTracking().Include(c => c.ComicCharactersI18ns)
            .FirstOrDefaultAsync(c => c.Id == id && c.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var names = await PlayerNamesAsync(row.PlayerId is Guid p ? [p] : [], cancellationToken);
        return ToDto(row, names);
    }

    public async Task<AdminComicCharacterDto> CreateCharacterAsync(
        AdminClubScope scope, Guid id, UpsertAdminComicCharacterRequest request, ImageFieldUpdate image, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        ValidateCharacter(request);
        await EnsurePlayerAsync(scope, request.PlayerId, cancellationToken);
        var now = DateTime.UtcNow;
        var maxOrder = await db.ComicCharacters.Where(c => c.ClubId == scope.ClubId).Select(c => (int?)c.SortOrder).MaxAsync(cancellationToken);
        var row = new ComicCharacter
        {
            Id = id, ClubId = scope.ClubId, PlayerId = request.PlayerId, SortOrder = request.SortOrder ?? (maxOrder ?? -1) + 1,
            ImageKey = image.Change ? image.Key : null, CreatedAt = now, UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        db.ComicCharacters.Add(row);
        SetCharacterI18n(row, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetCharacterAsync(scope, id, cancellationToken))!;
    }

    public async Task<AdminComicCharacterDto?> UpdateCharacterAsync(
        AdminClubScope scope, Guid id, UpsertAdminComicCharacterRequest request, ImageFieldUpdate image, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        ValidateCharacter(request);
        var row = await db.ComicCharacters.Include(c => c.ComicCharactersI18ns)
            .FirstOrDefaultAsync(c => c.Id == id && c.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        await EnsurePlayerAsync(scope, request.PlayerId, cancellationToken);
        row.PlayerId = request.PlayerId;
        if (request.SortOrder is int order)
        {
            row.SortOrder = order;
        }

        if (image.Change)
        {
            orphans.Image(row.ImageKey);
            row.ImageKey = image.Key;
        }

        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = scope.Identity.AdminUserId;
        SetCharacterI18n(row, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return await GetCharacterAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteCharacterAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var row = await db.ComicCharacters.FirstOrDefaultAsync(c => c.Id == id && c.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        orphans.Image(row.ImageKey);
        db.ComicCharacters.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReorderCharactersAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var rows = await db.ComicCharacters.Where(c => c.ClubId == scope.ClubId).OrderBy(c => c.SortOrder).ThenBy(c => c.RowSeq).ToListAsync(cancellationToken);
        var order = AdminReorder.Compute(rows.Select(r => r.Id).ToList(), ids, "角色");
        var byId = rows.ToDictionary(r => r.Id);
        for (var i = 0; i < order.Count; i++)
        {
            if (byId[order[i]].SortOrder != i)
            {
                byId[order[i]].SortOrder = i;
                byId[order[i]].UpdatedAt = DateTime.UtcNow;
                byId[order[i]].UpdatedBy = scope.Identity.AdminUserId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateCharacter(UpsertAdminComicCharacterRequest request)
    {
        AdminInput.RequireText(request.Content.Zh.Name, "中文角色名稱", 64, "nameZh");
        AdminInput.OptionalText(request.Content.Zh.Description, "中文角色設定", 20000, "descZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文角色名稱", 64, "nameEn");
            AdminInput.OptionalText(request.Content.En.Description, "英文角色設定", 20000, "descEn");
        }

        AdminInput.OptionalNonNegative(request.SortOrder, "排序", "sortOrder");
    }

    private async Task EnsurePlayerAsync(AdminClubScope scope, Guid? playerId, CancellationToken cancellationToken)
    {
        if (playerId is Guid p && !await db.Players.AsNoTracking().AnyAsync(x => x.Id == p && x.ClubId == scope.ClubId, cancellationToken))
        {
            throw new AdminValidationException("找不到指定的球員，請確認球員屬於目前的俱樂部。", "playerId");
        }
    }

    private void SetCharacterI18n(ComicCharacter row, AdminComicCharacterContentInput content)
    {
        UpsertCharacterLocale(row, RequestLocale.DefaultDbLocale, content.Zh);
        var en = row.ComicCharactersI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            UpsertCharacterLocale(row, "en", content.En);
        }
        else if (en is not null)
        {
            row.ComicCharactersI18ns.Remove(en);
            db.ComicCharactersI18ns.Remove(en);
        }
    }

    private void UpsertCharacterLocale(ComicCharacter row, string locale, AdminComicCharacterLocaleContent content)
    {
        var i18n = row.ComicCharactersI18ns.FirstOrDefault(i => i.Locale == locale);
        if (i18n is null)
        {
            i18n = new ComicCharactersI18n { ComicCharacterId = row.Id, Locale = locale };
            row.ComicCharactersI18ns.Add(i18n);
            db.ComicCharactersI18ns.Add(i18n);
        }

        i18n.Name = content.Name.Trim();
        i18n.Description = string.IsNullOrWhiteSpace(content.Description) ? null : content.Description;
    }

    private async Task<Dictionary<Guid, string>> PlayerNamesAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken)
    {
        if (playerIds.Count == 0)
        {
            return [];
        }

        var rows = await db.PlayersI18ns.AsNoTracking()
            .Where(i => playerIds.Contains(i.PlayerId) && i.Locale == RequestLocale.DefaultDbLocale && i.Name != null)
            .Select(i => new { i.PlayerId, Name = i.Name! }).ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.PlayerId, r => r.Name);
    }

    private AdminComicCharacterDto ToDto(ComicCharacter row, IReadOnlyDictionary<Guid, string> playerNames)
    {
        var zh = row.ComicCharactersI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = row.ComicCharactersI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminComicCharacterDto
        {
            Id = row.Id, PlayerId = row.PlayerId, PlayerName = row.PlayerId is Guid p && playerNames.TryGetValue(p, out var n) ? n : null,
            ImageKey = row.ImageKey, ImageUrl = ImageUrl(row.ImageKey), ImageThumbUrl = ThumbUrl(row.ImageKey), SortOrder = row.SortOrder,
            Zh = new AdminComicCharacterLocaleContent { Name = zh?.Name ?? "", Description = zh?.Description },
            En = en is null ? null : new AdminComicCharacterLocaleContent { Name = en.Name ?? "", Description = en.Description },
            UpdatedAt = row.UpdatedAt,
        };
    }

    // ═════════════ 集數 ═════════════

    public async Task<IReadOnlyList<AdminComicEpisodeListItemDto>> ListEpisodesAsync(AdminClubScope scope, string? status, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var query = db.ComicEpisodes.AsNoTracking().Where(e => e.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, StatusLabels.Keys.ToHashSet(), "狀態", "「草稿」或「已發布」");
            query = query.Where(e => e.Status == status);
        }

        var rows = await query.OrderByDescending(e => e.EpisodeNo)
            .Select(e => new
            {
                Episode = e,
                PageCount = e.ComicPages.Count,
                Zh = e.ComicEpisodesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Title).FirstOrDefault(),
                En = e.ComicEpisodesI18ns.Where(i => i.Locale == "en").Select(i => i.Title).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        return rows.Select(r => new AdminComicEpisodeListItemDto
        {
            Id = r.Episode.Id, EpisodeNo = r.Episode.EpisodeNo, CoverKey = r.Episode.CoverKey, CoverUrl = ImageUrl(r.Episode.CoverKey),
            CoverThumbUrl = ThumbUrl(r.Episode.CoverKey), PublishedOn = r.Episode.PublishedOn, Status = r.Episode.Status,
            StatusLabel = StatusLabels[r.Episode.Status], IsLatest = r.Episode.IsLatest, ViewCount = r.Episode.ViewCount,
            PageCount = r.PageCount, TitleZh = r.Zh, TitleEn = r.En, UpdatedAt = r.Episode.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminComicEpisodeDetailDto?> GetEpisodeAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var row = await db.ComicEpisodes.AsNoTracking().Include(e => e.ComicEpisodesI18ns).Include(e => e.ComicPages)
            .AsSplitQuery().FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        return row is null ? null : ToEpisodeDto(row);
    }

    public async Task<AdminComicEpisodeDetailDto> CreateEpisodeAsync(
        AdminClubScope scope, Guid id, UpsertAdminComicEpisodeRequest request, ImageFieldUpdate cover, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        ValidateEpisode(request);
        if (request.Status == "published")
        {
            throw new AdminValidationException("新集數還沒有內頁，請先存成草稿、上傳內頁後再發布。", "status");
        }

        await EnsureEpisodeNoFreeAsync(scope, request.EpisodeNo, null, cancellationToken);
        var now = DateTime.UtcNow;
        var row = new ComicEpisode
        {
            Id = id, ClubId = scope.ClubId, EpisodeNo = request.EpisodeNo, CoverKey = cover.Change ? cover.Key : null,
            PublishedOn = request.PublishedOn, Status = request.Status, CreatedAt = now, UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        db.ComicEpisodes.Add(row);
        SetEpisodeI18n(row, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        await RecalculateLatestAsync(scope.ClubId, cancellationToken);
        return (await GetEpisodeAsync(scope, id, cancellationToken))!;
    }

    public async Task<AdminComicEpisodeDetailDto?> UpdateEpisodeAsync(
        AdminClubScope scope, Guid id, UpsertAdminComicEpisodeRequest request, ImageFieldUpdate cover, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        ValidateEpisode(request);
        var row = await db.ComicEpisodes.Include(e => e.ComicEpisodesI18ns)
            .FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        if (request.Status == "published" && !await db.ComicPages.AnyAsync(p => p.ComicEpisodeId == id, cancellationToken))
        {
            throw new AdminValidationException("這一集還沒有內頁，請先上傳內頁再發布。", "status");
        }

        await EnsureEpisodeNoFreeAsync(scope, request.EpisodeNo, id, cancellationToken);
        row.EpisodeNo = request.EpisodeNo;
        row.Status = request.Status;
        row.PublishedOn = request.PublishedOn ?? (request.Status == "published" ? row.PublishedOn ?? TaiwanClock.Today : null);
        if (cover.Change)
        {
            orphans.Image(row.CoverKey);
            row.CoverKey = cover.Key;
        }

        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = scope.Identity.AdminUserId;
        SetEpisodeI18n(row, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        await RecalculateLatestAsync(scope.ClubId, cancellationToken);
        return await GetEpisodeAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteEpisodeAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var row = await db.ComicEpisodes.Include(e => e.ComicPages).FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        orphans.Image(row.CoverKey);
        foreach (var page in row.ComicPages)
        {
            orphans.Image(page.ImageKey);
        }

        db.ComicEpisodes.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        await RecalculateLatestAsync(scope.ClubId, cancellationToken);
        return true;
    }

    /// <summary>批次加入內頁（依上傳順序接在既有內頁之後）。</summary>
    public async Task<AdminComicEpisodeDetailDto?> AddPagesAsync(
        AdminClubScope scope, Guid id, IReadOnlyList<UploadedImageInfo> images, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var episode = await db.ComicEpisodes.FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        if (episode is null)
        {
            return null;
        }

        var maxOrder = await db.ComicPages.Where(p => p.ComicEpisodeId == id).Select(p => (int?)p.SortOrder).MaxAsync(cancellationToken);
        var next = (maxOrder ?? -1) + 1;
        var now = DateTime.UtcNow;
        foreach (var image in images)
        {
            db.ComicPages.Add(new ComicPage
            {
                Id = Guid.NewGuid(), ComicEpisodeId = id, ImageKey = image.Key, ImageWidth = image.Width, ImageHeight = image.Height,
                SortOrder = next++, CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
            });
        }

        episode.UpdatedAt = now;
        episode.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetEpisodeAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeletePageAsync(AdminClubScope scope, Guid id, Guid pageId, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var episode = await db.ComicEpisodes.Include(e => e.ComicPages).FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        var page = episode?.ComicPages.FirstOrDefault(p => p.Id == pageId);
        if (episode is null || page is null)
        {
            return false;
        }

        if (episode.Status == "published" && episode.ComicPages.Count == 1)
        {
            throw new AdminConflictException("不能刪除最後一張內頁", "這一集已經發布，至少要保留一張內頁；要刪除請先改回草稿。");
        }

        orphans.Image(page.ImageKey);
        db.ComicPages.Remove(page);
        episode.UpdatedAt = DateTime.UtcNow;
        episode.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminComicEpisodeDetailDto?> ReorderPagesAsync(AdminClubScope scope, Guid id, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var episode = await db.ComicEpisodes.Include(e => e.ComicPages).FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        if (episode is null)
        {
            return null;
        }

        var ordered = episode.ComicPages.OrderBy(p => p.SortOrder).ThenBy(p => p.RowSeq).ToList();
        var order = AdminReorder.Compute(ordered.Select(p => p.Id).ToList(), ids, "內頁");
        var byId = ordered.ToDictionary(p => p.Id);
        for (var i = 0; i < order.Count; i++)
        {
            byId[order[i]].SortOrder = i;
        }

        episode.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await GetEpisodeAsync(scope, id, cancellationToken);
    }

    /// <summary>重新判定「最新集數」：已發布且發布日不晚於今天（沒有發布日視為已到）的最大集數。</summary>
    internal async Task RecalculateLatestAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var today = TaiwanClock.Today;
        var rows = await db.ComicEpisodes.Where(e => e.ClubId == clubId && (e.IsLatest || e.Status == "published")).ToListAsync(cancellationToken);
        var latest = rows.Where(e => e.Status == "published" && (e.PublishedOn is null || e.PublishedOn <= today))
            .OrderByDescending(e => e.EpisodeNo).FirstOrDefault();
        var changed = false;
        foreach (var row in rows)
        {
            var should = latest is not null && row.Id == latest.Id;
            if (row.IsLatest != should)
            {
                row.IsLatest = should;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ValidateEpisode(UpsertAdminComicEpisodeRequest request)
    {
        if (request.EpisodeNo < 1)
        {
            throw new AdminValidationException("集數編號必須是 1 以上的整數。", "episodeNo");
        }

        AdminInput.OneOf(request.Status, StatusLabels.Keys.ToHashSet(), "狀態", "「草稿」或「已發布」", "status");
        AdminInput.RequireText(request.Content.Zh.Title, "中文集數標題", 128, "titleZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Title))
        {
            AdminInput.RequireText(request.Content.En.Title, "英文集數標題", 128, "titleEn");
        }
    }

    private async Task EnsureEpisodeNoFreeAsync(AdminClubScope scope, int episodeNo, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await db.ComicEpisodes.AsNoTracking().AnyAsync(e => e.ClubId == scope.ClubId && e.EpisodeNo == episodeNo && e.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("集數編號重複", $"第 {episodeNo} 集已經存在，請換一個集數編號。", "episodeNo");
        }
    }

    private void SetEpisodeI18n(ComicEpisode row, AdminComicEpisodeContentInput content)
    {
        UpsertEpisodeLocale(row, RequestLocale.DefaultDbLocale, content.Zh);
        var en = row.ComicEpisodesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Title))
        {
            UpsertEpisodeLocale(row, "en", content.En);
        }
        else if (en is not null)
        {
            row.ComicEpisodesI18ns.Remove(en);
            db.ComicEpisodesI18ns.Remove(en);
        }
    }

    private void UpsertEpisodeLocale(ComicEpisode row, string locale, AdminComicEpisodeLocaleContent content)
    {
        var i18n = row.ComicEpisodesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (i18n is null)
        {
            i18n = new ComicEpisodesI18n { ComicEpisodeId = row.Id, Locale = locale };
            row.ComicEpisodesI18ns.Add(i18n);
            db.ComicEpisodesI18ns.Add(i18n);
        }

        i18n.Title = content.Title.Trim();
    }

    private AdminComicEpisodeDetailDto ToEpisodeDto(ComicEpisode row)
    {
        var zh = row.ComicEpisodesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = row.ComicEpisodesI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminComicEpisodeDetailDto
        {
            Id = row.Id, EpisodeNo = row.EpisodeNo, CoverKey = row.CoverKey, CoverUrl = ImageUrl(row.CoverKey), CoverThumbUrl = ThumbUrl(row.CoverKey),
            PublishedOn = row.PublishedOn, Status = row.Status, StatusLabel = StatusLabels[row.Status], IsLatest = row.IsLatest, ViewCount = row.ViewCount,
            Zh = new AdminComicEpisodeLocaleContent { Title = zh?.Title ?? "" },
            En = en is null ? null : new AdminComicEpisodeLocaleContent { Title = en.Title ?? "" },
            Pages = row.ComicPages.OrderBy(p => p.SortOrder).ThenBy(p => p.RowSeq).Select(p => new AdminComicPageDto
            {
                Id = p.Id, ImageKey = p.ImageKey, ImageUrl = ImageUrl(p.ImageKey), ImageThumbUrl = ThumbUrl(p.ImageKey),
                ImageWidth = p.ImageWidth, ImageHeight = p.ImageHeight, SortOrder = p.SortOrder,
            }).ToList(),
            CreatedAt = row.CreatedAt, UpdatedAt = row.UpdatedAt,
        };
    }

    private string? ImageUrl(string? key) => key is null ? null : imageUrls.Resolve(key);

    private string? ThumbUrl(string? key) => key is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(key));
}
