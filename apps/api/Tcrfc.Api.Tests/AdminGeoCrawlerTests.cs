using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Features.AdminSeo;
using Tcrfc.Api.Features.Seo;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// `GEO-02` AI 爬蟲授權（S1-12b）。打真正的 HTTP 管線與真正的 <c>tcrfc_club_dev</c>。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminGeoCrawlerTests(AdminWriteApiFixture fixture)
{
    private async Task<HttpClient> CreateSuperAdminClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));
        return client;
    }

    private async Task<HttpClient> CreateContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    [Fact]
    public async Task CrawlerSettings_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/tcrfc/seo/crawler-settings")).StatusCode);
    }

    [Fact]
    public async Task CrawlerSettings_內容編輯角色_沒有sysadminonly權限_403()
    {
        using var client = await CreateContentEditorClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/tcrfc/seo/crawler-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(
            "/api/v1/admin/tcrfc/seo/crawler-settings",
            new UpdateCrawlerSettingsRequest { UserAgents = [], AdditionalExcludePaths = [] })).StatusCode);
    }

    [Fact]
    public async Task CrawlerSettings_尚未設定過_回傳規劃書預設清單與強制排除路徑()
    {
        using var client = await CreateSuperAdminClientAsync();

        var original = await CaptureCrawlerRowsAsync("tcrfc");
        try
        {
            await DeleteCrawlerRowsAsync("tcrfc");

            var response = await client.GetAsync("/api/v1/admin/tcrfc/seo/crawler-settings");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var dto = await response.Content.ReadFromJsonAsync<AdminCrawlerSettingsDto>(TestJson.Options);

            Assert.Contains(dto!.UserAgents, a => a.UserAgent == "GPTBot" && a.Allowed);
            Assert.Contains(dto.UserAgents, a => a.UserAgent == "ClaudeBot" && a.Allowed);
            Assert.Empty(dto.AdditionalExcludePaths);

            // 🔴 2026-09-25（協調者驗收退回）：強制排除路徑現在就同時涵蓋 /zh/ 與 /en/，
            // 不留成「/en/ 上線時再補」的已知缺口——即使 /en/ 頁面目前還不存在，這裡也要現在
            // 就輸出，不是等頁面上線才回頭補。
            Assert.Contains("/zh/member/", dto.MandatoryExcludePaths);
            Assert.Contains("/en/member/", dto.MandatoryExcludePaths);
            Assert.Contains("/zh/join/player/", dto.MandatoryExcludePaths);
            Assert.Contains("/en/join/player/", dto.MandatoryExcludePaths);
            Assert.Contains("/zh/academy/teams/", dto.MandatoryExcludePaths);
            Assert.Contains("/en/academy/teams/", dto.MandatoryExcludePaths);
            Assert.Contains("/m/", dto.MandatoryExcludePaths);
        }
        finally
        {
            await RestoreCrawlerRowsAsync("tcrfc", original);
        }
    }

    /// <summary>🔴 2026-09-25（協調者驗收退回）：**公開端點**（不需要登入，`apps/web` 的
    /// `robots.txt.ts` 實際消費的那一份）也要同時含 `/zh/…` 與 `/en/…` 的強制排除路徑——
    /// 不是只有後台 GET 才看得到雙語系，公開端點才是真正決定 <c>robots.txt</c> 內容的來源。</summary>
    [Fact]
    public async Task CrawlerSettings_公開端點_同時包含zh與en的強制排除路徑()
    {
        using var publicClient = fixture.CreateClient();

        var response = await publicClient.GetAsync("/api/v1/tcrfc/seo/crawler-settings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PublicCrawlerSettingsDto>(TestJson.Options);

        Assert.Contains("/zh/member/", dto!.ExcludePaths);
        Assert.Contains("/en/member/", dto.ExcludePaths);
        Assert.Contains("/zh/join/general/", dto.ExcludePaths);
        Assert.Contains("/en/join/general/", dto.ExcludePaths);
        Assert.Contains("/zh/order/lookup/", dto.ExcludePaths);
        Assert.Contains("/en/order/lookup/", dto.ExcludePaths);
        Assert.Contains("/zh/academy/teams/", dto.ExcludePaths);
        Assert.Contains("/en/academy/teams/", dto.ExcludePaths);
        Assert.Contains("/m/", dto.ExcludePaths); // 不展開語系，見 GeoCrawlerDefaults 檔頭。
    }

    [Fact]
    public async Task CrawlerSettings_系統管理員_可讀可寫_並驗證輸入格式()
    {
        using var client = await CreateSuperAdminClientAsync();

        var original = await CaptureCrawlerRowsAsync("tcrfc");
        try
        {
            // 使用者代理格式不正確（含空白）→ 400。
            var invalidAgent = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/seo/crawler-settings",
                new UpdateCrawlerSettingsRequest
                {
                    UserAgents = [new CrawlerAgentDto { UserAgent = "Bad Bot Name", Allowed = true }],
                    AdditionalExcludePaths = [],
                });
            Assert.Equal(HttpStatusCode.BadRequest, invalidAgent.StatusCode);

            // 使用者代理重複 → 400。
            var duplicateAgent = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/seo/crawler-settings",
                new UpdateCrawlerSettingsRequest
                {
                    UserAgents =
                    [
                        new CrawlerAgentDto { UserAgent = "GPTBot", Allowed = true },
                        new CrawlerAgentDto { UserAgent = "gptbot", Allowed = false },
                    ],
                    AdditionalExcludePaths = [],
                });
            Assert.Equal(HttpStatusCode.BadRequest, duplicateAgent.StatusCode);

            // 排除路徑沒有以「/」開頭 → 400。
            var invalidPath = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/seo/crawler-settings",
                new UpdateCrawlerSettingsRequest { UserAgents = [], AdditionalExcludePaths = ["not-a-path/"] });
            Assert.Equal(HttpStatusCode.BadRequest, invalidPath.StatusCode);

            // 排除路徑沒有以「/」結尾 → 400（目錄前綴慣例，避免部分字串誤配）。
            var noTrailingSlash = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/seo/crawler-settings",
                new UpdateCrawlerSettingsRequest { UserAgents = [], AdditionalExcludePaths = ["/zh/foo"] });
            Assert.Equal(HttpStatusCode.BadRequest, noTrailingSlash.StatusCode);

            // 合法輸入 → 200，且真的落地。
            var marker = $"/zh/test-extra-{Guid.NewGuid():N}/";
            var validUpdate = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/seo/crawler-settings",
                new UpdateCrawlerSettingsRequest
                {
                    UserAgents =
                    [
                        new CrawlerAgentDto { UserAgent = "GPTBot", Allowed = true },
                        new CrawlerAgentDto { UserAgent = "SomeDeniedBot", Allowed = false },
                    ],
                    AdditionalExcludePaths = [marker],
                });
            Assert.Equal(HttpStatusCode.OK, validUpdate.StatusCode);
            var updated = await validUpdate.Content.ReadFromJsonAsync<AdminCrawlerSettingsDto>(TestJson.Options);
            Assert.Equal(2, updated!.UserAgents.Count);
            Assert.Contains(updated.UserAgents, a => a.UserAgent == "SomeDeniedBot" && !a.Allowed);
            Assert.Contains(marker, updated.AdditionalExcludePaths);
        }
        finally
        {
            await RestoreCrawlerRowsAsync("tcrfc", original);
        }
    }

    /// <summary>
    /// 🔴 **反例：後台試圖移除強制排除路徑**——即使管理員把「後台自行再加的排除路徑」整份清空
    /// （送出空陣列，等同想要「不排除任何東西」），公開端點（<c>apps/web</c> 的 <c>robots.txt</c>
    /// 實際消費的那一份）仍然必須繼續輸出規劃書強制的排除路徑。這條路徑不是靠後台程式碼「檢查
    /// 使用者是不是想移除」擋下來的，而是 <see cref="UpdateCrawlerSettingsRequest"/> 這個型別
    /// 本身沒有欄位可以承載強制路徑，公開端點的合併邏輯（<see cref="SeoRepository.GetCrawlerSettingsAsync"/>）
    /// 一律用程式碼常數 <see cref="GeoCrawlerDefaults.GetMandatoryExcludePaths"/> 聯集——
    /// 結構上就不存在「移除強制路徑」這個操作，見 docs/14-invariants.md 個資防線的既有敘述。
    /// </summary>
    [Fact]
    public async Task CrawlerSettings_後台清空自加排除路徑_強制排除路徑仍然存在於公開端點()
    {
        using var client = await CreateSuperAdminClientAsync();

        var original = await CaptureCrawlerRowsAsync("tcrfc");
        try
        {
            // 先加一筆自訂路徑，確認它會出現，再整份清空成 []，驗證強制清單不受影響。
            await client.PutAsJsonAsync("/api/v1/admin/tcrfc/seo/crawler-settings",
                new UpdateCrawlerSettingsRequest { UserAgents = [], AdditionalExcludePaths = ["/zh/temp-extra/"] });

            var clearResponse = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/seo/crawler-settings",
                new UpdateCrawlerSettingsRequest { UserAgents = [], AdditionalExcludePaths = [] });
            Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
            var cleared = await clearResponse.Content.ReadFromJsonAsync<AdminCrawlerSettingsDto>(TestJson.Options);
            Assert.Empty(cleared!.AdditionalExcludePaths);

            using var publicClient = fixture.CreateClient();
            var publicSettings = await (await publicClient.GetAsync("/api/v1/tcrfc/seo/crawler-settings"))
                .Content.ReadFromJsonAsync<PublicCrawlerSettingsDto>(TestJson.Options);

            foreach (var mandatoryPath in GeoCrawlerDefaults.GetMandatoryExcludePaths("tcrfc"))
            {
                Assert.Contains(mandatoryPath, publicSettings!.ExcludePaths);
            }

            // 剛清空的自訂路徑不應該再出現。
            Assert.DoesNotContain("/zh/temp-extra/", publicSettings!.ExcludePaths);
        }
        finally
        {
            await RestoreCrawlerRowsAsync("tcrfc", original);
        }
    }

    /// <summary>跨俱樂部：藍鯨目前沒有對應的未成年學員照片頁面，強制清單不應該把 <c>tcrfc</c>
    /// 專屬的 <c>/zh/academy/teams/</c> 也套用到 <c>bw</c>，且 <c>tcrfc</c> 後台加的路徑不會
    /// 出現在 <c>bw</c> 的公開端點。</summary>
    [Fact]
    public async Task CrawlerSettings_跨俱樂部_強制清單與後台自加路徑不互相污染()
    {
        using var client = await CreateSuperAdminClientAsync();

        var originalTcrfc = await CaptureCrawlerRowsAsync("tcrfc");
        try
        {
            var marker = $"/zh/tcrfc-only-{Guid.NewGuid():N}/";
            var updateResponse = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/seo/crawler-settings",
                new UpdateCrawlerSettingsRequest { UserAgents = [], AdditionalExcludePaths = [marker] });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            using var publicClient = fixture.CreateClient();
            var bwSettings = await (await publicClient.GetAsync("/api/v1/bw/seo/crawler-settings"))
                .Content.ReadFromJsonAsync<PublicCrawlerSettingsDto>(TestJson.Options);

            Assert.DoesNotContain(marker, bwSettings!.ExcludePaths);
            Assert.DoesNotContain("/zh/academy/teams/", bwSettings.ExcludePaths);
            Assert.DoesNotContain("/en/academy/teams/", bwSettings.ExcludePaths);
        }
        finally
        {
            await RestoreCrawlerRowsAsync("tcrfc", originalTcrfc);
        }
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private sealed record CrawlerSnapshotRow(string SettingKey, string? SettingValue, string? SettingGroup);

    private static async Task<List<CrawlerSnapshotRow>> CaptureCrawlerRowsAsync(string clubCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.setting_key, s.setting_value, s.setting_group
            FROM settings s
            JOIN clubs c ON c.id = s.club_id
            WHERE c.code = @Club AND s.setting_key LIKE 'geo.crawler_%'
            """;
        command.Parameters.AddWithValue("@Club", clubCode);

        var rows = new List<CrawlerSnapshotRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new CrawlerSnapshotRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }

    private static async Task DeleteCrawlerRowsAsync(string clubCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE s FROM settings s
            JOIN clubs c ON c.id = s.club_id
            WHERE c.code = @Club AND s.setting_key LIKE 'geo.crawler_%'
            """;
        command.Parameters.AddWithValue("@Club", clubCode);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RestoreCrawlerRowsAsync(string clubCode, List<CrawlerSnapshotRow> original)
    {
        await DeleteCrawlerRowsAsync(clubCode);

        if (original.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();

        foreach (var row in original)
        {
            await using var insertSetting = connection.CreateCommand();
            insertSetting.CommandText = """
                INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
                SELECT @Id, c.id, @Key, @Value, @Group FROM clubs c WHERE c.code = @Club
                """;
            insertSetting.Parameters.AddWithValue("@Id", Guid.NewGuid());
            insertSetting.Parameters.AddWithValue("@Key", row.SettingKey);
            insertSetting.Parameters.AddWithValue("@Value", (object?)row.SettingValue ?? DBNull.Value);
            insertSetting.Parameters.AddWithValue("@Group", (object?)row.SettingGroup ?? DBNull.Value);
            insertSetting.Parameters.AddWithValue("@Club", clubCode);
            await insertSetting.ExecuteNonQueryAsync();
        }
    }
}
