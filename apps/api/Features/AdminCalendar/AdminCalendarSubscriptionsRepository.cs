using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCalendar;

/// <summary>
/// L4 iCal 訂閱網址管理與訂閱數統計（主站規劃書 §4.12 L4）：「全站」與每支隊別各一組 webcal 訂閱連結
/// （連結由公開端點 <c>GET /api/v1/{club}/calendar/feed.ics</c> 提供，見 <c>CalendarFeedRepository</c>），
/// 並統計各 feed 最近 7／30 天有多少個不同的訂閱來源。
/// </summary>
public sealed class AdminCalendarSubscriptionsRepository(ClubDbContext db)
{
    public async Task<AdminCalendarSubscriptionsDto> ListAsync(AdminClubScope scope, string baseUrl, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var since30 = today.AddDays(-30);
        var since7 = today.AddDays(-7);

        var rows = await db.CalendarFeedFetches.AsNoTracking()
            .Where(f => f.ClubId == scope.ClubId && f.FetchedOn >= since30)
            .Select(f => new { f.FeedKey, f.FetchedOn, f.ClientHash })
            .ToListAsync(cancellationToken);
        var lastFetch = await db.CalendarFeedFetches.AsNoTracking().Where(f => f.ClubId == scope.ClubId)
            .GroupBy(f => f.FeedKey).Select(g => new { Key = g.Key, Last = g.Max(x => x.FetchedOn) }).ToListAsync(cancellationToken);

        var teams = await db.Teams.AsNoTracking().Where(t => t.ClubId == scope.ClubId)
            .Select(t => new
            {
                t.Id, t.Code, t.SortOrder,
                NameZh = t.TeamsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        var overrides = await db.CalendarTeamSettings.AsNoTracking().Include(s => s.CalendarTeamSettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId).ToListAsync(cancellationToken);
        var clubName = await db.ClubsI18ns.AsNoTracking().Where(i => i.ClubId == scope.ClubId && i.Locale == RequestLocale.DefaultDbLocale)
            .Select(i => i.Name).FirstOrDefaultAsync(cancellationToken) ?? scope.ClubCode;

        AdminCalendarSubscriptionDto Build(string key, string label, bool isPublic)
        {
            var path = key == "all" ? $"/api/v1/{scope.ClubCode}/calendar/feed.ics" : $"/api/v1/{scope.ClubCode}/calendar/feed.ics?team={Uri.EscapeDataString(key)}";
            var mine = rows.Where(r => r.FeedKey == key).ToList();
            return new AdminCalendarSubscriptionDto
            {
                FeedKey = key, Label = label, HttpsUrl = baseUrl + path, WebcalUrl = "webcal://" + baseUrl.Split("://", 2)[^1] + path,
                Subscribers30d = mine.Select(r => r.ClientHash).Distinct().Count(),
                Subscribers7d = mine.Where(r => r.FetchedOn >= since7).Select(r => r.ClientHash).Distinct().Count(),
                LastFetchedOn = lastFetch.FirstOrDefault(l => l.Key == key)?.Last, IsPublic = isPublic,
            };
        }

        var feeds = new List<AdminCalendarSubscriptionDto> { Build("all", $"{clubName}・全站", true) };
        feeds.AddRange(teams.Select(t => new
            {
                t.Code,
                Order = overrides.FirstOrDefault(s => s.TeamId == t.Id)?.SortOrder ?? t.SortOrder,
                Label = overrides.FirstOrDefault(s => s.TeamId == t.Id)?.CalendarTeamSettingsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.DisplayName
                        ?? t.NameZh ?? t.Code,
                IsPublic = overrides.FirstOrDefault(s => s.TeamId == t.Id)?.IsPublic ?? true,
            })
            .OrderBy(t => t.Order).ThenBy(t => t.Code, StringComparer.Ordinal)
            .Select(t => Build(t.Code, t.Label, t.IsPublic)));

        return new AdminCalendarSubscriptionsDto
        {
            Feeds = feeds,
            StatsNote = "訂閱數是估計值：以「最近有多少個不同的訂閱來源」計算。Google 行事曆等服務由自己的伺服器代為抓取，多位訂閱者可能只會被算成一個來源，實際訂閱人數通常比這個數字高。",
        };
    }
}
