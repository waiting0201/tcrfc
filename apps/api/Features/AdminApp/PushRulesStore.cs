using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>
/// 自動推播規則（App 規劃書 §6.2、§8.3「自動推播的規則設定：賽事提醒提前時數、到期提醒天數等」）。存在 <c>app_settings.push.rules</c>（json）。
/// 🔴 這裡只保存規則；實際觸發（掃描賽事與會籍到期、產生批次）屬 App 開發階段。預設值照規劃書：賽事提醒開賽前 2 小時、會籍到期前 30 天與 7 天，
/// 新聞發布預設關閉（83 篇的發布節奏全推會打擾使用者）。
/// </summary>
public sealed class PushRulesStore(ClubDbContext dbContext)
{
    public const string SettingKey = "push.rules";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static readonly IReadOnlyList<(string Key, string Label, bool Default)> ToggleDefinitions =
    [
        ("match_reminder", "賽事提醒（開賽前）", true),
        ("venue_confirmed", "場地確認（由待定改為實際場地）", true),
        ("match_change", "賽事異動（日期、時間、場地或延賽）", true),
        ("match_result", "賽果發布", true),
        ("news_published", "新聞發布（依分類與追蹤俱樂部）", false),
        ("membership_expiry", "會籍到期提醒", true),
        ("membership_activated", "會籍開通完成", true),
        ("jersey_status", "球衣狀態異動（已寄出）", true),
        ("program_status", "課程報名狀態（已確認／已繳費）", true),
    ];

    private sealed class Stored
    {
        public int MatchReminderHours { get; set; } = 2;
        public List<int> MembershipExpiryDays { get; set; } = [30, 7];
        public Dictionary<string, bool> Toggles { get; set; } = [];
    }

    public async Task<AdminPushRulesDto> GetAsync(CancellationToken cancellationToken)
    {
        var row = await dbContext.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.SettingKey == SettingKey, cancellationToken);
        return ToDto(Parse(row?.SettingValue));
    }

    public async Task<AdminPushRulesDto> UpdateAsync(UpdateAdminPushRulesRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var row = await dbContext.AppSettings.FirstOrDefaultAsync(s => s.SettingKey == SettingKey, cancellationToken);
        var stored = Parse(row?.SettingValue);
        if (request.MatchReminderHours is { } hours)
        {
            if (hours is < 1 or > 72)
            {
                throw new AdminValidationException("賽事提醒的提前時數只能是 1 到 72 小時。", "matchReminderHours");
            }

            stored.MatchReminderHours = hours;
        }

        if (request.MembershipExpiryDays is { } days)
        {
            if (days.Count is 0 or > 5 || days.Any(d => d is < 1 or > 365) || days.Distinct().Count() != days.Count)
            {
                throw new AdminValidationException("會籍到期提醒請設 1 到 5 個不重複的天數（每個 1 到 365 天）。", "membershipExpiryDays");
            }

            stored.MembershipExpiryDays = days.OrderByDescending(d => d).ToList();
        }

        if (request.Toggles is not null)
        {
            var known = ToggleDefinitions.Select(t => t.Key).ToHashSet(StringComparer.Ordinal);
            var unknown = request.Toggles.Keys.FirstOrDefault(k => !known.Contains(k));
            if (unknown is not null)
            {
                throw new AdminValidationException($"沒有「{unknown}」這項自動推播。");
            }

            foreach (var (key, value) in request.Toggles)
            {
                stored.Toggles[key] = value;
            }
        }

        if (row is null)
        {
            row = new AppSetting { Id = Guid.NewGuid(), SettingKey = SettingKey };
            dbContext.AppSettings.Add(row);
        }

        row.SettingValue = JsonSerializer.Serialize(stored, Json);
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(stored);
    }

    private static Stored Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Stored();
        }

        try
        {
            return JsonSerializer.Deserialize<Stored>(json, Json) ?? new Stored();
        }
        catch (JsonException)
        {
            return new Stored();
        }
    }

    private static AdminPushRulesDto ToDto(Stored s) => new()
    {
        MatchReminderHours = s.MatchReminderHours,
        MembershipExpiryDays = s.MembershipExpiryDays,
        Toggles = ToggleDefinitions.Select(t => new AdminPushRuleToggleDto { Key = t.Key, Label = t.Label, Enabled = s.Toggles.GetValueOrDefault(t.Key, t.Default) }).ToList(),
    };
}
