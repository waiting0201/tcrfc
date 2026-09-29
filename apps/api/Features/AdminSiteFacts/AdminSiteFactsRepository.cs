using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteFacts;

/// <summary>
/// `I` 網站設定——`GEO-03`／`GEO-04` 事實的讀寫（S1-12d）。**不新增資料表**，沿用既有
/// <c>settings</c>／<c>settings_i18n</c>（跟 <c>Features/AdminSeo</c> 同一個原則）承載純量與
/// 逐語系事實，主場場地本身沿用既有 <c>venues</c>／<c>venues_i18n</c>。
///
/// 設定鍵詞彙（本輪定案，比照 <c>Features/AdminSeo/AdminSeoSettingsRepository</c> 檔頭「之後任何
/// 模組要沿用同一套鍵值命名慣例 <c>&lt;setting_group&gt;.&lt;name&gt;</c>」的既有指示）：
/// <c>setting_group='site'</c>：
/// - <c>site.founded_year</c>（單一值，數字字串，非人類語言）
/// - <c>site.founding_date</c>（單一值，ISO 8601 日期，非人類語言）
/// - <c>site.founding_date_display</c>（逐語系）
/// - <c>site.founding_title</c>（逐語系，可整筆不存在＝沒有這項事實）
/// - <c>site.league_name</c>（逐語系）
/// - <c>site.league_short_name</c>（逐語系，可整筆不存在）
/// - <c>site.squad_structure_summary</c>（逐語系）
/// - <c>site.squad_codes</c>（單一值，逗號分隔代碼清單，非人類語言）
/// - <c>site.contact_phone</c>（單一值，電話號碼，非人類語言）
/// - <c>site.contact_hours</c>（逐語系——營業時間是人類可讀文字，例如「平日 09:00–18:00」）
/// - <c>site.home_venue_ids</c>（單一值，逗號分隔 <c>venues.id</c> 清單，依顯示順序，第一筆＝主要主場）
/// - <c>site.blue_whale_site_url</c>（單一值，<c>https://</c> 網址，非人類語言，概念上只屬於
///   <c>tcrfc</c> 這個俱樂部，見 <see cref="AdminSiteFactsDto.BlueWhaleSiteUrl"/> 檔頭）
///
/// 🔴 **`Venue` 本身刻意不帶 <c>club_id</c>**（docs/12 §4.7：場地是地理實體，兩隊可能共用同一座
/// 球場，重複建會產生兩組人工標的座標）。「這個俱樂部的主場是哪幾筆既有 <c>Venue</c>」這件事
/// 本身才是俱樂部範圍的事實，因此用 <c>site.home_venue_ids</c>（<c>club_id</c> 必填）表達引用清單，
/// 不是在 <c>Venue</c> 上加 <c>club_id</c>／<c>is_home_ground</c> 欄位——這樣才不用改既有綱要。
/// 從清單移除一筆場地**不會刪除** <c>Venue</c> 列本身（可能仍被其他俱樂部或其他資料引用，例如
/// 賽事、梯次的地點），只是不再視為這個俱樂部的主場。
/// </summary>
public sealed class AdminSiteFactsRepository(ClubDbContext dbContext)
{
    private const string KeyFoundedYear = "site.founded_year";
    private const string KeyFoundingDate = "site.founding_date";
    private const string KeyFoundingDateDisplay = "site.founding_date_display";
    private const string KeyFoundingTitle = "site.founding_title";
    private const string KeyLeagueName = "site.league_name";
    private const string KeyLeagueShortName = "site.league_short_name";
    private const string KeySquadStructureSummary = "site.squad_structure_summary";
    private const string KeySquadCodes = "site.squad_codes";
    private const string KeyContactPhone = "site.contact_phone";
    private const string KeyContactHours = "site.contact_hours";
    private const string KeyHomeVenueIds = "site.home_venue_ids";

    /// <summary>台中藍鯨官方網站網址（主站規劃書 §3.6「06 女子足球」入口頁），見
    /// <see cref="AdminSiteFactsDto.BlueWhaleSiteUrl"/> 檔頭說明。單一值、非人類語言（網址本身
    /// 不需要逐語系）。</summary>
    private const string KeyBlueWhaleSiteUrl = "site.blue_whale_site_url";

    private const string Group = "site";

    private static readonly string[] AllKeys =
    [
        KeyFoundedYear, KeyFoundingDate, KeyFoundingDateDisplay, KeyFoundingTitle,
        KeyLeagueName, KeyLeagueShortName, KeySquadStructureSummary, KeySquadCodes,
        KeyContactPhone, KeyContactHours, KeyHomeVenueIds, KeyBlueWhaleSiteUrl,
    ];

    public async Task<AdminSiteFactsDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var settings = await dbContext.Settings.AsNoTracking()
            .Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey))
            .ToListAsync(cancellationToken);

        var homeVenues = await LoadHomeVenuesAsync(settings, cancellationToken);

        return BuildDto(settings, homeVenues);
    }

    /// <summary>整份取代（見 <see cref="AdminSiteFactsDto"/> 檔頭「整份取代語意」）。</summary>
    public async Task<AdminSiteFactsDto> UpdateAsync(
        AdminClubScope scope, UpdateSiteFactsRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FoundedYear))
        {
            throw new AdminSiteFactsValidationException("成立年份為必填欄位。");
        }

        if (string.IsNullOrWhiteSpace(request.FoundingDateDisplayZh))
        {
            throw new AdminSiteFactsValidationException("成立年份／日期顯示文字（中文）為必填欄位。");
        }

        if (string.IsNullOrWhiteSpace(request.LeagueNameZh))
        {
            throw new AdminSiteFactsValidationException("所屬聯賽名稱（中文）為必填欄位。");
        }

        if (string.IsNullOrWhiteSpace(request.SquadStructureZh))
        {
            throw new AdminSiteFactsValidationException("梯隊組成敘述（中文）為必填欄位。");
        }

        var homeVenueRequests = request.HomeVenues ?? [];
        foreach (var v in homeVenueRequests)
        {
            if (string.IsNullOrWhiteSpace(v.NameZh))
            {
                throw new AdminSiteFactsValidationException("每一筆場地的名稱（中文）為必填欄位。");
            }
        }

        ValidateBlueWhaleSiteUrl(request.BlueWhaleSiteUrl);

        var settings = await dbContext.Settings
            .Include(s => s.SettingsI18ns)
            .Where(s => s.ClubId == scope.ClubId && AllKeys.Contains(s.SettingKey))
            .ToDictionaryAsync(s => s.SettingKey, cancellationToken);

        UpsertValue(settings, KeyFoundedYear, scope.ClubId, request.FoundedYear, operatorId);
        UpsertValue(settings, KeyFoundingDate, scope.ClubId, request.FoundingDateIso, operatorId);
        UpsertI18nRequired(settings, KeyFoundingDateDisplay, scope.ClubId, request.FoundingDateDisplayZh!, request.FoundingDateDisplayEn, operatorId);
        UpsertI18nOptional(settings, KeyFoundingTitle, scope.ClubId, request.FoundingTitleZh, request.FoundingTitleEn, operatorId);
        UpsertI18nRequired(settings, KeyLeagueName, scope.ClubId, request.LeagueNameZh!, request.LeagueNameEn, operatorId);
        UpsertI18nOptional(settings, KeyLeagueShortName, scope.ClubId, request.LeagueShortNameZh, request.LeagueShortNameEn, operatorId);
        UpsertI18nRequired(settings, KeySquadStructureSummary, scope.ClubId, request.SquadStructureZh!, request.SquadStructureEn, operatorId);
        UpsertValue(settings, KeySquadCodes, scope.ClubId, JoinCodes(request.SquadCodes), operatorId);
        UpsertValue(settings, KeyContactPhone, scope.ClubId, request.ContactPhone, operatorId);
        UpsertI18nOptional(settings, KeyContactHours, scope.ClubId, request.ContactHoursZh, request.ContactHoursEn, operatorId);
        UpsertValue(settings, KeyBlueWhaleSiteUrl, scope.ClubId, request.BlueWhaleSiteUrl, operatorId);

        var venueIds = await UpsertHomeVenuesAsync(homeVenueRequests, operatorId, cancellationToken);
        UpsertValue(settings, KeyHomeVenueIds, scope.ClubId, string.Join(',', venueIds), operatorId);

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetAsync(scope, cancellationToken);
    }

    /// <summary>「驗證格式為 https 網址」——空白值合法（未設定），有值時必須是可解析的絕對網址
    /// 且 scheme 為 <c>https</c>。不接受相對路徑、<c>http://</c>、或非網址字串。</summary>
    private static void ValidateBlueWhaleSiteUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new AdminSiteFactsValidationException("台中藍鯨官網網址格式不正確，須為 https:// 開頭的完整網址。");
        }
    }

    private static string? JoinCodes(IReadOnlyList<string>? codes)
        => codes is null || codes.Count == 0 ? null : string.Join(',', codes.Select(c => c.Trim()).Where(c => c.Length > 0));

    private static IReadOnlyList<string> SplitCodes(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>依 <paramref name="requests"/> 建立／更新 <c>Venue</c>／<c>VenuesI18n</c>，回傳依原始
    /// 順序排列的 <c>venues.id</c> 清單（供寫回 <c>site.home_venue_ids</c>）。找不到呼叫端指定的
    /// <see cref="UpdateSiteFactVenueRequest.Id"/> 時回 400——不能默默改成新增一筆，那會讓呼叫端
    /// 以為在改既有場地，實際上卻多長出一筆重複場地。</summary>
    private async Task<List<Guid>> UpsertHomeVenuesAsync(
        IReadOnlyList<UpdateSiteFactVenueRequest> requests, Guid? operatorId, CancellationToken cancellationToken)
    {
        var result = new List<Guid>();
        if (requests.Count == 0)
        {
            return result;
        }

        var existingIds = requests.Where(r => r.Id.HasValue).Select(r => r.Id!.Value).ToList();
        var existingVenues = existingIds.Count == 0
            ? new Dictionary<Guid, Venue>()
            : await dbContext.Venues
                .Include(v => v.VenuesI18ns)
                .Where(v => existingIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var request in requests)
        {
            Venue venue;

            if (request.Id.HasValue)
            {
                if (!existingVenues.TryGetValue(request.Id.Value, out venue!))
                {
                    throw new AdminSiteFactsValidationException($"找不到場地（id={request.Id}），無法更新，請重新整理後再試。");
                }

                venue.UpdatedAt = now;
                venue.UpdatedBy = operatorId;
            }
            else
            {
                venue = new Venue
                {
                    Id = Guid.NewGuid(),
                    SortOrder = 0,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };
                dbContext.Venues.Add(venue);
            }

            UpsertVenueI18n(venue, RequestLocale.DefaultDbLocale, request.NameZh, request.Address);
            UpsertVenueI18n(venue, "en", request.NameEn, null);

            result.Add(venue.Id);
        }

        return result;
    }

    private void UpsertVenueI18n(Venue venue, string locale, string? name, string? address)
    {
        var row = venue.VenuesI18ns.FirstOrDefault(i => i.Locale == locale);

        if (string.IsNullOrWhiteSpace(name))
        {
            if (locale != RequestLocale.DefaultDbLocale && row is not null)
            {
                dbContext.Remove(row);
                venue.VenuesI18ns.Remove(row);
            }

            return;
        }

        if (row is null)
        {
            row = new VenuesI18n { VenueId = venue.Id, Locale = locale };
            venue.VenuesI18ns.Add(row);
            dbContext.VenuesI18ns.Add(row);
        }

        row.Name = name;
        row.Address = address;
    }

    private async Task<List<AdminSiteFactVenueDto>> LoadHomeVenuesAsync(
        IReadOnlyCollection<Setting> settings, CancellationToken cancellationToken)
    {
        var idsValue = settings.FirstOrDefault(s => s.SettingKey == KeyHomeVenueIds)?.SettingValue;
        var ids = ParseVenueIds(idsValue);
        if (ids.Count == 0)
        {
            return [];
        }

        var venues = await dbContext.Venues.AsNoTracking()
            .Include(v => v.VenuesI18ns)
            .Where(v => ids.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var result = new List<AdminSiteFactVenueDto>();
        foreach (var id in ids)
        {
            if (!venues.TryGetValue(id, out var venue))
            {
                // 引用的場地已不存在（理論上不會發生——本模組從不刪除 Venue 列），略過而不是整個 500。
                continue;
            }

            var zh = venue.VenuesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
            var en = venue.VenuesI18ns.FirstOrDefault(i => i.Locale == "en");

            result.Add(new AdminSiteFactVenueDto
            {
                Id = venue.Id,
                NameZh = zh?.Name ?? string.Empty,
                NameEn = en?.Name,
                Address = zh?.Address,
            });
        }

        return result;
    }

    private static List<Guid> ParseVenueIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var result = new List<Guid>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(token, out var id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private AdminSiteFactsDto BuildDto(IReadOnlyCollection<Setting> settings, IReadOnlyList<AdminSiteFactVenueDto> homeVenues)
    {
        string? Value(string key) => settings.FirstOrDefault(s => s.SettingKey == key)?.SettingValue;

        string? I18n(string key, string locale) => settings
            .FirstOrDefault(s => s.SettingKey == key)?.SettingsI18ns
            .FirstOrDefault(i => i.Locale == locale)?.Value;

        return new AdminSiteFactsDto
        {
            FoundedYear = Value(KeyFoundedYear),
            FoundingDateIso = Value(KeyFoundingDate),
            FoundingDateDisplayZh = I18n(KeyFoundingDateDisplay, RequestLocale.DefaultDbLocale),
            FoundingDateDisplayEn = I18n(KeyFoundingDateDisplay, "en"),
            FoundingTitleZh = I18n(KeyFoundingTitle, RequestLocale.DefaultDbLocale),
            FoundingTitleEn = I18n(KeyFoundingTitle, "en"),
            LeagueNameZh = I18n(KeyLeagueName, RequestLocale.DefaultDbLocale),
            LeagueNameEn = I18n(KeyLeagueName, "en"),
            LeagueShortNameZh = I18n(KeyLeagueShortName, RequestLocale.DefaultDbLocale),
            LeagueShortNameEn = I18n(KeyLeagueShortName, "en"),
            SquadStructureZh = I18n(KeySquadStructureSummary, RequestLocale.DefaultDbLocale),
            SquadStructureEn = I18n(KeySquadStructureSummary, "en"),
            SquadCodes = SplitCodes(Value(KeySquadCodes)),
            HomeVenues = homeVenues,
            ContactPhone = Value(KeyContactPhone),
            ContactHoursZh = I18n(KeyContactHours, RequestLocale.DefaultDbLocale),
            ContactHoursEn = I18n(KeyContactHours, "en"),
            BlueWhaleSiteUrl = Value(KeyBlueWhaleSiteUrl),
        };
    }

    private Setting GetOrCreate(Dictionary<string, Setting> settings, string key, Guid clubId, Guid? operatorId)
    {
        if (settings.TryGetValue(key, out var existing))
        {
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = operatorId;
            return existing;
        }

        var now = DateTime.UtcNow;
        var setting = new Setting
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            SettingKey = key,
            SettingGroup = Group,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Settings.Add(setting);
        settings[key] = setting;
        return setting;
    }

    /// <summary>比照 <c>AdminSeoSettingsRepository.UpsertValue</c>：只有「這個鍵已經有列」或
    /// 「這次要寫入非空白值」才會經過 <see cref="GetOrCreate"/>，避免每次 PUT 都把全部鍵各建一列
    /// 空殼。</summary>
    private void UpsertValue(Dictionary<string, Setting> settings, string key, Guid clubId, string? value, Guid? operatorId)
    {
        var hasValue = !string.IsNullOrWhiteSpace(value);
        if (!hasValue && !settings.ContainsKey(key))
        {
            return;
        }

        var setting = GetOrCreate(settings, key, clubId, operatorId);
        setting.SettingValue = hasValue ? value : null;
    }

    /// <summary>中文必填的逐語系鍵（比照 <c>AdminSeoSettingsRepository.UpsertI18n</c>）。呼叫端已先
    /// 驗證過 <paramref name="zhValue"/> 非空白。</summary>
    private void UpsertI18nRequired(Dictionary<string, Setting> settings, string key, Guid clubId, string zhValue, string? enValue, Guid? operatorId)
    {
        var setting = GetOrCreate(settings, key, clubId, operatorId);
        SetI18nRow(setting, RequestLocale.DefaultDbLocale, zhValue);
        SetOrRemoveI18nRow(setting, "en", enValue);
    }

    /// <summary>中文亦可為空的逐語系鍵（例如「首季頭銜」——不是每個俱樂部都有這筆事實）。
    /// 中文與英文皆空白時整筆刪除（不留下一個兩個語系都是空字串的殼）。</summary>
    private void UpsertI18nOptional(Dictionary<string, Setting> settings, string key, Guid clubId, string? zhValue, string? enValue, Guid? operatorId)
    {
        var hasZh = !string.IsNullOrWhiteSpace(zhValue);
        var hasEn = !string.IsNullOrWhiteSpace(enValue);

        if (!hasZh && !hasEn)
        {
            if (settings.TryGetValue(key, out var existingEmpty))
            {
                foreach (var row in existingEmpty.SettingsI18ns.ToList())
                {
                    dbContext.Remove(row);
                    existingEmpty.SettingsI18ns.Remove(row);
                }

                existingEmpty.UpdatedAt = DateTime.UtcNow;
                existingEmpty.UpdatedBy = operatorId;
            }

            return;
        }

        var setting = GetOrCreate(settings, key, clubId, operatorId);
        SetOrRemoveI18nRow(setting, RequestLocale.DefaultDbLocale, zhValue);
        SetOrRemoveI18nRow(setting, "en", enValue);
    }

    private void SetI18nRow(Setting setting, string locale, string value)
    {
        var row = setting.SettingsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new SettingsI18n { SettingId = setting.Id, Locale = locale };
            setting.SettingsI18ns.Add(row);
            dbContext.SettingsI18ns.Add(row);
        }

        row.Value = value;
    }

    private void SetOrRemoveI18nRow(Setting setting, string locale, string? value)
    {
        var row = setting.SettingsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (!string.IsNullOrWhiteSpace(value))
        {
            if (row is null)
            {
                row = new SettingsI18n { SettingId = setting.Id, Locale = locale };
                setting.SettingsI18ns.Add(row);
                dbContext.SettingsI18ns.Add(row);
            }

            row.Value = value;
        }
        else if (row is not null)
        {
            dbContext.Remove(row);
            setting.SettingsI18ns.Remove(row);
        }
    }
}
