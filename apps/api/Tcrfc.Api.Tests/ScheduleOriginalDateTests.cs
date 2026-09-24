using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Schedule;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S0-9l 後端半段：<c>matches.original_match_on</c>／<c>original_kickoff</c>（v3.13，延賽須標示原定時間）
/// 要出現在公開賽程 API 回應中。
///
/// ⚠️ 種子資料目前沒有任何 <c>status = 'postponed'</c> 的真實延賽紀錄（STATUS.md S0-9l 記錄在案），
/// 所以這裡的兩筆測資（一筆延賽、一筆正常）是這個測試自己直接寫 SQL 插入、測完自己刪乾淨，
/// 不寫進 <c>db/seed</c>——**這個功能只用人造資料驗過，沒有真實延賽資料可以核對**。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ScheduleOriginalDateTests(ApiFixture fixture)
{
    [Fact]
    public async Task 延賽的比賽回傳原定日期時間_正常比賽兩欄皆為null()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

        var (clubId, seasonId, teamId) = await LookupTcrfcSeasonTeamAsync(connectionString);

        var postponedMatchId = Guid.NewGuid();
        var scheduledMatchId = Guid.NewGuid();

        // 原定 2026-10-03 14:00 延到 2026-10-17 16:00。
        var originalMatchOn = new DateOnly(2026, 10, 3);
        const string originalKickoff = "14:00";
        var newMatchOn = new DateOnly(2026, 10, 17);

        try
        {
            await InsertMatchAsync(
                connectionString, postponedMatchId, clubId, seasonId, teamId,
                matchOn: newMatchOn, kickoff: "16:00", status: "postponed",
                originalMatchOn: originalMatchOn, originalKickoff: originalKickoff);

            await InsertMatchAsync(
                connectionString, scheduledMatchId, clubId, seasonId, teamId,
                matchOn: new DateOnly(2026, 10, 24), kickoff: "16:00", status: "scheduled",
                originalMatchOn: null, originalKickoff: null);

            using var client = fixture.CreateClient();
            var schedule = await client.GetFromJsonAsync<PagedResult<MatchDto>>(
                "/api/v1/tcrfc/schedule?team=D1&season=2026-27&pageSize=100", TestJson.Options);
            Assert.NotNull(schedule);

            var postponed = schedule!.Items.SingleOrDefault(m => m.Id == postponedMatchId);
            Assert.True(postponed is not null, "延賽測資應出現在賽程回應中");
            Assert.Equal(originalMatchOn, postponed!.OriginalMatchOn);
            Assert.Equal(originalKickoff, postponed.OriginalKickoff);

            var scheduled = schedule.Items.SingleOrDefault(m => m.Id == scheduledMatchId);
            Assert.True(scheduled is not null, "正常測資應出現在賽程回應中");
            Assert.Null(scheduled!.OriginalMatchOn);
            Assert.Null(scheduled.OriginalKickoff);
        }
        finally
        {
            await ExecuteAsync(connectionString, "DELETE FROM match_teams WHERE match_id IN (@A, @B);",
                ("@A", postponedMatchId), ("@B", scheduledMatchId));
            await ExecuteAsync(connectionString, "DELETE FROM matches WHERE id IN (@A, @B);",
                ("@A", postponedMatchId), ("@B", scheduledMatchId));
        }
    }

    private static async Task<(Guid ClubId, Guid SeasonId, Guid TeamId)> LookupTcrfcSeasonTeamAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var clubId = await ScalarAsync<Guid>(connection, "SELECT id FROM clubs WHERE code = 'tcrfc';");
        var seasonId = await ScalarAsync<Guid>(connection,
            "SELECT id FROM seasons WHERE club_id = @ClubId AND code = '2026-27';", ("@ClubId", clubId));
        var teamId = await ScalarAsync<Guid>(connection,
            "SELECT id FROM teams WHERE club_id = @ClubId AND code = 'D1';", ("@ClubId", clubId));

        return (clubId, seasonId, teamId);
    }

    private static async Task InsertMatchAsync(
        string connectionString, Guid matchId, Guid clubId, Guid seasonId, Guid teamId,
        DateOnly matchOn, string kickoff, string status, DateOnly? originalMatchOn, string? originalKickoff)
    {
        await ExecuteAsync(connectionString, """
            INSERT INTO matches (id, club_id, season_id, match_on, kickoff, status, original_match_on, original_kickoff)
            VALUES (@Id, @ClubId, @SeasonId, @MatchOn, @Kickoff, @Status, @OriginalMatchOn, @OriginalKickoff);
            """,
            ("@Id", matchId), ("@ClubId", clubId), ("@SeasonId", seasonId), ("@MatchOn", matchOn.ToDateTime(TimeOnly.MinValue)),
            ("@Kickoff", kickoff), ("@Status", status),
            ("@OriginalMatchOn", (object?)originalMatchOn?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value),
            ("@OriginalKickoff", (object?)originalKickoff ?? DBNull.Value));

        await ExecuteAsync(connectionString, """
            INSERT INTO match_teams (match_id, team_id) VALUES (@MatchId, @TeamId);
            """,
            ("@MatchId", matchId), ("@TeamId", teamId));
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
    }

    private static async Task ExecuteAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
