using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Common;

/// <summary>
/// 每俱樂部一份的「鍵值設定」（<c>settings</c> 表）讀寫小工具，B1 的會員編號規則（<c>member.*</c>）與行事曆顯示設定
/// （<c>calendar.*</c>）共用。寫入只追蹤變更，<b>由呼叫端 <c>SaveChangesAsync</c></b>，好讓設定與其他資料同一個交易。
/// </summary>
public sealed class ClubSettingsStore(ClubDbContext db)
{
    public async Task<Dictionary<string, string?>> GetManyAsync(
        Guid clubId, IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        var rows = await db.Settings.AsNoTracking()
            .Where(s => s.ClubId == clubId && keys.Contains(s.SettingKey))
            .Select(s => new { s.SettingKey, s.SettingValue })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(r => r.SettingKey, r => r.SettingValue, StringComparer.Ordinal);
    }

    public async Task UpsertAsync(
        Guid clubId, string key, string? value, string group, Guid? operatorId, CancellationToken cancellationToken)
    {
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.ClubId == clubId && s.SettingKey == key, cancellationToken);
        var now = DateTime.UtcNow;
        if (existing is null)
        {
            db.Settings.Add(new Setting
            {
                Id = Guid.NewGuid(),
                ClubId = clubId,
                SettingKey = key,
                SettingValue = value,
                SettingGroup = group,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = operatorId,
                UpdatedBy = operatorId,
            });
            return;
        }

        existing.SettingValue = value;
        existing.SettingGroup ??= group;
        existing.UpdatedAt = now;
        existing.UpdatedBy = operatorId;
    }
}
