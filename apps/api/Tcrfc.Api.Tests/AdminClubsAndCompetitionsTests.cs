using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminClubs;
using Tcrfc.Api.Features.AdminCompetitions;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// J4：<c>Club</c>（全域）與 <c>Competition</c>（俱樂部範圍）兩個型別的後台維護端點。
/// 打真正的 HTTP 管線與真正的 <c>tcrfc_club_dev</c>。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminClubsAndCompetitionsTests(AdminWriteApiFixture fixture)
{
    // ───────────────────────────── Club（全域） ─────────────────────────────

    [Fact]
    public async Task Club_情境一_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/clubs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Club_情境二_非超管_擋下()
    {
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/clubs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Club_超管_列出既有兩個俱樂部()
    {
        using var client = await CreateSuperAdminClientAsync();
        var clubs = await client.GetFromJsonAsync<List<AdminClubListItemDto>>("/api/v1/admin/clubs", TestJson.Options);
        Assert.NotNull(clubs);
        Assert.Contains(clubs!, c => c.Code == "tcrfc");
        Assert.Contains(clubs!, c => c.Code == "bw");
    }

    [Fact]
    public async Task Club_建立成功_代碼重複回409_網域重複回409_可更新()
    {
        using var client = await CreateSuperAdminClientAsync();
        var code = $"t{Guid.NewGuid():N}"[..12];
        var domain = $"{code}.test.invalid";

        try
        {
            var createResponse = await client.PostAsJsonAsync("/api/v1/admin/clubs", new CreateAdminClubRequest
            {
                Code = code,
                Domain = domain,
                Content = new AdminClubContentInput { Zh = new AdminClubLocaleContent { Name = "測試俱樂部" } },
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminClubDetailDto>(TestJson.Options);

            var codeConflict = await client.PostAsJsonAsync("/api/v1/admin/clubs", new CreateAdminClubRequest
            {
                Code = code,
                Domain = $"{code}-2.test.invalid",
                Content = new AdminClubContentInput { Zh = new AdminClubLocaleContent { Name = "測試俱樂部（代碼重複）" } },
            });
            Assert.Equal(HttpStatusCode.Conflict, codeConflict.StatusCode);

            var domainConflict = await client.PostAsJsonAsync("/api/v1/admin/clubs", new CreateAdminClubRequest
            {
                Code = $"{code}2",
                Domain = domain,
                Content = new AdminClubContentInput { Zh = new AdminClubLocaleContent { Name = "測試俱樂部（網域重複）" } },
            });
            Assert.Equal(HttpStatusCode.Conflict, domainConflict.StatusCode);

            var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/clubs/{created!.Id}", new UpdateAdminClubRequest
            {
                Domain = domain,
                Content = new AdminClubContentInput
                {
                    Zh = new AdminClubLocaleContent { Name = "測試俱樂部（已更新）" },
                    En = new AdminClubLocaleContent { Name = "Test Club (Updated)" },
                },
                IsCollectingSubject = false,
                DefaultLocale = "zh-Hant",
                SortOrder = 99,
            });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminClubDetailDto>(TestJson.Options);
            Assert.Equal("測試俱樂部（已更新）", updated!.Zh.Name);
            Assert.Equal("Test Club (Updated)", updated.En!.Name);
            Assert.False(updated.IsCollectingSubject);
        }
        finally
        {
            await DeleteClubByCodeAsync(code);
            await DeleteClubByCodeAsync($"{code}2");
        }
    }

    // ───────────────────────────── Competition（俱樂部範圍） ─────────────────────────────

    [Fact]
    public async Task Competition_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/competitions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Competition_跨俱樂部_擋下_授權範圍內_成功()
    {
        // partner.club@tcrfc.test（合作球隊管理，own_clubs）只被授權 bw。
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("partner.club@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tcrfcResponse = await client.GetAsync("/api/v1/admin/tcrfc/competitions");
        Assert.Equal(HttpStatusCode.Forbidden, tcrfcResponse.StatusCode);

        var bwResponse = await client.GetAsync("/api/v1/admin/bw/competitions");
        Assert.Equal(HttpStatusCode.OK, bwResponse.StatusCode);
    }

    [Fact]
    public async Task Competition_檢視者角色只有唯讀_建立會被擋下()
    {
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/competitions");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");
        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/competitions", new CreateAdminCompetitionRequest
        {
            SeasonId = seasonId,
            Code = $"t{Guid.NewGuid():N}"[..12],
            Content = new AdminCompetitionContentInput { Zh = new AdminCompetitionLocaleContent { Name = "測試賽事系列" } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task Competition_建立成功_代號重複回409_排程狀態回400_可更新()
    {
        using var client = await CreateSuperAdminClientAsync();
        var seasonId = await GetSeasonIdAsync("tcrfc", "2026-27");
        var code = $"t{Guid.NewGuid():N}"[..12];

        try
        {
            var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/competitions", new CreateAdminCompetitionRequest
            {
                SeasonId = seasonId,
                Code = code,
                CompType = "league",
                Content = new AdminCompetitionContentInput { Zh = new AdminCompetitionLocaleContent { Name = "測試企業甲級聯賽" } },
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminCompetitionDetailDto>(TestJson.Options);
            Assert.Equal("draft", created!.Status);

            var conflictResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/competitions", new CreateAdminCompetitionRequest
            {
                SeasonId = seasonId,
                Code = code,
                Content = new AdminCompetitionContentInput { Zh = new AdminCompetitionLocaleContent { Name = "重複代號" } },
            });
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

            var scheduledResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/competitions", new CreateAdminCompetitionRequest
            {
                SeasonId = seasonId,
                Code = $"{code}s",
                Status = "scheduled",
                Content = new AdminCompetitionContentInput { Zh = new AdminCompetitionLocaleContent { Name = "不該存在的排程賽事" } },
            });
            Assert.Equal(HttpStatusCode.BadRequest, scheduledResponse.StatusCode);

            var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/competitions/{created.Id}",
                new UpdateAdminCompetitionRequest
                {
                    SeasonId = seasonId,
                    Code = code,
                    Status = "published",
                    Content = new AdminCompetitionContentInput { Zh = new AdminCompetitionLocaleContent { Name = "測試企業甲級聯賽（已發布）" } },
                });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminCompetitionDetailDto>(TestJson.Options);
            Assert.Equal("published", updated!.Status);
        }
        finally
        {
            await DeleteCompetitionByCodeAsync("tcrfc", code);
            await DeleteCompetitionByCodeAsync("tcrfc", $"{code}s");
        }
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private async Task<HttpClient> CreateSuperAdminClientAsync()
    {
        var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<Guid> GetSeasonIdAsync(string clubCode, string seasonCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id FROM seasons s JOIN clubs c ON c.id = s.club_id
            WHERE c.code = @ClubCode AND s.code = @SeasonCode
            """;
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        command.Parameters.AddWithValue("@SeasonCode", seasonCode);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task DeleteClubByCodeAsync(string code)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM clubs_i18n WHERE club_id = (SELECT id FROM clubs WHERE code = @Code);
            DELETE FROM clubs WHERE code = @Code;
            """;
        command.Parameters.AddWithValue("@Code", code);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteCompetitionByCodeAsync(string clubCode, string competitionCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @Id uniqueidentifier;
            SELECT @Id = comp.id FROM competitions comp JOIN clubs c ON c.id = comp.club_id
            WHERE c.code = @ClubCode AND comp.code = @CompetitionCode;
            DELETE FROM competitions_i18n WHERE competition_id = @Id;
            DELETE FROM competitions WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        command.Parameters.AddWithValue("@CompetitionCode", competitionCode);
        await command.ExecuteNonQueryAsync();
    }
}
