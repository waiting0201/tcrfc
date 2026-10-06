using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminTrials;

/// <summary>
/// P4 試訓場次管理（主站規劃書 §4.4 P4：日期、地點、對象、名額、報名截止、報名名單管理），產出前台 3.3／4.7／6.3 的試訓資訊。
/// <c>trials.club_id</c> 必填，所有查詢限定目前操作的俱樂部，跨俱樂部的 id 一律 404。
/// 名額控管比照 P2 梯次：報名的實際佔用狀態（待確認／已確認／已繳費／完成）計入 <c>enrolled_count</c>，
/// 用原子 SQL 調整，達到名額上限時「開放」自動轉「額滿」（單向，人工才能改回）。
/// 「試訓是否同步至行事曆」由 L3 的全站開關決定（<c>calendar.sync_trials</c>，預設關閉），新場次沿用目前開關值。
/// </summary>
public sealed class AdminTrialsRepository(ClubDbContext db, ClubSettingsStore settings, IQueryCache cache)
{
    public const string SyncSettingKey = "calendar.sync_trials";
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "開放", "額滿", "候補", "已結束" };

    private static readonly string[] OccupyingStatuses = ["待確認", "已確認", "已繳費", "完成"];

    public async Task<IReadOnlyList<AdminTrialListItemDto>> ListAsync(
        AdminClubScope scope, Guid? teamId, string? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = db.Trials.AsNoTracking().Where(t => t.ClubId == scope.ClubId);
        if (teamId is Guid tid)
        {
            query = query.Where(t => t.TeamId == tid);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, Statuses, "狀態", "「開放」「額滿」「候補」或「已結束」");
            query = query.Where(t => t.Status == status);
        }

        if (from is DateOnly f)
        {
            query = query.Where(t => t.TrialOn >= f);
        }

        if (to is DateOnly toDate)
        {
            query = query.Where(t => t.TrialOn <= toDate);
        }

        var rows = await query.OrderByDescending(t => t.TrialOn).ThenBy(t => t.RowSeq)
            .Select(t => new
            {
                t.Id, t.TeamId, TeamCode = t.Team == null ? null : t.Team.Code, t.VenueId, t.TrialOn, t.Capacity, t.EnrolledCount, t.DeadlineOn, t.Status,
                t.SyncToCalendar, t.UpdatedAt,
                TeamName = t.Team == null ? null : t.Team.TeamsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                VenueName = t.Venue == null ? null : t.Venue.VenuesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                AudienceZh = t.TrialsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Audience).FirstOrDefault(),
                AudienceEn = t.TrialsI18ns.Where(i => i.Locale == "en").Select(i => i.Audience).FirstOrDefault(),
                Waitlist = t.Registrations.Count(r => r.Status == "候補"),
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return rows.Select(r => new AdminTrialListItemDto
        {
            Id = r.Id, TeamId = r.TeamId, TeamCode = r.TeamCode, TeamName = r.TeamName, VenueId = r.VenueId, VenueName = r.VenueName,
            TrialOn = r.TrialOn, Capacity = r.Capacity, EnrolledCount = r.EnrolledCount, DeadlineOn = r.DeadlineOn, Status = r.Status,
            IsSignupOpen = IsSignupOpen(r.Status, r.TrialOn, r.DeadlineOn, today), SyncToCalendar = r.SyncToCalendar,
            AudienceZh = r.AudienceZh, AudienceEn = r.AudienceEn, WaitlistCount = r.Waitlist, UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminTrialDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var t = await db.Trials.AsNoTracking().Include(x => x.TrialsI18ns)
            .Include(x => x.Team).ThenInclude(x => x!.TeamsI18ns).Include(x => x.Venue).ThenInclude(x => x!.VenuesI18ns)
            .FirstOrDefaultAsync(x => x.Id == id && x.ClubId == scope.ClubId, cancellationToken);
        if (t is null)
        {
            return null;
        }

        var waitlist = await db.Registrations.AsNoTracking().CountAsync(r => r.TrialId == id && r.Status == "候補", cancellationToken);
        var zh = t.TrialsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = t.TrialsI18ns.FirstOrDefault(i => i.Locale == "en");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new AdminTrialDetailDto
        {
            Id = t.Id, TeamId = t.TeamId, TeamCode = t.Team?.Code,
            TeamName = t.Team?.TeamsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
            VenueId = t.VenueId, VenueName = t.Venue?.VenuesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
            TrialOn = t.TrialOn, Capacity = t.Capacity, EnrolledCount = t.EnrolledCount, DeadlineOn = t.DeadlineOn, Status = t.Status,
            IsSignupOpen = IsSignupOpen(t.Status, t.TrialOn, t.DeadlineOn, today), SyncToCalendar = t.SyncToCalendar,
            Zh = new AdminTrialLocaleContent { Audience = zh?.Audience ?? "" },
            En = en is null ? null : new AdminTrialLocaleContent { Audience = en.Audience ?? "" },
            WaitlistCount = waitlist, CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt,
        };
    }

    private static bool IsSignupOpen(string status, DateOnly trialOn, DateOnly? deadlineOn, DateOnly today)
        => status == "開放" && trialOn >= today && (deadlineOn is null || deadlineOn >= today);

    public async Task<AdminTrialDetailDto> CreateAsync(
        AdminClubScope scope, UpsertAdminTrialRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        await ResolveRefsAsync(scope, request, cancellationToken);
        var syncValues = await settings.GetManyAsync(scope.ClubId, [SyncSettingKey], cancellationToken);
        var now = DateTime.UtcNow;
        var trial = new Trial
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, TeamId = request.TeamId, VenueId = request.VenueId, TrialOn = request.TrialOn,
            Capacity = request.Capacity, DeadlineOn = request.DeadlineOn, Status = request.Status ?? "開放",
            SyncToCalendar = string.Equals(syncValues.GetValueOrDefault(SyncSettingKey), "true", StringComparison.Ordinal),
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        db.Trials.Add(trial);
        SetI18n(trial, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return (await GetByIdAsync(scope, trial.Id, cancellationToken))!;
    }

    public async Task<AdminTrialDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminTrialRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        var trial = await db.Trials.Include(t => t.TrialsI18ns).FirstOrDefaultAsync(t => t.Id == id && t.ClubId == scope.ClubId, cancellationToken);
        if (trial is null)
        {
            return null;
        }

        await ResolveRefsAsync(scope, request, cancellationToken);
        if (request.Capacity is int cap && cap < trial.EnrolledCount)
        {
            throw new AdminValidationException($"名額不能低於目前已報名的人數（{trial.EnrolledCount} 人）。", "capacity");
        }

        trial.TeamId = request.TeamId;
        trial.VenueId = request.VenueId;
        trial.TrialOn = request.TrialOn;
        trial.Capacity = request.Capacity;
        trial.DeadlineOn = request.DeadlineOn;
        if (request.Status is not null)
        {
            trial.Status = request.Status;
        }

        trial.UpdatedAt = DateTime.UtcNow;
        trial.UpdatedBy = operatorId;
        SetI18n(trial, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var trial = await db.Trials.FirstOrDefaultAsync(t => t.Id == id && t.ClubId == scope.ClubId, cancellationToken);
        if (trial is null)
        {
            return false;
        }

        if (await db.Registrations.AsNoTracking().AnyAsync(r => r.TrialId == id, cancellationToken))
        {
            throw new AdminConflictException("場次已有報名", "這個試訓場次已經有人報名，不能刪除。可以把狀態改為「已結束」讓它不再接受報名。");
        }

        db.Trials.Remove(trial); // 側表由資料庫串聯刪除
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return true;
    }

    /// <summary>原子調整試訓的已報名數；額滿時把「開放」單向轉為「額滿」（同 P2 梯次）。名額扣減有下限保護。</summary>
    internal Task AdjustEnrolledCountAsync(Guid trialId, int delta, CancellationToken cancellationToken)
        => db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE trials
            SET enrolled_count = CASE WHEN enrolled_count + {delta} < 0 THEN 0 ELSE enrolled_count + {delta} END,
                status = CASE
                    WHEN status = N'開放' AND capacity IS NOT NULL AND (enrolled_count + {delta}) >= capacity
                    THEN N'額滿'
                    ELSE status
                END,
                updated_at = SYSUTCDATETIME()
            WHERE id = {trialId}", cancellationToken);

    internal static bool Occupies(string status) => OccupyingStatuses.Contains(status);

    private Task InvalidateAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => cache.InvalidateAsync("calendar", scope.ClubCode, cancellationToken);

    private static void Validate(UpsertAdminTrialRequest request)
    {
        if (request.Capacity is < 1)
        {
            throw new AdminValidationException("名額至少要 1 人；不限名額請留空。", "capacity");
        }

        if (request.DeadlineOn is DateOnly deadline && deadline > request.TrialOn)
        {
            throw new AdminValidationException("報名截止日不可晚於試訓日期。", "deadlineOn");
        }

        if (request.Status is not null)
        {
            AdminInput.OneOf(request.Status, Statuses, "狀態", "「開放」「額滿」「候補」或「已結束」", "status");
        }

        AdminInput.RequireText(request.Content.Zh.Audience, "中文對象說明", 255, "audienceZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Audience))
        {
            AdminInput.RequireText(request.Content.En.Audience, "英文對象說明", 255, "audienceEn");
        }
    }

    private async Task ResolveRefsAsync(AdminClubScope scope, UpsertAdminTrialRequest request, CancellationToken cancellationToken)
    {
        if (request.TeamId is Guid teamId && !await db.Teams.AsNoTracking().AnyAsync(t => t.Id == teamId && t.ClubId == scope.ClubId, cancellationToken))
        {
            throw new AdminValidationException("找不到指定的球隊，請確認球隊屬於目前的俱樂部。", "teamId");
        }

        if (request.VenueId is Guid venueId && !await db.Venues.AsNoTracking().AnyAsync(v => v.Id == venueId, cancellationToken))
        {
            throw new AdminValidationException("找不到指定的場地。", "venueId");
        }
    }

    private void SetI18n(Trial trial, AdminTrialContentInput content)
    {
        Upsert(trial, RequestLocale.DefaultDbLocale, content.Zh.Audience);
        var en = trial.TrialsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Audience))
        {
            Upsert(trial, "en", content.En.Audience);
        }
        else if (en is not null)
        {
            db.Remove(en);
        }
    }

    private void Upsert(Trial trial, string locale, string audience)
    {
        var row = trial.TrialsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new TrialsI18n { TrialId = trial.Id, Locale = locale };
            trial.TrialsI18ns.Add(row);
            db.TrialsI18ns.Add(row);
        }

        row.Audience = audience.Trim();
    }
}
