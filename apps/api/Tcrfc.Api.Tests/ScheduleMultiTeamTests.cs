using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Schedule;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 公開賽程列表：賽事可複選本方球隊（<c>match_teams</c>），一場掛兩隊的賽事在不帶 <c>team</c> 篩選時只能出現一列、
/// <c>TotalCount</c> 也只算一次（稽核 F 類：原本 JOIN <c>match_teams</c> 會每隊各列一次）。
/// 測資自己插、測完自己刪，不寫進 <c>db/seed</c>。用藍鯨：本機種子裡只有藍鯨有兩支以上的球隊。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ScheduleMultiTeamTests(ApiFixture fixture)
{
    [Fact]
    public async Task 多隊賽事_無球隊篩選只列一次_有球隊篩選每隊各一列_總數不重複計算()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

        Guid clubId, seasonId;
        List<(Guid Id, string Code)> teams = [];
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            clubId = await ScalarAsync<Guid>(connection, "SELECT id FROM clubs WHERE code = 'bw';");
            seasonId = await ScalarAsync<Guid>(connection, "SELECT TOP 1 id FROM seasons WHERE club_id = @C ORDER BY start_on DESC;", ("@C", clubId));
            await using var command = new SqlCommand("SELECT TOP 2 id, code FROM teams WHERE club_id = @C ORDER BY sort_order, code;", connection);
            command.Parameters.AddWithValue("@C", clubId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                teams.Add((reader.GetGuid(0), reader.GetString(1)));
            }
        }

        Assert.Equal(2, teams.Count);
        var matchId = Guid.NewGuid();
        var matchOn = new DateTime(2027, 5, 30);

        try
        {
            await ExecuteAsync(connectionString,
                "INSERT INTO matches (id, club_id, season_id, match_on, kickoff, status) VALUES (@Id, @C, @S, @On, N'15:00', N'scheduled');",
                ("@Id", matchId), ("@C", clubId), ("@S", seasonId), ("@On", matchOn));
            foreach (var team in teams)
            {
                await ExecuteAsync(connectionString, "INSERT INTO match_teams (match_id, team_id) VALUES (@M, @T);", ("@M", matchId), ("@T", team.Id));
            }

            using var client = fixture.CreateClient();

            // 不帶 team：用「該日期區間」縮小到只有這一場，總數必須是 1、且只有一列。
            var all = await client.GetFromJsonAsync<PagedResult<MatchDto>>(
                "/api/v1/bw/schedule?from=2027-05-30&to=2027-05-30&pageSize=91", TestJson.Options);
            Assert.NotNull(all);
            Assert.Single(all!.Items, m => m.Id == matchId);
            Assert.Equal(all.Items.Count, all.TotalCount);

            // 帶 team：兩隊各自查得到這場，且各只有一列。
            foreach (var team in teams)
            {
                var byTeam = await client.GetFromJsonAsync<PagedResult<MatchDto>>(
                    $"/api/v1/bw/schedule?team={team.Code}&from=2027-05-30&to=2027-05-30&pageSize=92", TestJson.Options);
                var item = Assert.Single(byTeam!.Items, m => m.Id == matchId);
                Assert.Equal(team.Code, item.TeamCode);
                Assert.Equal(byTeam.Items.Count, byTeam.TotalCount);
            }
        }
        finally
        {
            await ExecuteAsync(connectionString, "DELETE FROM match_teams WHERE match_id = @M;", ("@M", matchId));
            await ExecuteAsync(connectionString, "DELETE FROM matches WHERE id = @M;", ("@M", matchId));
        }
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)Convert.ChangeType(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException(sql), typeof(T));
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
