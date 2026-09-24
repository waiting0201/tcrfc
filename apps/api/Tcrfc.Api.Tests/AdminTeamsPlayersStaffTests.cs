using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminPlayers;
using Tcrfc.Api.Features.AdminStaff;
using Tcrfc.Api.Features.AdminTeams;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-7：C1（球隊）／C2（球員）／C3（教練與團隊成員）後台 CRUD ＋ 前台公開唯讀端點——
/// 不含圖片上傳的部分（授權、驗證、共同資料唯讀）。打真正的 HTTP 管線與真正的
/// <c>tcrfc_club_dev</c>，形狀比照 <c>AdminClubsAndCompetitionsTests</c>（同一批 S1-3 續作的
/// 既有先例）。**含圖片上傳的成功案例**在 <see cref="AdminTeamsPlayersStaffUploadTests"/>
/// （需要真實 Azurite，另開一個 collection——理由見該類別上的說明）。
///
/// 種子資料現況（見 apps/api/README.md「種子測試帳號」）：<c>tcrfc</c> 只有 <c>D1</c>
/// （<c>first_team</c>）一支球隊，沒有學院梯隊；<c>bw</c> 有 <c>BW1</c>／<c>BW-U15</c>／<c>BW-U12</c>。
/// <c>team.manager@tcrfc.test</c>（<c>team_competition</c> 角色）只被授權 <c>tcrfc</c>。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminTeamsPlayersStaffTests(AdminWriteApiFixture fixture)
{
    // ═════════════════════════════ C1 球隊 ═════════════════════════════

    [Fact]
    public async Task Team_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/teams");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Team_跨俱樂部_擋下()
    {
        // team.manager@tcrfc.test（team_competition，all_clubs 角色）只被授權 tcrfc，沒有 bw。
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("team.manager@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/bw/teams");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Team_檢視者角色只有唯讀_建立會被擋下()
    {
        using var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync("viewer@tcrfc.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/teams");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var form = AdminArticleMultipart.Build(new CreateAdminTeamRequest
        {
            Code = $"Z{Guid.NewGuid():N}"[..4].ToUpperInvariant(),
            Type = "academy",
            Gender = "men",
            Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = "測試梯隊" } },
        });
        var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/teams", form);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task Team_建立成功_代號全站重複回409_性別型別驗證_一線隊唯一_可更新()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var code = $"Z{Guid.NewGuid():N}"[..4].ToUpperInvariant();

        try
        {
            // 建立成功（academy），不夾帶圖片。
            var createForm = AdminArticleMultipart.Build(new CreateAdminTeamRequest
            {
                Code = code,
                Type = "academy",
                Gender = "men",
                AgeBand = "U10",
                Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = "測試梯隊" } },
            });
            var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/teams", createForm);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminTeamDetailDto>(TestJson.Options);

            // 🔴 隊別代號全站唯一——用另一個俱樂部（bw）建同代號一樣要擋下，不是只擋同俱樂部。
            using var superAdmin = await CreateClientAsync("super.admin@tcrfc.test");
            var bwConflictForm = AdminArticleMultipart.Build(new CreateAdminTeamRequest
            {
                Code = code,
                Type = "academy",
                Gender = "women",
                Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = "重複代號（藍鯨）" } },
            });
            var bwConflict = await superAdmin.PostAsync("/api/v1/admin/bw/teams", bwConflictForm);
            Assert.Equal(HttpStatusCode.Conflict, bwConflict.StatusCode);

            // 性別值域驗證。
            var badGenderForm = AdminArticleMultipart.Build(new CreateAdminTeamRequest
            {
                Code = $"{code}9",
                Type = "academy",
                Gender = "invalid",
                Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = "性別錯誤" } },
            });
            var badGenderResponse = await client.PostAsync("/api/v1/admin/tcrfc/teams", badGenderForm);
            Assert.Equal(HttpStatusCode.BadRequest, badGenderResponse.StatusCode);

            // 型別值域驗證。
            var badTypeForm = AdminArticleMultipart.Build(new CreateAdminTeamRequest
            {
                Code = $"{code}8",
                Type = "women", // 已廢除的值。
                Gender = "women",
                Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = "型別錯誤" } },
            });
            var badTypeResponse = await client.PostAsync("/api/v1/admin/tcrfc/teams", badTypeForm);
            Assert.Equal(HttpStatusCode.BadRequest, badTypeResponse.StatusCode);

            // 🔴 一線隊每俱樂部至多一筆——tcrfc 已經有 D1（first_team），再建一筆要擋下。
            var secondFirstTeamForm = AdminArticleMultipart.Build(new CreateAdminTeamRequest
            {
                Code = $"{code}7",
                Type = "first_team",
                Gender = "men",
                Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = "第二個一線隊" } },
            });
            var secondFirstTeamResponse = await client.PostAsync("/api/v1/admin/tcrfc/teams", secondFirstTeamForm);
            Assert.Equal(HttpStatusCode.Conflict, secondFirstTeamResponse.StatusCode);

            // 更新成功。
            var updateForm = AdminArticleMultipart.Build(new UpdateAdminTeamRequest
            {
                Code = code,
                Type = "academy",
                Gender = "men",
                AgeBand = "U11",
                Content = new AdminTeamContentInput
                {
                    Zh = new AdminTeamLocaleContent { Name = "測試梯隊（已更新）" },
                    En = new AdminTeamLocaleContent { Name = "Test Academy (Updated)" },
                },
            });
            var updateResponse = await client.PutAsync($"/api/v1/admin/tcrfc/teams/{created!.Id}", updateForm);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminTeamDetailDto>(TestJson.Options);
            Assert.Equal("測試梯隊（已更新）", updated!.Zh.Name);
            Assert.Equal("Test Academy (Updated)", updated.En!.Name);

            // 公開端點看得到（S1-7 新增的公開唯讀端點，並驗證寫入後快取已失效／不是舊值）。
            var publicTeams = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/tcrfc/teams", TestJson.Options);
            Assert.Contains(publicTeams!, t => t.GetProperty("code").GetString() == code
                && t.GetProperty("name").GetString() == "測試梯隊（已更新）");
        }
        finally
        {
            await DeleteTeamByCodeAsync(code);
            await DeleteTeamByCodeAsync($"{code}9");
            await DeleteTeamByCodeAsync($"{code}8");
            await DeleteTeamByCodeAsync($"{code}7");
        }
    }

    [Fact]
    public async Task 公開球隊端點_不外洩內部欄位()
    {
        using var client = fixture.CreateClient();
        var raw = await client.GetStringAsync("/api/v1/tcrfc/teams");
        using var doc = JsonDocument.Parse(raw);
        Assert.True(doc.RootElement.GetArrayLength() > 0);
        var first = doc.RootElement[0];
        var propertyNames = first.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        // 🔴 受限欄位不外洩：clubId／createdBy／updatedBy／createdAt 是後台管理欄位，
        // 公開端點不得出現（回歸測試，防止之後有人直接把 EF 實體序列化出去）。
        Assert.DoesNotContain("clubId", propertyNames);
        Assert.DoesNotContain("createdBy", propertyNames);
        Assert.DoesNotContain("updatedBy", propertyNames);
        Assert.DoesNotContain("createdAt", propertyNames);
    }

    // ═════════════════════════════ C2 球員 ═════════════════════════════

    [Fact]
    public async Task Player_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/players");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Player_跨俱樂部球隊指派擋下_背號範圍驗證_狀態驗證()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var tcrfcTeamId = await GetTeamIdAsync("tcrfc", "D1");
        var bwTeamId = await GetTeamIdAsync("bw", "BW1");

        // 跨俱樂部指派：tcrfc 範圍的端點指定 bw 的球隊要擋下（400，不是 404——這是輸入錯誤不是資源不存在）。
        var crossClubForm = AdminArticleMultipart.Build(new CreateAdminPlayerRequest
        {
            TeamId = bwTeamId,
            Content = new AdminPlayerContentInput { Zh = new AdminPlayerLocaleContent { Name = "跨俱樂部測試球員" } },
        });
        var crossClubResponse = await client.PostAsync("/api/v1/admin/tcrfc/players", crossClubForm);
        Assert.Equal(HttpStatusCode.BadRequest, crossClubResponse.StatusCode);

        // 背號超出範圍。
        var badShirtNoForm = AdminArticleMultipart.Build(new CreateAdminPlayerRequest
        {
            TeamId = tcrfcTeamId,
            ShirtNo = 999,
            Content = new AdminPlayerContentInput { Zh = new AdminPlayerLocaleContent { Name = "背號錯誤" } },
        });
        var badShirtNoResponse = await client.PostAsync("/api/v1/admin/tcrfc/players", badShirtNoForm);
        Assert.Equal(HttpStatusCode.BadRequest, badShirtNoResponse.StatusCode);

        // 狀態值域驗證。
        var badStatusForm = AdminArticleMultipart.Build(new CreateAdminPlayerRequest
        {
            TeamId = tcrfcTeamId,
            Status = "retired", // 不在允許清單內。
            Content = new AdminPlayerContentInput { Zh = new AdminPlayerLocaleContent { Name = "狀態錯誤" } },
        });
        var badStatusResponse = await client.PostAsync("/api/v1/admin/tcrfc/players", badStatusForm);
        Assert.Equal(HttpStatusCode.BadRequest, badStatusResponse.StatusCode);
    }

    [Fact]
    public async Task Player_建立成功不含照片_可更新_公開端點看得到()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var tcrfcTeamId = await GetTeamIdAsync("tcrfc", "D1");
        Guid? playerId = null;

        try
        {
            var createForm = AdminArticleMultipart.Build(new CreateAdminPlayerRequest
            {
                TeamId = tcrfcTeamId,
                ShirtNo = 77,
                Position = "MF",
                BirthOn = new DateOnly(2008, 5, 1),
                Status = "active",
                Content = new AdminPlayerContentInput { Zh = new AdminPlayerLocaleContent { Name = "測試球員" } },
            });
            var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/players", createForm);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminPlayerDetailDto>(TestJson.Options);
            playerId = created!.Id;
            Assert.Equal("active", created.Status);

            var updateForm = AdminArticleMultipart.Build(new UpdateAdminPlayerRequest
            {
                TeamId = tcrfcTeamId,
                ShirtNo = 78,
                Status = "loan",
                Content = new AdminPlayerContentInput { Zh = new AdminPlayerLocaleContent { Name = "測試球員（外借）" } },
            });
            var updateResponse = await client.PutAsync($"/api/v1/admin/tcrfc/players/{playerId}", updateForm);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminPlayerDetailDto>(TestJson.Options);
            Assert.Equal("loan", updated!.Status);
            Assert.Equal(78, updated.ShirtNo);

            // 公開端點：生日等既有判定為公開的欄位維持可見（docs/12b §8 未列 players 為受限），
            // 這是既有行為，本輪沒有改動，這裡只是回歸驗證；同時驗證寫入後快取已失效。
            var publicPlayers = await client.GetFromJsonAsync<PagedResult<JsonElement>>(
                "/api/v1/tcrfc/players?team=D1", TestJson.Options);
            var publicPlayer = publicPlayers!.Items.FirstOrDefault(p => p.GetProperty("id").GetGuid() == playerId);
            Assert.True(publicPlayer.ValueKind != JsonValueKind.Undefined, "公開端點應能查到剛建立的球員（快取已失效）。");
            Assert.True(publicPlayer.TryGetProperty("birthOn", out _));
        }
        finally
        {
            if (playerId is Guid id)
            {
                await DeletePlayerByIdAsync(id);
            }
        }
    }

    // ═════════════════════════════ C3 教練與團隊成員 ═════════════════════════════

    [Fact]
    public async Task Staff_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/staff");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_分組驗證_跨俱樂部球隊指派擋下()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var bwTeamId = await GetTeamIdAsync("bw", "BW1");

        // 分組值域驗證。
        var badGroupForm = AdminArticleMultipart.Build(new CreateAdminStaffRequest
        {
            StaffGroup = "不存在的分組",
            Content = new AdminStaffContentInput { Zh = new AdminStaffLocaleContent { Name = "分組錯誤" } },
        });
        var badGroupResponse = await client.PostAsync("/api/v1/admin/tcrfc/staff", badGroupForm);
        Assert.Equal(HttpStatusCode.BadRequest, badGroupResponse.StatusCode);

        // 跨俱樂部指派球隊要擋下。
        var crossClubForm = AdminArticleMultipart.Build(new CreateAdminStaffRequest
        {
            StaffGroup = "管理層",
            Content = new AdminStaffContentInput { Zh = new AdminStaffLocaleContent { Name = "跨俱樂部測試教練" } },
            Teams = [new AdminStaffTeamAssignmentInput { TeamId = bwTeamId }],
        });
        var crossClubResponse = await client.PostAsync("/api/v1/admin/tcrfc/staff", crossClubForm);
        Assert.Equal(HttpStatusCode.BadRequest, crossClubResponse.StatusCode);
    }

    [Fact]
    public async Task Staff_建立成功不含照片_含球隊指派_省略Teams維持不變_可更新()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var tcrfcTeamId = await GetTeamIdAsync("tcrfc", "D1");
        Guid? staffId = null;

        try
        {
            // 建立成功，含球隊指派（S0-3c 已拍板：「顧問」職稱歸入「管理層」分組——這裡驗證的是
            // 分組值域接受「管理層」這個字面值，不是驗證某一位具名人員的填值）。
            var createForm = AdminArticleMultipart.Build(new CreateAdminStaffRequest
            {
                StaffGroup = "管理層",
                Licence = "AFC A",
                Content = new AdminStaffContentInput
                {
                    Zh = new AdminStaffLocaleContent { Name = "測試教練", Title = "顧問" },
                },
                Teams = [new AdminStaffTeamAssignmentInput { TeamId = tcrfcTeamId, RoleCode = "head_coach" }],
            });
            var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/staff", createForm);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminStaffDetailDto>(TestJson.Options);
            staffId = created!.Id;
            Assert.False(created.IsShared);
            Assert.Single(created.Teams);
            Assert.Equal("D1", created.Teams[0].TeamCode);

            // 更新成功：省略 Teams＝維持不變（跟既有 AdminArticles 的 Tags／Relations 語意一致）。
            var updateForm = AdminArticleMultipart.Build(new UpdateAdminStaffRequest
            {
                StaffGroup = "行政",
                Content = new AdminStaffContentInput { Zh = new AdminStaffLocaleContent { Name = "測試教練（已更新）" } },
            });
            var updateResponse = await client.PutAsync($"/api/v1/admin/tcrfc/staff/{staffId}", updateForm);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminStaffDetailDto>(TestJson.Options);
            Assert.Equal("行政", updated!.StaffGroup);
            Assert.Single(updated.Teams); // 省略 Teams 沒有清空既有指派。

            // 公開端點看得到（含 IsShared=false），同時驗證寫入後快取已失效。
            var publicStaff = await client.GetFromJsonAsync<PagedResult<JsonElement>>(
                "/api/v1/tcrfc/staff", TestJson.Options);
            Assert.Contains(publicStaff!.Items, s => s.GetProperty("id").GetGuid() == staffId
                && s.GetProperty("isShared").GetBoolean() == false);
        }
        finally
        {
            if (staffId is Guid id)
            {
                await DeleteStaffByIdAsync(id);
            }
        }
    }

    [Fact]
    public async Task Staff_共同資料對俱樂部範圍端點唯讀()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var sharedStaffId = await InsertSharedStaffAsync("共同測試教練");

        try
        {
            // 列表看得到（IsShared=true）。
            var list = await client.GetFromJsonAsync<List<AdminStaffListItemDto>>("/api/v1/admin/tcrfc/staff", TestJson.Options);
            var sharedItem = list!.FirstOrDefault(s => s.Id == sharedStaffId);
            Assert.NotNull(sharedItem);
            Assert.True(sharedItem!.IsShared);

            // 詳情看得到。
            var getResponse = await client.GetAsync($"/api/v1/admin/tcrfc/staff/{sharedStaffId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            // 🔴 更新要擋下（403）——共同內容只有超管能編輯，這個俱樂部範圍端點目前完全不提供
            // 編輯共同內容的路徑（docs/14-invariants.md，逐字比照 articles 既有的處理）。
            var updateForm = AdminArticleMultipart.Build(new UpdateAdminStaffRequest
            {
                Content = new AdminStaffContentInput { Zh = new AdminStaffLocaleContent { Name = "試圖竄改共同資料" } },
            });
            var updateResponse = await client.PutAsync($"/api/v1/admin/tcrfc/staff/{sharedStaffId}", updateForm);
            Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        }
        finally
        {
            await DeleteStaffByIdAsync(sharedStaffId);
        }
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private async Task<HttpClient> CreateClientAsync(string username)
    {
        var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<Guid> GetTeamIdAsync(string clubCode, string teamCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.id FROM teams t JOIN clubs c ON c.id = t.club_id
            WHERE c.code = @ClubCode AND t.code = @TeamCode
            """;
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        command.Parameters.AddWithValue("@TeamCode", teamCode);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task DeleteTeamByCodeAsync(string code)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @Id uniqueidentifier = (SELECT id FROM teams WHERE code = @Code);
            DELETE FROM teams_i18n WHERE team_id = @Id;
            DELETE FROM teams WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Code", code);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeletePlayerByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM players_i18n WHERE player_id = @Id;
            DELETE FROM players WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteStaffByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM staff_teams WHERE staff_id = @Id;
            DELETE FROM staff_i18n WHERE staff_id = @Id;
            DELETE FROM staff WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>種子資料目前沒有任何 <c>club_id IS NULL</c> 的教練／團隊成員列（種子腳本
    /// 13 筆全部各自歸屬某一俱樂部，見任務回報），所以「共同資料唯讀」這個情境要自己造一筆，
    /// 直接寫資料庫繞過應用層（應用層本身就不提供建立共同資料的路徑，這正是要測的行為）。</summary>
    private static async Task<Guid> InsertSharedStaffAsync(string nameZh)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        var id = Guid.NewGuid();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO staff (id, club_id, staff_group) VALUES (@Id, NULL, N'行政');
                """;
            command.Parameters.AddWithValue("@Id", id);
            await command.ExecuteNonQueryAsync();
        }
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO staff_i18n (staff_id, locale, name) VALUES (@Id, N'zh-Hant', @Name);
                """;
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Name", nameZh);
            await command.ExecuteNonQueryAsync();
        }
        return id;
    }
}

/// <summary>
/// S1-7 圖片上傳的成功案例——需要真實 Azurite（不 mock 物件儲存，見
/// <see cref="AdminWriteAzuriteEnabledApiFixture"/> 上的說明），跟不需要圖片的授權／驗證測試
/// （<see cref="AdminTeamsPlayersStaffTests"/>）分開 collection，理由與既有
/// <c>AdminNewsCoverUploadTests</c>／<c>AdminArticlesTests</c> 的既有分法一致：
/// <see cref="AdminWriteApiFixture"/> 注入的是 <c>UnavailableImageStorageService</c>，
/// 帶檔案的請求打下去會是 500，不是這裡要驗證的行為。
/// </summary>
[Collection(AdminWriteAzuriteEnabledCollection.Name)]
public sealed class AdminTeamsPlayersStaffUploadTests(AdminWriteAzuriteEnabledApiFixture fixture)
{
    [Fact]
    public async Task Team_建立與更新可上傳與移除主視覺圖片()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var code = $"Y{Guid.NewGuid():N}"[..4].ToUpperInvariant();

        try
        {
            var createForm = AdminArticleMultipart.Build(new CreateAdminTeamRequest
            {
                Code = code,
                Type = "academy",
                Gender = "men",
                Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = "測試梯隊（圖片）" } },
            }, fileBytes: TestImages.SmallPng());
            var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/teams", createForm);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminTeamDetailDto>(TestJson.Options);
            Assert.NotNull(created!.HeroKey);

            var updateForm = AdminArticleMultipart.Build(new UpdateAdminTeamRequest
            {
                Code = code,
                Type = "academy",
                Gender = "men",
                Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = "測試梯隊（已移除圖片）" } },
                RemoveHero = true,
            });
            var updateResponse = await client.PutAsync($"/api/v1/admin/tcrfc/teams/{created.Id}", updateForm);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminTeamDetailDto>(TestJson.Options);
            Assert.Null(updated!.HeroKey);
        }
        finally
        {
            await DeleteTeamByCodeAsync(code);
        }
    }

    [Fact]
    public async Task Player_建立成功含照片上傳()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        var tcrfcTeamId = await GetTeamIdAsync("tcrfc", "D1");
        Guid? playerId = null;

        try
        {
            var createForm = AdminArticleMultipart.Build(new CreateAdminPlayerRequest
            {
                TeamId = tcrfcTeamId,
                ShirtNo = 79,
                Content = new AdminPlayerContentInput { Zh = new AdminPlayerLocaleContent { Name = "測試球員（圖片）" } },
            }, fileBytes: TestImages.SmallPng());
            var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/players", createForm);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminPlayerDetailDto>(TestJson.Options);
            playerId = created!.Id;
            Assert.NotNull(created.PhotoKey);
        }
        finally
        {
            if (playerId is Guid id)
            {
                await DeletePlayerByIdAsync(id);
            }
        }
    }

    [Fact]
    public async Task Staff_建立成功含照片上傳()
    {
        using var client = await CreateClientAsync("team.manager@tcrfc.test");
        Guid? staffId = null;

        try
        {
            var createForm = AdminArticleMultipart.Build(new CreateAdminStaffRequest
            {
                StaffGroup = "醫療",
                Content = new AdminStaffContentInput { Zh = new AdminStaffLocaleContent { Name = "測試隨隊醫護（圖片）" } },
            }, fileBytes: TestImages.SmallPng());
            var createResponse = await client.PostAsync("/api/v1/admin/tcrfc/staff", createForm);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminStaffDetailDto>(TestJson.Options);
            staffId = created!.Id;
            Assert.NotNull(created.PhotoKey);
        }
        finally
        {
            if (staffId is Guid id)
            {
                await DeleteStaffByIdAsync(id);
            }
        }
    }

    // ───────────────────────────── 內部工具（與 AdminTeamsPlayersStaffTests 重複，故意的——
    // 兩個類別各自綁定不同的 fixture 型別，共用的靜態工具方法沒有共同的基底類別可以掛，
    // 逐字複製比新開一個共用測試工具類別的維護成本低，見既有 AdminNewsCoverUploadTests 的同類分法）─

    private async Task<HttpClient> CreateClientAsync(string username)
    {
        var client = fixture.CreateClient();
        var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<Guid> GetTeamIdAsync(string clubCode, string teamCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.id FROM teams t JOIN clubs c ON c.id = t.club_id
            WHERE c.code = @ClubCode AND t.code = @TeamCode
            """;
        command.Parameters.AddWithValue("@ClubCode", clubCode);
        command.Parameters.AddWithValue("@TeamCode", teamCode);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task DeleteTeamByCodeAsync(string code)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @Id uniqueidentifier = (SELECT id FROM teams WHERE code = @Code);
            DELETE FROM teams_i18n WHERE team_id = @Id;
            DELETE FROM teams WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Code", code);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeletePlayerByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM players_i18n WHERE player_id = @Id;
            DELETE FROM players WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteStaffByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM staff_teams WHERE staff_id = @Id;
            DELETE FROM staff_i18n WHERE staff_id = @Id;
            DELETE FROM staff WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);
        await command.ExecuteNonQueryAsync();
    }
}
