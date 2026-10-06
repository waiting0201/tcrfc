using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Honors;

/// <summary>公開讀取：C5 榮譽與里程碑（前台 02 關於）。兩張表 <c>club_id</c> 皆必填。快取 entity <c>honors</c>。</summary>
public sealed class HonorsRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache, IImagePublicUrlResolver imageUrls)
{
    private sealed record AchievementRow(Guid Id, int? Year, string? SeasonCode, string TeamCode, string? TeamName, string? CompetitionName, string? Placing);
    private sealed record MilestoneRow(
        Guid Id, DateTime HappenedOn, string? ImageKey, int? ImageWidth, int? ImageHeight, string? Title, string? Description, string? ImageAlt);

    public async Task<IReadOnlyList<AchievementDto>> ListAchievementsAsync(ClubScope scope, string? teamCode, string dbLocale, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync("honors", scope.ClubCode, dbLocale, $"achievements:{teamCode}", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            const string sql = """
                SELECT a.id AS Id, a.year AS Year, s.code AS SeasonCode, t.code AS TeamCode,
                       COALESCE(NULLIF(tr.name, N''), td.name) AS TeamName,
                       COALESCE(NULLIF(ar.competition_name, N''), ad.competition_name) AS CompetitionName,
                       COALESCE(NULLIF(ar.placing, N''), ad.placing) AS Placing
                FROM achievements a
                JOIN seasons s ON s.id = a.season_id
                JOIN teams t ON t.id = a.team_id
                LEFT JOIN teams_i18n tr ON tr.team_id = t.id AND tr.locale = @Locale
                LEFT JOIN teams_i18n td ON td.team_id = t.id AND td.locale = @DefaultLocale
                LEFT JOIN achievements_i18n ar ON ar.achievement_id = a.id AND ar.locale = @Locale
                LEFT JOIN achievements_i18n ad ON ad.achievement_id = a.id AND ad.locale = @DefaultLocale
                WHERE a.club_id = @ClubId AND (@TeamCode IS NULL OR t.code = @TeamCode)
                ORDER BY a.year DESC, a.row_seq DESC
                """;
            var rows = await connection.QueryAsync<AchievementRow>(new CommandDefinition(
                sql, new { scope.ClubId, TeamCode = teamCode, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: ct));
            return (IReadOnlyList<AchievementDto>)rows.Select(r => new AchievementDto
            {
                Id = r.Id, Year = r.Year, SeasonCode = r.SeasonCode, TeamCode = r.TeamCode, TeamName = r.TeamName,
                CompetitionName = r.CompetitionName, Placing = r.Placing,
            }).ToList();
        }, cancellationToken);

    public async Task<IReadOnlyList<MilestoneDto>> ListMilestonesAsync(ClubScope scope, string dbLocale, CancellationToken cancellationToken)
        => await cache.GetOrCreateAsync("honors", scope.ClubCode, dbLocale, "milestones", async ct =>
        {
            using var connection = connectionFactory.CreateConnection();
            const string sql = """
                SELECT m.id AS Id, m.happened_on AS HappenedOn, m.image_key AS ImageKey, m.image_width AS ImageWidth, m.image_height AS ImageHeight,
                       COALESCE(NULLIF(r.title, N''), d.title) AS Title, COALESCE(NULLIF(r.description, N''), d.description) AS Description,
                       COALESCE(NULLIF(r.image_alt, N''), d.image_alt) AS ImageAlt
                FROM milestones m
                LEFT JOIN milestones_i18n r ON r.milestone_id = m.id AND r.locale = @Locale
                LEFT JOIN milestones_i18n d ON d.milestone_id = m.id AND d.locale = @DefaultLocale
                WHERE m.club_id = @ClubId AND m.is_visible = 1
                ORDER BY m.happened_on, m.sort_order, m.row_seq
                """;
            var rows = await connection.QueryAsync<MilestoneRow>(new CommandDefinition(
                sql, new { scope.ClubId, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: ct));
            return (IReadOnlyList<MilestoneDto>)rows.Select(r => new MilestoneDto
            {
                Id = r.Id, HappenedOn = DateOnly.FromDateTime(r.HappenedOn), Title = r.Title, Description = r.Description,
                ImageUrl = imageUrls.Resolve(r.ImageKey), ImageAlt = r.ImageAlt, ImageWidth = r.ImageWidth, ImageHeight = r.ImageHeight,
            }).ToList();
        }, cancellationToken);
}
