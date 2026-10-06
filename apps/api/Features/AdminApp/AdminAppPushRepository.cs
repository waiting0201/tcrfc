using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>呼叫者在 M3 的細項權限（決定回應裡有哪些可用操作，伺服器端各操作仍各自授權）。</summary>
public sealed record PushCaller(Guid AdminUserId, bool CanCreate, bool CanApprove)
{
    public static async Task<PushCaller> ResolveAsync(AdminSystemScope scope, IPermissionChecker checker, CancellationToken cancellationToken)
    {
        async Task<bool> Has(string code) => await checker.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, code, cancellationToken);
        return new PushCaller(scope.Identity.AdminUserId, await Has("app.push.create"), await Has("app.push.approve"));
    }
}

/// <summary>
/// M3 推播通知管理（App 規劃書 §6、§8.3）。工作流程：草稿 → 送審 → <b>覆核核可（系統管理員，且不得是建立者本人）</b> → 排程／發送。
/// 「推播是不可撤回的全體觸及行為」，所以：公關／媒體可以建立、預覽、試送與送審；核可需要另一位系統管理員，並且必須回報「預估觸及裝置數」作二次確認。
/// 阻擋中獎通知由 <see cref="PushContentGuard"/> 在建立、送審與核可三個時點都檢查。
/// </summary>
public sealed class AdminAppPushRepository(
    ClubDbContext dbContext, PushContentGuard guard, PushDispatcher dispatcher, IPushTransport transport, PushTokenProtector protector,
    IImagePublicUrlResolver imageUrls, SensitiveActionLogger audit)
{
    private static readonly IReadOnlyDictionary<string, string> StatusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["draft"] = "草稿", ["pending_review"] = "待覆核", ["scheduled"] = "已排程", ["sending"] = "發送中", ["sent"] = "已發送",
        ["partial"] = "部分送出", ["failed"] = "失敗", ["cancelled"] = "已取消",
    };

    private static readonly IReadOnlySet<string> Kinds = new HashSet<string>(["announcement", "news", "match"], StringComparer.Ordinal);
    public const string StatsNote = "「送出」是我方交給推播服務的則數；「送達」是推播服務已接受且沒有回報權杖失效的則數，代表已交付給推播服務，不等於已經到達使用者的手機；「開啟」是 App 回報的通知開啟次數。系統不追蹤是哪一位使用者開啟。";

    private static readonly IReadOnlyDictionary<string, string> TierLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["all"] = "全部裝置", ["fan_club"] = "球迷會員", ["registered"] = "一般會員", ["anonymous"] = "未登入裝置",
    };

    public async Task<IReadOnlyList<AdminPushMessageListItemDto>> ListAsync(string? status, CancellationToken cancellationToken)
    {
        if (status is not null && !StatusLabels.ContainsKey(status))
        {
            throw new AdminValidationException("狀態篩選不正確。");
        }

        var q = dbContext.PushMessages.AsNoTracking().AsQueryable();
        if (status is not null)
        {
            q = q.Where(m => m.Status == status);
        }

        return await q.OrderByDescending(m => m.RowSeq).Take(200).Select(m => new AdminPushMessageListItemDto
        {
            Id = m.Id, Kind = m.Kind, Status = m.Status, StatusLabel = m.Status, ScheduledAt = m.ScheduledAt, SentAt = m.SentAt, SentCount = m.SentCount,
            DeliveredCount = m.DeliveredCount, OpenedCount = m.OpenedCount, CreatedBy = m.CreatedBy, CreatedByName = m.CreatedByNavigation == null ? null : m.CreatedByNavigation.DisplayName, CreatedAt = m.CreatedAt,
            TitleZh = m.PushMessagesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Title).FirstOrDefault(),
        }).ToListAsync(cancellationToken).ContinueWith(t => (IReadOnlyList<AdminPushMessageListItemDto>)t.Result.Select(x => x with { StatusLabel = StatusLabels.GetValueOrDefault(x.Status, x.Status) }).ToList(), cancellationToken);
    }

    public async Task<AdminPushMessageDto?> GetAsync(Guid id, PushCaller caller, CancellationToken cancellationToken)
    {
        var m = await LoadAsync(id, cancellationToken);
        return m is null ? null : ToDto(m, caller);
    }

    public async Task<AdminPushMessageDto> CreateAsync(
        Guid id, UpsertAdminPushMessageRequest request, UploadedImageInfo? image, PushCaller caller, CancellationToken cancellationToken)
    {
        var (clubId, teams) = await ValidateAsync(request, cancellationToken);
        await guard.EnsureAllowedAsync(request.Content, request.DeepLink, cancellationToken);
        var now = DateTime.UtcNow;
        var m = new PushMessage { Id = id, Status = "draft", CreatedAt = now, UpdatedAt = now, CreatedBy = caller.AdminUserId, UpdatedBy = caller.AdminUserId };
        Apply(m, request, clubId, teams);
        if (image is not null)
        {
            m.ImageKey = image.Key;
            m.ImageWidth = image.Width;
            m.ImageHeight = image.Height;
        }

        SetI18n(m, request.Content);
        dbContext.PushMessages.Add(m);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(id, caller, cancellationToken))!;
    }

    public async Task<AdminPushMessageDto?> UpdateAsync(
        Guid id, UpsertAdminPushMessageRequest request, ImageFieldUpdate image, OrphanedObjects orphans, PushCaller caller, CancellationToken cancellationToken)
    {
        var m = await dbContext.PushMessages.Include(x => x.PushMessagesI18ns).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (m is null)
        {
            return null;
        }

        RequireStatus(m, ["draft"], "只有草稿可以修改；已送審的批次請先退回草稿。");
        var (clubId, teams) = await ValidateAsync(request, cancellationToken);
        await guard.EnsureAllowedAsync(request.Content, request.DeepLink, cancellationToken);
        Apply(m, request, clubId, teams);
        if (image.Change)
        {
            orphans.Image(m.ImageKey);
            m.ImageKey = image.Key;
            m.ImageWidth = image.Width;
            m.ImageHeight = image.Height;
        }

        SetI18n(m, request.Content);
        m.UpdatedAt = DateTime.UtcNow;
        m.UpdatedBy = caller.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, caller, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var m = await dbContext.PushMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (m is null)
        {
            return false;
        }

        RequireStatus(m, ["draft", "cancelled"], "只有草稿或已取消的批次可以刪除；已發送的紀錄要保留。");
        orphans.Image(m.ImageKey);
        dbContext.PushMessages.Remove(m);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ═════════ 試算、預覽、試送 ═════════

    /// <summary>發送前的分眾人數試算：只回人數（依平台與語系分），不寫入、不回傳裝置清單。</summary>
    public async Task<AdminPushAudienceEstimateDto> EstimateAsync(PushAudienceRequest request, CancellationToken cancellationToken)
    {
        var spec = await ResolveSpecAsync(request.AudienceTier, request.AudienceClubCode, request.AudienceTeamCodes, cancellationToken);
        return await EstimateAsync(spec, cancellationToken);
    }

    private async Task<AdminPushAudienceEstimateDto> EstimateAsync(PushAudienceSpec spec, CancellationToken cancellationToken)
    {
        var rows = await PushAudience.Devices(dbContext, spec).GroupBy(d => new { d.Platform, d.Locale })
            .Select(g => new { g.Key.Platform, g.Key.Locale, Count = g.Count() }).ToListAsync(cancellationToken);
        return new AdminPushAudienceEstimateDto
        {
            Total = rows.Sum(r => r.Count),
            Breakdown = rows.Select(r => new AdminPushStatRowDto
            {
                Platform = r.Platform, PlatformLabel = AdLabels.Of(AdLabels.Platform, r.Platform), Locale = r.Locale ?? "unknown",
                LocaleLabel = AdLabels.Of(AdLabels.Locale, r.Locale ?? "unknown"), Sent = r.Count, Delivered = 0, Opened = 0,
            }).OrderBy(r => r.Platform).ThenBy(r => r.Locale).ToList(),
        };
    }

    public async Task<AdminPushPreviewDto?> PreviewAsync(Guid id, CancellationToken cancellationToken)
    {
        var m = await LoadAsync(id, cancellationToken);
        if (m is null)
        {
            return null;
        }

        var zh = Content(m, RequestLocale.DefaultDbLocale) ?? new PushLocaleContent { Title = string.Empty, Body = string.Empty };
        var en = Content(m, "en");
        return new AdminPushPreviewDto
        {
            Zh = zh, En = en, EnEffective = en is null || string.IsNullOrWhiteSpace(en.Title) ? zh : en, DeepLink = m.DeepLink,
            ImageUrl = imageUrls.Resolve(m.ImageKey), AudienceSummary = Summarize(m),
        };
    }

    /// <summary>指定測試裝置試送。傳輸尚未串接時如實回報，不會假裝成功。</summary>
    public async Task<AdminPushTestSendResultDto?> TestSendAsync(Guid id, PushTestSendRequest request, AdminSystemScope scope, CancellationToken cancellationToken)
    {
        var m = await LoadAsync(id, cancellationToken);
        if (m is null)
        {
            return null;
        }

        if (request.DeviceInstallIds.Count is 0 or > 10)
        {
            throw new AdminValidationException("試送請指定 1 到 10 台測試裝置。");
        }

        await guard.EnsureAllowedAsync(new PushContentInput { Zh = Content(m, RequestLocale.DefaultDbLocale)!, En = Content(m, "en") }, m.DeepLink, cancellationToken);
        var ids = request.DeviceInstallIds.Select(AppInput.RequireDeviceId).Distinct().ToList();
        var devices = await dbContext.AppDevices.AsNoTracking().Where(d => ids.Contains(d.DeviceInstallId) && d.PushTokenStatus == "valid")
            .Select(d => new { d.Id, d.Platform, d.Locale, d.PushTokenEncrypted }).ToListAsync(cancellationToken);
        var unknown = ids.Count - devices.Count;
        int sent = 0, failed = 0;
        var configured = true;
        foreach (var d in devices)
        {
            var locale = d.Locale == "en" ? "en" : RequestLocale.DefaultDbLocale;
            var c = Content(m, locale) is { } localized && !string.IsNullOrWhiteSpace(localized.Title) ? localized : Content(m, RequestLocale.DefaultDbLocale)!;
            var token = protector.TryDecrypt(d.PushTokenEncrypted);
            if (token is null)
            {
                failed++;
                continue;
            }

            var r = await transport.SendAsync(new PushTarget(d.Id, d.Platform, token), new PushPayload(m.Id, "[測試] " + c.Title, c.Body, imageUrls.Resolve(m.ImageKey), m.DeepLink), cancellationToken);
            if (r.Outcome == PushSendOutcome.NotConfigured)
            {
                configured = false;
                break;
            }

            if (r.Outcome == PushSendOutcome.Accepted) { sent++; } else { failed++; }
        }

        audit.Record(scope, "推播測試送出", $"批次 {id}", devices.Count, "測試裝置試送");
        return new AdminPushTestSendResultDto
        {
            Configured = configured, Sent = sent, Failed = failed, UnknownDevices = unknown,
            Message = configured ? $"已送出 {sent} 台，失敗 {failed} 台，找不到或權杖無效 {unknown} 台。" : "推播服務尚未串接（APNs 與 FCM 金鑰尚未建立），目前無法試送。",
        };
    }

    // ═════════ 工作流程 ═════════

    public async Task<AdminPushMessageDto?> SubmitAsync(Guid id, PushCaller caller, CancellationToken cancellationToken)
    {
        var m = await dbContext.PushMessages.Include(x => x.PushMessagesI18ns).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (m is null)
        {
            return null;
        }

        RequireStatus(m, ["draft"], "只有草稿可以送審。");
        var zh = Content(m, RequestLocale.DefaultDbLocale);
        if (zh is null || string.IsNullOrWhiteSpace(zh.Title) || string.IsNullOrWhiteSpace(zh.Body))
        {
            throw new AdminValidationException("送審前必須填寫繁中的標題與內文。", "titleZh");
        }

        await guard.EnsureAllowedAsync(new PushContentInput { Zh = zh, En = Content(m, "en") }, m.DeepLink, cancellationToken);
        m.Status = "pending_review";
        m.RejectNote = null;
        m.UpdatedAt = DateTime.UtcNow;
        m.UpdatedBy = caller.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, caller, cancellationToken);
    }

    public async Task<AdminPushMessageDto?> ReturnAsync(Guid id, string? note, PushCaller caller, CancellationToken cancellationToken)
    {
        var m = await dbContext.PushMessages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (m is null)
        {
            return null;
        }

        RequireStatus(m, ["pending_review"], "只有待覆核的批次可以退回。");
        m.Status = "draft";
        m.RejectNote = AdminInput.RequireText(note, "退回原因", 255, "note");
        m.UpdatedAt = DateTime.UtcNow;
        m.UpdatedBy = caller.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, caller, cancellationToken);
    }

    /// <summary>核可：🔴 雙人覆核（核可者不得是建立者）＋二次確認（<c>ExpectedAudience</c> 必須等於伺服器當下重算的人數）。
    /// 核可後排程；排程時間已到（或沒填）就立刻發送。</summary>
    public async Task<AdminPushMessageDto?> ApproveAsync(Guid id, ApprovePushMessageRequest request, PushCaller caller, AdminSystemScope scope, CancellationToken cancellationToken)
    {
        var m = await dbContext.PushMessages.Include(x => x.PushMessagesI18ns).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (m is null)
        {
            return null;
        }

        RequireStatus(m, ["pending_review"], "只有待覆核的批次可以核可。");
        if (m.CreatedBy == caller.AdminUserId)
        {
            throw new AdminConflictException("不能自己覆核自己的批次", "推播是不可撤回的全體觸及行為，必須由另一位系統管理員覆核，建立者本人不能核可。");
        }

        var zh = Content(m, RequestLocale.DefaultDbLocale)!;
        await guard.EnsureAllowedAsync(new PushContentInput { Zh = zh, En = Content(m, "en") }, m.DeepLink, cancellationToken);
        var estimate = (await EstimateAsync(PushAudienceSpec.From(m), cancellationToken)).Total;
        if (estimate == 0)
        {
            throw new AdminConflictException("沒有符合條件的裝置", "依目前的分眾條件，沒有可以收到推播的裝置（權杖有效且已允許推播）。請調整分眾條件。");
        }

        if (request.ExpectedAudience != estimate)
        {
            throw new AdminConflictException("預估人數已變動", $"你確認的預估觸及裝置數是 {request.ExpectedAudience}，但現在重新計算是 {estimate}。請確認新的人數後再核可一次。");
        }

        var now = DateTime.UtcNow;
        m.Status = "scheduled";
        m.ReviewedBy = caller.AdminUserId;
        m.ReviewedAt = now;
        m.AudienceEstimate = estimate;
        m.ScheduledAt ??= now;
        m.UpdatedAt = now;
        m.UpdatedBy = caller.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "核可推播批次", $"批次 {id}", estimate, "雙人覆核核可");
        if (m.ScheduledAt <= now)
        {
            await dispatcher.DispatchAsync(id, cancellationToken);
        }

        return await GetAsync(id, caller, cancellationToken);
    }

    /// <summary>取消尚未送達的批次（草稿、待覆核、已排程）。發送中或已發送的批次不能取消。</summary>
    public async Task<AdminPushMessageDto?> CancelAsync(Guid id, PushCaller caller, AdminSystemScope scope, CancellationToken cancellationToken)
    {
        var affected = await dbContext.PushMessages.Where(m => m.Id == id && (m.Status == "draft" || m.Status == "pending_review" || m.Status == "scheduled"))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "cancelled").SetProperty(m => m.UpdatedAt, DateTime.UtcNow).SetProperty(m => m.UpdatedBy, caller.AdminUserId), cancellationToken);
        if (affected == 0)
        {
            if (!await dbContext.PushMessages.AnyAsync(m => m.Id == id, cancellationToken))
            {
                return null;
            }

            throw new AdminConflictException("批次不能取消", "只有草稿、待覆核與已排程（尚未開始發送）的批次可以取消。");
        }

        audit.Record(scope, "取消推播批次", $"批次 {id}", 1, null);
        return await GetAsync(id, caller, cancellationToken);
    }

    /// <summary>失敗重送：從游標續送（已處理的裝置不會重送）。需要核可權限——重送也是全體觸及行為。</summary>
    public async Task<AdminPushMessageDto?> RetryAsync(Guid id, PushCaller caller, AdminSystemScope scope, CancellationToken cancellationToken)
    {
        var affected = await dbContext.PushMessages.Where(m => m.Id == id && (m.Status == "failed" || m.Status == "partial") && m.ReviewedBy != null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "scheduled").SetProperty(m => m.FailureMessage, (string?)null).SetProperty(m => m.UpdatedAt, DateTime.UtcNow), cancellationToken);
        if (affected == 0)
        {
            if (!await dbContext.PushMessages.AnyAsync(m => m.Id == id, cancellationToken))
            {
                return null;
            }

            throw new AdminConflictException("批次不能重送", "只有已核可、但發送失敗或部分送出的批次可以重送。");
        }

        audit.Record(scope, "重送推播批次", $"批次 {id}", 1, null);
        await dispatcher.DispatchAsync(id, cancellationToken);
        return await GetAsync(id, caller, cancellationToken);
    }

    // ═════════ 內部 ═════════

    private async Task<PushMessage?> LoadAsync(Guid id, CancellationToken cancellationToken)
        => await dbContext.PushMessages.AsNoTracking().Include(m => m.PushMessagesI18ns).Include(m => m.PushMessageStats).Include(m => m.AudienceClub)
            .Include(m => m.CreatedByNavigation).Include(m => m.ReviewedByNavigation)
            .AsSplitQuery().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    private static void RequireStatus(PushMessage m, string[] allowed, string message)
    {
        if (!allowed.Contains(m.Status))
        {
            throw new AdminConflictException("目前狀態不能這樣操作", message);
        }
    }

    private async Task<PushAudienceSpec> ResolveSpecAsync(string? tier, string? clubCode, IReadOnlyList<string>? teamCodes, CancellationToken cancellationToken)
    {
        var t = AdminInput.OneOf(tier ?? "all", PushAudienceSpec.Tiers, "會籍層級", "「全部」「球迷會員」「一般會員」或「未登入」", "audienceTier");
        Guid? clubId = null;
        if (!string.IsNullOrWhiteSpace(clubCode))
        {
            clubId = await dbContext.Clubs.AsNoTracking().Where(c => c.Code == clubCode).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken)
                     ?? throw new AdminValidationException("找不到這個俱樂部代碼。", "audienceClubCode");
        }

        var teams = (teamCodes ?? []).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList();
        if (teams.Count > 0)
        {
            var known = await dbContext.Teams.AsNoTracking().Where(x => teams.Contains(x.Code)).Select(x => x.Code).ToListAsync(cancellationToken);
            var unknown = teams.Except(known).FirstOrDefault();
            if (unknown is not null)
            {
                throw new AdminValidationException("分眾條件裡有找不到的球隊，請重新選擇。", "audienceTeamCodes");
            }
        }

        return new PushAudienceSpec(t, clubId, teams);
    }

    private async Task<(Guid? ClubId, IReadOnlyList<string> Teams)> ValidateAsync(UpsertAdminPushMessageRequest r, CancellationToken cancellationToken)
    {
        AdminInput.RequireText(r.Content.Zh.Title, "標題（繁中）", 120, "titleZh");
        AdminInput.RequireText(r.Content.Zh.Body, "內文（繁中）", 500, "bodyZh");
        if (r.Content.En is { } en)
        {
            AdminInput.OptionalText(en.Title, "標題（英文）", 120, "titleEn");
            AdminInput.OptionalText(en.Body, "內文（英文）", 500, "bodyEn");
        }

        AdminInput.OneOf(r.Kind ?? "announcement", Kinds, "推播類型", "「一般公告」「新聞」或「賽事」", "kind");
        var link = AdminInput.OptionalText(r.DeepLink, "深連結", 500, "deepLink");
        if (link is not null && !link.StartsWith("tcrfc://", StringComparison.Ordinal))
        {
            AdminInput.OptionalHttpUrl(link, "深連結", 500, "deepLink");
        }

        if (r.ScheduledAt is { } at && at < DateTimeOffset.UtcNow.AddMinutes(-5))
        {
            throw new AdminValidationException("排程時間不能是過去的時間。", "scheduledAt");
        }

        var spec = await ResolveSpecAsync(r.AudienceTier, r.AudienceClubCode, r.AudienceTeamCodes, cancellationToken);
        return (spec.ClubId, spec.TeamCodes);
    }

    private static void Apply(PushMessage m, UpsertAdminPushMessageRequest r, Guid? clubId, IReadOnlyList<string> teams)
    {
        m.Kind = r.Kind ?? "announcement";
        m.DeepLink = AdminInput.OptionalText(r.DeepLink, "深連結", 500);
        m.AudienceTier = r.AudienceTier ?? "all";
        m.AudienceClubId = clubId;
        m.AudienceTeamCodes = teams.Count == 0 ? null : JsonSerializer.Serialize(teams);
        m.ScheduledAt = r.ScheduledAt?.UtcDateTime;
    }

    private static void SetI18n(PushMessage m, PushContentInput c)
    {
        Upsert(m, RequestLocale.DefaultDbLocale, c.Zh);
        if (c.En is not null)
        {
            Upsert(m, "en", c.En);
        }
    }

    private static void Upsert(PushMessage m, string locale, PushLocaleContent c)
    {
        var row = m.PushMessagesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new PushMessagesI18n { PushMessageId = m.Id, Locale = locale };
            m.PushMessagesI18ns.Add(row);
        }

        row.Title = AdminInput.OptionalText(c.Title, "標題", 120);
        row.Body = AdminInput.OptionalText(c.Body, "內文", 500);
        row.ImageAlt = AdminInput.OptionalText(c.ImageAlt, "圖片說明文字", 200);
    }

    private static PushLocaleContent? Content(PushMessage m, string locale)
    {
        var i = m.PushMessagesI18ns.FirstOrDefault(x => x.Locale == locale);
        return i is null ? null : new PushLocaleContent { Title = i.Title ?? string.Empty, Body = i.Body ?? string.Empty, ImageAlt = i.ImageAlt };
    }

    private static string Summarize(PushMessage m)
    {
        var parts = new List<string> { TierLabels.GetValueOrDefault(m.AudienceTier, m.AudienceTier) };
        if (m.AudienceClub is not null)
        {
            parts.Add($"俱樂部：{m.AudienceClub.Code}");
        }

        var teams = PushAudienceSpec.ParseTeamCodes(m.AudienceTeamCodes);
        if (teams.Count > 0)
        {
            parts.Add("追蹤球隊：" + string.Join("、", teams));
        }

        return string.Join("｜", parts);
    }

    private static IReadOnlyList<string> Actions(PushMessage m, PushCaller c)
    {
        var a = new List<string>();
        switch (m.Status)
        {
            case "draft" when c.CanCreate:
                a.AddRange(["edit", "submit", "test_send", "cancel", "delete"]);
                break;
            case "pending_review":
                if (c.CanApprove && m.CreatedBy != c.AdminUserId) { a.Add("approve"); }
                if (c.CanApprove) { a.Add("return"); }
                if (c.CanCreate) { a.Add("cancel"); }
                break;
            case "scheduled" when c.CanCreate:
                a.Add("cancel");
                break;
            case "failed" or "partial":
                if (c.CanApprove && m.ReviewedBy != null) { a.Add("retry"); }
                break;
            case "cancelled" when c.CanCreate:
                a.Add("delete");
                break;
        }

        return a;
    }

    private AdminPushMessageDto ToDto(PushMessage m, PushCaller caller) => new()
    {
        Id = m.Id, Kind = m.Kind, Status = m.Status, StatusLabel = StatusLabels.GetValueOrDefault(m.Status, m.Status),
        Content = new PushContentInput { Zh = Content(m, RequestLocale.DefaultDbLocale) ?? new PushLocaleContent { Title = string.Empty, Body = string.Empty }, En = Content(m, "en") },
        ImageKey = m.ImageKey, ImageUrl = imageUrls.Resolve(m.ImageKey), DeepLink = m.DeepLink, AudienceTier = m.AudienceTier,
        AudienceTierLabel = TierLabels.GetValueOrDefault(m.AudienceTier, m.AudienceTier), AudienceClubCode = m.AudienceClub?.Code,
        AudienceTeamCodes = PushAudienceSpec.ParseTeamCodes(m.AudienceTeamCodes), ScheduledAt = m.ScheduledAt, AudienceEstimate = m.AudienceEstimate,
        CreatedBy = m.CreatedBy, CreatedByName = m.CreatedByNavigation?.DisplayName, ReviewedBy = m.ReviewedBy, ReviewedByName = m.ReviewedByNavigation?.DisplayName, ReviewedAt = m.ReviewedAt, RejectNote = m.RejectNote, SentAt = m.SentAt,
        SentCount = m.SentCount, DeliveredCount = m.DeliveredCount, FailedCount = m.FailedCount, OpenedCount = m.OpenedCount, FailureMessage = m.FailureMessage,
        Stats = m.PushMessageStats.OrderBy(s => s.Platform).ThenBy(s => s.Locale).Select(s => new AdminPushStatRowDto
        {
            Platform = s.Platform, PlatformLabel = AdLabels.Of(AdLabels.Platform, s.Platform), Locale = s.Locale, LocaleLabel = AdLabels.Of(AdLabels.Locale, s.Locale),
            Sent = s.Sent, Delivered = s.Delivered, Opened = s.Opened,
        }).ToList(),
        StatsNote = StatsNote, AvailableActions = Actions(m, caller), UpdatedAt = m.UpdatedAt,
    };
}
