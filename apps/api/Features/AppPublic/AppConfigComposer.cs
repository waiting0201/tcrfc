using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.AppPublic;

/// <summary>
/// 由資料庫組出 <see cref="AppConfigDocument"/>。來源：<c>app_releases</c>（最低支援版本／建議版本旗標與更新文案）、
/// <c>app_settings</c> 的 <c>maintenance.all／ios／android</c>（維護模式，全域與分平台）、<c>app_feature_flags</c>（功能開關，
/// 平台值 <c>all</c> 對兩個平台都生效、單一平台的值優先）。
/// </summary>
public sealed class AppConfigComposer(ClubDbContext dbContext)
{
    public const string MaintenanceKeyPrefix = "maintenance.";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AppConfigDocument> ComposeAsync(CancellationToken cancellationToken)
    {
        var releases = await dbContext.AppReleases.AsNoTracking().Include(r => r.AppReleasesI18ns)
            .Where(r => r.IsMinSupported || r.IsRecommended).ToListAsync(cancellationToken);
        var flags = await dbContext.AppFeatureFlags.AsNoTracking().ToListAsync(cancellationToken);
        var settings = await dbContext.AppSettings.AsNoTracking().Where(s => s.SettingKey.StartsWith(MaintenanceKeyPrefix))
            .ToDictionaryAsync(s => s.SettingKey, s => s.SettingValue, cancellationToken);

        AppPlatformConfig Build(string platform)
        {
            var min = releases.FirstOrDefault(r => r.Platform == platform && r.IsMinSupported);
            var rec = releases.FirstOrDefault(r => r.Platform == platform && r.IsRecommended);
            var global = ParseMaintenance(settings.GetValueOrDefault(MaintenanceKeyPrefix + "all"));
            var own = ParseMaintenance(settings.GetValueOrDefault(MaintenanceKeyPrefix + platform));
            var maintenance = own.Enabled ? own : global;

            var values = new SortedDictionary<string, bool>(StringComparer.Ordinal);
            foreach (var f in flags.Where(f => f.Platform == "all"))
            {
                values[f.FlagKey] = f.IsEnabled;
            }

            foreach (var f in flags.Where(f => f.Platform == platform))
            {
                values[f.FlagKey] = f.IsEnabled; // 單一平台的設定優先於 all
            }

            return new AppPlatformConfig
            {
                MinSupportedVersion = min?.Version,
                RecommendedVersion = rec?.Version,
                ForceUpdateMessage = Text(min, i => i.ForceMessage),
                RecommendUpdateMessage = Text(rec, i => i.RecommendMessage),
                WhatsNew = Text(rec ?? min, i => i.WhatsNew),
                Maintenance = maintenance,
                FeatureFlags = values,
            };
        }

        return new AppConfigDocument { GeneratedAt = DateTime.UtcNow, Ios = Build("ios"), Android = Build("android") };
    }

    public static AppConfigEvaluation Evaluate(AppPlatformConfig config, string? appVersion)
    {
        var below = (string? threshold) => threshold is not null && AppVersion.Compare(appVersion, threshold) is < 0;
        return new AppConfigEvaluation
        {
            Maintenance = config.Maintenance.Enabled,
            UpdateRequired = below(config.MinSupportedVersion),
            UpdateRecommended = !below(config.MinSupportedVersion) && below(config.RecommendedVersion),
        };
    }

    private static AppBilingualText? Text(AppRelease? release, Func<AppReleasesI18n, string?> pick)
    {
        if (release is null)
        {
            return null;
        }

        var zh = pick(release.AppReleasesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale) ?? new AppReleasesI18n());
        var en = pick(release.AppReleasesI18ns.FirstOrDefault(i => i.Locale == "en") ?? new AppReleasesI18n());
        return zh is null && en is null ? null : new AppBilingualText(zh, en);
    }

    internal static AppMaintenanceNode ParseMaintenance(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppMaintenanceNode { Enabled = false };
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredMaintenance>(json, Json);
            return stored is null
                ? new AppMaintenanceNode { Enabled = false }
                : new AppMaintenanceNode { Enabled = stored.Enabled, Message = stored.Enabled ? new AppBilingualText(stored.MessageZh, stored.MessageEn) : null };
        }
        catch (JsonException)
        {
            return new AppMaintenanceNode { Enabled = false };
        }
    }

    /// <summary>資料庫裡 <c>maintenance.*</c> 設定的 JSON 形狀。</summary>
    public sealed record StoredMaintenance(bool Enabled, string? MessageZh, string? MessageEn);

    public static string SerializeMaintenance(StoredMaintenance value) => JsonSerializer.Serialize(value, Json);
}
