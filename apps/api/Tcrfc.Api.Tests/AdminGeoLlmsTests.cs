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
/// `GEO-01` <c>llms.txt</c> 內容維護（S1-12a）。打真正的 HTTP 管線與真正的 <c>tcrfc_club_dev</c>，
/// 跟這個測試專案既有的紀律一致（<c>AdminSeoTests</c> 同一套）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminGeoLlmsTests(AdminWriteApiFixture fixture)
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
    public async Task LlmsContent_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/tcrfc/seo/llms-content")).StatusCode);
    }

    [Fact]
    public async Task LlmsContent_內容編輯角色_沒有sysadminonly權限_403()
    {
        using var client = await CreateContentEditorClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/tcrfc/seo/llms-content")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(
            "/api/v1/admin/tcrfc/seo/llms-content", new UpdateLlmsContentRequest())).StatusCode);
    }

    /// <summary>五個區塊皆可為空——跟必填的 <c>seo.title_template</c> 不同，見
    /// <see cref="AdminLlmsContentDto"/> 檔頭。整份送空白請求應該直接成功並清空既有值。</summary>
    [Fact]
    public async Task LlmsContent_系統管理員_可讀可寫_全部欄位皆可為空_中文可空英文可空()
    {
        using var client = await CreateSuperAdminClientAsync();

        var original = await CaptureLlmsRowsAsync("tcrfc");
        try
        {
            var getResponse = await client.GetAsync("/api/v1/admin/tcrfc/seo/llms-content");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var marker = Guid.NewGuid().ToString("N");
            var updateRequest = new UpdateLlmsContentRequest
            {
                PositioningZh = $"測試站點定位｜{marker}",
                KeyPagesZh = "- [關於我們](/zh/about/)",
                FactsSummaryZh = "成立於測試年份。",
                LicenseZh = "測試授權文字。",
                ContactZh = "test@example.test",
            };

            var updateResponse = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/seo/llms-content", updateRequest);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminLlmsContentDto>(TestJson.Options);
            Assert.Equal(updateRequest.PositioningZh, updated!.PositioningZh);
            Assert.Null(updated.PositioningEn); // 省略英文＝清空既有英文值（同 seo.setting.* 既有語意）。

            // 公開端點（不需要登入）應該立即看到同一份值（測試環境是 NoOpQueryCache，不必等 TTL）。
            using var publicClient = fixture.CreateClient();
            var publicContent = await (await publicClient.GetAsync("/api/v1/tcrfc/seo/llms-content"))
                .Content.ReadFromJsonAsync<PublicLlmsContentDto>(TestJson.Options);
            Assert.Equal(updateRequest.PositioningZh, publicContent!.PositioningZh);
            Assert.Equal(updateRequest.KeyPagesZh, publicContent.KeyPagesZh);

            // 整份送空白請求 → 全部欄位成功清空為 null，不是 400（五個區塊皆可為空）。
            var clearResponse = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/seo/llms-content", new UpdateLlmsContentRequest());
            Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
            var cleared = await clearResponse.Content.ReadFromJsonAsync<AdminLlmsContentDto>(TestJson.Options);
            Assert.Null(cleared!.PositioningZh);
            Assert.Null(cleared.KeyPagesZh);
        }
        finally
        {
            await RestoreLlmsRowsAsync("tcrfc", original);
        }
    }

    /// <summary>跨俱樂部：<c>tcrfc</c> 寫入的內容不得出現在 <c>bw</c> 的公開端點——兩站各自一份
    /// （`GEO-09`），資料以 <c>club_id</c> 隔開，不是同一列共用。</summary>
    [Fact]
    public async Task LlmsContent_跨俱樂部_tcrfc寫入不會出現在bw()
    {
        using var client = await CreateSuperAdminClientAsync();

        var originalTcrfc = await CaptureLlmsRowsAsync("tcrfc");
        try
        {
            var marker = $"tcrfc-only-{Guid.NewGuid():N}";
            var updateResponse = await client.PutAsJsonAsync(
                "/api/v1/admin/tcrfc/seo/llms-content", new UpdateLlmsContentRequest { PositioningZh = marker });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            using var publicClient = fixture.CreateClient();
            var bwContent = await (await publicClient.GetAsync("/api/v1/bw/seo/llms-content"))
                .Content.ReadFromJsonAsync<PublicLlmsContentDto>(TestJson.Options);
            Assert.NotEqual(marker, bwContent!.PositioningZh);
        }
        finally
        {
            await RestoreLlmsRowsAsync("tcrfc", originalTcrfc);
        }
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private sealed record LlmsSnapshotRow(string SettingKey, string? SettingValue, string? SettingGroup, string Locale, string? I18nValue);

    private static async Task<List<LlmsSnapshotRow>> CaptureLlmsRowsAsync(string clubCode)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.setting_key, s.setting_value, s.setting_group, si.locale, si.value
            FROM settings s
            JOIN clubs c ON c.id = s.club_id
            LEFT JOIN settings_i18n si ON si.setting_id = s.id
            WHERE c.code = @Club AND s.setting_key LIKE 'geo.llms_%'
            """;
        command.Parameters.AddWithValue("@Club", clubCode);

        var rows = new List<LlmsSnapshotRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new LlmsSnapshotRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null! : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return rows;
    }

    private static async Task RestoreLlmsRowsAsync(string clubCode, List<LlmsSnapshotRow> original)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();

        await using (var deleteI18n = connection.CreateCommand())
        {
            deleteI18n.CommandText = """
                DELETE si FROM settings_i18n si
                JOIN settings s ON s.id = si.setting_id
                JOIN clubs c ON c.id = s.club_id
                WHERE c.code = @Club AND s.setting_key LIKE 'geo.llms_%'
                """;
            deleteI18n.Parameters.AddWithValue("@Club", clubCode);
            await deleteI18n.ExecuteNonQueryAsync();
        }

        await using (var deleteSettings = connection.CreateCommand())
        {
            deleteSettings.CommandText = """
                DELETE s FROM settings s
                JOIN clubs c ON c.id = s.club_id
                WHERE c.code = @Club AND s.setting_key LIKE 'geo.llms_%'
                """;
            deleteSettings.Parameters.AddWithValue("@Club", clubCode);
            await deleteSettings.ExecuteNonQueryAsync();
        }

        if (original.Count == 0)
        {
            return;
        }

        foreach (var group in original.GroupBy(r => r.SettingKey))
        {
            var first = group.First();
            var settingId = Guid.NewGuid();

            await using (var insertSetting = connection.CreateCommand())
            {
                insertSetting.CommandText = """
                    INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
                    SELECT @Id, c.id, @Key, @Value, @Group FROM clubs c WHERE c.code = @Club
                    """;
                insertSetting.Parameters.AddWithValue("@Id", settingId);
                insertSetting.Parameters.AddWithValue("@Key", first.SettingKey);
                insertSetting.Parameters.AddWithValue("@Value", (object?)first.SettingValue ?? DBNull.Value);
                insertSetting.Parameters.AddWithValue("@Group", (object?)first.SettingGroup ?? DBNull.Value);
                insertSetting.Parameters.AddWithValue("@Club", clubCode);
                await insertSetting.ExecuteNonQueryAsync();
            }

            foreach (var row in group.Where(r => r.Locale is not null))
            {
                await using var insertI18n = connection.CreateCommand();
                insertI18n.CommandText = """
                    INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@SettingId, @Locale, @Value)
                    """;
                insertI18n.Parameters.AddWithValue("@SettingId", settingId);
                insertI18n.Parameters.AddWithValue("@Locale", row.Locale);
                insertI18n.Parameters.AddWithValue("@Value", (object?)row.I18nValue ?? DBNull.Value);
                await insertI18n.ExecuteNonQueryAsync();
            }
        }
    }
}
