using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Standings;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;
using Tcrfc.Api.Images;

namespace Tcrfc.Api.Features.AdminPlayers;

/// <summary>C2「球員」——俱樂部範圍 CRUD（主站規劃書 §4.3 C2）。
/// 🔴 <c>IQueryCache</c> 只為了寫入後失效——公開唯讀端點 <c>Features/Players/PlayersEndpoints.cs</c>
/// （entity="players"）已接快取，寫入這裡不失效會讓公開頁面在 TTL 到期前顯示舊資料，
/// 逐字比照 <c>AdminArticlesRepository.InvalidatePublicCacheAsync</c> 的既有做法。</summary>
public sealed class AdminPlayersRepository(ClubDbContext dbContext, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    private static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.Ordinal) { "active", "departed", "loan", "overseas" };

    /// <summary>肖像同意狀態值域（S1-7a，db/club-schema.sql <c>CK_players_portrait_consent_status</c>）。</summary>
    private static readonly HashSet<string> AllowedPortraitConsentStatuses =
        new(StringComparer.Ordinal) { "not_consented", "consented", "consented_by_guardian" };

    public async Task<IReadOnlyList<AdminPlayerListItemDto>> ListAsync(
        AdminClubScope scope, Guid? teamId, string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Players.AsNoTracking().Where(p => p.ClubId == scope.ClubId);

        if (teamId is Guid t)
        {
            query = query.Where(p => p.TeamId == t);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status == status);
        }

        var rows = await query
            .OrderBy(p => p.Team.SortOrder).ThenBy(p => p.ShirtNo)
            .Select(p => new
            {
                p.Id,
                p.TeamId,
                TeamCode = p.Team.Code,
                p.Slug,
                p.ShirtNo,
                p.Position,
                p.BirthOn,
                p.Status,
                p.PhotoKey,
                p.PhotoWidth,
                p.PhotoHeight,
                p.PortraitConsentStatus,
                p.UpdatedAt,
                NameZh = p.PlayersI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = p.PlayersI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminPlayerListItemDto
        {
            Id = r.Id,
            TeamId = r.TeamId,
            TeamCode = r.TeamCode,
            Slug = r.Slug,
            ShirtNo = r.ShirtNo,
            Position = r.Position,
            BirthOn = r.BirthOn,
            Status = r.Status,
            PhotoKey = r.PhotoKey,
            PhotoUrl = imageUrls.Resolve(r.PhotoKey),
            PhotoThumbUrl = imageUrls.ResolveThumbnail(r.PhotoKey),
            PhotoWidth = r.PhotoKey is null ? null : r.PhotoWidth,
            PhotoHeight = r.PhotoKey is null ? null : r.PhotoHeight,
            PortraitConsentStatus = r.PortraitConsentStatus,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminPlayerDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var player = await dbContext.Players.AsNoTracking()
            .Include(p => p.Team)
            .Include(p => p.PlayersI18ns)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);

        return player is null ? null : ToDetailDto(player);
    }

    /// <summary>🔴 建立一律歸屬 <paramref name="scope"/> 當下的俱樂部（<c>players.club_id</c> 必填），
    /// <paramref name="request"/>.TeamId 必須是這個俱樂部自己的球隊——見 <see cref="ResolveTeamAsync"/>。</summary>
    public async Task<AdminPlayerDetailDto> CreateAsync(
        AdminClubScope scope, TeamRowScope rowScope, Guid playerId, CreateAdminPlayerRequest request, string? photoKey, int? photoWidth, int? photoHeight, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateStatus(request.Status);
        ValidatePortraitConsentStatus(request.PortraitConsentStatus);
        ValidateContent(request.Content);
        ValidateNumericRanges(request.ShirtNo, request.HeightCm, request.WeightKg);
        var team = await ResolveTeamAsync(scope, request.TeamId, cancellationToken);

        // 🔴 S1-8 新增：列級授權——球員必屬於某支球隊（team_id 必填），直接用該球隊檢查即可。
        if (!rowScope.Allows(team.Id, team.Type))
        {
            throw new AdminForbiddenException("你的角色資料範圍不允許在這支球隊底下建立球員。");
        }

        // 網址代稱：有指定就驗證，沒指定依英文姓名／隊別與背號自動產生；兩者都要在同一俱樂部內唯一。
        string slug;
        if (request.Slug is { } requested)
        {
            PlayerSlug.Validate(requested);
            await EnsureSlugAvailableAsync(scope, requested, null, cancellationToken);
            slug = requested;
        }
        else
        {
            slug = await MakeUniqueSlugAsync(scope, PlayerSlug.Suggest(request.Content.En?.Name, team.Code, request.ShirtNo, playerId), cancellationToken);
        }

        var now = DateTime.UtcNow;
        var player = new Player
        {
            Id = playerId,
            ClubId = scope.ClubId,
            TeamId = team.Id,
            Slug = slug,
            ShirtNo = request.ShirtNo,
            Position = request.Position,
            BirthOn = request.BirthOn,
            HeightCm = request.HeightCm,
            WeightKg = request.WeightKg,
            Nationality = request.Nationality,
            PreferredFoot = request.PreferredFoot,
            JoinedOn = request.JoinedOn,
            Status = request.Status ?? "active",
            PhotoKey = photoKey,
            PhotoWidth = photoKey is null ? null : photoWidth,
            PhotoHeight = photoKey is null ? null : photoHeight,
            // 🔴 fail-closed（docs/12 §12 第 32 點）：省略時預設 not_consented，新建球員預設
            // 不對公開端點輸出照片，直到後台明確填寫已取得同意。
            PortraitConsentStatus = request.PortraitConsentStatus ?? "not_consented",
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Players.Add(player);
        AddOrReplaceI18n(player, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(player, "en", request.Content.En);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("players", scope.ClubCode, cancellationToken);
        return (await GetByIdAsync(scope, player.Id, cancellationToken))!;
    }

    public async Task<AdminPlayerDetailDto?> UpdateAsync(
        AdminClubScope scope, TeamRowScope rowScope, Guid id, UpdateAdminPlayerRequest request, PhotoKeyUpdate photoUpdate, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateStatus(request.Status);
        ValidatePortraitConsentStatus(request.PortraitConsentStatus);
        ValidateContent(request.Content);
        ValidateNumericRanges(request.ShirtNo, request.HeightCm, request.WeightKg);

        var player = await dbContext.Players
            .Include(p => p.Team)
            .Include(p => p.PlayersI18ns)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);

        if (player is null)
        {
            return null;
        }

        // 🔴 S1-8 新增：既有球隊（改隊之前）與目標球隊都要允許——避免 academy_only 範圍的帳號
        // 先看到一位一線隊球員就無法比對，或反過來把一位學院球員轉調到一線隊藉此逃脫範圍限制。
        if (!rowScope.Allows(player.TeamId, player.Team.Type))
        {
            throw new AdminForbiddenException("你的角色資料範圍不允許修改這位球員。");
        }

        var team = await ResolveTeamAsync(scope, request.TeamId, cancellationToken);

        if (!rowScope.Allows(team.Id, team.Type))
        {
            throw new AdminForbiddenException("你的角色資料範圍不允許把球員指派到這支球隊。");
        }

        if (request.Slug is { } newSlug && newSlug != player.Slug)
        {
            PlayerSlug.Validate(newSlug);
            await EnsureSlugAvailableAsync(scope, newSlug, player.Id, cancellationToken);
            player.Slug = newSlug;
        }

        player.TeamId = team.Id;
        player.ShirtNo = request.ShirtNo;
        player.Position = request.Position;
        player.BirthOn = request.BirthOn;
        player.HeightCm = request.HeightCm;
        player.WeightKg = request.WeightKg;
        player.Nationality = request.Nationality;
        player.PreferredFoot = request.PreferredFoot;
        player.JoinedOn = request.JoinedOn;
        player.Status = request.Status ?? "active";
        player.PortraitConsentStatus = request.PortraitConsentStatus ?? "not_consented";
        player.UpdatedAt = DateTime.UtcNow;
        player.UpdatedBy = operatorId;

        if (photoUpdate.Change)
        {
            player.PhotoKey = photoUpdate.NewKey;
            player.PhotoWidth = photoUpdate.NewKey is null ? null : photoUpdate.Width;
            player.PhotoHeight = photoUpdate.NewKey is null ? null : photoUpdate.Height;
        }

        AddOrReplaceI18n(player, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = player.PlayersI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(player, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("players", scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>同一俱樂部內網址代稱不可重複（<c>UQ_players_club_slug</c> 是最後防線，這裡先查好回日常中文 409）。</summary>
    private async Task EnsureSlugAvailableAsync(AdminClubScope scope, string slug, Guid? exceptPlayerId, CancellationToken cancellationToken)
    {
        var taken = await dbContext.Players.AsNoTracking()
            .AnyAsync(p => p.ClubId == scope.ClubId && p.Slug == slug && p.Id != exceptPlayerId, cancellationToken);
        if (taken)
        {
            throw new AdminPlayerSlugConflictException($"網址代稱「{slug}」已經有另一位球員在使用，請換一個。");
        }
    }

    private async Task<string> MakeUniqueSlugAsync(AdminClubScope scope, string baseSlug, CancellationToken cancellationToken)
    {
        var candidate = baseSlug;
        for (var n = 2; await dbContext.Players.AsNoTracking().AnyAsync(p => p.ClubId == scope.ClubId && p.Slug == candidate, cancellationToken); n++)
        {
            var suffix = $"-{n}";
            candidate = PlayerSlug.Truncate(baseSlug[..Math.Min(baseSlug.Length, PlayerSlug.MaxLength - suffix.Length)].TrimEnd('-')) + suffix;
        }

        return candidate;
    }

    /// <summary>🔴 跨俱樂部指派球隊在這裡擋下——<paramref name="teamId"/> 必須屬於
    /// <paramref name="scope"/>.ClubId，否則球員的 <c>club_id</c>（跟著俱樂部路由填）與
    /// <c>team_id</c> 指到的球隊俱樂部會互相矛盾。</summary>
    private async Task<Team> ResolveTeamAsync(AdminClubScope scope, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (team is null || team.ClubId != scope.ClubId)
        {
            throw new AdminPlayerValidationException("找不到這個俱樂部的球隊，請重新整理後再試一次。", "teamId");
        }
        return team;
    }

    private void AddOrReplaceI18n(Player player, string locale, AdminPlayerLocaleContent content)
    {
        var existing = player.PlayersI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new PlayersI18n { PlayerId = player.Id, Locale = locale };
            player.PlayersI18ns.Add(existing);
            dbContext.PlayersI18ns.Add(existing);
        }

        existing.Name = content.Name;
        existing.Bio = content.Bio;
        existing.PhotoAlt = string.IsNullOrWhiteSpace(content.PhotoAlt) ? null : content.PhotoAlt.Trim();
    }

    private static void ValidateStatus(string? status)
    {
        if (status is not null && !AllowedStatuses.Contains(status))
        {
            throw new AdminPlayerValidationException(
                "狀態只能選「現役」「離隊」「外借」或「海外發展」。", "status");
        }
    }

    private static void ValidatePortraitConsentStatus(string? portraitConsentStatus)
    {
        if (portraitConsentStatus is not null && !AllowedPortraitConsentStatuses.Contains(portraitConsentStatus))
        {
            throw new AdminPlayerValidationException(
                "肖像同意狀態只能選「未同意」「本人已同意」或「監護人已同意」。", "portraitConsentStatus");
        }
    }

    private static void ValidateContent(AdminPlayerContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Name))
        {
            throw new AdminPlayerValidationException("中文姓名為必填欄位。", "nameZh");
        }

        if ((content.Zh.PhotoAlt?.Trim().Length ?? 0) > 200)
        {
            throw new AdminPlayerValidationException("照片替代文字（中文）不可超過 200 字。", "photoAltZh");
        }

        if ((content.En?.PhotoAlt?.Trim().Length ?? 0) > 200)
        {
            throw new AdminPlayerValidationException("照片替代文字（英文）不可超過 200 字。", "photoAltEn");
        }
    }

    private static void ValidateNumericRanges(int? shirtNo, int? heightCm, int? weightKg)
    {
        if (shirtNo is < 1 or > 99)
        {
            throw new AdminPlayerValidationException("背號只能是 1 到 99 之間的整數。", "shirtNo");
        }
        if (heightCm is < 100 or > 250)
        {
            throw new AdminPlayerValidationException("身高數值不合理，請確認單位為公分。", "heightCm");
        }
        if (weightKg is < 30 or > 150)
        {
            throw new AdminPlayerValidationException("體重數值不合理，請確認單位為公斤。", "weightKg");
        }
    }

    // ═══════════════════════════ 賽季數據（B-13）═══════════════════════════
    // 🔴 player_season_stats 沒有 source 欄位：「這一列存在」就是手動數據（公開端 StandingsRepository 以此判斷 source=manual），
    // 「清除」＝刪除這一列，公開端自動回到賽事彙總。

    private async Task<Player?> LoadPlayerForStatsAsync(AdminClubScope scope, Guid playerId, CancellationToken cancellationToken)
        => await dbContext.Players.Include(p => p.Team).FirstOrDefaultAsync(p => p.Id == playerId && p.ClubId == scope.ClubId, cancellationToken);

    public async Task<AdminPlayerSeasonStatsDto?> GetSeasonStatsAsync(AdminClubScope scope, Guid playerId, CancellationToken cancellationToken)
    {
        var player = await dbContext.Players.AsNoTracking().FirstOrDefaultAsync(p => p.Id == playerId && p.ClubId == scope.ClubId, cancellationToken);
        if (player is null)
        {
            return null;
        }

        var seasons = await dbContext.Seasons.AsNoTracking().Where(s => s.ClubId == scope.ClubId).OrderByDescending(s => s.StartOn).ToListAsync(cancellationToken);
        var manual = await dbContext.PlayerSeasonStats.AsNoTracking().Where(m => m.PlayerId == playerId).ToDictionaryAsync(m => m.SeasonId, cancellationToken);
        var items = new List<AdminPlayerSeasonStatDto>();
        foreach (var season in seasons)
        {
            items.Add(await BuildSeasonStatAsync(scope, playerId, season, manual.GetValueOrDefault(season.Id), cancellationToken));
        }

        return new AdminPlayerSeasonStatsDto { PlayerId = playerId, Items = items };
    }

    private async Task<AdminPlayerSeasonStatDto> BuildSeasonStatAsync(
        AdminClubScope scope, Guid playerId, Season season, PlayerSeasonStat? manual, CancellationToken cancellationToken)
    {
        var auto = (await StandingsRepository.AutoTotalsAsync(dbContext, scope.ClubId, season.Id, playerId, cancellationToken)).GetValueOrDefault(playerId);
        return new AdminPlayerSeasonStatDto
        {
            SeasonId = season.Id, SeasonCode = season.Code, StartOn = season.StartOn, EndOn = season.EndOn,
            Source = manual is not null ? "manual" : auto is not null ? "auto" : "none",
            Manual = manual is null ? null : new AdminPlayerSeasonStatValues
            {
                Appearances = manual.Appearances, Goals = manual.Goals, Assists = manual.Assists, YellowCards = manual.YellowCards, RedCards = manual.RedCards,
            },
            Auto = auto is null ? null : new AdminPlayerSeasonStatValues
            {
                Appearances = auto.Appearances, Goals = auto.Goals, Assists = null, YellowCards = auto.Yellow, RedCards = auto.Red,
            },
        };
    }

    public async Task<AdminPlayerSeasonStatDto?> SetSeasonStatAsync(
        AdminClubScope scope, TeamRowScope rowScope, Guid playerId, Guid seasonId, SetAdminPlayerSeasonStatRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var player = await LoadPlayerForStatsAsync(scope, playerId, cancellationToken);
        var season = await dbContext.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == seasonId && s.ClubId == scope.ClubId, cancellationToken);
        if (player is null || season is null)
        {
            return null;
        }

        if (!rowScope.Allows(player.TeamId, player.Team.Type))
        {
            throw new AdminForbiddenException("你的角色資料範圍不允許修改這位球員。");
        }

        ValidateStat(request.Appearances, "出賽場次", "appearances");
        ValidateStat(request.Goals, "進球", "goals");
        ValidateStat(request.Assists, "助攻", "assists");
        ValidateStat(request.YellowCards, "黃牌", "yellowCards");
        ValidateStat(request.RedCards, "紅牌", "redCards");

        var now = DateTime.UtcNow;
        var row = await dbContext.PlayerSeasonStats.FirstOrDefaultAsync(m => m.PlayerId == playerId && m.SeasonId == seasonId, cancellationToken);
        if (row is null)
        {
            row = new PlayerSeasonStat { Id = Guid.NewGuid(), PlayerId = playerId, SeasonId = seasonId, CreatedAt = now, CreatedBy = operatorId };
            dbContext.PlayerSeasonStats.Add(row);
        }

        row.Appearances = request.Appearances;
        row.Goals = request.Goals;
        row.Assists = request.Assists;
        row.YellowCards = request.YellowCards;
        row.RedCards = request.RedCards;
        row.UpdatedAt = now;
        row.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildSeasonStatAsync(scope, playerId, season, row, cancellationToken);
    }

    /// <returns><c>null</c>＝找不到球員或賽季（404）；<c>false</c>＝這個賽季本來就沒有手動數據；<c>true</c>＝已清除。</returns>
    public async Task<bool?> ClearSeasonStatAsync(
        AdminClubScope scope, TeamRowScope rowScope, Guid playerId, Guid seasonId, CancellationToken cancellationToken)
    {
        var player = await LoadPlayerForStatsAsync(scope, playerId, cancellationToken);
        var seasonExists = await dbContext.Seasons.AsNoTracking().AnyAsync(s => s.Id == seasonId && s.ClubId == scope.ClubId, cancellationToken);
        if (player is null || !seasonExists)
        {
            return null;
        }

        if (!rowScope.Allows(player.TeamId, player.Team.Type))
        {
            throw new AdminForbiddenException("你的角色資料範圍不允許修改這位球員。");
        }

        var row = await dbContext.PlayerSeasonStats.FirstOrDefaultAsync(m => m.PlayerId == playerId && m.SeasonId == seasonId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        dbContext.PlayerSeasonStats.Remove(row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateStat(int value, string label, string field)
    {
        if (value is < 0 or > 9999)
        {
            throw new AdminPlayerValidationException($"{label}必須是 0 到 9999 之間的整數。", field);
        }
    }

    private AdminPlayerDetailDto ToDetailDto(Player player)
    {
        var zh = player.PlayersI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = player.PlayersI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminPlayerDetailDto
        {
            Id = player.Id,
            TeamId = player.TeamId,
            TeamCode = player.Team.Code,
            Slug = player.Slug,
            ShirtNo = player.ShirtNo,
            Position = player.Position,
            BirthOn = player.BirthOn,
            HeightCm = player.HeightCm,
            WeightKg = player.WeightKg,
            Nationality = player.Nationality,
            PreferredFoot = player.PreferredFoot,
            JoinedOn = player.JoinedOn,
            Status = player.Status,
            PhotoKey = player.PhotoKey,
            PhotoUrl = imageUrls.Resolve(player.PhotoKey),
            PhotoThumbUrl = imageUrls.ResolveThumbnail(player.PhotoKey),
            PhotoWidth = player.PhotoKey is null ? null : player.PhotoWidth,
            PhotoHeight = player.PhotoKey is null ? null : player.PhotoHeight,
            PortraitConsentStatus = player.PortraitConsentStatus,
            Zh = new AdminPlayerLocaleContent { Name = zh?.Name ?? "", Bio = zh?.Bio, PhotoAlt = zh?.PhotoAlt },
            En = en is null ? null : new AdminPlayerLocaleContent { Name = en.Name ?? "", Bio = en.Bio, PhotoAlt = en.PhotoAlt },
            CreatedAt = player.CreatedAt,
            UpdatedAt = player.UpdatedAt,
        };
    }
}
