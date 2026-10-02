using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityLedgerTestSupport;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// N7 站台設定（規劃書 §6.7）：站台文案 zh／en、全站金額上下限預設值、徵信名單開關、系統信樣板（四封）、金流與發票憑證的環境切換（金鑰只寫不讀）。
/// 🔴 這些資料是<b>共用本機庫上的單一份設定</b>，不能用「依標記清掉」的方式還原——每個測試前拍下整組相關列（settings／settings_i18n／
/// email_templates／email_templates_i18n／payment_channels），測試後原樣還原，絕不讓測試把種子文案改掉還繼續綠燈。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminSettingsTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Settings = AdminBase + "/settings";
    private const string Templates = AdminBase + "/email-templates";
    private const string Channels = AdminBase + "/payment-channels";

    private Snapshot _snapshot = null!;

    public async Task InitializeAsync()
    {
        fx.ResetDoubles();
        _snapshot = await Snapshot.TakeAsync(fx.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _snapshot.RestoreAsync(fx.ConnectionString);
        await fx.CleanupAsync();
    }

    private async Task<HttpClient> SysAsync() => fx.CreateClientFor(await fx.CreateAdminAsync(true));

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 站台文案與規則
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 站台設定_局部更新只動有帶的欄位_空字串清空_公開端點立即反映()
    {
        var admin = await fx.CreateAdminAsync(true);
        using var sys = fx.CreateClientFor(admin);
        var before = await ReadAsync<AdminSiteSettingsDto>(await sys.GetAsync(Settings));

        var updated = await ReadAsync<AdminSiteSettingsDto>(await sys.PutAsJsonAsync(Settings, new
        {
            homeIntroZh = "CT 首頁說明（繁中）",
            homeIntroEn = "CT home intro",
            noticeEn = "",                    // 清空英文捐款須知
            clubSiteUrl = "https://club.example.org",
            defaultMinAmount = 50,
            defaultMaxAmount = 200000,
            creditListEnabled = false,
        }, TestJson.WriteOptions));

        Assert.Equal("CT 首頁說明（繁中）", updated.HomeIntroZh);
        Assert.Equal("CT home intro", updated.HomeIntroEn);
        Assert.True(string.IsNullOrEmpty(updated.NoticeEn));
        Assert.Equal(before.NoticeZh, updated.NoticeZh);                       // 沒帶的欄位不變
        Assert.Equal(before.PrivacyPolicyZh, updated.PrivacyPolicyZh);
        Assert.Equal("https://club.example.org", updated.ClubSiteUrl);
        Assert.Equal(50, updated.DefaultMinAmount);
        Assert.Equal(200000, updated.DefaultMaxAmount);
        Assert.False(updated.CreditListEnabled);

        // 公開端點立刻反映（不經任何快取）；英文缺漏的欄位回退繁中並標示
        using var anonymous = fx.CreateClient();
        var zh = await anonymous.GetFromJsonAsync<PublicSettingsDto>($"{Base}/settings?lang=zh", TestJson.Options);
        Assert.Equal("CT 首頁說明（繁中）", zh!.HomeIntro);
        Assert.Equal(50, zh.DefaultMinAmount);
        Assert.False(zh.CreditListEnabled);
        var en = await anonymous.GetFromJsonAsync<PublicSettingsDto>($"{Base}/settings?lang=en", TestJson.Options);
        Assert.Equal("CT home intro", en!.HomeIntro);
        Assert.Equal(before.NoticeZh, en.Notice);   // 英文被清空 → 回退繁中
        Assert.True(en.IsFallback);

        // 稽核記錄「改了哪些」，不記文案內容
        var summary = await fx.ScalarAsync<string>("SELECT TOP 1 change_summary FROM audit_logs WHERE admin_user_id = @a AND action = N'setting.update' ORDER BY seq DESC", ("@a", admin.Id));
        Assert.Contains("首頁說明（繁中）", summary);
        Assert.DoesNotContain("CT 首頁說明", summary);
    }

    [Theory]
    [InlineData("""{ "defaultMinAmount": 0 }""")]
    [InlineData("""{ "defaultMinAmount": 500, "defaultMaxAmount": 100 }""")]
    [InlineData("""{ "defaultMaxAmount": 99999999 }""")]
    [InlineData("""{ "clubSiteUrl": "javascript:alert(1)" }""")]
    public async Task 站台設定_不合法的值回400_且完全不寫入(string body)
    {
        using var sys = await SysAsync();
        var before = await ReadAsync<AdminSiteSettingsDto>(await sys.GetAsync(Settings));

        var response = await sys.PutAsync(Settings, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var after = await ReadAsync<AdminSiteSettingsDto>(await sys.GetAsync(Settings));
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task 站台設定_文案過長回400_授權_內容編輯可改文案_檢視者只能看()
    {
        using var sys = await SysAsync();
        using var viewer = fx.CreateClientFor(await fx.CreateAdminAsync(false, "viewer"));
        using var cs = fx.CreateClientFor(await fx.CreateAdminAsync(false, "customer_service_admin"));

        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PutAsJsonAsync(Settings, new { homeIntroZh = new string('字', 2001) }, TestJson.WriteOptions)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Settings)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync(Settings, new { homeIntroZh = "檢視者不可" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cs.GetAsync(Settings)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.CreateClient().GetAsync(Settings)).StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 系統信樣板
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 系統信樣板_列出四封與可用標記_更新後下一封信立即套用_英文主旨本文需同時填或同時空()
    {
        var (projectId, _) = await CreateProjectAsync(fx);
        var failed = await SeedPaidDonationAsync(fx, projectId, null, 500, DateTime.UtcNow, invoiceIssueStatus: "failed");
        var invoiceId = await fx.ScalarAsync<Guid>("SELECT id FROM donation_invoices WHERE donation_id = @d", ("@d", failed.Id));
        using var sys = await SysAsync();

        var list = await ReadAsync<List<AdminEmailTemplateDto>>(await sys.GetAsync(Templates));
        Assert.Equal(["donation_thanks", "invoice_failed", "invoice_issued", "refund_notice"], list.Select(t => t.Code).OrderBy(c => c, StringComparer.Ordinal).ToArray());
        var issued = list.Single(t => t.Code == "invoice_issued");
        Assert.Contains(issued.Tokens, t => t.Token == "{invoice_no}");
        Assert.NotNull(issued.Zh);

        var marker = $"CT-樣板-{Guid.NewGuid():N}"[..16];
        var updated = await ReadAsync<AdminEmailTemplateDto>(await sys.PutAsJsonAsync($"{Templates}/invoice_issued",
            new { subjectZh = $"{marker} 主旨 {{order_no}}", bodyZh = $"{marker} 本文 {{invoice_no}}" }, TestJson.WriteOptions));
        Assert.StartsWith(marker, updated.Zh!.Subject);

        // 真的套用到下一封信：手動填入憑證號碼會寄出 invoice_issued
        Assert.Equal(HttpStatusCode.OK, (await sys.PostAsJsonAsync($"{AdminBase}/invoices/{invoiceId}/manual-number", new { invoiceNo = "TPL-12345678", reason = "樣板測試" }, TestJson.WriteOptions)).StatusCode);
        var mail = fx.Mail.Sent.Single(m => m.TemplateCode == "invoice_issued");
        Assert.Contains($"{marker} 主旨 {failed.OrderNo}", mail.Subject);
        Assert.Contains($"{marker} 本文 TPL-12345678", mail.Body);

        // 英文版：兩個一起填 → 儲存；只填一個 → 400；兩個都空 → 移除英文版
        var withEn = await ReadAsync<AdminEmailTemplateDto>(await sys.PutAsJsonAsync($"{Templates}/invoice_issued", new { subjectEn = "Receipt issued", bodyEn = "Your receipt {invoice_no}" }, TestJson.WriteOptions));
        Assert.Equal("Receipt issued", withEn.En!.Subject);
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PutAsJsonAsync($"{Templates}/invoice_issued", new { subjectEn = "只有主旨" }, TestJson.WriteOptions)).StatusCode);
        var withoutEn = await ReadAsync<AdminEmailTemplateDto>(await sys.PutAsJsonAsync($"{Templates}/invoice_issued", new { subjectEn = "", bodyEn = "" }, TestJson.WriteOptions));
        Assert.Null(withoutEn.En);
        Assert.NotNull(withoutEn.Zh); // 繁中版不受影響

        Assert.Equal(HttpStatusCode.NotFound, (await sys.PutAsJsonAsync($"{Templates}/not_a_template", new { subjectZh = "x", bodyZh = "y" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PutAsJsonAsync($"{Templates}/invoice_issued", new { subjectZh = "", bodyZh = "有本文沒主旨" }, TestJson.WriteOptions)).StatusCode);
    }

    [Fact]
    public async Task 系統信樣板_停用後不再寄出_授權為編輯站台設定權限()
    {
        var (projectId, _) = await CreateProjectAsync(fx);
        var failed = await SeedPaidDonationAsync(fx, projectId, null, 500, DateTime.UtcNow, invoiceIssueStatus: "failed");
        var invoiceId = await fx.ScalarAsync<Guid>("SELECT id FROM donation_invoices WHERE donation_id = @d", ("@d", failed.Id));
        using var sys = await SysAsync();
        using var viewer = fx.CreateClientFor(await fx.CreateAdminAsync(false, "viewer"));

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Templates)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync($"{Templates}/invoice_issued", new { isActive = false }, TestJson.WriteOptions)).StatusCode);

        var off = await ReadAsync<AdminEmailTemplateDto>(await sys.PutAsJsonAsync($"{Templates}/invoice_issued", new { isActive = false }, TestJson.WriteOptions));
        Assert.False(off.IsActive);
        Assert.Equal(HttpStatusCode.OK, (await sys.PostAsJsonAsync($"{AdminBase}/invoices/{invoiceId}/manual-number", new { invoiceNo = "OFF-12345678", reason = "樣板停用測試" }, TestJson.WriteOptions)).StatusCode);
        Assert.Empty(fx.Mail.Sent.Where(m => m.TemplateCode == "invoice_issued"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 金流與發票憑證
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 金流憑證_只寫不讀_回應沒有任何憑證內容_僅系統管理員_寫稽核但不記憑證()
    {
        var admin = await fx.CreateAdminAsync(true);
        using var sys = fx.CreateClientFor(admin);
        const string Secret = "CT-SECRET-channel-credential-0123456789";

        var response = await sys.PutAsJsonAsync($"{Channels}/line_pay/credential", new { environment = "sandbox", credential = Secret }, TestJson.WriteOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Secret, raw);
        Assert.DoesNotContain("\"credential\"", raw, StringComparison.OrdinalIgnoreCase); // 沒有憑證欄位（hasCredential 只是布林）
        var channels = await ReadAsync<List<AdminPaymentChannelDto>>(await sys.GetAsync(Channels));
        var linePay = channels.Single(c => c.ChannelType == "line_pay");
        Assert.True(linePay.Environments.Single(e => e.Environment == "sandbox").HasCredential);

        // 資料庫裡是密文，不是明文
        var stored = await fx.ScalarAsync<string>("SELECT credential_encrypted FROM payment_channels WHERE channel_type = N'line_pay' AND environment = N'sandbox'");
        Assert.DoesNotContain(Secret, stored);

        var summary = await fx.ScalarAsync<string>("SELECT TOP 1 change_summary FROM audit_logs WHERE admin_user_id = @a AND action = N'payment_channel.credential_set' ORDER BY seq DESC", ("@a", admin.Id));
        Assert.DoesNotContain(Secret, summary);

        // 其他角色一律 403（含能管站台設定的角色）
        using var cs = fx.CreateClientFor(await fx.CreateAdminAsync(false, "customer_service_admin"));
        using var biz = fx.CreateClientFor(await fx.CreateAdminAsync(false, "business_sponsorship"));
        Assert.Equal(HttpStatusCode.Forbidden, (await cs.GetAsync(Channels)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await biz.PutAsJsonAsync($"{Channels}/line_pay/credential", new { environment = "sandbox", credential = "x" }, TestJson.WriteOptions)).StatusCode);
    }

    [Fact]
    public async Task 環境切換_正式環境沒有憑證不能切過去_需要二次確認_切換後發票字軌讀作用中環境_寫稽核()
    {
        var admin = await fx.CreateAdminAsync(true);
        using var sys = fx.CreateClientFor(admin);
        var (projectId, _) = await CreateProjectAsync(fx);
        await fx.ExecuteAsync("DELETE FROM payment_channels"); // 乾淨起點；DisposeAsync 會原樣還原

        // 沒有二次確認 → 400；正式環境沒設憑證 → 409
        Assert.Equal(HttpStatusCode.BadRequest, (await sys.PutAsJsonAsync($"{Channels}/einvoice/environment", new { environment = "production" }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await sys.PutAsJsonAsync($"{Channels}/einvoice/environment", new { environment = "production", confirm = true }, TestJson.WriteOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sys.PutAsJsonAsync($"{Channels}/unknown/environment", new { environment = "production", confirm = true }, TestJson.WriteOptions)).StatusCode);

        // 設了憑證但沒有字軌 → 電子發票仍不能切到正式
        await sys.PutAsJsonAsync($"{Channels}/einvoice/credential", new { environment = "production", credential = "prod-key" }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Conflict, (await sys.PutAsJsonAsync($"{Channels}/einvoice/environment", new { environment = "production", confirm = true }, TestJson.WriteOptions)).StatusCode);

        await sys.PutAsJsonAsync($"{Channels}/einvoice/credential", new { environment = "production", credential = "prod-key", invoicePrefix = "ZP" }, TestJson.WriteOptions);
        await sys.PutAsJsonAsync($"{Channels}/einvoice/credential", new { environment = "sandbox", credential = "sandbox-key", invoicePrefix = "ZS" }, TestJson.WriteOptions);

        // 預設（Development 主機）作用中是測試環境 → 開票用測試字軌 ZS
        var pendingA = await SeedPaidDonationAsync(fx, projectId, null, 500, DateTime.UtcNow, invoiceIssueStatus: "failed");
        Assert.Equal(HttpStatusCode.OK, (await sys.PostAsync($"{AdminBase}/donations/{pendingA.Id}/invoice/reissue", null)).StatusCode);
        Assert.Equal("ZS", fx.Invoices.Requests.Last().TrackPrefix);

        // 切到正式 → 之後開票改用正式字軌 ZP；稽核記錄切換前後
        var switched = await ReadAsync<List<AdminPaymentChannelDto>>(await sys.PutAsJsonAsync($"{Channels}/einvoice/environment", new { environment = "production", confirm = true }, TestJson.WriteOptions));
        Assert.Equal("production", switched.Single(c => c.ChannelType == "einvoice").ActiveEnvironment);
        Assert.Equal("sandbox", switched.Single(c => c.ChannelType == "line_pay").ActiveEnvironment); // 另一個管道不受影響
        var pendingB = await SeedPaidDonationAsync(fx, projectId, null, 500, DateTime.UtcNow, invoiceIssueStatus: "failed");
        Assert.Equal(HttpStatusCode.OK, (await sys.PostAsync($"{AdminBase}/donations/{pendingB.Id}/invoice/reissue", null)).StatusCode);
        Assert.Equal("ZP", fx.Invoices.Requests.Last().TrackPrefix);
        Assert.Contains("測試 → 正式", await fx.ScalarAsync<string>("SELECT TOP 1 change_summary FROM audit_logs WHERE admin_user_id = @a AND action = N'payment_channel.env_switch' ORDER BY seq DESC", ("@a", admin.Id)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 還原
    // ═══════════════════════════════════════════════════════════════════════

    private sealed class Snapshot
    {
        private readonly List<object?[]> _settings = [];
        private readonly List<object?[]> _settingsI18n = [];
        private readonly List<object?[]> _templates = [];
        private readonly List<object?[]> _templatesI18n = [];
        private readonly List<object?[]> _channels = [];

        private static async Task<List<object?[]>> ReadAsync(SqlConnection c, string sql)
        {
            var rows = new List<object?[]>();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new object?[reader.FieldCount];
                for (var i = 0; i < row.Length; i++)
                {
                    row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(row);
            }

            return rows;
        }

        public static async Task<Snapshot> TakeAsync(string connectionString)
        {
            var s = new Snapshot();
            await using var c = new SqlConnection(connectionString);
            await c.OpenAsync();
            s._settings.AddRange(await ReadAsync(c, "SELECT id, setting_key, value, updated_at, updated_by FROM settings"));
            s._settingsI18n.AddRange(await ReadAsync(c, "SELECT setting_id, locale, value FROM settings_i18n"));
            s._templates.AddRange(await ReadAsync(c, "SELECT id, is_active, updated_at, updated_by FROM email_templates"));
            s._templatesI18n.AddRange(await ReadAsync(c, "SELECT email_template_id, locale, subject, body FROM email_templates_i18n"));
            s._channels.AddRange(await ReadAsync(c, "SELECT id, channel_type, environment, credential_encrypted, invoice_prefix, rotated_at, created_at, updated_at, created_by, updated_by FROM payment_channels"));
            return s;
        }

        private static async Task ExecAsync(SqlConnection c, SqlTransaction t, string sql, params (string, object?)[] ps)
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = t;
            cmd.CommandText = sql;
            foreach (var (n, v) in ps)
            {
                cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
            }

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RestoreAsync(string connectionString)
        {
            await using var c = new SqlConnection(connectionString);
            await c.OpenAsync();
            await using var t = (SqlTransaction)await c.BeginTransactionAsync();

            // settings：雙語內容整批清掉再原樣寫回；快照之外新增的列（本測試新建的 key）刪除；快照內的列把值還原。
            await ExecAsync(c, t, "DELETE FROM settings_i18n");
            var knownIds = _settings.Select(r => (Guid)r[0]!).ToList();
            if (knownIds.Count > 0)
            {
                await ExecAsync(c, t, $"DELETE FROM settings WHERE id NOT IN ({string.Join(",", knownIds.Select(k => $"'{k}'"))})");
            }

            foreach (var row in _settings)
            {
                await ExecAsync(c, t, "UPDATE settings SET value = @v, updated_at = @u, updated_by = @b WHERE id = @id", ("@v", row[2]), ("@u", row[3]), ("@b", row[4]), ("@id", row[0]));
            }

            foreach (var row in _settingsI18n)
            {
                await ExecAsync(c, t, "INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@a, @b, @c)", ("@a", row[0]), ("@b", row[1]), ("@c", row[2]));
            }

            // email templates
            foreach (var row in _templates)
            {
                await ExecAsync(c, t, "UPDATE email_templates SET is_active = @a, updated_at = @u, updated_by = @b WHERE id = @id", ("@a", row[1]), ("@u", row[2]), ("@b", row[3]), ("@id", row[0]));
            }

            await ExecAsync(c, t, "DELETE FROM email_templates_i18n");
            foreach (var row in _templatesI18n)
            {
                await ExecAsync(c, t, "INSERT INTO email_templates_i18n (email_template_id, locale, subject, body) VALUES (@a, @b, @c, @d)",
                    ("@a", row[0]), ("@b", row[1]), ("@c", row[2]), ("@d", row[3]));
            }

            // payment channels
            await ExecAsync(c, t, "DELETE FROM payment_channels");
            foreach (var row in _channels)
            {
                await ExecAsync(c, t, "INSERT INTO payment_channels (id, channel_type, environment, credential_encrypted, invoice_prefix, rotated_at, created_at, updated_at, created_by, updated_by) VALUES (@a, @b, @c, @d, @e, @f, @g, @h, @i, @j)",
                    ("@a", row[0]), ("@b", row[1]), ("@c", row[2]), ("@d", row[3]), ("@e", row[4]), ("@f", row[5]), ("@g", row[6]), ("@h", row[7]), ("@i", row[8]), ("@j", row[9]));
            }

            await t.CommitAsync();
        }
    }
}
