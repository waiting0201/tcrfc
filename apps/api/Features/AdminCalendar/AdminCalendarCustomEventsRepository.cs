using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCalendar;

/// <summary>
/// L2「自建事件」——俱樂部範圍 CRUD（主站規劃書 §4.12 L2）。<c>calendar_custom_events</c> 是行事曆
/// 唯一的自有資料，賽事本身仍在 C4（<c>Features/AdminMatches</c>）維護，本檔完全不碰 <c>matches</c>。
///
/// 🔴 沒有列級授權（<see cref="TeamRowScope"/>）：矩陣把「自建事件」（內容編輯／公關媒體）與
/// 「賽事事件」「梯隊賽事」（競技／球隊管理／學院／課程管理）分成不同的格子——後兩者對應的是
/// <c>team.match.*</c>（已由 C4 套用 <c>TeamRowScope</c>），不是這一組 <c>calendar.custom_event.*</c>
/// 權限碼；能拿到 <c>calendar.custom_event.create/update/delete</c> 的角色（內容編輯、公關／媒體、
/// 系統管理員、合作球隊管理）在 <c>db/seed/generate-club-seed-sql.py</c> 一律是
/// <c>scope_type="all"</c>（或 <c>own_clubs</c>，效果等同 <c>all</c>，見既有 <c>AdminTeamRowScopeResolver</c>
/// 對 <c>own_clubs</c> 的既有處理）——沒有任何角色需要「只能碰特定球隊的自建事件」這種列級限制，
/// 因此不套用 <see cref="TeamRowScope"/>，<see cref="TeamIds"/> 只做「這些球隊是不是屬於本俱樂部」
/// 的資料正確性檢查，不做授權檢查。
/// </summary>
public sealed class AdminCalendarCustomEventsRepository(ClubDbContext dbContext, IQueryCache cache)
{
    public async Task<IReadOnlyList<AdminCalendarCustomEventListItemDto>> ListAsync(
        AdminClubScope scope, string? teamCode, CancellationToken cancellationToken)
    {
        var query = dbContext.CalendarCustomEvents.AsNoTracking().Where(e => e.ClubId == scope.ClubId);

        if (teamCode is not null)
        {
            if (string.Equals(teamCode, "club", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(e =>
                    !dbContext.Set<CalendarEventTeam>().Any(t => t.SourceType == "custom" && t.SourceId == e.Id));
            }
            else
            {
                query = query.Where(e => dbContext.Set<CalendarEventTeam>()
                    .Any(t => t.SourceType == "custom" && t.SourceId == e.Id && t.Team.Code == teamCode));
            }
        }

        var rows = await query
            .OrderByDescending(e => e.StartsAt)
            .Select(e => new
            {
                e.Id,
                e.StartsAt,
                e.EndsAt,
                e.IsAllDay,
                e.RepeatRule,
                e.IsPublic,
                e.CoverKey,
                e.UpdatedAt,
                EventTypeCode = e.EventType != null ? e.EventType.Code : null,
                TitleZh = e.CalendarCustomEventsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Title).FirstOrDefault(),
                TitleEn = e.CalendarCustomEventsI18ns.Where(i => i.Locale == "en").Select(i => i.Title).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();
        var teamCodesById = await LoadTeamCodesAsync(ids, cancellationToken);

        return rows.Select(r => new AdminCalendarCustomEventListItemDto
        {
            Id = r.Id,
            StartsAt = r.StartsAt,
            EndsAt = r.EndsAt,
            IsAllDay = r.IsAllDay,
            RepeatRule = r.RepeatRule,
            IsPublic = r.IsPublic,
            CoverKey = r.CoverKey,
            TeamCodes = teamCodesById.GetValueOrDefault(r.Id, []),
            EventTypeCode = r.EventTypeCode,
            TitleZh = r.TitleZh,
            TitleEn = r.TitleEn,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminCalendarCustomEventDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.CalendarCustomEvents.AsNoTracking()
            .Include(e => e.CalendarCustomEventsI18ns)
            .Include(e => e.CalendarEventExceptions)
            .FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var teamCodesById = await LoadTeamCodesAsync([id], cancellationToken);
        var teamIds = await dbContext.Set<CalendarEventTeam>().AsNoTracking()
            .Where(t => t.SourceType == "custom" && t.SourceId == id)
            .Select(t => t.TeamId)
            .ToListAsync(cancellationToken);

        return ToDetailDto(entity, teamIds, teamCodesById.GetValueOrDefault(id, []));
    }

    public async Task<AdminCalendarCustomEventDetailDto> CreateAsync(
        AdminClubScope scope, Guid eventId, CreateAdminCalendarCustomEventRequest request, string? coverKey,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateContent(request.Content);
        ValidateRepeatRule(request.RepeatRule, request.RepeatUntil);
        ValidateTimeRange(request.StartsAt, request.EndsAt);
        await ValidateEventTypeAsync(request.EventTypeId, cancellationToken);
        await ValidateVenueAsync(request.VenueId, cancellationToken);
        var teamIds = await ResolveTeamIdsAsync(scope, request.TeamIds ?? [], cancellationToken);

        var now = DateTime.UtcNow;
        var entity = new CalendarCustomEvent
        {
            Id = eventId,
            ClubId = scope.ClubId,
            EventTypeId = request.EventTypeId,
            VenueId = request.VenueId,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            IsAllDay = request.IsAllDay,
            RepeatRule = request.RepeatRule,
            RepeatUntil = request.RepeatUntil,
            IsPublic = request.IsPublic,
            CoverKey = coverKey,
            CtaUrl = request.CtaUrl,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.CalendarCustomEvents.Add(entity);
        AddOrReplaceI18n(entity, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(entity, "en", request.Content.En);
        }

        foreach (var excludedOn in (request.ExceptionDates ?? []).Distinct())
        {
            dbContext.Add(new CalendarEventException { CalendarCustomEventId = eventId, ExcludedOn = excludedOn });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var teamId in teamIds)
        {
            dbContext.Add(new CalendarEventTeam { SourceType = "custom", SourceId = eventId, TeamId = teamId });
        }
        if (teamIds.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await cache.InvalidateAsync("calendar", scope.ClubCode, cancellationToken);
        return (await GetByIdAsync(scope, eventId, cancellationToken))!;
    }

    public async Task<AdminCalendarCustomEventDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminCalendarCustomEventRequest request, CalendarEventCoverKeyUpdate coverUpdate,
        Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateContent(request.Content);
        ValidateRepeatRule(request.RepeatRule, request.RepeatUntil);
        ValidateTimeRange(request.StartsAt, request.EndsAt);
        await ValidateEventTypeAsync(request.EventTypeId, cancellationToken);
        await ValidateVenueAsync(request.VenueId, cancellationToken);

        var entity = await dbContext.CalendarCustomEvents
            .Include(e => e.CalendarCustomEventsI18ns)
            .Include(e => e.CalendarEventExceptions)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        if (entity.ClubId != scope.ClubId)
        {
            return null; // 跨俱樂部：回 404，不洩漏存在與否（比照 AdminProgramsRepository 既有慣例）。
        }

        entity.EventTypeId = request.EventTypeId;
        entity.VenueId = request.VenueId;
        entity.StartsAt = request.StartsAt;
        entity.EndsAt = request.EndsAt;
        entity.IsAllDay = request.IsAllDay;
        entity.RepeatRule = request.RepeatRule;
        entity.RepeatUntil = request.RepeatUntil;
        entity.IsPublic = request.IsPublic;
        entity.CtaUrl = request.CtaUrl;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = operatorId;

        if (coverUpdate.Change)
        {
            entity.CoverKey = coverUpdate.NewKey;
        }

        AddOrReplaceI18n(entity, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = entity.CalendarCustomEventsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(entity, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        // 省略＝維持不變、空陣列＝清空——比照 AdminMatchesRepository 對 Goals／Cards／Lineups 的既有語意。
        if (request.ExceptionDates is not null)
        {
            foreach (var existing in entity.CalendarEventExceptions.ToList())
            {
                dbContext.Remove(existing);
            }
            foreach (var excludedOn in request.ExceptionDates.Distinct())
            {
                dbContext.Add(new CalendarEventException { CalendarCustomEventId = id, ExcludedOn = excludedOn });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.TeamIds is not null)
        {
            var teamIds = await ResolveTeamIdsAsync(scope, request.TeamIds, cancellationToken);
            var existingLinks = await dbContext.Set<CalendarEventTeam>()
                .Where(t => t.SourceType == "custom" && t.SourceId == id)
                .ToListAsync(cancellationToken);
            dbContext.RemoveRange(existingLinks);
            foreach (var teamId in teamIds)
            {
                dbContext.Add(new CalendarEventTeam { SourceType = "custom", SourceId = id, TeamId = teamId });
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await cache.InvalidateAsync("calendar", scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>硬刪除——自建事件是純資料紀錄，沒有「離隊」那種需要保留歷史狀態的語意，逐字比照
    /// <c>Features/AdminMatches</c> 對 <c>Match</c> 的既有判斷。<c>calendar_custom_events_i18n</c>／
    /// <c>calendar_event_exceptions</c> 皆為 <c>ON DELETE CASCADE</c>（db/club-schema.sql），但
    /// <c>calendar_event_teams</c> 是多型關聯表、對 <c>calendar_custom_events</c> 沒有真正的外鍵
    /// （<c>source_id</c> 可能指向 <c>matches</c> 或 <c>calendar_custom_events</c>），需要在同一個
    /// SaveChanges 裡手動一併刪除，不會自動 cascade。</summary>
    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.CalendarCustomEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null || entity.ClubId != scope.ClubId)
        {
            return false;
        }

        var links = await dbContext.Set<CalendarEventTeam>()
            .Where(t => t.SourceType == "custom" && t.SourceId == id)
            .ToListAsync(cancellationToken);
        dbContext.RemoveRange(links);

        dbContext.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("calendar", scope.ClubCode, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AdminEventTypeDto>> ListEventTypesAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.EventTypes.AsNoTracking()
            .OrderBy(t => t.SortOrder)
            .Select(t => new
            {
                t.Id,
                t.Code,
                t.Colour,
                t.Icon,
                NameZh = t.EventTypesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = t.EventTypesI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminEventTypeDto
        {
            Id = r.Id,
            Code = r.Code,
            Colour = r.Colour,
            Icon = r.Icon,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
        }).ToList();
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadTeamCodesAsync(
        IReadOnlyList<Guid> eventIds, CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.Set<CalendarEventTeam>().AsNoTracking()
            .Where(t => t.SourceType == "custom" && eventIds.Contains(t.SourceId))
            .Select(t => new { t.SourceId, t.Team.Code })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(r => r.SourceId)
            .ToDictionary(g => g.Key, IReadOnlyList<string> (g) => g.Select(r => r.Code).OrderBy(c => c, StringComparer.Ordinal).ToList());
    }

    private async Task<List<Guid>> ResolveTeamIdsAsync(AdminClubScope scope, IReadOnlyList<Guid> teamIds, CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
        {
            return [];
        }

        var teams = await dbContext.Teams
            .Where(t => teamIds.Contains(t.Id) && t.ClubId == scope.ClubId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (teams.Count != teamIds.Distinct().Count())
        {
            throw new AdminCalendarValidationException("所屬隊別裡有找不到的球隊，請確認選擇的隊伍屬於本俱樂部。");
        }

        return teams;
    }

    private async Task ValidateEventTypeAsync(Guid? eventTypeId, CancellationToken cancellationToken)
    {
        if (eventTypeId is { } id && !await dbContext.EventTypes.AsNoTracking().AnyAsync(t => t.Id == id, cancellationToken))
        {
            throw new AdminCalendarValidationException("找不到指定的事件分類。");
        }
    }

    private async Task ValidateVenueAsync(Guid? venueId, CancellationToken cancellationToken)
    {
        if (venueId is { } id && !await dbContext.Venues.AsNoTracking().AnyAsync(v => v.Id == id, cancellationToken))
        {
            throw new AdminCalendarValidationException("找不到指定的場地。");
        }
    }

    private static void ValidateContent(AdminCalendarEventContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Title))
        {
            throw new AdminCalendarValidationException("中文標題為必填欄位。");
        }
    }

    private static void ValidateTimeRange(DateTime startsAt, DateTime? endsAt)
    {
        if (endsAt is { } end && end < startsAt)
        {
            throw new AdminCalendarValidationException("結束時間不能早於起始時間。");
        }
    }

    private static void ValidateRepeatRule(string? repeatRule, DateOnly? repeatUntil)
    {
        if (repeatRule is null)
        {
            return;
        }

        if (!RecurrenceExpander.AllowedRepeatRules.Contains(repeatRule))
        {
            throw new AdminCalendarValidationException("重複規則只能是「weekly」「biweekly」或「monthly」其中一種。");
        }
    }

    private void AddOrReplaceI18n(CalendarCustomEvent entity, string locale, AdminCalendarEventLocaleContent content)
    {
        var existing = entity.CalendarCustomEventsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new CalendarCustomEventsI18n { CalendarCustomEventId = entity.Id, Locale = locale };
            entity.CalendarCustomEventsI18ns.Add(existing);
            dbContext.CalendarCustomEventsI18ns.Add(existing);
        }

        existing.Title = content.Title;
        existing.Description = content.Description;
    }

    private static AdminCalendarCustomEventDetailDto ToDetailDto(
        CalendarCustomEvent entity, IReadOnlyList<Guid> teamIds, IReadOnlyList<string> teamCodes)
    {
        var zh = entity.CalendarCustomEventsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = entity.CalendarCustomEventsI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminCalendarCustomEventDetailDto
        {
            Id = entity.Id,
            EventTypeId = entity.EventTypeId,
            VenueId = entity.VenueId,
            StartsAt = entity.StartsAt,
            EndsAt = entity.EndsAt,
            IsAllDay = entity.IsAllDay,
            RepeatRule = entity.RepeatRule,
            RepeatUntil = entity.RepeatUntil,
            ExceptionDates = entity.CalendarEventExceptions.Select(e => e.ExcludedOn).OrderBy(d => d).ToList(),
            IsPublic = entity.IsPublic,
            CoverKey = entity.CoverKey,
            CtaUrl = entity.CtaUrl,
            TeamIds = teamIds,
            TeamCodes = teamCodes,
            Zh = new AdminCalendarEventLocaleContent { Title = zh?.Title ?? "", Description = zh?.Description },
            En = en is null ? null : new AdminCalendarEventLocaleContent { Title = en.Title ?? "", Description = en.Description },
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }
}
