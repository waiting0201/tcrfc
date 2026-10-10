using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminComics;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Comics;

/// <summary>
/// 8.1 台中磐石漫畫／動畫的公開讀取（主站規劃書 §3.8）：關於企劃、角色、集數列表、線上閱讀器資料。
/// 🔴 <b>台中藍鯨不設漫畫</b>（藍鯨規劃書 §1.3／§2.1）：所有方法一律先過 <see cref="EnsureSupported"/>，藍鯨回 403，比照後台 F1 的擋法
/// （<see cref="AdminComicsRepository.EnsureSupported"/>）。<b>全部集數免費公開閱讀，不設付費牆、不需登入。</b>
/// 可見條件：<c>status = published</c> 且發布日不晚於今天（沒有發布日視為已到）。<c>isLatest</c> 以讀取當下的「可見集數中最大者」計算，
/// 不信任資料庫欄位（欄位只在後台異動時重算，到了發布日不會自動更新）。
/// 不快取（後台 F1 寫入尚未接公開快取失效；資料量小）。
/// </summary>
public sealed class ComicsRepository(ClubDbContext db, IImagePublicUrlResolver imageUrls)
{
    public static void EnsureSupported(ClubScope scope)
    {
        if (string.Equals(scope.ClubCode, "bw", StringComparison.OrdinalIgnoreCase))
        {
            throw new FeatureNotAvailableException("台中藍鯨不設漫畫，這個功能只在台中磐石使用。");
        }
    }

    private IQueryable<ComicEpisode> Visible(ClubScope scope)
    {
        var today = TaiwanClock.Today;
        return db.ComicEpisodes.AsNoTracking()
            .Where(e => e.ClubId == scope.ClubId && e.Status == "published" && (e.PublishedOn == null || e.PublishedOn <= today));
    }

    public async Task<ComicAboutPublicDto> GetAboutAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        string[] keys = [AdminComicsRepository.KeyAboutTitle, AdminComicsRepository.KeyAboutBody];
        var map = await db.Settings.AsNoTracking().Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && keys.Contains(s.SettingKey)).ToDictionaryAsync(s => s.SettingKey, StringComparer.Ordinal, cancellationToken);
        var (titleZh, titleEn) = ClubTextSettings.Get(map, AdminComicsRepository.KeyAboutTitle);
        var (bodyZh, bodyEn) = ClubTextSettings.Get(map, AdminComicsRepository.KeyAboutBody);
        var en = dbLocale == "en";
        return new ComicAboutPublicDto { Title = RequestLocale.Pick(en ? titleEn : titleZh, titleZh), Body = RequestLocale.Pick(en ? bodyEn : bodyZh, bodyZh) };
    }

    public async Task<IReadOnlyList<ComicCharacterPublicDto>> ListCharactersAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var rows = await db.ComicCharacters.AsNoTracking().Include(c => c.ComicCharactersI18ns)
            .Where(c => c.ClubId == scope.ClubId).OrderBy(c => c.SortOrder).ThenBy(c => c.RowSeq).ToListAsync(cancellationToken);
        return rows.Select(c =>
        {
            var requested = c.ComicCharactersI18ns.FirstOrDefault(i => i.Locale == dbLocale);
            var fallback = c.ComicCharactersI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
            return new ComicCharacterPublicDto
            {
                Id = c.Id, Name = RequestLocale.Pick(requested?.Name, fallback?.Name), Description = RequestLocale.Pick(requested?.Description, fallback?.Description),
                ImageUrl = Url(c.ImageKey), ImageThumbUrl = Thumb(c.ImageKey), PlayerId = c.PlayerId,
                ImageWidth = c.ImageKey is null ? null : c.ImageWidth, ImageHeight = c.ImageKey is null ? null : c.ImageHeight,
                ImageAlt = c.ImageKey is null ? null : RequestLocale.Pick(requested?.ImageAlt, fallback?.ImageAlt),
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<ComicEpisodeListItemDto>> ListEpisodesAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var rows = await Visible(scope).Include(e => e.ComicEpisodesI18ns).OrderByDescending(e => e.EpisodeNo).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();
        var pageCounts = await db.ComicPages.AsNoTracking().Where(p => ids.Contains(p.ComicEpisodeId))
            .GroupBy(p => p.ComicEpisodeId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count, cancellationToken);
        var latest = rows.Count == 0 ? (int?)null : rows.Max(r => r.EpisodeNo);
        return rows.Select(e => ToListItem(e, dbLocale, e.EpisodeNo == latest, pageCounts.GetValueOrDefault(e.Id))).ToList();
    }

    /// <summary>最新集數（首頁同步曝光、8.1 置頂區塊）。沒有任何可見集數回 null。</summary>
    public async Task<ComicEpisodeListItemDto?> GetLatestAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var e = await Visible(scope).Include(x => x.ComicEpisodesI18ns).OrderByDescending(x => x.EpisodeNo).FirstOrDefaultAsync(cancellationToken);
        if (e is null)
        {
            return null;
        }

        var count = await db.ComicPages.AsNoTracking().CountAsync(p => p.ComicEpisodeId == e.Id, cancellationToken);
        return ToListItem(e, dbLocale, true, count);
    }

    public async Task<ComicEpisodeDetailDto?> GetEpisodeAsync(ClubScope scope, int episodeNo, string dbLocale, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var visibleNos = await Visible(scope).Select(e => e.EpisodeNo).OrderBy(n => n).ToListAsync(cancellationToken);
        if (!visibleNos.Contains(episodeNo))
        {
            return null;
        }

        var e = await Visible(scope).Include(x => x.ComicEpisodesI18ns).Include(x => x.ComicPages).AsSplitQuery()
            .FirstAsync(x => x.EpisodeNo == episodeNo, cancellationToken);
        var index = visibleNos.IndexOf(episodeNo);
        var pages = e.ComicPages.OrderBy(p => p.SortOrder).ThenBy(p => p.RowSeq)
            .Select((p, i) => new ComicPagePublicDto { PageNo = i + 1, ImageUrl = Url(p.ImageKey), ImageThumbUrl = Thumb(p.ImageKey), Width = p.ImageWidth, Height = p.ImageHeight, Alt = GalleryImageAlt.Pick(dbLocale, p.ImageAltZh, p.ImageAltEn) }).ToList();
        var requested = e.ComicEpisodesI18ns.FirstOrDefault(i => i.Locale == dbLocale);
        var fallback = e.ComicEpisodesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        return new ComicEpisodeDetailDto
        {
            EpisodeNo = e.EpisodeNo, Title = RequestLocale.Pick(requested?.Title, fallback?.Title), CoverUrl = Url(e.CoverKey), PublishedOn = e.PublishedOn,
            CoverWidth = e.CoverKey is null ? null : e.CoverWidth, CoverHeight = e.CoverKey is null ? null : e.CoverHeight,
            CoverAlt = e.CoverKey is null ? null : RequestLocale.Pick(requested?.CoverAlt, fallback?.CoverAlt),
            IsLatest = index == visibleNos.Count - 1, Pages = pages,
            PreviousEpisodeNo = index > 0 ? visibleNos[index - 1] : null, NextEpisodeNo = index < visibleNos.Count - 1 ? visibleNos[index + 1] : null,
        };
    }

    /// <summary>閱讀數＋1（資料庫端遞增，不是讀出再寫回；同新聞瀏覽數的做法）。集數不可見回 false。</summary>
    public async Task<bool> IncrementViewCountAsync(ClubScope scope, int episodeNo, CancellationToken cancellationToken)
    {
        EnsureSupported(scope);
        var affected = await Visible(scope).Where(e => e.EpisodeNo == episodeNo)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ViewCount, e => e.ViewCount + 1), cancellationToken);
        return affected > 0;
    }

    private ComicEpisodeListItemDto ToListItem(ComicEpisode e, string dbLocale, bool isLatest, int pageCount)
    {
        var requested = e.ComicEpisodesI18ns.FirstOrDefault(i => i.Locale == dbLocale);
        var fallback = e.ComicEpisodesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        return new ComicEpisodeListItemDto
        {
            EpisodeNo = e.EpisodeNo, Title = RequestLocale.Pick(requested?.Title, fallback?.Title), CoverUrl = Url(e.CoverKey), CoverThumbUrl = Thumb(e.CoverKey),
            CoverWidth = e.CoverKey is null ? null : e.CoverWidth, CoverHeight = e.CoverKey is null ? null : e.CoverHeight,
            CoverAlt = e.CoverKey is null ? null : RequestLocale.Pick(requested?.CoverAlt, fallback?.CoverAlt),
            PublishedOn = e.PublishedOn, IsLatest = isLatest, PageCount = pageCount,
        };
    }

    private string? Url(string? key) => key is null ? null : imageUrls.Resolve(key);

    private string? Thumb(string? key) => key is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(key));
}
