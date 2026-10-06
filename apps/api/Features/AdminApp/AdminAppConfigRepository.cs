using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>
/// M5 App 設定與連線檢查（App 規劃書 §8.5）：功能開關、金鑰與憑證列管、診斷回報收件匣與彙總、連線檢查。
/// 🔴 憑證列管只存「列管資訊」（種類、代號、日期），<b>絕不存金鑰本身</b>——金鑰在 Key Vault／環境變數。輪替寫敏感操作日誌。
/// 🔴 功能開關 <c>payment_mode</c>（<c>off／external／inapp</c>）有方向規則（docs/19 §10）：只能降級，不得從 <c>external</c> 或 <c>off</c> 遠端開成 <c>inapp</c>——
/// 以 external 送審、通過後再遠端開啟未經審查的 inapp 是違規，會被下架。
/// </summary>
public sealed partial class AdminAppConfigRepository(
    ClubDbContext dbContext, AppConfigComposer composer, IAppConfigPublisher publisher, IPushTransport transport, SensitiveActionLogger audit)
{
    public const int DueSoonDays = 60;
    public const string PaymentModeKey = "payment_mode";
    private static readonly string[] PaymentModes = ["off", "external", "inapp"];

    [GeneratedRegex(@"^[a-z0-9]+(_[a-z0-9]+)+$")]
    private static partial Regex FlagKeyFormat();

    private static readonly IReadOnlyDictionary<string, string> KindLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["apns_key"] = "Apple 推播金鑰", ["fcm_credential"] = "Google 推播憑證", ["apple_developer_program"] = "Apple 開發者帳號會籍",
        ["google_play_account"] = "Google Play 開發者帳號", ["maps_api_key"] = "地圖服務金鑰", ["other"] = "其他",
    };

    private static readonly IReadOnlyDictionary<string, string> ReportTypeLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["crash"] = "崩潰", ["abnormal_exit"] = "異常退出", ["api_error"] = "連線錯誤", ["startup_time"] = "啟動耗時", ["user_report"] = "使用者回報",
    };

    private static readonly IReadOnlyDictionary<string, string> ReportStatusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["new"] = "新回報", ["reviewing"] = "處理中", ["resolved"] = "已解決", ["ignored"] = "略過",
    };

    // ═════════ 功能開關 ═════════

    public async Task<IReadOnlyList<AdminAppFeatureFlagDto>> ListFlagsAsync(CancellationToken cancellationToken)
        => (await dbContext.AppFeatureFlags.AsNoTracking().OrderBy(f => f.FlagKey).ThenBy(f => f.Platform).ToListAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<AppConfigChangeDto<AdminAppFeatureFlagDto>> CreateFlagAsync(UpsertAdminAppFeatureFlagRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (key, platform) = ValidateFlag(request);
        if (await dbContext.AppFeatureFlags.AnyAsync(f => f.FlagKey == key && f.Platform == platform, cancellationToken))
        {
            throw new AdminConflictException("開關重複", $"「{key}」在這個平台範圍已經有開關了。");
        }

        if (key == PaymentModeKey && request.StringValue == "inapp")
        {
            throw new AdminConflictException("不能開啟 App 內付款", "付款模式只能設為「關閉」或「外開瀏覽器」；App 內付款須以審查通過的版本出廠，不能用遠端開關開啟。");
        }

        var now = DateTime.UtcNow;
        var f = new AppFeatureFlag { Id = Guid.NewGuid(), FlagKey = key, Platform = platform, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId };
        ApplyFlag(f, request);
        dbContext.AppFeatureFlags.Add(f);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AppConfigChangeDto<AdminAppFeatureFlagDto> { Value = ToDto(f), EdgePublish = await PublishAsync(cancellationToken) };
    }

    public async Task<AppConfigChangeDto<AdminAppFeatureFlagDto>?> UpdateFlagAsync(Guid id, UpsertAdminAppFeatureFlagRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (key, platform) = ValidateFlag(request);
        var f = await dbContext.AppFeatureFlags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (f is null)
        {
            return null;
        }

        if (f.FlagKey != key || f.Platform != platform)
        {
            throw new AdminValidationException("開關名稱與平台範圍建立後不能修改；請新增另一個開關。");
        }

        if (key == PaymentModeKey)
        {
            // 方向規則：只能降級。inapp 之外的值不得改成 inapp。
            if (request.StringValue == "inapp" && f.StringValue != "inapp")
            {
                throw new AdminConflictException("不能開啟 App 內付款", "付款模式只能從「App 內付款」降級回「外開瀏覽器」或「關閉」，不能反向遠端開啟——那會讓未經審查的功能上線，被商店下架。");
            }
        }

        ApplyFlag(f, request);
        f.UpdatedAt = DateTime.UtcNow;
        f.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AppConfigChangeDto<AdminAppFeatureFlagDto> { Value = ToDto(f), EdgePublish = await PublishAsync(cancellationToken) };
    }

    public async Task<AppConfigChangeDto<bool>?> DeleteFlagAsync(Guid id, CancellationToken cancellationToken)
    {
        var f = await dbContext.AppFeatureFlags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (f is null)
        {
            return null;
        }

        dbContext.AppFeatureFlags.Remove(f);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AppConfigChangeDto<bool> { Value = true, EdgePublish = await PublishAsync(cancellationToken) };
    }

    private (string Key, string Platform) ValidateFlag(UpsertAdminAppFeatureFlagRequest r)
    {
        var key = AdminInput.RequireText(r.FlagKey, "開關名稱", 64, "flagKey");
        if (!FlagKeyFormat().IsMatch(key))
        {
            throw new AdminValidationException("開關名稱的格式是「模組_功能」，只能用小寫英文字母、數字與底線，例如 ads_enabled。", "flagKey");
        }

        var platform = AdminInput.OneOf(r.Platform ?? "all", new HashSet<string>(["all", "ios", "android"], StringComparer.Ordinal), "平台範圍", "「全部」「iOS」或「Android」", "platform");
        AdminInput.OptionalText(r.Description, "說明", 255, "description");
        if (key == PaymentModeKey)
        {
            AdminInput.OneOf(r.StringValue, PaymentModes.ToHashSet(StringComparer.Ordinal), "付款模式", "「關閉」「外開瀏覽器」或「App 內付款」", "stringValue");
        }
        else if (r.StringValue is not null)
        {
            throw new AdminValidationException("只有付款模式是三態開關，其他開關只有開或關。", "stringValue");
        }

        return (key, platform);
    }

    private static void ApplyFlag(AppFeatureFlag f, UpsertAdminAppFeatureFlagRequest r)
    {
        f.StringValue = r.StringValue;
        f.IsEnabled = r.StringValue is null ? r.IsEnabled : r.StringValue != "off";
        f.Description = AdminInput.OptionalText(r.Description, "說明", 255);
    }

    private static AdminAppFeatureFlagDto ToDto(AppFeatureFlag f) => new()
    {
        Id = f.Id, FlagKey = f.FlagKey, IsEnabled = f.IsEnabled, StringValue = f.StringValue, Platform = f.Platform,
        PlatformLabel = f.Platform switch { "ios" => "iOS", "android" => "Android", _ => "全部平台" }, Description = f.Description, UpdatedAt = f.UpdatedAt,
    };

    private async Task<AppConfigPublishResult> PublishAsync(CancellationToken cancellationToken)
        => await publisher.PublishAsync(await composer.ComposeAsync(cancellationToken), cancellationToken);

    // ═════════ 憑證列管 ═════════

    public async Task<IReadOnlyList<AdminAppCredentialDto>> ListCredentialsAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.AppCredentials.AsNoTracking().ToListAsync(cancellationToken);
        var today = TaiwanClock.Today;
        return rows.Select(c => ToDto(c, today)).OrderBy(c => c.NextDueOn is null ? 1 : 0).ThenBy(c => c.NextDueOn).ThenBy(c => c.Label).ToList();
    }

    public async Task<AdminAppCredentialDto> CreateCredentialAsync(UpsertAdminAppCredentialRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        var now = DateTime.UtcNow;
        var c = new AppCredential { Id = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId };
        ApplyCredential(c, request);
        dbContext.AppCredentials.Add(c);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(c, TaiwanClock.Today);
    }

    public async Task<AdminAppCredentialDto?> UpdateCredentialAsync(Guid id, UpsertAdminAppCredentialRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        var c = await dbContext.AppCredentials.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null)
        {
            return null;
        }

        ApplyCredential(c, request);
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(c, TaiwanClock.Today);
    }

    /// <summary>記錄一次輪替：上次輪替日設為今天（台灣日期），並可一併更新新的屆期日。輪替須寫入稽核日誌（規劃書 §8.5）。</summary>
    public async Task<AdminAppCredentialDto?> RotateAsync(Guid id, DateOnly? newExpiresOn, AdminSystemScope scope, CancellationToken cancellationToken)
    {
        var c = await dbContext.AppCredentials.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null)
        {
            return null;
        }

        var today = TaiwanClock.Today;
        if (newExpiresOn is { } e && e <= today)
        {
            throw new AdminValidationException("新的屆期日必須晚於今天。", "expiresOn");
        }

        c.LastRotatedOn = today;
        if (newExpiresOn is not null)
        {
            c.ExpiresOn = newExpiresOn;
        }

        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = scope.Identity.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "App 憑證輪替", $"{KindLabels.GetValueOrDefault(c.Kind, c.Kind)}「{c.Label}」", 1, "記錄輪替");
        return ToDto(c, today);
    }

    public async Task<bool> DeleteCredentialAsync(Guid id, CancellationToken cancellationToken)
    {
        var c = await dbContext.AppCredentials.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null)
        {
            return false;
        }

        dbContext.AppCredentials.Remove(c);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void Validate(UpsertAdminAppCredentialRequest r)
    {
        AdminInput.OneOf(r.Kind, KindLabels.Keys.ToHashSet(StringComparer.Ordinal), "種類", "Apple 推播金鑰、Google 推播憑證、Apple 或 Google Play 開發者帳號、地圖金鑰或其他", "kind");
        AdminInput.RequireText(r.Label, "名稱", 120, "label");
        AdminInput.OptionalText(r.ExternalRef, "外部識別", 200, "externalRef");
        AdminInput.OptionalText(r.Note, "備註", 500, "note");
        if (r.RotationPeriodDays is <= 0 or > 3650)
        {
            throw new AdminValidationException("輪替週期請填 1 到 3650 天。", "rotationPeriodDays");
        }

        if (r.ExpiresOn is { } e && e < r.CreatedOn)
        {
            throw new AdminValidationException("屆期日不可早於建立日。", "expiresOn");
        }
    }

    private static void ApplyCredential(AppCredential c, UpsertAdminAppCredentialRequest r)
    {
        c.Kind = r.Kind;
        c.Label = r.Label.Trim();
        c.ExternalRef = AdminInput.OptionalText(r.ExternalRef, "外部識別", 200);
        c.CreatedOn = r.CreatedOn;
        c.LastRotatedOn = r.LastRotatedOn;
        c.ExpiresOn = r.ExpiresOn;
        c.RotationPeriodDays = r.RotationPeriodDays;
        c.Note = AdminInput.OptionalText(r.Note, "備註", 500);
    }

    /// <summary>下次屆期日與健康度。部分金鑰沒有到期日，告警以自訂輪替週期為基準；屆期或屆期前 60 天告警。</summary>
    internal static AdminAppCredentialDto ToDto(AppCredential c, DateOnly today)
    {
        var next = c.ExpiresOn ?? (c.RotationPeriodDays is { } days ? (c.LastRotatedOn ?? c.CreatedOn).AddDays(days) : (DateOnly?)null);
        int? left = next is null ? null : next.Value.DayNumber - today.DayNumber;
        var health = left is null ? "untracked" : left < 0 ? "overdue" : left <= DueSoonDays ? "due_soon" : "ok";
        return new AdminAppCredentialDto
        {
            Id = c.Id, Kind = c.Kind, KindLabel = KindLabels.GetValueOrDefault(c.Kind, c.Kind), Label = c.Label, ExternalRef = c.ExternalRef,
            CreatedOn = c.CreatedOn, LastRotatedOn = c.LastRotatedOn, ExpiresOn = c.ExpiresOn, RotationPeriodDays = c.RotationPeriodDays,
            NextDueOn = next, DaysUntilDue = left, Health = health,
            HealthLabel = health switch { "overdue" => "已屆期", "due_soon" => "即將屆期", "untracked" => "未設定屆期日或輪替週期", _ => "正常" }, Note = c.Note,
        };
    }

    // ═════════ 診斷 ═════════

    public async Task<PagedResult<AdminAppDiagnosticListItemDto>> ListDiagnosticsAsync(
        string? type, string? status, string? platform, string? appVersion, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
        var q = dbContext.AppDiagnosticReports.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(type))
        {
            AdminInput.OneOf(type, ReportTypeLabels.Keys.ToHashSet(StringComparer.Ordinal), "回報類型", "「崩潰」「異常退出」「連線錯誤」「啟動耗時」或「使用者回報」");
            q = q.Where(r => r.ReportType == type);
        }

        if (!string.IsNullOrEmpty(status))
        {
            AdminInput.OneOf(status, ReportStatusLabels.Keys.ToHashSet(StringComparer.Ordinal), "處理狀態", "「新回報」「處理中」「已解決」或「略過」");
            q = q.Where(r => r.Status == status);
        }

        if (!string.IsNullOrEmpty(platform))
        {
            var pf = AppInput.RequirePlatform(platform);
            q = q.Where(r => r.Platform == pf);
        }

        if (!string.IsNullOrEmpty(appVersion)) { q = q.Where(r => r.AppVersion == appVersion); }
        var total = await q.CountAsync(cancellationToken);
        var rows = await q.OrderByDescending(r => r.OccurredAt).ThenBy(r => r.RowSeq).Skip((p - 1) * size).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<AdminAppDiagnosticListItemDto> { Items = rows.Select(ToItem).ToList(), Page = p, PageSize = size, TotalCount = total };
    }

    public async Task<AdminAppDiagnosticDetailDto?> GetDiagnosticAsync(Guid id, CancellationToken cancellationToken)
    {
        var r = await dbContext.AppDiagnosticReports.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return r is null ? null : new AdminAppDiagnosticDetailDto { Report = ToItem(r), Detail = r.Detail };
    }

    public async Task<AdminAppDiagnosticListItemDto?> UpdateDiagnosticStatusAsync(Guid id, string status, Guid? operatorId, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(status, ReportStatusLabels.Keys.ToHashSet(StringComparer.Ordinal), "處理狀態", "「新回報」「處理中」「已解決」或「略過」");
        var r = await dbContext.AppDiagnosticReports.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (r is null)
        {
            return null;
        }

        r.Status = status;
        r.UpdatedAt = DateTime.UtcNow;
        r.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToItem(r);
    }

    public async Task<AdminAppDiagnosticSummaryDto> DiagnosticSummaryAsync(int? days, CancellationToken cancellationToken)
    {
        var window = days is > 0 and <= 90 ? days.Value : 7;
        var from = DateTime.UtcNow.AddDays(-window);
        var reports = await dbContext.AppDiagnosticReports.AsNoTracking().Where(r => r.OccurredAt >= from)
            .Select(r => new { r.Platform, r.AppVersion, r.ReportType, r.DeviceInstallId, r.MetricValue }).ToListAsync(cancellationToken);
        var activeByVersion = await dbContext.AppDevices.AsNoTracking().Where(d => d.LastActiveAt >= from)
            .GroupBy(d => new { d.Platform, d.AppVersion }).Select(g => new { g.Key.Platform, g.Key.AppVersion, Count = g.Count() }).ToListAsync(cancellationToken);

        var versionRows = reports.GroupBy(r => (r.Platform, r.AppVersion)).Select(g =>
        {
            var active = activeByVersion.Where(a => a.Platform == g.Key.Platform && a.AppVersion == g.Key.AppVersion).Sum(a => a.Count);
            var crashes = g.Where(r => r.ReportType == "crash").ToList();
            var crashDevices = crashes.Where(r => r.DeviceInstallId is not null).Select(r => r.DeviceInstallId).Distinct().Count();
            return new AdminAppDiagnosticVersionRowDto
            {
                Platform = g.Key.Platform, PlatformLabel = AdLabels.Of(AdLabels.Platform, g.Key.Platform), AppVersion = g.Key.AppVersion, Crashes = crashes.Count,
                ActiveDevices = active, DevicesWithCrash = crashDevices,
                CrashFreeDevicePercent = active == 0 ? null : Math.Round(100m * (1 - Math.Min(1m, (decimal)crashDevices / active)), 1),
            };
        }).OrderBy(r => r.Platform).ThenByDescending(r => AppVersion.TryParse(r.AppVersion, out var v) ? v : default).ToList();

        var startup = reports.Where(r => r.ReportType == "startup_time" && r.MetricValue is not null).Select(r => r.MetricValue!.Value).OrderBy(x => x).ToList();
        return new AdminAppDiagnosticSummaryDto
        {
            Days = window, ByVersion = versionRows,
            ByType = reports.GroupBy(r => r.ReportType).Select(g => new AdminAppDeviceCountRowDto { Key = g.Key, Label = ReportTypeLabels.GetValueOrDefault(g.Key, g.Key), Count = g.Count() }).OrderByDescending(x => x.Count).ToList(),
            StartupMedianMs = startup.Count == 0 ? null : startup[startup.Count / 2],
            StartupP90Ms = startup.Count == 0 ? null : startup[Math.Min(startup.Count - 1, (int)Math.Ceiling(startup.Count * 0.9) - 1)],
        };
    }

    private static AdminAppDiagnosticListItemDto ToItem(AppDiagnosticReport r) => new()
    {
        Id = r.Id, Platform = r.Platform, PlatformLabel = AdLabels.Of(AdLabels.Platform, r.Platform), AppVersion = r.AppVersion, BuildNumber = r.BuildNumber,
        OsVersion = r.OsVersion, OccurredAt = r.OccurredAt, ReportType = r.ReportType, ReportTypeLabel = ReportTypeLabels.GetValueOrDefault(r.ReportType, r.ReportType),
        MetricValue = r.MetricValue, Summary = r.Summary, Status = r.Status, StatusLabel = ReportStatusLabels.GetValueOrDefault(r.Status, r.Status),
    };

    // ═════════ 連線檢查 ═════════

    /// <summary>一次檢查 App 相關依賴：資料庫、推播服務（Apple 與 Google）、邊緣靜態設定、憑證屆期、最低支援版本與診斷回報。「尚未串接」與「錯誤」分開顯示。</summary>
    public async Task<AdminAppConnectionCheckDto> ConnectionCheckAsync(CancellationToken cancellationToken)
    {
        var items = new List<AdminAppConnectionCheckItemDto>();
        static AdminAppConnectionCheckItemDto Item(string key, string label, string status, string message) => new()
        {
            Key = key, Label = label, Status = status, Message = message,
            StatusLabel = status switch { "ok" => "正常", "warning" => "注意", "not_configured" => "尚未串接", _ => "異常" },
        };

        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            items.Add(Item("database", "資料庫", "ok", "連線正常。"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            items.Add(Item("database", "資料庫", "error", "無法連線到資料庫。"));
        }

        items.Add(!transport.IsConfigured
            ? Item("push_transport", "推播服務（Apple 與 Google）", "not_configured", "尚未串接：Apple 與 Google 推播服務的金鑰尚未建立，推播批次會停在「失敗」並保留，串接後可重送。")
            : Item("push_transport", "推播服務（Apple 與 Google）", "ok", "已設定推播服務。"));

        items.Add(publisher.IsConfigured
            ? Item("edge_config", "備援設定來源", "ok", "已串接備援設定來源，App 的設定存檔時會同步。")
            : Item("edge_config", "備援設定來源", "not_configured", "尚未串接備援設定來源；伺服器掛掉時 App 讀不到維護與強制更新公告。"));

        var creds = await ListCredentialsAsync(cancellationToken);
        var overdue = creds.Count(c => c.Health == "overdue");
        var soon = creds.Count(c => c.Health == "due_soon");
        items.Add(creds.Count == 0
            ? Item("credentials", "金鑰與憑證列管", "warning", "還沒有列管任何金鑰或憑證。")
            : overdue > 0 ? Item("credentials", "金鑰與憑證列管", "error", $"有 {overdue} 項已屆期，{soon} 項即將屆期（60 天內）。")
            : soon > 0 ? Item("credentials", "金鑰與憑證列管", "warning", $"有 {soon} 項將在 60 天內屆期。")
            : Item("credentials", "金鑰與憑證列管", "ok", $"{creds.Count} 項都在期限內。"));

        var minSet = await dbContext.AppReleases.AsNoTracking().Where(r => r.IsMinSupported).Select(r => r.Platform).ToListAsync(cancellationToken);
        items.Add(minSet.Count == 2
            ? Item("min_version", "最低支援版本", "ok", "兩個平台都已設定最低支援版本。")
            : Item("min_version", "最低支援版本", "warning", "尚未替所有平台設定最低支援版本（沒設定就不會強制更新）。"));

        var newReports = await dbContext.AppDiagnosticReports.CountAsync(r => r.Status == "new" && r.ReportType == "crash", cancellationToken);
        items.Add(newReports > 0 ? Item("diagnostics", "診斷回報", "warning", $"有 {newReports} 筆尚未處理的崩潰回報。") : Item("diagnostics", "診斷回報", "ok", "沒有未處理的崩潰回報。"));

        return new AdminAppConnectionCheckDto { CheckedAt = DateTime.UtcNow, Items = items };
    }
}
