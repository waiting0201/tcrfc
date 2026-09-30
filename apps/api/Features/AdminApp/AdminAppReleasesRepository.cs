using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminAds;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>
/// M1 App 版本與發布管理（App 規劃書 §8.1）。<b>僅限系統管理員</b>（規劃書 §11 補充規則 1）：誤設最低支援版本會讓全體使用者無法使用。
/// 版本清單、最低支援版本（強制更新）、建議版本（可略過）、更新說明與提示文案（中英）、維護模式（全域或分平台）。
/// 每次改動設定後都會把 <see cref="AppConfigDocument"/> 交給 <see cref="IAppConfigPublisher"/> 推送到邊緣靜態設定（尚未串接時只回報結果）。
/// </summary>
public sealed class AdminAppReleasesRepository(
    ClubDbContext dbContext, AppConfigComposer composer, IAppConfigPublisher publisher, SensitiveActionLogger audit)
{
    private static readonly IReadOnlySet<string> Statuses = new HashSet<string>(["testing", "live", "withdrawn"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> Scopes = new HashSet<string>(["all", "ios", "android"], StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, string> StatusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["testing"] = "測試", ["live"] = "已上架", ["withdrawn"] = "已下架",
    };

    public async Task<IReadOnlyList<AdminAppReleaseDto>> ListAsync(string? platform, CancellationToken cancellationToken)
    {
        var q = dbContext.AppReleases.AsNoTracking().Include(r => r.AppReleasesI18ns).AsQueryable();
        if (!string.IsNullOrEmpty(platform))
        {
            var pf = AppInput.RequirePlatform(platform); // 先驗證再組查詢（例外寫在 Where 運算式裡會被 EF 包成 500）
            q = q.Where(r => r.Platform == pf);
        }

        var rows = await q.ToListAsync(cancellationToken);
        return rows.OrderBy(r => r.Platform).ThenByDescending(r => AppVersion.TryParse(r.Version, out var v) ? v : default).ThenByDescending(r => r.RowSeq)
            .Select(ToDto).ToList();
    }

    public async Task<AdminAppReleaseDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var r = await dbContext.AppReleases.AsNoTracking().Include(x => x.AppReleasesI18ns).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return r is null ? null : ToDto(r);
    }

    public async Task<AdminAppReleaseDto> CreateAsync(UpsertAdminAppReleaseRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (platform, version) = Validate(request);
        if (await dbContext.AppReleases.AnyAsync(r => r.Platform == platform && r.Version == version, cancellationToken))
        {
            throw new AdminConflictException("版本重複", $"{AdLabels.Of(AdLabels.Platform, platform)} 已經有 {version} 這個版本了。");
        }

        var now = DateTime.UtcNow;
        var r = new AppRelease { Id = Guid.NewGuid(), Platform = platform, Version = version, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId };
        Apply(r, request);
        SetI18n(r, request.Content);
        dbContext.AppReleases.Add(r);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(r.Id, cancellationToken))!;
    }

    public async Task<AppConfigChangeDto<AdminAppReleaseDto>?> UpdateAsync(
        Guid id, UpsertAdminAppReleaseRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (platform, version) = Validate(request);
        var r = await dbContext.AppReleases.Include(x => x.AppReleasesI18ns).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (r is null)
        {
            return null;
        }

        if (r.Platform != platform || r.Version != version)
        {
            throw new AdminValidationException("版本號與平台建立後不能修改；請新增另一個版本。");
        }

        var status = request.Status ?? r.Status;
        if (status != "live" && (r.IsMinSupported || r.IsRecommended))
        {
            throw new AdminConflictException("版本正被設為更新門檻", "這個版本目前是「最低支援版本」或「建議版本」，不能改成測試或已下架；請先改設其他版本。");
        }

        Apply(r, request);
        r.UpdatedAt = DateTime.UtcNow;
        r.UpdatedBy = operatorId;
        SetI18n(r, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        var publish = await PublishAsync(cancellationToken);
        return new AppConfigChangeDto<AdminAppReleaseDto> { Value = (await GetAsync(id, cancellationToken))!, EdgePublish = publish };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var r = await dbContext.AppReleases.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (r is null)
        {
            return false;
        }

        if (r.IsMinSupported || r.IsRecommended || r.Status == "live")
        {
            throw new AdminConflictException("版本使用中", "已上架或被設為更新門檻的版本不能刪除；請先下架並改設其他版本。");
        }

        dbContext.AppReleases.Remove(r);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>設定最低支援版本／建議版本旗標。同平台的舊旗標自動取消。🔴 最低支援版本會強制更新舊版使用者，須二次確認。</summary>
    public async Task<AppConfigChangeDto<AdminAppReleaseDto>?> SetFlagsAsync(
        Guid id, SetReleaseFlagsRequest request, AdminSystemScope scope, CancellationToken cancellationToken)
    {
        var r = await dbContext.AppReleases.Include(x => x.AppReleasesI18ns).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (r is null)
        {
            return null;
        }

        if ((request.IsMinSupported || request.IsRecommended) && r.Status != "live")
        {
            throw new AdminConflictException("版本尚未上架", "只有「已上架」的版本可以設為最低支援版本或建議版本。");
        }

        if (request.IsMinSupported && !r.IsMinSupported && !request.ConfirmForceUpdate)
        {
            throw new AdminConflictException("需要再確認一次",
                $"把 {AdLabels.Of(AdLabels.Platform, r.Platform)} {r.Version} 設為最低支援版本後，低於這個版本的使用者一打開 App 就必須更新，無法略過。請確認後勾選「我了解會強制舊版更新」再送出。");
        }

        var now = DateTime.UtcNow;
        var before = (r.IsMinSupported, r.IsRecommended);
        if (request.IsMinSupported)
        {
            await dbContext.AppReleases.Where(x => x.Platform == r.Platform && x.Id != id && x.IsMinSupported)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsMinSupported, false).SetProperty(x => x.UpdatedAt, now), cancellationToken);
        }

        if (request.IsRecommended)
        {
            await dbContext.AppReleases.Where(x => x.Platform == r.Platform && x.Id != id && x.IsRecommended)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRecommended, false).SetProperty(x => x.UpdatedAt, now), cancellationToken);
        }

        r.IsMinSupported = request.IsMinSupported;
        r.IsRecommended = request.IsRecommended;
        r.UpdatedAt = now;
        r.UpdatedBy = scope.Identity.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "調整 App 更新門檻", $"{r.Platform} {r.Version}：最低支援 {before.IsMinSupported}→{r.IsMinSupported}、建議 {before.IsRecommended}→{r.IsRecommended}", 1, null);
        var publish = await PublishAsync(cancellationToken);
        return new AppConfigChangeDto<AdminAppReleaseDto> { Value = ToDto(r), EdgePublish = publish };
    }

    // ═════════ 維護模式 ═════════

    public async Task<IReadOnlyList<AdminAppMaintenanceDto>> ListMaintenanceAsync(CancellationToken cancellationToken)
    {
        var stored = await dbContext.AppSettings.AsNoTracking().Where(s => s.SettingKey.StartsWith(AppConfigComposer.MaintenanceKeyPrefix))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, cancellationToken);
        return Scopes.OrderBy(s => s == "all" ? 0 : 1).ThenBy(s => s).Select(scope => ToMaintenanceDto(scope, stored.GetValueOrDefault(AppConfigComposer.MaintenanceKeyPrefix + scope))).ToList();
    }

    public async Task<AppConfigChangeDto<AdminAppMaintenanceDto>> SetMaintenanceAsync(
        string scope, SetAppMaintenanceRequest request, AdminSystemScope actor, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(scope, Scopes, "維護範圍", "「全部平台」「iOS」或「Android」");
        var zh = AdminInput.OptionalText(request.MessageZh, "維護訊息（繁中）", 500);
        var en = AdminInput.OptionalText(request.MessageEn, "維護訊息（英文）", 500);
        if (request.Enabled && zh is null)
        {
            throw new AdminValidationException("開啟維護模式必須填寫繁中的維護訊息（使用者會在畫面上看到）。");
        }

        var key = AppConfigComposer.MaintenanceKeyPrefix + scope;
        var row = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);
        var json = AppConfigComposer.SerializeMaintenance(new AppConfigComposer.StoredMaintenance(request.Enabled, zh, en));
        if (row is null)
        {
            row = new AppSetting { Id = Guid.NewGuid(), SettingKey = key };
            dbContext.AppSettings.Add(row);
        }

        row.SettingValue = json;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = actor.Identity.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        audit.Record(actor, request.Enabled ? "開啟 App 維護模式" : "關閉 App 維護模式", scope, 1, null);
        var publish = await PublishAsync(cancellationToken);
        return new AppConfigChangeDto<AdminAppMaintenanceDto> { Value = ToMaintenanceDto(scope, json), EdgePublish = publish };
    }

    private static AdminAppMaintenanceDto ToMaintenanceDto(string scope, string? json)
    {
        var node = AppConfigComposer.ParseMaintenance(json);
        return new AdminAppMaintenanceDto
        {
            Scope = scope, ScopeLabel = scope switch { "all" => "全部平台", "ios" => "iOS", _ => "Android" },
            Enabled = node.Enabled, MessageZh = node.Message?.Zh, MessageEn = node.Message?.En,
        };
    }

    public async Task<AppConfigPublishResult> PublishAsync(CancellationToken cancellationToken)
        => await publisher.PublishAsync(await composer.ComposeAsync(cancellationToken), cancellationToken);

    private static (string Platform, string Version) Validate(UpsertAdminAppReleaseRequest r)
    {
        var platform = AppInput.RequirePlatform(r.Platform);
        var version = AdminInput.RequireText(r.Version, "版本號", 32);
        if (!AppVersion.IsValid(version))
        {
            throw new AdminValidationException("版本號的格式是「主.次.修」，例如 1.2.0。");
        }

        AdminInput.OptionalText(r.BuildNumber, "建置號", 32);
        AdminInput.OneOf(r.Status ?? "testing", Statuses, "狀態", "「測試」「已上架」或「已下架」");
        AdminInput.OptionalText(r.Content.Zh.WhatsNew, "更新說明（繁中）", 2000);
        AdminInput.OptionalText(r.Content.Zh.ForceMessage, "強制更新文案（繁中）", 500);
        AdminInput.OptionalText(r.Content.Zh.RecommendMessage, "建議更新文案（繁中）", 500);
        return (platform, version);
    }

    private static void Apply(AppRelease r, UpsertAdminAppReleaseRequest request)
    {
        r.BuildNumber = AdminInput.OptionalText(request.BuildNumber, "建置號", 32);
        r.ReleasedOn = request.ReleasedOn;
        r.Status = request.Status ?? r.Status ?? "testing";
    }

    private static void SetI18n(AppRelease r, ReleaseContentInput c)
    {
        Upsert(r, RequestLocale.DefaultDbLocale, c.Zh);
        if (c.En is not null)
        {
            Upsert(r, "en", c.En);
        }
    }

    private static void Upsert(AppRelease r, string locale, ReleaseLocaleContent c)
    {
        var row = r.AppReleasesI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new AppReleasesI18n { AppReleaseId = r.Id, Locale = locale };
            r.AppReleasesI18ns.Add(row);
        }

        row.WhatsNew = AdminInput.OptionalText(c.WhatsNew, "更新說明", 2000);
        row.ForceMessage = AdminInput.OptionalText(c.ForceMessage, "強制更新文案", 500);
        row.RecommendMessage = AdminInput.OptionalText(c.RecommendMessage, "建議更新文案", 500);
    }

    private static AdminAppReleaseDto ToDto(AppRelease r)
    {
        ReleaseLocaleContent? Content(string locale)
        {
            var i = r.AppReleasesI18ns.FirstOrDefault(x => x.Locale == locale);
            return i is null ? null : new ReleaseLocaleContent { WhatsNew = i.WhatsNew, ForceMessage = i.ForceMessage, RecommendMessage = i.RecommendMessage };
        }

        return new AdminAppReleaseDto
        {
            Id = r.Id, Platform = r.Platform, PlatformLabel = AdLabels.Of(AdLabels.Platform, r.Platform), Version = r.Version, BuildNumber = r.BuildNumber,
            ReleasedOn = r.ReleasedOn, Status = r.Status, StatusLabel = StatusLabels.GetValueOrDefault(r.Status, r.Status),
            IsMinSupported = r.IsMinSupported, IsRecommended = r.IsRecommended, Zh = Content(RequestLocale.DefaultDbLocale), En = Content("en"), UpdatedAt = r.UpdatedAt,
        };
    }
}
