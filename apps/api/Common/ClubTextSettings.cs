using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Common;

/// <summary>
/// 每俱樂部一份的「雙語文案設定」（<c>settings</c>＋<c>settings_i18n</c>）讀寫小工具：漫畫企劃說明（F1）、商店入口與政策（S6）共用。
/// 單值設定（不分語系）用 <see cref="ClubSettingsStore"/>；這裡處理「同一個設定鍵有中文與英文兩份文字」的情況。
/// 寫入只追蹤變更，由呼叫端 <c>SaveChangesAsync</c>（好讓設定與其他資料同一個交易）。
/// </summary>
public sealed class ClubTextSettings(ClubDbContext db)
{
    public async Task<Dictionary<string, Setting>> LoadAsync(Guid clubId, IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
        => await db.Settings.Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == clubId && keys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, StringComparer.Ordinal, cancellationToken);

    public static (string? Zh, string? En) Get(IReadOnlyDictionary<string, Setting> map, string key)
    {
        if (!map.TryGetValue(key, out var setting))
        {
            return (null, null);
        }

        return (setting.SettingsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Value,
                setting.SettingsI18ns.FirstOrDefault(i => i.Locale == "en")?.Value);
    }

    public static string? GetValue(IReadOnlyDictionary<string, Setting> map, string key)
        => map.TryGetValue(key, out var setting) ? setting.SettingValue : null;

    public static DateTime? LatestUpdate(IReadOnlyDictionary<string, Setting> map)
        => map.Count == 0 ? null : map.Values.Max(s => s.UpdatedAt);

    /// <summary>單值設定（不分語系）。<paramref name="value"/> 為 <c>null</c> 代表清除（設定列保留、值為空）。</summary>
    public void SetValue(Dictionary<string, Setting> map, Guid clubId, string key, string group, string? value, Guid? operatorId)
    {
        var setting = GetOrAdd(map, clubId, key, group, operatorId);
        setting.SettingValue = value;
    }

    /// <summary>雙語文字。中文或英文為空白時，對應的那一份側表列移除。</summary>
    public void SetText(Dictionary<string, Setting> map, Guid clubId, string key, string group, string? zh, string? en, Guid? operatorId)
    {
        var setting = GetOrAdd(map, clubId, key, group, operatorId);
        SetLocale(setting, RequestLocale.DefaultDbLocale, zh);
        SetLocale(setting, "en", en);
    }

    private void SetLocale(Setting setting, string locale, string? value)
    {
        var row = setting.SettingsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (row is not null)
            {
                setting.SettingsI18ns.Remove(row);
                db.SettingsI18ns.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new SettingsI18n { SettingId = setting.Id, Locale = locale };
            setting.SettingsI18ns.Add(row);
            db.SettingsI18ns.Add(row);
        }

        row.Value = value;
    }

    private Setting GetOrAdd(Dictionary<string, Setting> map, Guid clubId, string key, string group, Guid? operatorId)
    {
        var now = DateTime.UtcNow;
        if (map.TryGetValue(key, out var existing))
        {
            existing.UpdatedAt = now;
            existing.UpdatedBy = operatorId;
            existing.SettingGroup ??= group;
            return existing;
        }

        var created = new Setting
        {
            Id = Guid.NewGuid(), ClubId = clubId, SettingKey = key, SettingGroup = group,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        db.Settings.Add(created);
        map[key] = created;
        return created;
    }
}
