using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Common;

/// <summary>
/// 每俱樂部一份的鍵值設定（<c>settings</c>／<c>settings_i18n</c>）的<b>可含逐語系文案</b>讀寫小工具，I 網站設定的政策頁、維護模式、多語系規則、
/// EDM 設定共用（不含逐語系的純值版本見 <see cref="ClubSettingsStore"/>）。寫入只追蹤變更，<b>由呼叫端 <c>SaveChangesAsync</c></b>，
/// 讓設定與其他資料在同一個交易。慣例同 <c>AdminSiteFactsRepository</c>：新建側表列時要同時加進導覽集合與 <c>DbSet</c>
/// （只加導覽集合會被 EF 當成既有列而發 UPDATE，docs/18 E 系列已踩過）。
/// </summary>
public sealed class ClubSettingsEditor(ClubDbContext db)
{
    public Task<Dictionary<string, Setting>> LoadAsync(Guid clubId, IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
        => db.Settings.Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == clubId && keys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, StringComparer.Ordinal, cancellationToken);

    public static string? Value(IReadOnlyDictionary<string, Setting> map, string key)
        => map.TryGetValue(key, out var s) ? s.SettingValue : null;

    public static string? I18n(IReadOnlyDictionary<string, Setting> map, string key, string locale)
        => map.TryGetValue(key, out var s) ? s.SettingsI18ns.FirstOrDefault(i => i.Locale == locale)?.Value : null;

    public static DateTime? UpdatedAt(IReadOnlyDictionary<string, Setting> map, string key)
        => map.TryGetValue(key, out var s) ? s.UpdatedAt : null;

    /// <summary>寫入單一值；空白視為清除（已有列則把值設為 null，沒有列就不建空殼）。</summary>
    public void SetValue(Dictionary<string, Setting> map, Guid clubId, string key, string group, string? value, Guid? operatorId)
    {
        var hasValue = !string.IsNullOrWhiteSpace(value);
        if (!hasValue && !map.ContainsKey(key))
        {
            return;
        }

        var setting = GetOrCreate(map, clubId, key, group, operatorId);
        setting.SettingValue = hasValue ? value : null;
    }

    /// <summary>寫入逐語系文案：繁中與其他語系各自「有值就寫、空白就刪該語系列」；全部空白且原本沒有設定列就不建。</summary>
    public void SetI18n(Dictionary<string, Setting> map, Guid clubId, string key, string group, IReadOnlyDictionary<string, string?> valuesByLocale, Guid? operatorId)
    {
        var anyValue = valuesByLocale.Values.Any(v => !string.IsNullOrWhiteSpace(v));
        if (!anyValue && !map.ContainsKey(key))
        {
            return;
        }

        var setting = GetOrCreate(map, clubId, key, group, operatorId);
        foreach (var (locale, value) in valuesByLocale)
        {
            var row = setting.SettingsI18ns.FirstOrDefault(i => i.Locale == locale);
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (row is null)
                {
                    row = new SettingsI18n { SettingId = setting.Id, Locale = locale };
                    setting.SettingsI18ns.Add(row);
                    db.SettingsI18ns.Add(row);
                }

                row.Value = value;
            }
            else if (row is not null)
            {
                db.SettingsI18ns.Remove(row);
                setting.SettingsI18ns.Remove(row);
            }
        }
    }

    /// <summary>繁中＋英文兩語系的便利寫法。</summary>
    public void SetI18n(Dictionary<string, Setting> map, Guid clubId, string key, string group, string? zh, string? en, Guid? operatorId)
        => SetI18n(map, clubId, key, group,
            new Dictionary<string, string?> { [RequestLocale.DefaultDbLocale] = zh, ["en"] = en }, operatorId);

    private Setting GetOrCreate(Dictionary<string, Setting> map, Guid clubId, string key, string group, Guid? operatorId)
    {
        var now = DateTime.UtcNow;
        if (map.TryGetValue(key, out var existing))
        {
            existing.UpdatedAt = now;
            existing.UpdatedBy = operatorId;
            return existing;
        }

        var setting = new Setting
        {
            Id = Guid.NewGuid(), ClubId = clubId, SettingKey = key, SettingGroup = group,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        db.Settings.Add(setting);
        map[key] = setting;
        return setting;
    }
}
