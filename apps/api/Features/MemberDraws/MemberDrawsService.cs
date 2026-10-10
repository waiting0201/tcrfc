using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.MemberDraws;

/// <summary>
/// 會員可見的抽獎資訊（App 規劃書 §3.10，唯讀）。🔴 <b>不讀 <c>draw_rosters</c>、<c>draw_roster_versions</c></b>（名單快照是後台稽核資產，不對前台開放）：
/// 個人資格由 <c>memberships</c> 的狀態與期間即時推得，條件與後台名單產生（<c>AdminDrawsRepository.QueryEligibleAsync</c>）相同——
/// 俱樂部的球迷會員（<c>fan_club</c>）會籍、狀態 <c>active</c>／<c>expired</c>、期間涵蓋基準時間（台北日期）、會員帳號啟用。
/// 只列已鎖定名單之後的活動（<c>roster_locked</c>／<c>drawn</c>／<c>announced</c>／<c>closed</c>）；草稿與作廢不列。
/// </summary>
public sealed class MemberDrawsService(ClubDbContext db, IImagePublicUrlResolver imageUrls)
{
    private const int MaxItems = 30;

    private static readonly string[] VisibleStatuses = ["roster_locked", "drawn", "announced", "closed"];

    private static readonly Dictionary<string, (string Zh, string En)> StatusLabels = new(StringComparer.Ordinal)
    {
        ["roster_locked"] = ("名單已鎖定", "Roster locked"),
        ["drawn"] = ("已抽出", "Drawn"),
        ["announced"] = ("已公布", "Announced"),
        ["closed"] = ("已結案", "Closed"),
    };

    private static readonly Dictionary<string, (string Zh, string En)> OccasionLabels = new(StringComparer.Ordinal)
    {
        ["home_match"] = ("主場賽事日", "Home match day"),
        ["livestream"] = ("直播", "Livestream"),
        ["other"] = ("其他", "Other"),
    };

    public async Task<IReadOnlyList<MemberDrawDto>> ListAsync(Guid memberId, string lang, CancellationToken cancellationToken)
    {
        var dbLocale = RequestLocale.ToDbLocale(lang);
        var en = dbLocale == "en";

        var memberActive = await db.Members.AsNoTracking().AnyAsync(m => m.Id == memberId && m.Status == "active", cancellationToken);
        var draws = await db.MemberDraws.AsNoTracking()
            .Where(d => VisibleStatuses.Contains(d.Status) && d.SnapshotAt != null)
            .OrderByDescending(d => d.SnapshotAt).ThenByDescending(d => d.RowSeq)
            .Take(MaxItems)
            .Select(d => new
            {
                d.Id, d.DrawCode, d.ClubId, ClubCode = d.Club.Code, d.SnapshotAt, d.DrawnAt, d.DrawOccasion, d.ClaimDeadlineOn, d.Status, d.CoverKey, d.CoverWidth, d.CoverHeight,
                ArticleSlug = d.AnnouncementArticle != null && d.AnnouncementArticle.Status == "published" ? d.AnnouncementArticle.Slug : null,
                ArticleCategory = d.AnnouncementArticle != null ? d.AnnouncementArticle.ArticleCategory.Code : null,
                I18n = d.MemberDrawsI18ns.Where(i => i.Locale == dbLocale || i.Locale == RequestLocale.DefaultDbLocale).ToList(),
                ClubI18n = d.Club.ClubsI18ns.Where(i => i.Locale == dbLocale || i.Locale == RequestLocale.DefaultDbLocale).ToList(),
            })
            .ToListAsync(cancellationToken);

        // 你的會籍（只取需要判斷的欄位）：俱樂部 → 該俱樂部的球迷會籍期間清單
        var memberships = await db.Memberships.AsNoTracking()
            .Where(ms => ms.MemberId == memberId && ms.Tier == "fan_club" && (ms.Status == "active" || ms.Status == "expired"))
            .Select(ms => new { ms.ClubId, ms.MembershipStartOn, ms.MembershipEndOn })
            .ToListAsync(cancellationToken);

        return draws.Select(d =>
        {
            var requested = d.I18n.FirstOrDefault(i => i.Locale == dbLocale);
            var fallback = d.I18n.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
            var clubRequested = d.ClubI18n.FirstOrDefault(i => i.Locale == dbLocale);
            var clubFallback = d.ClubI18n.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
            var asOf = TaiwanClock.ToDate(d.SnapshotAt!.Value);
            var eligible = memberActive && memberships.Any(ms => ms.ClubId == d.ClubId
                && (ms.MembershipStartOn == null || ms.MembershipStartOn <= asOf) && (ms.MembershipEndOn == null || ms.MembershipEndOn >= asOf));
            var showArticle = d.Status is "announced" or "closed" && d.ArticleSlug is not null && d.ArticleCategory is not null;
            var (statusZh, statusEn) = StatusLabels[d.Status];

            return new MemberDrawDto
            {
                Id = d.Id,
                DrawCode = d.DrawCode,
                Club = new MemberDrawClubDto { Code = d.ClubCode, Name = RequestLocale.Pick(clubRequested?.Name, clubFallback?.Name) },
                Name = RequestLocale.Pick(requested?.Name, fallback?.Name),
                PrizeDescription = RequestLocale.Pick(requested?.PrizeDescription, fallback?.PrizeDescription),
                Rules = RequestLocale.Pick(requested?.Rules, fallback?.Rules),
                Notes = RequestLocale.Pick(requested?.Notes, fallback?.Notes),
                IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requested?.Name),
                CoverUrl = imageUrls.Resolve(d.CoverKey),
                CoverWidth = d.CoverKey is null ? null : d.CoverWidth,
                CoverHeight = d.CoverKey is null ? null : d.CoverHeight,
                CoverAlt = d.CoverKey is null ? null : RequestLocale.Pick(requested?.CoverAlt, fallback?.CoverAlt),
                Occasion = d.DrawOccasion,
                OccasionLabel = d.DrawOccasion is not null && OccasionLabels.TryGetValue(d.DrawOccasion, out var o) ? (en ? o.En : o.Zh) : null,
                SnapshotAt = d.SnapshotAt,
                DrawnAt = d.DrawnAt,
                ClaimDeadlineOn = d.ClaimDeadlineOn,
                Status = d.Status,
                StatusLabel = en ? statusEn : statusZh,
                IsEligible = eligible,
                Announcement = showArticle ? new MemberDrawAnnouncementDto { Slug = d.ArticleSlug!, CategoryCode = d.ArticleCategory! } : null,
            };
        }).ToList();
    }
}
