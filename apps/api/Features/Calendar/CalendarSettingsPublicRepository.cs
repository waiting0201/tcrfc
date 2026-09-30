using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminCalendar;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Calendar;

/// <summary>13 賽事行事曆前台讀取 L3 設定（預設檢視、預設範圍、預設隊別、嵌入元件的預設篩選、可選隊別與賽事類型）。
/// 只回公開的隊別與賽事類型；快取實體 <c>calendar</c>（L3 寫入時失效）。</summary>
public sealed class CalendarSettingsPublicRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    private sealed record SettingRow(string SettingKey, string? SettingValue);
    private sealed record TeamRow(string Code, string? DisplayName, string? Colour, int SortOrder);
    private sealed record TypeRow(string Code, string? Name, string? Colour, string? Icon);

    public Task<PublicCalendarSettingsDto> GetAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
        => cache.GetOrCreateAsync("calendar", scope.ClubCode, dbLocale == "en" ? "en" : "zh", "settings", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            var values = (await connection.QueryAsync<SettingRow>(new CommandDefinition(
                "SELECT setting_key AS SettingKey, setting_value AS SettingValue FROM settings WHERE club_id = @ClubId AND setting_key LIKE N'calendar.%'",
                new { scope.ClubId }, cancellationToken: ct))).ToDictionary(r => r.SettingKey, r => r.SettingValue, StringComparer.Ordinal);

            var teams = (await connection.QueryAsync<TeamRow>(new CommandDefinition(
                """
                SELECT t.code AS Code,
                       COALESCE(NULLIF(dn.display_name, N''), NULLIF(ti.name, N''), NULLIF(tz.name, N''), t.code) AS DisplayName,
                       COALESCE(s.colour, t.team_color) AS Colour, COALESCE(s.sort_order, t.sort_order) AS SortOrder
                FROM teams t
                LEFT JOIN calendar_team_settings s ON s.team_id = t.id
                LEFT JOIN calendar_team_settings_i18n dn ON dn.calendar_team_setting_id = s.id AND dn.locale = @Locale
                LEFT JOIN teams_i18n ti ON ti.team_id = t.id AND ti.locale = @Locale
                LEFT JOIN teams_i18n tz ON tz.team_id = t.id AND tz.locale = N'zh-Hant'
                WHERE t.club_id = @ClubId AND COALESCE(s.is_public, 1) = 1
                ORDER BY COALESCE(s.sort_order, t.sort_order), t.code
                """,
                new { scope.ClubId, Locale = dbLocale }, cancellationToken: ct))).ToList();

            var types = (await connection.QueryAsync<TypeRow>(new CommandDefinition(
                """
                SELECT e.code AS Code, COALESCE(NULLIF(i.name, N''), NULLIF(z.name, N''), e.code) AS Name, e.colour AS Colour, e.icon AS Icon
                FROM event_types e
                LEFT JOIN event_types_i18n i ON i.event_type_id = e.id AND i.locale = @Locale
                LEFT JOIN event_types_i18n z ON z.event_type_id = e.id AND z.locale = N'zh-Hant'
                WHERE e.is_public = 1 ORDER BY e.sort_order, e.code
                """,
                new { Locale = dbLocale }, cancellationToken: ct))).ToList();

            var view = values.GetValueOrDefault(AdminCalendarSettingsRepository.ViewKey);
            var range = values.GetValueOrDefault(AdminCalendarSettingsRepository.RangeKey);
            var publicCodes = teams.Select(t => t.Code).ToHashSet(StringComparer.Ordinal);
            var defaultTeam = values.GetValueOrDefault(AdminCalendarSettingsRepository.TeamKey);
            return new PublicCalendarSettingsDto
            {
                DefaultView = view is not null && AdminCalendarSettingsRepository.Views.Contains(view) ? view : "list",
                DefaultRange = range is not null && AdminCalendarSettingsRepository.Ranges.Contains(range) ? range : "upcoming",
                DefaultTeamCode = defaultTeam is { Length: > 0 } && (defaultTeam == "all" || publicCodes.Contains(defaultTeam)) ? defaultTeam : "all",
                HomeTeamCodes = AdminCalendarSettingsRepository.ParseCodes(values.GetValueOrDefault(AdminCalendarSettingsRepository.HomeTeamsKey))
                    .Where(publicCodes.Contains).ToList(),
                FirstTeamCode = values.GetValueOrDefault(AdminCalendarSettingsRepository.FirstTeamKey) is { Length: > 0 } f && publicCodes.Contains(f) ? f : null,
                Teams = teams.Select(t => new PublicCalendarTeamDto { Code = t.Code, DisplayName = t.DisplayName ?? t.Code, Colour = t.Colour, SortOrder = t.SortOrder }).ToList(),
                EventTypes = types.Select(t => new PublicCalendarEventTypeDto { Code = t.Code, Name = t.Name ?? t.Code, Colour = t.Colour, Icon = t.Icon }).ToList(),
            };
        }, cancellationToken);
}
