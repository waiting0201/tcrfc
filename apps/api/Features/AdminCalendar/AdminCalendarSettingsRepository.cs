using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminCalendar;

/// <summary>
/// L3 分類與顯示設定（主站規劃書 §4.12 L3）：隊別分類設定、賽事類型維護、前台預設檢視與範圍、嵌入元件預設篩選、
/// 「試訓是否同步至行事曆」開關。
///
/// <b>資料落點</b>：隊別分類由 C1 球隊自動帶入，這裡只存前台顯示覆寫（<c>calendar_team_settings</c>：顯示名稱、代表色、排序、是否公開）；
/// 預設檢視等單值設定存在該俱樂部的 <c>settings</c>（<c>calendar.*</c> 鍵）；賽事類型（<c>event_types</c>）<b>兩隊共用、不帶 club_id</b>，
/// 所以寫入一律只有系統管理員（共同內容唯讀規則，§5.4），受限帳號可看不可改。
/// 試訓同步開關寫入設定並<b>同步更新該俱樂部所有試訓的 <c>sync_to_calendar</c></b>（<c>calendar_events</c> 視圖依該欄位納入試訓）。
/// </summary>
public sealed class AdminCalendarSettingsRepository(
    ClubDbContext db, ClubSettingsStore settings, IQueryCache cache, AdminCalendarCustomEventsRepository customEvents)
{
    public const string ViewKey = "calendar.default_view";
    public const string RangeKey = "calendar.default_range";
    public const string TeamKey = "calendar.default_team";
    public const string HomeTeamsKey = "calendar.embed.home_teams";
    public const string FirstTeamKey = "calendar.embed.first_team_code";
    public const string SyncTrialsKey = "calendar.sync_trials";

    public static readonly IReadOnlySet<string> Views = new HashSet<string>(StringComparer.Ordinal) { "list", "month" };
    public static readonly IReadOnlySet<string> Ranges = new HashSet<string>(StringComparer.Ordinal) { "upcoming", "this_month", "next_30_days", "season" };

    /// <summary>系統預設圖示集（賽事類型圖示只能從這裡選，不是上傳圖片）：代碼為圖示名稱，標籤為給畫面顯示的中文。</summary>
    public static readonly IReadOnlyList<AdminEventTypeIconDto> Icons =
    [
        new() { Code = "megaphone", Label = "擴音器" }, new() { Code = "pen-line", Label = "簽名筆" }, new() { Code = "users", Label = "群眾" },
        new() { Code = "whistle", Label = "哨子" }, new() { Code = "alert-circle", Label = "警示" }, new() { Code = "calendar", Label = "日曆" },
        new() { Code = "trophy", Label = "獎盃" }, new() { Code = "mic", Label = "麥克風" }, new() { Code = "camera", Label = "相機" },
        new() { Code = "handshake", Label = "握手" }, new() { Code = "heart", Label = "愛心" }, new() { Code = "flag", Label = "旗幟" },
        new() { Code = "star", Label = "星星" }, new() { Code = "ticket", Label = "票券" }, new() { Code = "gift", Label = "禮物" },
        new() { Code = "graduation-cap", Label = "學位帽" }, new() { Code = "shirt", Label = "球衣" }, new() { Code = "map-pin", Label = "地點" },
        new() { Code = "music", Label = "音符" }, new() { Code = "newspaper", Label = "報紙" },
    ];

    private static readonly Regex Hex = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly Regex TypeCode = new("^[a-z][a-z0-9_-]*$", RegexOptions.Compiled);

    private static readonly string[] AllKeys = [ViewKey, RangeKey, TeamKey, HomeTeamsKey, FirstTeamKey, SyncTrialsKey];

    // ═══════════════════ 設定 ═══════════════════

    public async Task<AdminCalendarSettingsDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var values = await settings.GetManyAsync(scope.ClubId, AllKeys, cancellationToken);
        var teams = await LoadTeamSettingsAsync(scope, cancellationToken);
        var types = await customEvents.ListEventTypesAsync(cancellationToken);
        return new AdminCalendarSettingsDto
        {
            DefaultView = Pick(values, ViewKey, Views, "list"),
            DefaultRange = Pick(values, RangeKey, Ranges, "upcoming"),
            DefaultTeamCode = values.GetValueOrDefault(TeamKey) is { Length: > 0 } t ? t : "all",
            HomeTeamCodes = ParseCodes(values.GetValueOrDefault(HomeTeamsKey)),
            FirstTeamCode = values.GetValueOrDefault(FirstTeamKey) is { Length: > 0 } f ? f : null,
            SyncTrials = string.Equals(values.GetValueOrDefault(SyncTrialsKey), "true", StringComparison.Ordinal),
            Teams = teams,
            EventTypes = types,
        };
    }

    public async Task<AdminCalendarSettingsDto> UpdateAsync(
        AdminClubScope scope, UpdateCalendarSettingsRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.DefaultView, Views.ToHashSet(), "預設檢視", "「列表」或「月曆」", "defaultView");
        AdminInput.OneOf(request.DefaultRange, Ranges.ToHashSet(), "預設顯示範圍", "「即將到來」「本月」「未來 30 天」或「整個球季」", "defaultRange");

        var teamCodes = await db.Teams.AsNoTracking().Where(t => t.ClubId == scope.ClubId).Select(t => t.Code).ToListAsync(cancellationToken);
        var defaultTeam = string.IsNullOrWhiteSpace(request.DefaultTeamCode) ? "all" : request.DefaultTeamCode.Trim();
        if (defaultTeam != "all" && !teamCodes.Contains(defaultTeam))
        {
            throw new AdminValidationException("預設選取的隊別不存在，請確認隊別屬於目前的俱樂部。", "defaultTeamCode");
        }

        var home = (request.HomeTeamCodes ?? []).Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToList();
        if (home.Any(c => !teamCodes.Contains(c)))
        {
            throw new AdminValidationException("首頁顯示的隊別含有不存在的隊別，請確認隊別屬於目前的俱樂部。", "homeTeamCodes");
        }

        var first = string.IsNullOrWhiteSpace(request.FirstTeamCode) ? null : request.FirstTeamCode.Trim();
        if (first is not null && !teamCodes.Contains(first))
        {
            throw new AdminValidationException("一線隊頁固定顯示的隊別不存在，請確認隊別屬於目前的俱樂部。", "firstTeamCode");
        }

        var operatorId = scope.Identity.AdminUserId;
        await settings.UpsertAsync(scope.ClubId, ViewKey, request.DefaultView, "calendar", operatorId, cancellationToken);
        await settings.UpsertAsync(scope.ClubId, RangeKey, request.DefaultRange, "calendar", operatorId, cancellationToken);
        await settings.UpsertAsync(scope.ClubId, TeamKey, defaultTeam, "calendar", operatorId, cancellationToken);
        await settings.UpsertAsync(scope.ClubId, HomeTeamsKey, JsonSerializer.Serialize(home), "calendar", operatorId, cancellationToken);
        await settings.UpsertAsync(scope.ClubId, FirstTeamKey, first, "calendar", operatorId, cancellationToken);
        await settings.UpsertAsync(scope.ClubId, SyncTrialsKey, request.SyncTrials ? "true" : "false", "calendar", operatorId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        // 視圖依 trials.sync_to_calendar 納入試訓，開關要連動全部既有場次。
        var sync = request.SyncTrials;
        await db.Trials.Where(t => t.ClubId == scope.ClubId && t.SyncToCalendar != sync)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SyncToCalendar, sync), cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await GetAsync(scope, cancellationToken);
    }

    private static string Pick(Dictionary<string, string?> values, string key, IReadOnlySet<string> allowed, string fallback)
        => values.GetValueOrDefault(key) is { } v && allowed.Contains(v) ? v : fallback;

    internal static IReadOnlyList<string> ParseCodes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // ═══════════════════ 隊別分類 ═══════════════════

    private async Task<IReadOnlyList<AdminCalendarTeamSettingDto>> LoadTeamSettingsAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var rows = await db.Teams.AsNoTracking().Where(t => t.ClubId == scope.ClubId)
            .Select(t => new
            {
                t.Id, t.Code, t.Type, t.TeamColor, t.SortOrder,
                NameZh = t.TeamsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        var overrides = await db.CalendarTeamSettings.AsNoTracking().Include(s => s.CalendarTeamSettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId).ToListAsync(cancellationToken);

        return rows.Select(r =>
        {
            var o = overrides.FirstOrDefault(s => s.TeamId == r.Id);
            return new AdminCalendarTeamSettingDto
            {
                TeamId = r.Id, Code = r.Code, Type = r.Type, TeamNameZh = r.NameZh,
                DisplayNameZh = o?.CalendarTeamSettingsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.DisplayName,
                DisplayNameEn = o?.CalendarTeamSettingsI18ns.FirstOrDefault(i => i.Locale == "en")?.DisplayName,
                Colour = o?.Colour, EffectiveColour = o?.Colour ?? r.TeamColor, SortOrder = o?.SortOrder,
                EffectiveSortOrder = o?.SortOrder ?? r.SortOrder, IsPublic = o?.IsPublic ?? true,
            };
        }).OrderBy(t => t.EffectiveSortOrder).ThenBy(t => t.Code, StringComparer.Ordinal).ToList();
    }

    public async Task<IReadOnlyList<AdminCalendarTeamSettingDto>> UpdateTeamsAsync(
        AdminClubScope scope, UpdateCalendarTeamSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.Teams.Count == 0 || request.Teams.Select(t => t.TeamId).Distinct().Count() != request.Teams.Count)
        {
            throw new AdminValidationException("隊別設定不可為空，也不可重複。", "teams");
        }

        var teamIds = await db.Teams.AsNoTracking().Where(t => t.ClubId == scope.ClubId).Select(t => t.Id).ToListAsync(cancellationToken);
        for (var i = 0; i < request.Teams.Count; i++)
        {
            var input = request.Teams[i];
            if (!teamIds.Contains(input.TeamId))
            {
                throw new AdminValidationException("設定裡含有不存在的隊別，請確認隊別屬於目前的俱樂部。", FieldKey.Item("teams", i, "teamId"));
            }

            if (!string.IsNullOrWhiteSpace(input.Colour) && !Hex.IsMatch(input.Colour.Trim()))
            {
                throw new AdminValidationException("代表色請填 6 位色碼，例如 #0B3D91。", FieldKey.Item("teams", i, "colour"));
            }

            AdminInput.OptionalText(input.DisplayNameZh, "中文顯示名稱", 64, FieldKey.Item("teams", i, "displayNameZh"));
            AdminInput.OptionalText(input.DisplayNameEn, "英文顯示名稱", 64, FieldKey.Item("teams", i, "displayNameEn"));
            AdminInput.OptionalNonNegative(input.SortOrder, "排序", FieldKey.Item("teams", i, "sortOrder"));
        }

        var existing = await db.CalendarTeamSettings.Include(s => s.CalendarTeamSettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var input in request.Teams)
        {
            var row = existing.FirstOrDefault(s => s.TeamId == input.TeamId);
            if (row is null)
            {
                row = new CalendarTeamSetting
                {
                    Id = Guid.NewGuid(), ClubId = scope.ClubId, TeamId = input.TeamId, CreatedAt = now, CreatedBy = scope.Identity.AdminUserId,
                };
                db.CalendarTeamSettings.Add(row);
            }

            row.Colour = string.IsNullOrWhiteSpace(input.Colour) ? null : input.Colour.Trim().ToUpperInvariant();
            row.SortOrder = input.SortOrder;
            row.IsPublic = input.IsPublic;
            row.UpdatedAt = now;
            row.UpdatedBy = scope.Identity.AdminUserId;
            SetDisplayName(row, RequestLocale.DefaultDbLocale, input.DisplayNameZh);
            SetDisplayName(row, "en", input.DisplayNameEn);
        }

        await db.SaveChangesAsync(cancellationToken);
        await InvalidateAsync(scope, cancellationToken);
        return await LoadTeamSettingsAsync(scope, cancellationToken);
    }

    private void SetDisplayName(CalendarTeamSetting row, string locale, string? value)
    {
        var existing = row.CalendarTeamSettingsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (existing is not null)
            {
                db.Remove(existing);
            }

            return;
        }

        if (existing is null)
        {
            existing = new CalendarTeamSettingsI18n { CalendarTeamSettingId = row.Id, Locale = locale };
            row.CalendarTeamSettingsI18ns.Add(existing);
            db.CalendarTeamSettingsI18ns.Add(existing);
        }

        existing.DisplayName = value.Trim();
    }

    // ═══════════════════ 賽事類型（兩隊共用，僅系統管理員可寫） ═══════════════════

    private static void RequireSharedWritable(AdminClubScope scope)
    {
        if (!scope.Identity.IsSuperAdmin)
        {
            throw new SharedContentReadOnlyException("賽事類型");
        }
    }

    public async Task<AdminEventTypeDto> CreateEventTypeAsync(AdminClubScope scope, UpsertEventTypeRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        RequireSharedWritable(scope);
        var (code, colour, icon) = ValidateType(request);
        if (await db.EventTypes.AsNoTracking().AnyAsync(t => t.Code == code, cancellationToken))
        {
            throw new AdminConflictException("類型代碼重複", $"已經有代碼為「{code}」的賽事類型了，請換一個代碼。", "code");
        }

        var now = DateTime.UtcNow;
        var type = new EventType
        {
            Id = Guid.NewGuid(), Code = code, Colour = colour, Icon = icon, IsPublic = request.IsPublic, SortOrder = request.SortOrder,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        db.EventTypes.Add(type);
        SetTypeNames(type, request);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateSharedAsync(cancellationToken);
        return await GetEventTypeAsync(type.Id, cancellationToken);
    }

    public async Task<AdminEventTypeDto?> UpdateEventTypeAsync(AdminClubScope scope, Guid id, UpsertEventTypeRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        RequireSharedWritable(scope);
        var (code, colour, icon) = ValidateType(request);
        var type = await db.EventTypes.Include(t => t.EventTypesI18ns).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (type is null)
        {
            return null;
        }

        if (!string.Equals(code, type.Code, StringComparison.Ordinal))
        {
            throw new AdminValidationException("類型代碼建立後不能變更；需要換代碼請新增一個新的類型。", "code");
        }

        type.Colour = colour;
        type.Icon = icon;
        type.IsPublic = request.IsPublic;
        type.SortOrder = request.SortOrder;
        type.UpdatedAt = DateTime.UtcNow;
        type.UpdatedBy = operatorId;
        SetTypeNames(type, request);
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateSharedAsync(cancellationToken);
        return await GetEventTypeAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteEventTypeAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        RequireSharedWritable(scope);
        var type = await db.EventTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (type is null)
        {
            return false;
        }

        if (await db.CalendarCustomEvents.AsNoTracking().AnyAsync(e => e.EventTypeId == id, cancellationToken))
        {
            throw new AdminConflictException("類型仍被使用", "已經有自建活動使用這個類型，不能刪除。可以改為「不公開」讓前台不再顯示。");
        }

        db.EventTypes.Remove(type); // 側表由資料庫串聯刪除
        await db.SaveChangesAsync(cancellationToken);
        await InvalidateSharedAsync(cancellationToken);
        return true;
    }

    public async Task ReorderEventTypesAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, Guid? operatorId, CancellationToken cancellationToken)
    {
        RequireSharedWritable(scope);
        if (ids.Count == 0 || ids.Distinct().Count() != ids.Count)
        {
            throw new AdminValidationException("排序清單不可為空，也不可重複。");
        }

        var types = await db.EventTypes.OrderBy(t => t.SortOrder).ThenBy(t => t.RowSeq).ToListAsync(cancellationToken);
        var byId = types.ToDictionary(t => t.Id);
        if (ids.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("排序清單含有不存在的類型，請重新整理後再試。");
        }

        var order = ids.Concat(types.Select(t => t.Id).Where(i => !ids.Contains(i))).ToList();
        var now = DateTime.UtcNow;
        for (var i = 0; i < order.Count; i++)
        {
            var t = byId[order[i]];
            if (t.SortOrder != i)
            {
                t.SortOrder = i;
                t.UpdatedAt = now;
                t.UpdatedBy = operatorId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await InvalidateSharedAsync(cancellationToken);
    }

    private async Task<AdminEventTypeDto> GetEventTypeAsync(Guid id, CancellationToken cancellationToken)
        => (await customEvents.ListEventTypesAsync(cancellationToken)).First(t => t.Id == id);

    private static (string Code, string? Colour, string? Icon) ValidateType(UpsertEventTypeRequest request)
    {
        var code = AdminInput.RequireText(request.Code, "類型代碼", 32, "code");
        if (!TypeCode.IsMatch(code))
        {
            throw new AdminValidationException("類型代碼只能使用小寫英文字母、數字、底線與連字號，且要以英文字母開頭。", "code");
        }

        AdminInput.RequireText(request.NameZh, "中文名稱", 64, "nameZh");
        AdminInput.OptionalText(request.NameEn, "英文名稱", 64, "nameEn");
        var colour = string.IsNullOrWhiteSpace(request.Colour) ? null : request.Colour.Trim().ToUpperInvariant();
        if (colour is not null && !Hex.IsMatch(colour))
        {
            throw new AdminValidationException("識別色請填 6 位色碼，例如 #B91C1C。", "colour");
        }

        var icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
        if (icon is not null && Icons.All(i => i.Code != icon))
        {
            throw new AdminValidationException("圖示請從系統提供的圖示清單中選擇。", "icon");
        }

        AdminInput.OptionalNonNegative(request.SortOrder, "排序", "sortOrder");
        return (code, colour, icon);
    }

    private void SetTypeNames(EventType type, UpsertEventTypeRequest request)
    {
        void Upsert(string locale, string name)
        {
            var row = type.EventTypesI18ns.FirstOrDefault(i => i.Locale == locale);
            if (row is null)
            {
                row = new EventTypesI18n { EventTypeId = type.Id, Locale = locale };
                type.EventTypesI18ns.Add(row);
                db.EventTypesI18ns.Add(row);
            }

            row.Name = name.Trim();
        }

        Upsert(RequestLocale.DefaultDbLocale, request.NameZh);
        var en = type.EventTypesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (!string.IsNullOrWhiteSpace(request.NameEn))
        {
            Upsert("en", request.NameEn);
        }
        else if (en is not null)
        {
            db.Remove(en);
        }
    }

    // ═══════════════════ 快取 ═══════════════════

    private Task InvalidateAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => cache.InvalidateAsync("calendar", scope.ClubCode, cancellationToken);

    /// <summary>賽事類型兩隊共用：所有俱樂部的行事曆快取一起失效。</summary>
    private async Task InvalidateSharedAsync(CancellationToken cancellationToken)
    {
        var codes = await db.Clubs.AsNoTracking().Select(c => c.Code).ToListAsync(cancellationToken);
        foreach (var code in codes)
        {
            await cache.InvalidateAsync("calendar", code, cancellationToken);
        }
    }
}
