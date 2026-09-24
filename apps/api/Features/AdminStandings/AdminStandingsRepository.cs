using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminStandings;

/// <summary>
/// C4「積分榜」——俱樂部範圍 CRUD（主站規劃書 §4.3 C4：「積分榜：手動維護表格或匯入 CSV」）。
///
/// 🔴 **為什麼不套用 <see cref="TeamRowScope"/> 列級授權**：`standings` 表只有
/// <c>(club_id, season_id, team_name, rank, played, points)</c>，`team_name` 是自由文字
/// （docs/12-database-schema.md §12 第 24 點「`Team` 只放本會四隊，積分榜其餘球隊是
/// `team_name` 字串」），沒有任何欄位指向本方 `teams.id`。列級授權需要知道「這一列屬於哪支
/// 本方球隊」才能判斷 `academy_only`／`own_teams` 准不准碰，這張表的結構完全無法回答這個問題
/// ——一份積分榜代表整個聯賽的排名表（本方與對手的名次同列並陳），不是「本方某支球隊的積分」。
/// 實務上目前只有一線隊（D1）參加有正式積分榜的聯賽（企業甲組），學院梯隊的友誼賽事沒有積分榜
/// 需求（規劃書 3.1「Results & Standings」只出現在 FOOTBALL CLUB／一線隊頁面），因此本輪判斷
/// 這張表暫時只開放給**不受列級限制**的角色（<c>role_permissions.scope_type = 'all'</c>）——
/// 沒有把 `team.standing.*` 指派給 `academy_program`（見 db/seed/generate-club-seed-sql.py
/// 的角色指派說明），不是遺漏，是「這張表結構上答不出『這屬於哪支學院梯隊』，寧可不開放也不要
/// 開放了卻擋不住」的保守決定。**若日後真的要讓學院管理者也維護學院賽事的積分榜，必須先讓
/// `standings` 表加上可為空的 `team_id` 外鍵**——這是本次回報的綱要缺口之一，不在本輪自行加欄位。
///
/// 🔴 <c>IQueryCache</c>：積分榜目前**沒有對應的公開讀取端點**（`Features/Schedule` 只讀
/// `matches`），故本檔不呼叫 <c>IQueryCache.InvalidateAsync</c>——沒有快取需要失效。若之後依
/// §3.13／首頁區塊需求新增公開積分榜端點，屆時要比照既有模式補上快取失效，見任務回報「公開唯讀」
/// 段的說明。
/// </summary>
public sealed class AdminStandingsRepository(ClubDbContext dbContext)
{
    public async Task<IReadOnlyList<AdminStandingListItemDto>> ListAsync(
        AdminClubScope scope, Guid? seasonId, CancellationToken cancellationToken)
    {
        var query = dbContext.Standings.AsNoTracking().Where(s => s.ClubId == scope.ClubId);
        if (seasonId is Guid sid)
        {
            query = query.Where(s => s.SeasonId == sid);
        }

        var rows = await query
            .OrderBy(s => s.SeasonId).ThenBy(s => s.Rank)
            .Select(s => new
            {
                s.Id,
                s.SeasonId,
                SeasonCode = s.Season.Code,
                s.TeamName,
                s.Rank,
                s.Played,
                s.Points,
                s.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminStandingListItemDto
        {
            Id = r.Id,
            SeasonId = r.SeasonId,
            SeasonCode = r.SeasonCode,
            TeamName = r.TeamName,
            Rank = r.Rank,
            Played = r.Played,
            Points = r.Points,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminStandingDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var standing = await dbContext.Standings.AsNoTracking()
            .Include(s => s.Season)
            .FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);

        return standing is null ? null : ToDetailDto(standing);
    }

    public async Task<AdminStandingDetailDto> CreateAsync(
        AdminClubScope scope, CreateAdminStandingRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateTeamName(request.TeamName);
        var season = await ResolveSeasonAsync(scope, request.SeasonId, cancellationToken);

        var now = DateTime.UtcNow;
        var standing = new Standing
        {
            Id = Guid.NewGuid(),
            ClubId = scope.ClubId,
            SeasonId = season.Id,
            TeamName = request.TeamName,
            Rank = request.Rank,
            Played = request.Played,
            Points = request.Points,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Standings.Add(standing);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(scope, standing.Id, cancellationToken))!;
    }

    public async Task<AdminStandingDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminStandingRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateTeamName(request.TeamName);

        var standing = await dbContext.Standings.FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);
        if (standing is null)
        {
            return null;
        }

        var season = await ResolveSeasonAsync(scope, request.SeasonId, cancellationToken);

        standing.SeasonId = season.Id;
        standing.TeamName = request.TeamName;
        standing.Rank = request.Rank;
        standing.Played = request.Played;
        standing.Points = request.Points;
        standing.UpdatedAt = DateTime.UtcNow;
        standing.UpdatedBy = operatorId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var standing = await dbContext.Standings.FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);
        if (standing is null)
        {
            return false;
        }

        dbContext.Standings.Remove(standing);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ───────────────────────────── CSV 批次匯入（整季替換） ─────────────────────────────

    private static readonly string[] CsvHeader = ["賽季代碼", "名次", "球隊名稱", "出賽場次", "積分"];

    /// <summary>
    /// CSV 格式（UTF-8 BOM，中文表頭）：<c>賽季代碼,名次,球隊名稱,出賽場次,積分</c>。**一份檔案只能
    /// 包含同一個賽季的資料**（表頭下每一列的賽季代碼都必須相同）——積分榜是「這個賽季的一整張
    /// 排名表」，混雜多個賽季在同一份匯入操作裡沒有實際使用情境，也會讓「整季替換」語意複雜化。
    ///
    /// 🔴 **這是「整季替換」，不是逐列 upsert**：積分榜每週滾動更新，最常見的維護方式是把官方
    /// 聯賽網站的最新排名表整份複製貼上重新匯入（名次每週都在變動，沒有「這一列對應官網哪一列」
    /// 的穩定自然鍵，`team_name` 本身也可能因排版差異而對不齊）——因此策略是**先刪除這個賽季的
    /// 全部既有列，再整批寫入新檔案的內容**，確保匯入後的資料庫狀態跟這份檔案完全一致（不會有
    /// 「球隊掉出聯賽後舊列還留著」這種殘留資料）。**整批驗證通過才刪除＋寫入**（同一個交易），
    /// 驗證失敗時既有資料完全不受影響。
    /// </summary>
    public async Task<StandingCsvImportResultDto> ImportCsvAsync(
        AdminClubScope scope, string csvContent, Guid? operatorId, CancellationToken cancellationToken)
    {
        var rows = CsvUtils.Parse(csvContent);
        if (rows.Count == 0)
        {
            throw new AdminStandingValidationException("檔案是空的，找不到任何資料列。");
        }

        var header = rows[0];
        if (header.Count != CsvHeader.Length || !header.SequenceEqual(CsvHeader, StringComparer.Ordinal))
        {
            throw new AdminStandingValidationException($"檔案格式不正確，表頭必須依序是「{string.Join("、", CsvHeader)}」。");
        }

        var seasonByCode = await dbContext.Seasons.AsNoTracking()
            .Where(s => s.ClubId == scope.ClubId)
            .ToDictionaryAsync(s => s.Code, StringComparer.Ordinal, cancellationToken);

        var errors = new List<StandingCsvImportRowErrorDto>();
        var parsedRows = new List<(Season Season, string TeamName, int? Rank, int? Played, int? Points)>();
        Season? fileSeason = null;

        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var row = rows[i];

            if (row.Count != CsvHeader.Length)
            {
                errors.Add(new StandingCsvImportRowErrorDto { RowNumber = rowNumber, Reason = $"欄位數不正確，應為 {CsvHeader.Length} 欄，實際 {row.Count} 欄。" });
                continue;
            }

            var seasonCode = row[0].Trim();
            var rankText = row[1].Trim();
            var teamName = row[2].Trim();
            var playedText = row[3].Trim();
            var pointsText = row[4].Trim();

            var rowErrors = new List<string>();

            if (!seasonByCode.TryGetValue(seasonCode, out var season))
            {
                rowErrors.Add($"賽季代碼「{seasonCode}」不存在於這個俱樂部。");
            }
            else if (fileSeason is null)
            {
                fileSeason = season;
            }
            else if (fileSeason.Id != season.Id)
            {
                rowErrors.Add("一份檔案只能包含同一個賽季的資料，這一列的賽季代碼跟前面幾列不一致。");
            }

            if (teamName.Length == 0)
            {
                rowErrors.Add("球隊名稱為必填欄位。");
            }

            int? rank = null;
            if (rankText.Length > 0)
            {
                if (!int.TryParse(rankText, out var parsedRank))
                {
                    rowErrors.Add("名次必須是整數。");
                }
                else
                {
                    rank = parsedRank;
                }
            }

            int? played = null;
            if (playedText.Length > 0)
            {
                if (!int.TryParse(playedText, out var parsedPlayed))
                {
                    rowErrors.Add("出賽場次必須是整數。");
                }
                else
                {
                    played = parsedPlayed;
                }
            }

            int? points = null;
            if (pointsText.Length > 0)
            {
                if (!int.TryParse(pointsText, out var parsedPoints))
                {
                    rowErrors.Add("積分必須是整數。");
                }
                else
                {
                    points = parsedPoints;
                }
            }

            if (rowErrors.Count > 0)
            {
                errors.Add(new StandingCsvImportRowErrorDto { RowNumber = rowNumber, Reason = string.Join("；", rowErrors) });
                continue;
            }

            parsedRows.Add((season!, teamName, rank, played, points));
        }

        if (errors.Count == 0 && parsedRows.Count == 0)
        {
            throw new AdminStandingValidationException("檔案沒有任何有效的資料列。");
        }

        if (errors.Count > 0)
        {
            return new StandingCsvImportResultDto { ReplacedCount = 0, DeletedCount = 0, Errors = errors };
        }

        var seasonId = fileSeason!.Id;
        var existing = await dbContext.Standings.Where(s => s.ClubId == scope.ClubId && s.SeasonId == seasonId).ToListAsync(cancellationToken);
        var deletedCount = existing.Count;
        dbContext.Standings.RemoveRange(existing);

        var now = DateTime.UtcNow;
        foreach (var parsed in parsedRows)
        {
            dbContext.Standings.Add(new Standing
            {
                Id = Guid.NewGuid(),
                ClubId = scope.ClubId,
                SeasonId = parsed.Season.Id,
                TeamName = parsed.TeamName,
                Rank = parsed.Rank,
                Played = parsed.Played,
                Points = parsed.Points,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = operatorId,
                UpdatedBy = operatorId,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new StandingCsvImportResultDto { ReplacedCount = parsedRows.Count, DeletedCount = deletedCount, Errors = [] };
    }

    private async Task<Season> ResolveSeasonAsync(AdminClubScope scope, Guid seasonId, CancellationToken cancellationToken)
    {
        var season = await dbContext.Seasons.FirstOrDefaultAsync(s => s.Id == seasonId, cancellationToken);
        if (season is null || season.ClubId != scope.ClubId)
        {
            throw new AdminStandingValidationException($"找不到這個俱樂部的球季（id={seasonId}）。");
        }
        return season;
    }

    private static void ValidateTeamName(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            throw new AdminStandingValidationException("球隊名稱為必填欄位。");
        }
    }

    private static AdminStandingDetailDto ToDetailDto(Standing standing) => new()
    {
        Id = standing.Id,
        SeasonId = standing.SeasonId,
        SeasonCode = standing.Season.Code,
        TeamName = standing.TeamName,
        Rank = standing.Rank,
        Played = standing.Played,
        Points = standing.Points,
        CreatedAt = standing.CreatedAt,
        UpdatedAt = standing.UpdatedAt,
    };
}
