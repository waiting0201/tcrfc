using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminEnquiries;
using Tcrfc.Api.Features.AdminSiteSettings;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminDashboard;

/// <summary>
/// 儀表板首頁（主站規劃書 §4.1 A）：待辦提醒、內容概況、FAQ 概況、近期行程、會員概況、快速入口；轉換概況與流量概況另兩支端點。
///
/// ### 權限模型（規劃書 §6 矩陣沒有「儀表板」欄）
/// 儀表板是每個後台帳號的首頁，所以<b>不新增專屬權限碼</b>：端點只要呼叫者在該俱樂部持有下面 <see cref="AllCandidateCodes"/> 任一個權限就放行
/// （一個帳號連一個可看的項目都沒有 → 403，等同沒有任何可用的後台功能），然後<b>每個區塊各自依對應模組的檢視權限決定要不要算、要不要回</b>：
/// 沒有權限的區塊回 <c>null</c>／不出現在清單，而不是回 0。計數只回數字不回個資；詢問類別依 G2 的類別授權過濾（同 <see cref="AdminEnquiriesRepository"/>）。
///
/// ### 範圍
/// 一律限定目前操作的俱樂部；有 <c>club_id IS NULL</c>（兩隊共同）的內容型別（新聞、FAQ、教練、慈善計畫）算入兩站都會顯示的共同內容。
/// 日期一律用台灣當地日期（<see cref="TaiwanClock"/>）；資料庫時間戳為 UTC。
///
/// ### 執行層決定（規劃書只列項目名稱）
/// 未處理詢問＝狀態「新進」；待審報名＝狀態「待確認」；即將截止的營隊＝7 天內報名截止、狀態「開放／候補」的梯次；即將到期的贊助合約＝到期提醒日已到、合約尚未結束
/// （與 E2 清單的「提醒中」同一個判定，<c>AdminSponsorsRepository.ComputeContractStatus</c>）；FAQ 負評提醒＝👎 ≥ <see cref="NegativeFeedbackMinCount"/> 且多於 👍。
/// 「待出貨訂單」「待確認會籍申請」「候補」規劃書儀表板沒列，本次不放進待辦（會籍申請數放在會員概況的 <c>PendingUpgrades</c>）。
/// 儀表板不走快取：數字要即時，查詢都是索引友善的計數，每次載入約十幾個輕量查詢。
/// </summary>
public sealed class AdminDashboardRepository(
    ClubDbContext db, IPermissionChecker permissions, AdminEnquiriesRepository enquiries, TranslationStatusReader translationReader,
    IAnalyticsSource analytics)
{
    public const int NegativeFeedbackMinCount = 3;
    public const int SessionClosingWithinDays = 7;
    public const int UpcomingDays = 14;
    public const int UpcomingLimit = 30;
    private const int TodoItemLimit = 5;

    // 各區塊需要的檢視權限碼。
    private const string RegistrationView = "program.registration.view";
    private const string TrialRegistrationView = "program.trial_registration.view";
    private const string SessionView = "program.session.view";
    private const string TrialView = "program.trial.view";
    private const string SponsorView = "business.sponsor.view";
    private const string LeadView = "business.lead.view";
    private const string ArticleView = "content.article.view";
    private const string FaqView = "content.faq.view";
    private const string MatchView = "team.match.view";
    private const string CalendarEventView = "calendar.custom_event.view";
    private const string CalendarView = "calendar.view";
    private const string FanEventView = "culture.fan_event.view";
    private const string MembershipView = "member.membership.view";
    private const string TranslatorView = "site.string.view";
    private const string TranslatorTranslate = "site.string.translate";
    private const string LocaleView = "site.locale.view";

    private static readonly (string Code, string Label, string Permission)[] QuickEntries =
    [
        ("publish_news", "發布新聞", "content.article.create"),
        ("add_match", "新增賽事", "team.match.create"),
        ("add_session", "新增報名梯次", "program.session.create"),
        ("add_faq", "新增 FAQ", "content.faq.create"),
        ("add_calendar_event", "新增行事曆事件", "calendar.custom_event.create"),
    ];

    /// <summary>儀表板端點放行用的權限碼集合：所有區塊與快速入口用到的碼的聯集。</summary>
    public static readonly IReadOnlyList<string> AllCandidateCodes = BuildAllCandidateCodes();

    private static IReadOnlyList<string> BuildAllCandidateCodes()
    {
        var all = new List<string>(AdminEnquiriesRepository.ViewCandidateCodes)
        {
            RegistrationView, TrialRegistrationView, SessionView, TrialView, SponsorView, LeadView, ArticleView, FaqView,
            MatchView, CalendarEventView, CalendarView, FanEventView, MembershipView, "member.account.view",
            // 翻譯人員（只被指派字串翻譯表）與語系管理者：看得到所有內容類別的未翻譯數（矩陣：翻譯人員在各內容欄都是「僅翻譯欄位」）。
            TranslatorView, TranslatorTranslate, LocaleView,
        };
        all.AddRange(TranslationStatusReader.Catalog.Select(c => c.ViewPermission));
        all.AddRange(QuickEntries.Select(q => q.Permission));
        return all.Distinct(StringComparer.Ordinal).ToList();
    }

    public async Task<AdminDashboardDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var held = await permissions.GetHeldPermissionCodesAsync(
            scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, AllCandidateCodes, cancellationToken);
        var now = DateTime.UtcNow;
        var today = TaiwanClock.Today;

        var todos = await BuildTodosAsync(scope, held, now, today, cancellationToken);
        var content = held.Contains(ArticleView) || SeesAllTranslationTypes(held) || TranslationStatusReader.Catalog.Any(c => held.Contains(c.ViewPermission))
            ? await BuildContentAsync(scope, held, now, today, cancellationToken)
            : null;
        var faq = held.Contains(FaqView) ? await BuildFaqAsync(scope, cancellationToken) : null;
        var upcoming = await BuildUpcomingAsync(scope, held, today, cancellationToken);
        var members = held.Contains(MembershipView) ? await BuildMembersAsync(scope, today, cancellationToken) : null;

        return new AdminDashboardDto
        {
            GeneratedAt = now,
            Todos = todos,
            Content = content,
            Faq = faq,
            Upcoming = upcoming,
            Members = members,
            QuickEntries = QuickEntries.Where(q => held.Contains(q.Permission))
                .Select(q => new AdminDashboardQuickEntryDto { Code = q.Code, Label = q.Label }).ToList(),
        };
    }

    // ── 待辦提醒 ─────────────────────────────────────────────────────────────────
    private async Task<IReadOnlyList<AdminDashboardTodoDto>> BuildTodosAsync(
        AdminClubScope scope, IReadOnlySet<string> held, DateTime now, DateOnly today, CancellationToken cancellationToken)
    {
        var todos = new List<AdminDashboardTodoDto>();

        if (AdminEnquiriesRepository.ViewCandidateCodes.Any(held.Contains))
        {
            var allowed = await enquiries.ResolveViewFormCodeFilterAsync(scope, cancellationToken);
            var query = db.Enquiries.AsNoTracking().Where(e => e.ClubId == scope.ClubId && e.Status == "新進");
            if (allowed is not null)
            {
                var codes = allowed.ToList();
                query = query.Where(e => codes.Contains(e.Form.FormCode));
            }

            todos.Add(Todo("enquiries_new", "未處理的詢問", await query.CountAsync(cancellationToken), "狀態為「新進」的詢問"));
        }

        if (held.Contains(RegistrationView) || held.Contains(TrialRegistrationView))
        {
            var pending = db.Registrations.AsNoTracking().Where(r => r.ClubId == scope.ClubId && r.Status == "待確認");
            var count = 0;
            if (held.Contains(RegistrationView))
            {
                count += await pending.CountAsync(r => r.SessionId != null, cancellationToken);
            }

            if (held.Contains(TrialRegistrationView))
            {
                count += await pending.CountAsync(r => r.TrialId != null, cancellationToken);
            }

            todos.Add(Todo("registrations_pending", "待審核的報名", count, "狀態為「待確認」的報名"));
        }

        if (held.Contains(SessionView))
        {
            var limit = now.AddDays(SessionClosingWithinDays);
            var closing = db.Sessions.AsNoTracking().Where(s => s.ClubId == scope.ClubId && (s.Status == "開放" || s.Status == "候補")
                && s.SignupClosesAt != null && s.SignupClosesAt >= now && s.SignupClosesAt <= limit);
            var rows = await closing.OrderBy(s => s.SignupClosesAt).Take(TodoItemLimit)
                .Select(s => new
                {
                    s.Id, s.SignupClosesAt,
                    Name = s.TrainingProgram.ProgramsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);
            todos.Add(Todo("sessions_closing_soon", "即將截止的營隊", await closing.CountAsync(cancellationToken), $"{SessionClosingWithinDays} 天內截止報名",
                rows.Select(r => new AdminDashboardTodoItemDto
                {
                    Id = r.Id, Title = string.IsNullOrWhiteSpace(r.Name) ? "（未命名課程）" : r.Name!, Date = TaiwanClock.ToDate(r.SignupClosesAt!.Value),
                }).ToList()));
        }

        if (held.Contains(SponsorView))
        {
            var expiring = db.Sponsors.AsNoTracking().Where(s => s.ClubId == scope.ClubId && s.ContractEndOn != null && s.ContractEndOn >= today
                && s.ExpiryAlertOn != null && s.ExpiryAlertOn <= today);
            var rows = await expiring.OrderBy(s => s.ContractEndOn).Take(TodoItemLimit)
                .Select(s => new
                {
                    s.Id, s.ContractEndOn,
                    Name = s.SponsorsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);
            todos.Add(Todo("sponsor_contracts_expiring", "即將到期的贊助合約", await expiring.CountAsync(cancellationToken), "合約到期提醒日已到、尚未到期",
                rows.Select(r => new AdminDashboardTodoItemDto
                {
                    Id = r.Id, Title = string.IsNullOrWhiteSpace(r.Name) ? "（未命名贊助商）" : r.Name!, Date = r.ContractEndOn,
                }).ToList()));
        }

        return todos;
    }

    private static AdminDashboardTodoDto Todo(string code, string label, int count, string hint, IReadOnlyList<AdminDashboardTodoItemDto>? items = null)
        => new() { Code = code, Label = label, Count = count, Hint = hint, Items = items ?? [] };

    // ── 內容概況 ─────────────────────────────────────────────────────────────────
    private async Task<AdminDashboardContentDto> BuildContentAsync(
        AdminClubScope scope, IReadOnlySet<string> held, DateTime now, DateOnly today, CancellationToken cancellationToken)
    {
        int? published = null;
        int? draft = null;
        int? scheduled = null;
        if (held.Contains(ArticleView))
        {
            var monthStart = TaiwanClock.StartOfDayUtc(new DateOnly(today.Year, today.Month, 1));
            var nextMonthStart = TaiwanClock.StartOfDayUtc(new DateOnly(today.Year, today.Month, 1).AddMonths(1));
            var articles = db.Articles.AsNoTracking().Where(a => a.ClubId == scope.ClubId || a.ClubId == null);
            published = await articles.CountAsync(a => a.Status == "published" && a.PublishedAt != null
                && a.PublishedAt >= monthStart && a.PublishedAt < nextMonthStart && a.PublishedAt <= now, cancellationToken);
            draft = await articles.CountAsync(a => a.Status == "draft", cancellationToken);
            scheduled = await articles.CountAsync(a => a.Status == "scheduled", cancellationToken);
        }

        return new AdminDashboardContentDto
        {
            PublishedThisMonth = published,
            DraftCount = draft,
            ScheduledCount = scheduled,
            Untranslated = await BuildUntranslatedAsync(scope, held, cancellationToken),
        };
    }

    private async Task<IReadOnlyList<AdminDashboardUntranslatedDto>> BuildUntranslatedAsync(
        AdminClubScope scope, IReadOnlySet<string> held, CancellationToken cancellationToken)
    {
        var types = TranslationStatusReader.Catalog.Where(c => SeesAllTranslationTypes(held) || held.Contains(c.ViewPermission)).ToList();
        var locales = await db.Locales.AsNoTracking().Where(l => l.IsEnabled).OrderBy(l => l.SortOrder).ThenBy(l => l.Code).ToListAsync(cancellationToken);
        var targets = locales.Where(l => !l.IsDefault).ToList();
        if (types.Count == 0 || targets.Count == 0)
        {
            return [];
        }

        var defaultLocale = RequestLocale.DefaultDbLocale;
        var codes = locales.Select(l => l.Code).ToList();
        var rows = await translationReader.ReadAsync(scope.ClubId, types, codes, cancellationToken);
        var labels = types.ToDictionary(t => t.Type, t => t.LabelZh, StringComparer.Ordinal);
        return targets.Select(locale =>
        {
            var missing = rows.Where(r => r.DoneLocales.Contains(defaultLocale) && !r.DoneLocales.Contains(locale.Code)).ToList();
            return new AdminDashboardUntranslatedDto
            {
                Locale = locale.Code,
                LocaleName = locale.Name,
                Count = missing.Count,
                ByType = missing.GroupBy(r => r.Type).Select(g => new AdminDashboardUntranslatedTypeDto
                {
                    Type = g.Key, TypeLabel = labels[g.Key], Count = g.Count(),
                }).OrderByDescending(t => t.Count).ToList(),
            };
        }).ToList();
    }

    private static bool SeesAllTranslationTypes(IReadOnlySet<string> held)
        => held.Contains(TranslatorView) || held.Contains(TranslatorTranslate) || held.Contains(LocaleView);

    // ── FAQ 概況 ─────────────────────────────────────────────────────────────────
    private async Task<AdminDashboardFaqDto> BuildFaqAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var def = RequestLocale.DefaultDbLocale;
        var published = db.Faqs.AsNoTracking().Where(f => (f.ClubId == scope.ClubId || f.ClubId == null) && f.Status == "published");

        async Task<List<AdminDashboardFaqItemDto>> Load(IQueryable<Data.EfEntities.Faq> q)
        {
            var rows = await q.Take(10).Select(f => new
            {
                f.Id, f.ViewCount, f.HelpfulCount, f.UnhelpfulCount,
                Question = f.FaqsI18ns.Where(i => i.Locale == def).Select(i => i.Question).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
            return rows.Select(r => new AdminDashboardFaqItemDto
            {
                Id = r.Id, Question = string.IsNullOrWhiteSpace(r.Question) ? "（未命名題目）" : r.Question!,
                ViewCount = r.ViewCount, HelpfulCount = r.HelpfulCount, UnhelpfulCount = r.UnhelpfulCount,
            }).ToList();
        }

        return new AdminDashboardFaqDto
        {
            TopQuestions = await Load(published.OrderByDescending(f => f.ViewCount).ThenBy(f => f.RowSeq)),
            NegativeFeedback = await Load(published
                .Where(f => f.UnhelpfulCount >= NegativeFeedbackMinCount && f.UnhelpfulCount > f.HelpfulCount)
                .OrderByDescending(f => f.UnhelpfulCount).ThenBy(f => f.RowSeq)),
        };
    }

    // ── 近期行程（未來 14 天）─────────────────────────────────────────────────────────
    private async Task<IReadOnlyList<AdminDashboardUpcomingItemDto>> BuildUpcomingAsync(
        AdminClubScope scope, IReadOnlySet<string> held, DateOnly today, CancellationToken cancellationToken)
    {
        var last = today.AddDays(UpcomingDays - 1);
        var startUtc = TaiwanClock.StartOfDayUtc(today);
        var endUtc = TaiwanClock.StartOfDayUtc(last.AddDays(1));
        var def = RequestLocale.DefaultDbLocale;
        var items = new List<AdminDashboardUpcomingItemDto>();

        if (held.Contains(MatchView))
        {
            var matches = await db.Matches.AsNoTracking()
                .Where(m => m.ClubId == scope.ClubId && m.MatchOn >= today && m.MatchOn <= last)
                .OrderBy(m => m.MatchOn).Take(UpcomingLimit)
                .Select(m => new
                {
                    m.Id, m.MatchOn, m.Kickoff, m.Opponent, m.VenueId,
                    OpponentZh = m.MatchesI18ns.Where(i => i.Locale == def).Select(i => i.Opponent).FirstOrDefault(),
                    TeamCode = m.Teams.OrderBy(t => t.SortOrder).Select(t => t.Code).FirstOrDefault(),
                    VenueName = m.Venue == null ? null : m.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);
            items.AddRange(matches.Select(m =>
            {
                var opponent = string.IsNullOrWhiteSpace(m.OpponentZh) ? m.Opponent : m.OpponentZh;
                var warnings = new List<string>();
                if (string.IsNullOrWhiteSpace(opponent))
                {
                    warnings.Add("尚未填寫對手");
                }

                if (m.VenueId is null)
                {
                    warnings.Add("尚未設定場地");
                }

                if (string.IsNullOrWhiteSpace(m.Kickoff))
                {
                    warnings.Add("尚未設定開球時間");
                }

                return new AdminDashboardUpcomingItemDto
                {
                    Source = "match", Id = m.Id, Date = m.MatchOn, Time = string.IsNullOrWhiteSpace(m.Kickoff) ? null : m.Kickoff!.Trim(),
                    Title = string.IsNullOrWhiteSpace(opponent) ? "賽事（對手未定）" : $"對戰 {opponent!.Trim()}",
                    TeamCode = m.TeamCode, VenueName = m.VenueName, Warnings = warnings,
                };
            }));
        }

        if (held.Contains(SessionView))
        {
            var sessions = await db.Sessions.AsNoTracking()
                .Where(s => s.ClubId == scope.ClubId && s.StartOn != null && s.StartOn >= today && s.StartOn <= last && s.Status != "已結束")
                .OrderBy(s => s.StartOn).Take(UpcomingLimit)
                .Select(s => new
                {
                    s.Id, s.StartOn, s.Capacity, s.EnrolledCount, s.VenueId,
                    HasCoach = s.TrainingProgram.Staff.Any(),
                    Name = s.TrainingProgram.ProgramsI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                    VenueName = s.Venue == null ? null : s.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);
            items.AddRange(sessions.Select(s =>
            {
                var warnings = new List<string>();
                if (!s.HasCoach)
                {
                    warnings.Add("尚未指派教練");
                }

                if (s.Capacity is { } cap && s.EnrolledCount < cap)
                {
                    warnings.Add($"名額未滿（已報名 {s.EnrolledCount}／{cap}）");
                }

                if (s.VenueId is null)
                {
                    warnings.Add("尚未設定地點");
                }

                if (s.Capacity is null)
                {
                    warnings.Add("尚未設定名額上限");
                }

                return new AdminDashboardUpcomingItemDto
                {
                    Source = "session", Id = s.Id, Date = s.StartOn!.Value, Time = null,
                    Title = string.IsNullOrWhiteSpace(s.Name) ? "營隊／課程梯次" : s.Name!, VenueName = s.VenueName, Warnings = warnings,
                };
            }));
        }

        if (held.Contains(TrialView))
        {
            var trials = await db.Trials.AsNoTracking()
                .Where(t => t.ClubId == scope.ClubId && t.TrialOn >= today && t.TrialOn <= last && t.Status != "已結束")
                .OrderBy(t => t.TrialOn).Take(UpcomingLimit)
                .Select(t => new
                {
                    t.Id, t.TrialOn, t.Capacity, t.EnrolledCount, t.VenueId,
                    TeamCode = t.Team == null ? null : t.Team.Code,
                    Audience = t.TrialsI18ns.Where(i => i.Locale == def).Select(i => i.Audience).FirstOrDefault(),
                    VenueName = t.Venue == null ? null : t.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);
            items.AddRange(trials.Select(t =>
            {
                var warnings = new List<string>();
                if (t.Capacity is { } cap && t.EnrolledCount < cap)
                {
                    warnings.Add($"名額未滿（已報名 {t.EnrolledCount}／{cap}）");
                }

                if (t.VenueId is null)
                {
                    warnings.Add("尚未設定地點");
                }

                if (string.IsNullOrWhiteSpace(t.Audience))
                {
                    warnings.Add("尚未填寫試訓對象");
                }

                return new AdminDashboardUpcomingItemDto
                {
                    Source = "trial", Id = t.Id, Date = t.TrialOn, Time = null,
                    Title = string.IsNullOrWhiteSpace(t.Audience) ? "試訓" : $"試訓｜{t.Audience!.Trim()}",
                    TeamCode = t.TeamCode, VenueName = t.VenueName, Warnings = warnings,
                };
            }));
        }

        if (held.Contains(CalendarEventView) || held.Contains(CalendarView))
        {
            var events = await db.CalendarCustomEvents.AsNoTracking()
                .Where(e => e.ClubId == scope.ClubId && e.StartsAt >= startUtc && e.StartsAt < endUtc)
                .OrderBy(e => e.StartsAt).Take(UpcomingLimit)
                .Select(e => new
                {
                    e.Id, e.StartsAt, e.IsAllDay, e.VenueId,
                    Title = e.CalendarCustomEventsI18ns.Where(i => i.Locale == def).Select(i => i.Title).FirstOrDefault(),
                    VenueName = e.Venue == null ? null : e.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);
            items.AddRange(events.Select(e =>
            {
                var local = e.StartsAt.AddHours(8);
                var warnings = new List<string>();
                if (e.VenueId is null)
                {
                    warnings.Add("尚未設定地點");
                }

                if (string.IsNullOrWhiteSpace(e.Title))
                {
                    warnings.Add("尚未填寫事件名稱");
                }

                return new AdminDashboardUpcomingItemDto
                {
                    Source = "event", Id = e.Id, Date = DateOnly.FromDateTime(local), Time = e.IsAllDay ? null : local.ToString("HH:mm"),
                    Title = string.IsNullOrWhiteSpace(e.Title) ? "行事曆事件" : e.Title!, VenueName = e.VenueName, Warnings = warnings,
                };
            }));
        }

        if (held.Contains(FanEventView))
        {
            var events = await db.FanEvents.AsNoTracking()
                .Where(e => e.ClubId == scope.ClubId && e.Status == "published" && e.StartsAt != null && e.StartsAt >= startUtc && e.StartsAt < endUtc)
                .OrderBy(e => e.StartsAt).Take(UpcomingLimit)
                .Select(e => new
                {
                    e.Id, e.StartsAt, e.Capacity, e.VenueId,
                    Registered = e.FanEventRegistrations.Count(r => r.Status == "registered" || r.Status == "attended"),
                    Name = e.FanEventsI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                    VenueName = e.Venue == null ? null : e.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);
            items.AddRange(events.Select(e =>
            {
                var local = e.StartsAt!.Value.AddHours(8);
                var warnings = new List<string>();
                if (e.Capacity is { } cap && e.Registered < cap)
                {
                    warnings.Add($"名額未滿（已報名 {e.Registered}／{cap}）");
                }

                if (e.VenueId is null)
                {
                    warnings.Add("尚未設定地點");
                }

                return new AdminDashboardUpcomingItemDto
                {
                    Source = "fan_event", Id = e.Id, Date = DateOnly.FromDateTime(local), Time = local.ToString("HH:mm"),
                    Title = string.IsNullOrWhiteSpace(e.Name) ? "球迷會活動" : e.Name!, VenueName = e.VenueName, Warnings = warnings,
                };
            }));
        }

        return items.OrderBy(i => i.Date).ThenBy(i => i.Time ?? string.Empty, StringComparer.Ordinal).ThenBy(i => i.Source, StringComparer.Ordinal)
            .Take(UpcomingLimit).ToList();
    }

    // ── 會員概況 ─────────────────────────────────────────────────────────────────
    private async Task<AdminDashboardMembersDto> BuildMembersAsync(AdminClubScope scope, DateOnly today, CancellationToken cancellationToken)
    {
        var memberships = db.Memberships.AsNoTracking().Where(m => m.ClubId == scope.ClubId);
        var in30 = today.AddDays(30);
        return new AdminDashboardMembersDto
        {
            ActiveMemberships = await memberships.CountAsync(m => m.Status == "active", cancellationToken),
            ActivePaidMemberships = await memberships.CountAsync(m => m.Status == "active" && m.Tier == "fan_club", cancellationToken),
            ExpiringIn30Days = await memberships.CountAsync(m => m.Status == "active" && m.Tier == "fan_club"
                && m.MembershipEndOn != null && m.MembershipEndOn >= today && m.MembershipEndOn <= in30, cancellationToken),
            PendingUpgrades = await memberships.CountAsync(m => m.Status == "pending", cancellationToken),
        };
    }

    // ── 轉換概況 ─────────────────────────────────────────────────────────────────
    public async Task<AdminDashboardConversionDto> GetConversionAsync(AdminClubScope scope, string? period, CancellationToken cancellationToken)
    {
        var isWeek = string.IsNullOrWhiteSpace(period) || period.Trim().Equals("week", StringComparison.OrdinalIgnoreCase);
        if (!isWeek && !period!.Trim().Equals("month", StringComparison.OrdinalIgnoreCase))
        {
            throw new AdminValidationException("統計週期只能是「週」或「月」。");
        }

        var held = await permissions.GetHeldPermissionCodesAsync(
            scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, AllCandidateCodes, cancellationToken);
        var today = TaiwanClock.Today;
        var bucketCount = isWeek ? 8 : 6;
        var first = isWeek
            ? today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).AddDays(-7 * (bucketCount - 1))
            : new DateOnly(today.Year, today.Month, 1).AddMonths(-(bucketCount - 1));
        var fromUtc = TaiwanClock.StartOfDayUtc(first);

        int Index(DateTime utc)
        {
            var d = TaiwanClock.ToDate(utc);
            return isWeek ? (d.DayNumber - first.DayNumber) / 7 : ((d.Year - first.Year) * 12) + d.Month - first.Month;
        }

        int[]? Series(IEnumerable<DateTime>? stamps)
        {
            if (stamps is null)
            {
                return null;
            }

            var counts = new int[bucketCount];
            foreach (var s in stamps)
            {
                var i = Index(s);
                if (i >= 0 && i < bucketCount)
                {
                    counts[i]++;
                }
            }

            return counts;
        }

        // 詢問（含各表單送出數）：依 G2 類別授權過濾。
        List<(DateTime At, string FormCode)>? enquiryRows = null;
        if (AdminEnquiriesRepository.ViewCandidateCodes.Any(held.Contains))
        {
            var allowed = await enquiries.ResolveViewFormCodeFilterAsync(scope, cancellationToken);
            var q = db.Enquiries.AsNoTracking().Where(e => e.ClubId == scope.ClubId && e.CreatedAt >= fromUtc);
            if (allowed is not null)
            {
                var codes = allowed.ToList();
                q = q.Where(e => codes.Contains(e.Form.FormCode));
            }

            enquiryRows = (await q.Select(e => new { e.CreatedAt, e.Form.FormCode }).ToListAsync(cancellationToken))
                .Select(e => (e.CreatedAt, e.FormCode)).ToList();
        }

        // 提案下載：Lead 權限（獨立於詢問匣的類別授權）。
        List<DateTime>? proposalRows = null;
        if (held.Contains(LeadView))
        {
            proposalRows = await db.Enquiries.AsNoTracking()
                .Where(e => e.ClubId == scope.ClubId && e.CreatedAt >= fromUtc && e.Form.FormCode == FormCatalog.ProposalDownload)
                .Select(e => e.CreatedAt).ToListAsync(cancellationToken);
        }

        List<DateTime>? registrationRows = null;
        if (held.Contains(RegistrationView) || held.Contains(TrialRegistrationView))
        {
            var sessionRegs = held.Contains(RegistrationView);
            var trialRegs = held.Contains(TrialRegistrationView);
            registrationRows = await db.Registrations.AsNoTracking()
                .Where(r => r.ClubId == scope.ClubId && r.CreatedAt >= fromUtc
                    && ((sessionRegs && r.SessionId != null) || (trialRegs && r.TrialId != null)))
                .Select(r => r.CreatedAt).ToListAsync(cancellationToken);
        }

        List<DateTime>? memberRows = null;
        List<(DateTime At, bool Renewal)>? paymentRows = null;
        if (held.Contains(MembershipView))
        {
            memberRows = await db.Memberships.AsNoTracking()
                .Where(m => m.ClubId == scope.ClubId && m.CreatedAt >= fromUtc).Select(m => m.CreatedAt).ToListAsync(cancellationToken);
            paymentRows = (await db.MembershipPayments.AsNoTracking()
                .Where(p => p.ClubId == scope.ClubId && p.CreatedAt >= fromUtc)
                .Select(p => new
                {
                    p.CreatedAt,
                    HasEarlier = db.MembershipPayments.Any(x => x.MembershipId == p.MembershipId && x.CreatedAt < p.CreatedAt),
                }).ToListAsync(cancellationToken)).Select(p => (p.CreatedAt, p.HasEarlier)).ToList();
        }

        var enquirySeries = Series(enquiryRows?.Where(r => r.FormCode != FormCatalog.ProposalDownload).Select(r => r.At));
        var proposalSeries = Series(proposalRows);
        var registrationSeries = Series(registrationRows);
        var memberSeries = Series(memberRows);
        var newPaidSeries = Series(paymentRows?.Where(p => !p.Renewal).Select(p => p.At));
        var renewalSeries = Series(paymentRows?.Where(p => p.Renewal).Select(p => p.At));

        var buckets = Enumerable.Range(0, bucketCount).Select(i => new AdminDashboardConversionBucketDto
        {
            Start = isWeek ? first.AddDays(7 * i) : first.AddMonths(i),
            Enquiries = enquirySeries?[i],
            Registrations = registrationSeries?[i],
            ProposalDownloads = proposalSeries?[i],
            NewMembers = memberSeries?[i],
            NewPaidMemberships = newPaidSeries?[i],
            Renewals = renewalSeries?[i],
        }).ToList();

        int? Sum(int[]? s) => s?.Sum();
        return new AdminDashboardConversionDto
        {
            Period = isWeek ? "week" : "month",
            From = first,
            To = today,
            Totals = new AdminDashboardConversionBucketDto
            {
                Enquiries = Sum(enquirySeries), Registrations = Sum(registrationSeries), ProposalDownloads = Sum(proposalSeries),
                NewMembers = Sum(memberSeries), NewPaidMemberships = Sum(newPaidSeries), Renewals = Sum(renewalSeries),
            },
            Buckets = buckets,
            Forms = (enquiryRows ?? []).GroupBy(r => r.FormCode)
                .Select(g => new AdminDashboardFormCountDto { FormCode = g.Key, FormName = FormCatalog.DisplayNameZh(g.Key), Count = g.Count() })
                .OrderByDescending(f => f.Count).ThenBy(f => f.FormCode, StringComparer.Ordinal).ToList(),
        };
    }

    // ── 流量概況（GA4 接縫）────────────────────────────────────────────────────────
    public async Task<AdminDashboardTrafficDto> GetTrafficAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var to = TaiwanClock.Today;
        var from = to.AddDays(-6);
        var result = await analytics.GetOverviewAsync(new AnalyticsQuery(scope.ClubCode, from, to), cancellationToken);
        return new AdminDashboardTrafficDto { Configured = result.Configured, Message = result.Message, From = from, To = to, Overview = result.Overview };
    }
}
