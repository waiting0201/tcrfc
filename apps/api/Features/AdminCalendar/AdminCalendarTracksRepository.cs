using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCalendar;

/// <summary>
/// L1 進階（主站規劃書 §4.12 L1）：隊別分軌檢視、衝突偵測、拖曳改期回寫。
///
/// <b>分軌</b>：每支球隊一條軌道並排（一場跨隊賽事同時出現在它涉及的每條軌道），沒有隊別的自建活動與同步的試訓放 <c>ClubEvents</c>。
/// 軌道的名稱／代表色／排序沿用 L3 設定（沒設定就沿用球隊本身的值）。
///
/// <b>衝突偵測</b>：兩件事的時段重疊，而且「同一場地」或「同一梯隊」。賽事只存日期與開賽時間（當地牆上時間，<c>Asia/Taipei</c>
/// 換算成 UTC，見 <c>CalendarIcsRepository</c>），沒有時長，比照 .ics 匯出估 2 小時；沒有開賽時間的賽事視為整天。
/// 自建活動的時間戳是 UTC 時間點，全天活動以 UTC 日期整天計。已取消的賽事不參與；試訓只有日期沒有時間，不參與衝突偵測。
///
/// <b>拖曳改期</b>：賽事回寫 <c>matches</c>（權限是來源模組的 <c>team.match.update</c> 加球隊列級授權——「行事曆權限跟隨來源模組」）；
/// 自建活動回寫 <c>calendar_custom_events</c>。有衝突時不寫入、回衝突清單，畫面警示後帶 <c>acknowledgeConflicts</c> 重送才寫入。
/// 🔴 「改期時觸發通知」：本系統目前沒有可用的通知通路，本輪不通知，回應的 <c>notificationSent</c> 恆為 false（見 README「待裁決」）。
/// </summary>
public sealed class AdminCalendarTracksRepository(
    ClubDbContext db, AdminCalendarOverviewRepository overview, IQueryCache cache)
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);
    private static readonly TimeSpan MatchDuration = TimeSpan.FromHours(2);

    private sealed record Slot(AdminCalendarEventDto Event, DateTime StartUtc, DateTime EndUtc);

    // ═══════════════════ 分軌 ═══════════════════

    public async Task<AdminCalendarTracksDto> TracksAsync(
        AdminClubScope scope, DateOnly from, DateOnly toExclusive, CancellationToken cancellationToken)
    {
        var events = await overview.ListAsync(scope, from, toExclusive, null, null, cancellationToken);
        var teams = await db.Teams.AsNoTracking().Where(t => t.ClubId == scope.ClubId)
            .Select(t => new
            {
                t.Id, t.Code, t.TeamColor, t.SortOrder,
                NameZh = t.TeamsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        var overrides = await db.CalendarTeamSettings.AsNoTracking().Include(s => s.CalendarTeamSettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId).ToListAsync(cancellationToken);

        var tracks = teams.Select(t =>
        {
            var o = overrides.FirstOrDefault(s => s.TeamId == t.Id);
            var display = o?.CalendarTeamSettingsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.DisplayName;
            return new AdminCalendarTrackDto
            {
                TeamId = t.Id, TeamCode = t.Code, Name = display ?? t.NameZh ?? t.Code, Colour = o?.Colour ?? t.TeamColor,
                SortOrder = o?.SortOrder ?? t.SortOrder,
                Events = events.Where(e => e.TeamCodes.Contains(t.Code)).ToList(),
            };
        }).OrderBy(t => t.SortOrder).ThenBy(t => t.TeamCode, StringComparer.Ordinal).ToList();

        return new AdminCalendarTracksDto
        {
            From = from, ToExclusive = toExclusive, Tracks = tracks,
            ClubEvents = events.Where(e => e.TeamCodes.Count == 0).ToList(),
            Conflicts = DetectConflicts(events),
        };
    }

    // ═══════════════════ 衝突偵測 ═══════════════════

    public async Task<IReadOnlyList<AdminCalendarConflictDto>> ConflictsAsync(
        AdminClubScope scope, DateOnly from, DateOnly toExclusive, CancellationToken cancellationToken)
        => DetectConflicts(await overview.ListAsync(scope, from, toExclusive, null, null, cancellationToken));

    private static IReadOnlyList<AdminCalendarConflictDto> DetectConflicts(IReadOnlyList<AdminCalendarEventDto> events)
    {
        var slots = events.Select(ToSlot).Where(s => s is not null).Select(s => s!).ToList();
        var conflicts = new List<AdminCalendarConflictDto>();
        for (var i = 0; i < slots.Count; i++)
        {
            for (var j = i + 1; j < slots.Count; j++)
            {
                var conflict = Compare(slots[i], slots[j]);
                if (conflict is not null)
                {
                    conflicts.Add(conflict);
                }
            }
        }

        return conflicts.OrderBy(c => c.First.StartsAt).ThenBy(c => c.First.Title, StringComparer.Ordinal).ToList();
    }

    private static AdminCalendarConflictDto? Compare(Slot a, Slot b)
    {
        if (!(a.StartUtc < b.EndUtc && b.StartUtc < a.EndUtc))
        {
            return null;
        }

        var reasons = new List<string>();
        var sameVenue = a.Event.VenueId is Guid av && b.Event.VenueId is Guid bv && av == bv;
        var sharedTeams = a.Event.TeamCodes.Intersect(b.Event.TeamCodes, StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList();
        if (sameVenue)
        {
            reasons.Add("venue");
        }

        if (sharedTeams.Count > 0)
        {
            reasons.Add("team");
        }

        if (reasons.Count == 0)
        {
            return null;
        }

        var parts = new List<string>();
        if (sameVenue)
        {
            parts.Add($"同一場地（{a.Event.VenueName ?? "未命名場地"}）");
        }

        if (sharedTeams.Count > 0)
        {
            // 不帶隊別代號（D1、BW1…）：後台介面不顯示代號（docs/06 §1）；球隊名稱由前端依 SharedTeamCodes 另外顯示
            parts.Add("同一支球隊");
        }

        return new AdminCalendarConflictDto
        {
            Reasons = reasons,
            Description = $"「{a.Event.Title}」與「{b.Event.Title}」時段重疊，而且是{string.Join("、", parts)}。",
            VenueName = sameVenue ? a.Event.VenueName : null,
            SharedTeamCodes = sharedTeams,
            First = ToRef(a.Event),
            Second = ToRef(b.Event),
        };
    }

    private static AdminCalendarConflictEventDto ToRef(AdminCalendarEventDto e) => new()
    {
        SourceType = e.SourceType, SourceId = e.SourceId, Title = e.Title, StartsAt = e.StartsAt, Kickoff = e.Kickoff, TeamCodes = e.TeamCodes,
        VenueName = e.VenueName,
    };

    private static Slot? ToSlot(AdminCalendarEventDto e)
    {
        switch (e.SourceType)
        {
            case "match":
                if (e.Status == "cancelled")
                {
                    return null;
                }

                return MatchSlot(e, e.StartsAt.Date, e.Kickoff);
            case "custom":
                if (e.IsAllDay)
                {
                    var start = e.StartsAt.Date;
                    var endDay = (e.EndsAt ?? e.StartsAt).Date.AddDays(1);
                    return new Slot(e, start, endDay);
                }

                var startUtc = DateTime.SpecifyKind(e.StartsAt, DateTimeKind.Utc);
                var endUtc = e.EndsAt is { } end ? DateTime.SpecifyKind(end, DateTimeKind.Utc) : startUtc + MatchDuration;
                return new Slot(e, startUtc, endUtc <= startUtc ? startUtc + MatchDuration : endUtc);
            default:
                return null; // 試訓只有日期沒有時間，不參與衝突偵測。
        }
    }

    private static Slot MatchSlot(AdminCalendarEventDto e, DateTime day, string? kickoff)
    {
        if (!string.IsNullOrEmpty(kickoff) && TimeOnly.TryParse(kickoff, out var time))
        {
            var start = day + time.ToTimeSpan() - TaipeiOffset;
            return new Slot(e, start, start + MatchDuration);
        }

        var dayStart = day - TaipeiOffset;
        return new Slot(e, dayStart, dayStart.AddDays(1));
    }

    private async Task<IReadOnlyList<AdminCalendarConflictDto>> ConflictsForCandidateAsync(
        AdminClubScope scope, AdminCalendarEventDto candidate, DateOnly day, CancellationToken cancellationToken)
    {
        // 多取前後各一天，涵蓋 UTC 與當地日期換算的邊界。
        var others = (await overview.ListAsync(scope, day.AddDays(-1), day.AddDays(2), null, null, cancellationToken))
            .Where(e => !(e.SourceType == candidate.SourceType && e.SourceId == candidate.SourceId))
            .ToList();
        var slot = ToSlot(candidate);
        if (slot is null)
        {
            return [];
        }

        return others.Select(ToSlot).Where(s => s is not null).Select(s => Compare(slot, s!)).Where(c => c is not null).Select(c => c!).ToList();
    }

    // ═══════════════════ 拖曳改期：賽事 ═══════════════════

    public async Task<AdminCalendarRescheduleResultDto?> RescheduleMatchAsync(
        AdminClubScope scope, TeamRowScope rowScope, Guid id, RescheduleMatchRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var match = await db.Matches.Include(m => m.Teams).Include(m => m.MatchesI18ns)
            .FirstOrDefaultAsync(m => m.Id == id && m.ClubId == scope.ClubId, cancellationToken);
        if (match is null)
        {
            return null;
        }

        if (!rowScope.AllowsAll(match.Teams.Select(t => (t.Id, t.Type)).ToList()))
        {
            throw new AdminForbiddenException("你的角色資料範圍不允許修改這筆賽事。");
        }

        if (match.Status is "played" or "live" or "cancelled")
        {
            throw new AdminValidationException("已開賽、已結束或已取消的賽事不能改期。");
        }

        var newKickoff = ResolveKickoff(request.Kickoff, match.Kickoff);
        var venueName = match.VenueId is Guid vid
            ? await db.Venues.AsNoTracking().Where(v => v.Id == vid)
                .Select(v => v.VenuesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault()).FirstOrDefaultAsync(cancellationToken)
            : null;
        var candidate = new AdminCalendarEventDto
        {
            SourceType = "match", SourceId = match.Id, StartsAt = request.MatchOn.ToDateTime(TimeOnly.MinValue), IsAllDay = string.IsNullOrEmpty(newKickoff),
            Title = match.Opponent ?? "(未定對手)", TeamCodes = match.Teams.Select(t => t.Code).OrderBy(c => c, StringComparer.Ordinal).ToList(),
            VenueName = venueName, VenueId = match.VenueId, Kickoff = newKickoff, Status = match.Status,
        };

        var conflicts = await ConflictsForCandidateAsync(scope, candidate, request.MatchOn, cancellationToken);
        if (conflicts.Count > 0 && !request.AcknowledgeConflicts)
        {
            return ResultOf(match, saved: false, conflicts);
        }

        if (request.MarkAsPostponed)
        {
            // 延賽必須有原定日期（C4 規則）：第一次標延賽時記下改期前的日期與時間，之後再改期不覆蓋原定值。
            match.OriginalMatchOn ??= match.MatchOn;
            match.OriginalKickoff ??= match.Kickoff;
            match.Status = "postponed";
        }

        match.MatchOn = request.MatchOn;
        match.Kickoff = newKickoff;
        match.UpdatedAt = DateTime.UtcNow;
        match.UpdatedBy = operatorId;
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("schedule", scope.ClubCode, cancellationToken);
        await cache.InvalidateAsync("calendar", scope.ClubCode, cancellationToken);
        return ResultOf(match, saved: true, []);
    }

    private static AdminCalendarRescheduleResultDto ResultOf(Data.EfEntities.Match match, bool saved, IReadOnlyList<AdminCalendarConflictDto> conflicts) => new()
    {
        Saved = saved, SourceType = "match", SourceId = match.Id, StartsAt = match.MatchOn.ToDateTime(TimeOnly.MinValue), Kickoff = match.Kickoff,
        Status = match.Status, OriginalMatchOn = match.OriginalMatchOn, Conflicts = conflicts, NotificationSent = false,
    };

    private static string? ResolveKickoff(string? requested, string? current)
    {
        if (requested is null)
        {
            return current;
        }

        if (requested.Length == 0)
        {
            return null;
        }

        if (!TimeOnly.TryParseExact(requested, "HH:mm", out _))
        {
            throw new AdminValidationException("開賽時間請填 24 小時制的「時:分」，例如 19:00；不填則維持原開賽時間。", "kickoff");
        }

        return requested;
    }

    // ═══════════════════ 拖曳改期：自建活動 ═══════════════════

    public async Task<AdminCalendarRescheduleResultDto?> MoveCustomEventAsync(
        AdminClubScope scope, Guid id, MoveCustomEventRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var ev = await db.CalendarCustomEvents.Include(e => e.CalendarCustomEventsI18ns)
            .FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        if (ev is null)
        {
            return null;
        }

        var isAllDay = request.IsAllDay ?? ev.IsAllDay;
        if (request.EndsAt is DateTime end && end < request.StartsAt)
        {
            throw new AdminValidationException("結束時間不可早於開始時間。", "endsAt");
        }

        var teamCodes = await db.CalendarEventTeams.AsNoTracking().Where(t => t.SourceType == "custom" && t.SourceId == id)
            .Select(t => t.Team.Code).OrderBy(c => c).ToListAsync(cancellationToken);
        var venueName = ev.VenueId is Guid vid
            ? await db.Venues.AsNoTracking().Where(v => v.Id == vid)
                .Select(v => v.VenuesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault()).FirstOrDefaultAsync(cancellationToken)
            : null;
        var title = ev.CalendarCustomEventsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Title ?? "(未命名活動)";
        var candidate = new AdminCalendarEventDto
        {
            SourceType = "custom", SourceId = ev.Id, StartsAt = request.StartsAt, EndsAt = request.EndsAt, IsAllDay = isAllDay, Title = title,
            TeamCodes = teamCodes, VenueName = venueName, VenueId = ev.VenueId,
        };

        var day = DateOnly.FromDateTime(request.StartsAt);
        var conflicts = await ConflictsForCandidateAsync(scope, candidate, day, cancellationToken);
        if (conflicts.Count > 0 && !request.AcknowledgeConflicts)
        {
            return CustomResult(ev, request.StartsAt, saved: false, conflicts);
        }

        ev.StartsAt = request.StartsAt;
        ev.EndsAt = request.EndsAt;
        ev.IsAllDay = isAllDay;
        ev.UpdatedAt = DateTime.UtcNow;
        ev.UpdatedBy = operatorId;
        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("calendar", scope.ClubCode, cancellationToken);
        return CustomResult(ev, ev.StartsAt, saved: true, []);
    }

    private static AdminCalendarRescheduleResultDto CustomResult(
        Data.EfEntities.CalendarCustomEvent ev, DateTime startsAt, bool saved, IReadOnlyList<AdminCalendarConflictDto> conflicts) => new()
    {
        Saved = saved, SourceType = "custom", SourceId = ev.Id, StartsAt = startsAt, Conflicts = conflicts, NotificationSent = false,
    };
}
