using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.AdminSeo;
using Tcrfc.Api.Features.Seo;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-12：H 搜尋與 AI 能見度——全站 SEO 預設／追蹤碼、301 轉址管理、孤立頁面偵測，
/// 以及對應的公開讀取端點。打真正的 HTTP 管線與真正的 <c>tcrfc_club_dev</c>，不 mock，
/// 跟這個測試專案既有的紀律一致。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminSeoTests(AdminWriteApiFixture fixture)
{
    private static string UniquePath(string label) => $"/s1-12-test-{label}-{Guid.NewGuid():N}/";

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

    // ───────────────────────────── 全站 SEO 預設／追蹤碼 ─────────────────────────────

    [Fact]
    public async Task Settings_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/tcrfc/seo/settings")).StatusCode);
    }

    [Fact]
    public async Task Settings_內容編輯角色_沒有sysadminonly權限_403()
    {
        // 矩陣「SEO／設定」欄除了內容編輯的「單頁 SEO」（跟隨既有 content.page/article.update
        // 權限，不在這裡）之外，十個角色裡只有系統管理員打勾——seo.setting.* 是 sysadmin_only，
        // 非超管角色即使被指派了角色，PermissionChecker 仍會擋下（見 docs/12b §7.4「S1-12 新增」）。
        using var client = await CreateContentEditorClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/tcrfc/seo/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync(
            "/api/v1/admin/tcrfc/seo/settings", BuildSettingsMultipart(new UpdateSeoSettingsRequest { TitleTemplateZh = "x" }))).StatusCode);
    }

    /// <summary>
    /// 🔴 驗收退回後補做（2026-09-25）：這個測試會寫入 <c>tcrfc_club_dev</c> 的 <c>settings</c>／
    /// <c>settings_i18n</c>——那是本機開發環境同一份 API 唯一用的資料庫，寫入的值會立刻反映在
    /// 公開端點（本測試也依此斷言），若不還原，會**永久改變本機前台實際顯示的標題樣板**。測試前
    /// 先讀出原值，測試後（含斷言失敗時）用 <c>finally</c> 還原——這批設定目前沒有「刪除」語意
    /// （<see cref="AdminSeoSettingsRepository"/> 是 upsert-only），還原用直接的 SQL upsert／delete，
    /// 不能只靠呼叫端點本身（沒有端點可以把值還原成「這個鍵原本不存在」）。
    /// </summary>
    [Fact]
    public async Task Settings_系統管理員_可讀可寫_中文必填英文可空()
    {
        using var client = await CreateSuperAdminClientAsync();

        var original = await CaptureSeoSettingsRowsAsync();
        try
        {
            var getResponse = await client.GetAsync("/api/v1/admin/tcrfc/seo/settings");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            Assert.NotNull(await getResponse.Content.ReadFromJsonAsync<AdminSeoSettingsDto>(TestJson.Options));

            // 缺中文標題樣板 → 400。
            var invalidResponse = await client.PutAsync("/api/v1/admin/tcrfc/seo/settings",
                BuildSettingsMultipart(new UpdateSeoSettingsRequest { DefaultDescriptionZh = "描述" }));
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

            var marker = Guid.NewGuid().ToString("N");
            var updateRequest = new UpdateSeoSettingsRequest
            {
                TitleTemplateZh = $"{{title}}｜台中磐石足球俱樂部｜{marker}",
                DefaultDescriptionZh = "測試用預設描述。",
                Ga4MeasurementId = "G-TEST123",
            };

            var updateResponse = await client.PutAsync("/api/v1/admin/tcrfc/seo/settings", BuildSettingsMultipart(updateRequest));
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminSeoSettingsDto>(TestJson.Options);
            Assert.Equal(updateRequest.TitleTemplateZh, updated!.TitleTemplateZh);
            Assert.Null(updated.TitleTemplateEn); // 省略英文＝清空既有英文列。
            Assert.Equal("G-TEST123", updated.Ga4MeasurementId);

            // 再讀一次，確認真的落地而不是回應內容剛好對得上。
            var reread = await (await client.GetAsync("/api/v1/admin/tcrfc/seo/settings"))
                .Content.ReadFromJsonAsync<AdminSeoSettingsDto>(TestJson.Options);
            Assert.Equal(updateRequest.TitleTemplateZh, reread!.TitleTemplateZh);

            // 公開端點（不需要登入）應該看得到同一份值——沒有接快取的環境（測試預設 NoOpQueryCache）
            // 應該立即反映，不必等 TTL。
            using var publicClient = fixture.CreateClient();
            var publicSettings = await (await publicClient.GetAsync("/api/v1/tcrfc/seo/settings"))
                .Content.ReadFromJsonAsync<PublicSeoSettingsDto>(TestJson.Options);
            Assert.Equal(updateRequest.TitleTemplateZh, publicSettings!.TitleTemplateZh);
            Assert.Equal("G-TEST123", publicSettings.Ga4MeasurementId);
        }
        finally
        {
            await RestoreSeoSettingsRowsAsync(original);
        }
    }

    /// <summary>比照 <c>Features/AdminSeo/AdminSeoSettingsRequestForm</c> 的既有欄位命名，
    /// 純文字更新不夾帶 <c>ogImage</c> 檔案欄位（圖片情境見 <c>AdminSeoImageTests</c>）。</summary>
    private static MultipartFormDataContent BuildSettingsMultipart(UpdateSeoSettingsRequest request)
    {
        var form = new MultipartFormDataContent();
        var json = System.Text.Json.JsonSerializer.Serialize(request, TestJson.WriteOptions);
        var payloadContent = new StringContent(json, System.Text.Encoding.UTF8);
        payloadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        form.Add(payloadContent, "payload");
        return form;
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private sealed record SettingSnapshotRow(string SettingKey, string? SettingValue, string? SettingGroup, string Locale, string? I18nValue);

    /// <summary>讀出 <c>tcrfc</c> 俱樂部目前 <c>seo.*</c>／<c>tracking.*</c> 這批鍵的完整現況
    /// （含有沒有 i18n 列），供測試結束後精準還原——本測試執行前這批鍵理論上不存在
    /// （見 apps/api/README.md「S1-12」段「已知取捨」的更正說明），但用「讀出再還原」而不是
    /// 「假設一定是空的就直接刪」，這樣即使日後種子資料真的預先種了這些鍵，這個測試也不會
    /// 把別人合法的資料洗掉。</summary>
    private static async Task<List<SettingSnapshotRow>> CaptureSeoSettingsRowsAsync()
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.setting_key, s.setting_value, s.setting_group, si.locale, si.value
            FROM settings s
            JOIN clubs c ON c.id = s.club_id
            LEFT JOIN settings_i18n si ON si.setting_id = s.id
            WHERE c.code = 'tcrfc' AND (s.setting_key LIKE 'seo.%' OR s.setting_key LIKE 'tracking.%')
            """;

        var rows = new List<SettingSnapshotRow>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new SettingSnapshotRow(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null! : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return rows;
    }

    private static async Task RestoreSeoSettingsRowsAsync(List<SettingSnapshotRow> original)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();

        // 先整批刪掉測試跑完後現存的這批鍵（含 i18n），再依原始快照重建——比逐欄比對差異
        // 簡單可靠，反正這批鍵的資料量極小（七個鍵、至多兩個語系）。
        await using (var deleteI18n = connection.CreateCommand())
        {
            deleteI18n.CommandText = """
                DELETE si FROM settings_i18n si
                JOIN settings s ON s.id = si.setting_id
                JOIN clubs c ON c.id = s.club_id
                WHERE c.code = 'tcrfc' AND (s.setting_key LIKE 'seo.%' OR s.setting_key LIKE 'tracking.%')
                """;
            await deleteI18n.ExecuteNonQueryAsync();
        }

        await using (var deleteSettings = connection.CreateCommand())
        {
            deleteSettings.CommandText = """
                DELETE s FROM settings s
                JOIN clubs c ON c.id = s.club_id
                WHERE c.code = 'tcrfc' AND (s.setting_key LIKE 'seo.%' OR s.setting_key LIKE 'tracking.%')
                """;
            await deleteSettings.ExecuteNonQueryAsync();
        }

        if (original.Count == 0)
        {
            return; // 測試前這批鍵原本就不存在，刪光即是還原完成。
        }

        foreach (var group in original.GroupBy(r => r.SettingKey))
        {
            var first = group.First();
            var settingId = Guid.NewGuid();

            await using (var insertSetting = connection.CreateCommand())
            {
                insertSetting.CommandText = """
                    INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group)
                    SELECT @Id, c.id, @Key, @Value, @Group FROM clubs c WHERE c.code = 'tcrfc'
                    """;
                insertSetting.Parameters.AddWithValue("@Id", settingId);
                insertSetting.Parameters.AddWithValue("@Key", first.SettingKey);
                insertSetting.Parameters.AddWithValue("@Value", (object?)first.SettingValue ?? DBNull.Value);
                insertSetting.Parameters.AddWithValue("@Group", (object?)first.SettingGroup ?? DBNull.Value);
                await insertSetting.ExecuteNonQueryAsync();
            }

            foreach (var row in group.Where(r => r.Locale is not null))
            {
                await using var insertI18n = connection.CreateCommand();
                insertI18n.CommandText = "INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@SettingId, @Locale, @Value)";
                insertI18n.Parameters.AddWithValue("@SettingId", settingId);
                insertI18n.Parameters.AddWithValue("@Locale", row.Locale);
                insertI18n.Parameters.AddWithValue("@Value", (object?)row.I18nValue ?? DBNull.Value);
                await insertI18n.ExecuteNonQueryAsync();
            }
        }
    }

    // ───────────────────────────── 301 轉址管理 ─────────────────────────────

    [Fact]
    public async Task Redirect_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/tcrfc/seo/redirects")).StatusCode);
    }

    [Fact]
    public async Task Redirect_內容編輯角色_403()
    {
        using var client = await CreateContentEditorClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/tcrfc/seo/redirects")).StatusCode);
    }

    [Fact]
    public async Task Redirect_建立更新刪除完整流程_重複來源網址回409_格式錯誤回400()
    {
        using var client = await CreateSuperAdminClientAsync();
        var fromPath = UniquePath("crud");

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seo/redirects",
            new CreateRedirectRequest { FromPath = fromPath, ToPath = "/zh/about/", IsActive = true });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<AdminRedirectDto>(TestJson.Options);
        Assert.NotNull(created);
        Assert.True(created!.IsActive);

        try
        {
            // 格式錯誤：不是以 / 開頭。
            var invalidResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seo/redirects",
                new CreateRedirectRequest { FromPath = "no-leading-slash", ToPath = "/zh/about/" });
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

            // 來源網址重複 → 409。
            var conflictResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seo/redirects",
                new CreateRedirectRequest { FromPath = fromPath, ToPath = "/zh/academy/" });
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

            // 來源與目的相同 → 400。
            var selfLoopResponse = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/seo/redirects/{created.Id}",
                new UpdateRedirectRequest { ToPath = fromPath, IsActive = true });
            Assert.Equal(HttpStatusCode.BadRequest, selfLoopResponse.StatusCode);

            var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/seo/redirects/{created.Id}",
                new UpdateRedirectRequest { ToPath = "/zh/club/", IsActive = false });
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminRedirectDto>(TestJson.Options);
            Assert.Equal("/zh/club/", updated!.ToPath);
            Assert.False(updated.IsActive);

            // 停用後不應出現在公開端點的生效清單。
            using var publicClient = fixture.CreateClient();
            var activeRedirects = await (await publicClient.GetAsync("/api/v1/tcrfc/seo/redirects"))
                .Content.ReadFromJsonAsync<List<PublicRedirectDto>>(TestJson.Options);
            Assert.DoesNotContain(activeRedirects!, r => r.FromPath == fromPath);

            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/tcrfc/seo/redirects/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getAfterDelete = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/seo/redirects/{created.Id}",
                new UpdateRedirectRequest { ToPath = "/zh/club/", IsActive = true });
            Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/seo/redirects/{created!.Id}");
        }
    }

    [Fact]
    public async Task Redirect_公開端點_生效中的規則會出現()
    {
        using var client = await CreateSuperAdminClientAsync();
        var fromPath = UniquePath("public-active");

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seo/redirects",
            new CreateRedirectRequest { FromPath = fromPath, ToPath = "/zh/about/", IsActive = true });
        var created = await createResponse.Content.ReadFromJsonAsync<AdminRedirectDto>(TestJson.Options);

        try
        {
            using var publicClient = fixture.CreateClient();
            var activeRedirects = await (await publicClient.GetAsync("/api/v1/tcrfc/seo/redirects"))
                .Content.ReadFromJsonAsync<List<PublicRedirectDto>>(TestJson.Options);
            var match = Assert.Single(activeRedirects!, r => r.FromPath == fromPath);
            Assert.Equal("/zh/about/", match.ToPath);
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/seo/redirects/{created!.Id}");
        }
    }

    [Fact]
    public async Task RedirectCsv_匯出格式為UTF8含BOM_匯入為upsert依來源網址()
    {
        using var client = await CreateSuperAdminClientAsync();
        var fromPath = UniquePath("csv");

        var createResponse = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seo/redirects",
            new CreateRedirectRequest { FromPath = fromPath, ToPath = "/zh/about/", IsActive = true });
        var created = await createResponse.Content.ReadFromJsonAsync<AdminRedirectDto>(TestJson.Options);

        try
        {
            var exportResponse = await client.GetAsync("/api/v1/admin/tcrfc/seo/redirects/export");
            Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
            var bytes = await exportResponse.Content.ReadAsByteArrayAsync();
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);

            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var rows = CsvUtils.Parse(text);
            Assert.Equal(new[] { "來源網址", "目的網址", "啟用狀態" }, rows[0]);
            var exportedRow = rows.Skip(1).Single(r => r[0] == fromPath);
            Assert.Equal("/zh/about/", exportedRow[1]);
            Assert.Equal("啟用", exportedRow[2]);

            // 匯入：更新既有那一筆（改目的網址與停用）＋ 新增一筆。
            var newPath = UniquePath("csv-new");
            var csv = CsvUtils.BuildCsv(
            [
                ["來源網址", "目的網址", "啟用狀態"],
                [fromPath, "/zh/academy/", "停用"],
                [newPath, "/zh/club/", "啟用"],
            ]);

            var importResponse = await client.PostAsync("/api/v1/admin/tcrfc/seo/redirects/import", new StringContent(csv));
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
            var importResult = await importResponse.Content.ReadFromJsonAsync<RedirectCsvImportResultDto>(TestJson.Options);
            Assert.Equal(2, importResult!.ImportedCount);
            Assert.Empty(importResult.Errors);

            var listResponse = await client.GetAsync($"/api/v1/admin/tcrfc/seo/redirects?keyword={Uri.EscapeDataString(fromPath)}");
            var list = await listResponse.Content.ReadFromJsonAsync<PagedResult<AdminRedirectDto>>(TestJson.Options);
            var upserted = Assert.Single(list!.Items);
            Assert.Equal("/zh/academy/", upserted.ToPath);
            Assert.False(upserted.IsActive);

            // 表頭錯誤 → 整批不寫入。
            var badHeaderCsv = CsvUtils.BuildCsv([["錯誤表頭"], ["x"]]);
            var badResponse = await client.PostAsync("/api/v1/admin/tcrfc/seo/redirects/import", new StringContent(badHeaderCsv));
            Assert.Equal(HttpStatusCode.BadRequest, badResponse.StatusCode);

            // 清掉新增的那一筆。
            var newList = await (await client.GetAsync($"/api/v1/admin/tcrfc/seo/redirects?keyword={Uri.EscapeDataString(newPath)}"))
                .Content.ReadFromJsonAsync<PagedResult<AdminRedirectDto>>(TestJson.Options);
            foreach (var item in newList!.Items)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/seo/redirects/{item.Id}");
            }
        }
        finally
        {
            await client.DeleteAsync($"/api/v1/admin/tcrfc/seo/redirects/{created!.Id}");
        }
    }

    // ───────────────────────────── 孤立頁面偵測 ─────────────────────────────

    [Fact]
    public async Task OrphanReport_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/tcrfc/seo/orphan-pages")).StatusCode);
    }

    [Fact]
    public async Task OrphanReport_內容編輯角色_403()
    {
        using var client = await CreateContentEditorClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/tcrfc/seo/orphan-pages")).StatusCode);
    }

    [Fact]
    public async Task OrphanReport_沒有被引用的已發布文章會出現在清單()
    {
        using var editorClient = await CreateContentEditorClientAsync();
        var slug = $"s1-12-orphan-{Guid.NewGuid():N}";

        var createRequest = new CreateArticleRequest
        {
            Slug = slug,
            CategoryCode = "club",
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = $"孤立頁面測試 {slug}" } },
        };
        var createResponse = await editorClient.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(createRequest));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        var publishResponse = await editorClient.PostAsJsonAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}/publish",
            new PublishArticleRequest { ExpectedUpdatedAt = created.UpdatedAt },
            TestJson.WriteOptions);
        publishResponse.EnsureSuccessStatusCode();
        // 🔴 發布會改變 updated_at——刪除時的並行權杖要用發布後的值，不是建立時的舊值，
        // 否則 DELETE 端點的 expectedUpdatedAt 對不起來會回 409，且這裡完全沒檢查回應狀態碼，
        // 失敗會被靜默吞掉、在 tcrfc_club_dev 留下孤兒測試文章（已實測抓到，見 docs/18 E-62）。
        var published = (await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        try
        {
            using var superAdmin = await CreateSuperAdminClientAsync();
            var report = await (await superAdmin.GetAsync("/api/v1/admin/tcrfc/seo/orphan-pages"))
                .Content.ReadFromJsonAsync<OrphanPageReportDto>(TestJson.Options);

            Assert.Contains(report!.Items, i => i.EntityType == "article" && i.Path == $"/zh/news/{slug}/");
        }
        finally
        {
            var deleteResponse = await editorClient.DeleteAsync(
                $"/api/v1/admin/tcrfc/news/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(published.UpdatedAt.ToString("o"))}");
            Assert.True(deleteResponse.IsSuccessStatusCode, $"清理測試文章失敗：{deleteResponse.StatusCode}");
        }
    }

    // ───────────────────────────── 公開 Sitemap 項目 ─────────────────────────────

    [Fact]
    public async Task SitemapEntries_排除設定為noindex或排除的文章()
    {
        using var editorClient = await CreateContentEditorClientAsync();
        var includedSlug = $"s1-12-sitemap-in-{Guid.NewGuid():N}";
        var excludedSlug = $"s1-12-sitemap-out-{Guid.NewGuid():N}";

        var included = await CreateAndPublishAsync(editorClient, includedSlug, isExcludedFromSitemap: false);
        var excluded = await CreateAndPublishAsync(editorClient, excludedSlug, isExcludedFromSitemap: true);

        try
        {
            using var publicClient = fixture.CreateClient();
            var entries = await (await publicClient.GetAsync("/api/v1/tcrfc/seo/sitemap-entries"))
                .Content.ReadFromJsonAsync<List<SitemapEntryDto>>(TestJson.Options);

            Assert.Contains(entries!, e => e.Path == $"/zh/news/{includedSlug}/");
            Assert.DoesNotContain(entries!, e => e.Path == $"/zh/news/{excludedSlug}/");
        }
        finally
        {
            // 🔴 見 OrphanReport 測試上的同一則說明：DELETE 端點需要發布後的 updated_at 當並行
            // 權杖，且一定要斷言回應成功，否則清理失敗會被靜默吞掉（docs/18 E-62）。
            var deleteIncluded = await editorClient.DeleteAsync(
                $"/api/v1/admin/tcrfc/news/{included.Id}?expectedUpdatedAt={Uri.EscapeDataString(included.UpdatedAt.ToString("o"))}");
            Assert.True(deleteIncluded.IsSuccessStatusCode, $"清理測試文章失敗：{deleteIncluded.StatusCode}");

            var deleteExcluded = await editorClient.DeleteAsync(
                $"/api/v1/admin/tcrfc/news/{excluded.Id}?expectedUpdatedAt={Uri.EscapeDataString(excluded.UpdatedAt.ToString("o"))}");
            Assert.True(deleteExcluded.IsSuccessStatusCode, $"清理測試文章失敗：{deleteExcluded.StatusCode}");
        }
    }

    private async Task<AdminArticleDetailDto> CreateAndPublishAsync(HttpClient editorClient, string slug, bool isExcludedFromSitemap)
    {
        var createRequest = new CreateArticleRequest
        {
            Slug = slug,
            CategoryCode = "club",
            Content = new AdminArticleContentInput { Zh = new AdminArticleLocaleContent { Title = $"Sitemap 測試 {slug}" } },
            IsExcludedFromSitemap = isExcludedFromSitemap,
        };
        var createResponse = await editorClient.PostAsync("/api/v1/admin/tcrfc/news", AdminArticleMultipart.Build(createRequest));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;

        var publishResponse = await editorClient.PostAsJsonAsync(
            $"/api/v1/admin/tcrfc/news/{created.Id}/publish",
            new PublishArticleRequest { ExpectedUpdatedAt = created.UpdatedAt },
            TestJson.WriteOptions);
        publishResponse.EnsureSuccessStatusCode();

        return (await publishResponse.Content.ReadFromJsonAsync<AdminArticleDetailDto>(TestJson.Options))!;
    }
}
