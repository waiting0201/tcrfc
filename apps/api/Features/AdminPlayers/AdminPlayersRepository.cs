using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPlayers;

/// <summary>C2「球員」——俱樂部範圍 CRUD（主站規劃書 §4.3 C2）。
/// 🔴 <c>IQueryCache</c> 只為了寫入後失效——公開唯讀端點 <c>Features/Players/PlayersEndpoints.cs</c>
/// （entity="players"）已接快取，寫入這裡不失效會讓公開頁面在 TTL 到期前顯示舊資料，
/// 逐字比照 <c>AdminArticlesRepository.InvalidatePublicCacheAsync</c> 的既有做法。</summary>
public sealed class AdminPlayersRepository(ClubDbContext dbContext, IQueryCache cache)
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
                p.ShirtNo,
                p.Position,
                p.BirthOn,
                p.Status,
                p.PhotoKey,
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
            ShirtNo = r.ShirtNo,
            Position = r.Position,
            BirthOn = r.BirthOn,
            Status = r.Status,
            PhotoKey = r.PhotoKey,
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
        AdminClubScope scope, TeamRowScope rowScope, Guid playerId, CreateAdminPlayerRequest request, string? photoKey, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateStatus(request.Status);
        ValidatePortraitConsentStatus(request.PortraitConsentStatus);
        ValidateContent(request.Content);
        ValidateNumericRanges(request.ShirtNo, request.HeightCm, request.WeightKg);
        var team = await ResolveTeamAsync(scope, request.TeamId, cancellationToken);

        // 🔴 S1-8 新增：列級授權——球員必屬於某支球隊（team_id 必填），直接用該球隊檢查即可。
        if (!rowScope.Allows(team.Id, team.Type))
        {
            throw new AdminForbiddenException("你的球隊授權範圍不允許在這支球隊底下建立球員。");
        }

        var now = DateTime.UtcNow;
        var player = new Player
        {
            Id = playerId,
            ClubId = scope.ClubId,
            TeamId = team.Id,
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
            throw new AdminForbiddenException("你的球隊授權範圍不允許修改這位球員。");
        }

        var team = await ResolveTeamAsync(scope, request.TeamId, cancellationToken);

        if (!rowScope.Allows(team.Id, team.Type))
        {
            throw new AdminForbiddenException("你的球隊授權範圍不允許把球員指派到這支球隊。");
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

    /// <summary>🔴 跨俱樂部指派球隊在這裡擋下——<paramref name="teamId"/> 必須屬於
    /// <paramref name="scope"/>.ClubId，否則球員的 <c>club_id</c>（跟著俱樂部路由填）與
    /// <c>team_id</c> 指到的球隊俱樂部會互相矛盾。</summary>
    private async Task<Team> ResolveTeamAsync(AdminClubScope scope, Guid teamId, CancellationToken cancellationToken)
    {
        var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (team is null || team.ClubId != scope.ClubId)
        {
            throw new AdminPlayerValidationException($"找不到這個俱樂部的球隊（id={teamId}）。");
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
    }

    private static void ValidateStatus(string? status)
    {
        if (status is not null && !AllowedStatuses.Contains(status))
        {
            throw new AdminPlayerValidationException(
                "狀態只能是「active」（現役）、「departed」（離隊）、「loan」（外借）或「overseas」（海外發展）。");
        }
    }

    private static void ValidatePortraitConsentStatus(string? portraitConsentStatus)
    {
        if (portraitConsentStatus is not null && !AllowedPortraitConsentStatuses.Contains(portraitConsentStatus))
        {
            throw new AdminPlayerValidationException(
                "肖像同意狀態只能是「not_consented」（未同意）、「consented」（本人已同意）或" +
                "「consented_by_guardian」（監護人已同意）。");
        }
    }

    private static void ValidateContent(AdminPlayerContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Name))
        {
            throw new AdminPlayerValidationException("中文姓名為必填欄位。");
        }
    }

    private static void ValidateNumericRanges(int? shirtNo, int? heightCm, int? weightKg)
    {
        if (shirtNo is < 1 or > 99)
        {
            throw new AdminPlayerValidationException("背號只能是 1 到 99 之間的整數。");
        }
        if (heightCm is < 100 or > 250)
        {
            throw new AdminPlayerValidationException("身高數值不合理，請確認單位為公分。");
        }
        if (weightKg is < 30 or > 150)
        {
            throw new AdminPlayerValidationException("體重數值不合理，請確認單位為公斤。");
        }
    }

    private static AdminPlayerDetailDto ToDetailDto(Player player)
    {
        var zh = player.PlayersI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = player.PlayersI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminPlayerDetailDto
        {
            Id = player.Id,
            TeamId = player.TeamId,
            TeamCode = player.Team.Code,
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
            PortraitConsentStatus = player.PortraitConsentStatus,
            Zh = new AdminPlayerLocaleContent { Name = zh?.Name ?? "", Bio = zh?.Bio },
            En = en is null ? null : new AdminPlayerLocaleContent { Name = en.Name ?? "", Bio = en.Bio },
            CreatedAt = player.CreatedAt,
            UpdatedAt = player.UpdatedAt,
        };
    }
}
