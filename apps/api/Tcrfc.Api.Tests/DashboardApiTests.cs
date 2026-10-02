using System.Net;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminDashboard;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// A 儀表板（後台）。🔴 本檔寫於沒有資料庫憑證的工作樹，<b>尚未實跑</b>（docs/18 E-121）；查詢能否翻譯成 SQL 另有不需資料庫的測試
/// （<c>SiteBackendOfflineTranslationTests</c>）。數字一律用「造資料前後的差值」斷言，不假設共用庫的基準值。測試資料以 <c>ZZDASH</c> 前綴辨識，finally 清掉。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class DashboardApiTests(AdminWriteApiFixture fixture)
{
    private const string Url = "/api/v1/admin/tcrfc/dashboard";
    private const string Marker = "ZZDASH";

    private static async Task CleanupAsync()
    {
        await BizTest.ExecuteSqlAsync("DELETE FROM registrations WHERE registration_no LIKE 'ZZDASH-%'");
        await BizTest.ExecuteSqlAsync("DELETE FROM sessions WHERE program_id IN (SELECT id FROM programs WHERE slug LIKE 'zz-dash-%')");
        await BizTest.ExecuteSqlAsync("DELETE FROM programs WHERE slug LIKE 'zz-dash-%'");
        await BizTest.ExecuteSqlAsync("DELETE FROM trials WHERE id IN (SELECT trial_id FROM trials_i18n WHERE audience LIKE 'ZZDASH%')");
        await BizTest.ExecuteSqlAsync("DELETE FROM sponsors WHERE slug LIKE 'zz-dash-%'");
        await BizTest.ExecuteSqlAsync("DELETE FROM faqs WHERE slug LIKE 'zz-dash-%'");
        await BizTest.ExecuteSqlAsync("DELETE FROM articles WHERE slug LIKE 'zz-dash-%'");
        await BizTest.ExecuteSqlAsync("DELETE FROM enquiries WHERE internal_note = N'ZZDASH'");
    }

    private async Task<AdminDashboardDto> DashboardAsync(HttpClient client)
        => await BizTest.ReadAsync<AdminDashboardDto>(await client.GetAsync(Url));

    private static int Count(AdminDashboardDto d, string code) => d.Todos.Single(t => t.Code == code).Count;

    [Fact]
    public async Task 權限_未登入401_沒有該俱樂部授權403_沒有任何可看項目403_有任一可看項目200()
    {
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        using var partner = await BizTest.ClientAsync(fixture, "partner.club@tcrfc.test"); // 只被授權藍鯨
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await partner.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await partner.GetAsync("/api/v1/admin/bw/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync("/api/v1/admin/no-such-club/dashboard")).StatusCode);
    }

    [Fact]
    public async Task 區塊依權限出現_檢視者沒有快速入口_內容編輯有發布新聞與新增FAQ_客服有詢問與報名待辦_系統管理員全有()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");

        var all = await DashboardAsync(admin);
        Assert.Equal(
            ["enquiries_new", "registrations_pending", "sessions_closing_soon", "sponsor_contracts_expiring"],
            all.Todos.Select(t => t.Code).ToList());
        Assert.Equal(["publish_news", "add_match", "add_session", "add_faq", "add_calendar_event"], all.QuickEntries.Select(q => q.Code).ToList());
        Assert.NotNull(all.Content);
        Assert.NotNull(all.Faq);
        Assert.NotNull(all.Members);

        Assert.Empty((await DashboardAsync(viewer)).QuickEntries);

        var editorView = await DashboardAsync(editor);
        Assert.Contains(editorView.QuickEntries, q => q.Code == "publish_news");
        Assert.Contains(editorView.QuickEntries, q => q.Code == "add_faq");
        Assert.DoesNotContain(editorView.QuickEntries, q => q.Code == "add_match");
        Assert.Null(editorView.Members); // 內容編輯沒有會員模組權限

        var serviceView = await DashboardAsync(service);
        Assert.Contains(serviceView.Todos, t => t.Code == "enquiries_new");
        Assert.Contains(serviceView.Todos, t => t.Code == "registrations_pending");
        Assert.DoesNotContain(serviceView.Todos, t => t.Code == "sponsor_contracts_expiring"); // 客服沒有商務權限
    }

    [Fact]
    public async Task 翻譯人員_進得去儀表板_看得到全類別未翻譯數_看不到其他區塊_新聞數字是null不是0()
    {
        var translator = await SiteSettingsTest.CreateTranslatorAsync();
        try
        {
            using var client = fixture.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync(translator));
            var view = await DashboardAsync(client);
            Assert.Empty(view.Todos);
            Assert.Empty(view.QuickEntries);
            Assert.Null(view.Faq);
            Assert.Null(view.Members);
            Assert.Empty(view.Upcoming);
            Assert.NotNull(view.Content);
            Assert.Null(view.Content!.PublishedThisMonth);
            Assert.NotEmpty(view.Content.Untranslated); // 至少有英文這一欄
        }
        finally
        {
            await SiteSettingsTest.DeleteAccountAsync(translator);
        }
    }

    [Fact]
    public async Task 待辦提醒_四項數字隨資料增加_各自只算符合條件的()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var before = await DashboardAsync(admin);

            await BizTest.ExecuteSqlAsync(
                """
                DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
                -- 詢問：一筆新進（算）、一筆處理中（不算）
                DECLARE @form uniqueidentifier = (SELECT TOP 1 id FROM forms WHERE club_id = @club AND form_code = N'general_contact');
                INSERT INTO enquiries (id, club_id, form_id, status, internal_note) VALUES (NEWID(), @club, @form, N'新進', N'ZZDASH');
                INSERT INTO enquiries (id, club_id, form_id, status, internal_note) VALUES (NEWID(), @club, @form, N'處理中', N'ZZDASH');
                -- 報名：試訓的一筆待確認（算）、一筆已確認（不算）
                DECLARE @trial uniqueidentifier = NEWID();
                INSERT INTO trials (id, club_id, trial_on) VALUES (@trial, @club, DATEADD(day, 30, CAST(SYSUTCDATETIME() AS date)));
                INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (@trial, N'zh-Hant', N'ZZDASH 待辦試訓');
                INSERT INTO registrations (id, registration_no, club_id, trial_id, applicant_name, status) VALUES (NEWID(), N'ZZDASH-0001', @club, @trial, N'ZZDASH 甲', N'待確認');
                INSERT INTO registrations (id, registration_no, club_id, trial_id, applicant_name, status) VALUES (NEWID(), N'ZZDASH-0002', @club, @trial, N'ZZDASH 乙', N'已確認');
                -- 營隊：3 天後截止（算）、30 天後截止（不算）、已額滿（不算）
                DECLARE @prog uniqueidentifier = NEWID();
                INSERT INTO programs (id, club_id, slug, status) VALUES (@prog, @club, N'zz-dash-prog', N'published');
                INSERT INTO programs_i18n (program_id, locale, name) VALUES (@prog, N'zh-Hant', N'ZZDASH 夏令營');
                INSERT INTO sessions (id, club_id, program_id, status, signup_closes_at) VALUES (NEWID(), @club, @prog, N'開放', DATEADD(day, 3, SYSUTCDATETIME()));
                INSERT INTO sessions (id, club_id, program_id, status, signup_closes_at) VALUES (NEWID(), @club, @prog, N'開放', DATEADD(day, 30, SYSUTCDATETIME()));
                INSERT INTO sessions (id, club_id, program_id, status, signup_closes_at) VALUES (NEWID(), @club, @prog, N'額滿', DATEADD(day, 3, SYSUTCDATETIME()));
                -- 贊助合約：提醒日已到且未到期（算）、提醒日未到（不算）、已到期（不算）
                INSERT INTO sponsors (id, club_id, slug, contract_end_on, expiry_alert_on)
                VALUES (NEWID(), @club, N'zz-dash-s1', DATEADD(day, 10, CAST(SYSUTCDATETIME() AS date)), DATEADD(day, -1, CAST(SYSUTCDATETIME() AS date)));
                INSERT INTO sponsors (id, club_id, slug, contract_end_on, expiry_alert_on)
                VALUES (NEWID(), @club, N'zz-dash-s2', DATEADD(day, 60, CAST(SYSUTCDATETIME() AS date)), DATEADD(day, 30, CAST(SYSUTCDATETIME() AS date)));
                INSERT INTO sponsors (id, club_id, slug, contract_end_on, expiry_alert_on)
                VALUES (NEWID(), @club, N'zz-dash-s3', DATEADD(day, -5, CAST(SYSUTCDATETIME() AS date)), DATEADD(day, -30, CAST(SYSUTCDATETIME() AS date)));
                """);

            var after = await DashboardAsync(admin);
            Assert.Equal(Count(before, "enquiries_new") + 1, Count(after, "enquiries_new"));
            Assert.Equal(Count(before, "registrations_pending") + 1, Count(after, "registrations_pending"));
            Assert.Equal(Count(before, "sessions_closing_soon") + 1, Count(after, "sessions_closing_soon"));
            Assert.Equal(Count(before, "sponsor_contracts_expiring") + 1, Count(after, "sponsor_contracts_expiring"));
            Assert.Contains(after.Todos.Single(t => t.Code == "sessions_closing_soon").Items, i => i.Title == "ZZDASH 夏令營");
            Assert.True(after.Todos.Single(t => t.Code == "sessions_closing_soon").Items.Count <= 5);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 兩個俱樂部各算各的_藍鯨的資料不進磐石的待辦()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var before = await DashboardAsync(admin);
            await BizTest.ExecuteSqlAsync(
                """
                DECLARE @bw uniqueidentifier = (SELECT id FROM clubs WHERE code = N'bw');
                DECLARE @form uniqueidentifier = (SELECT TOP 1 id FROM forms WHERE club_id = @bw AND form_code = N'general_contact');
                INSERT INTO enquiries (id, club_id, form_id, status, internal_note) VALUES (NEWID(), @bw, @form, N'新進', N'ZZDASH');
                """);
            var after = await DashboardAsync(admin);
            Assert.Equal(Count(before, "enquiries_new"), Count(after, "enquiries_new"));
            var bw = await BizTest.ReadAsync<AdminDashboardDto>(await admin.GetAsync("/api/v1/admin/bw/dashboard"));
            Assert.True(Count(bw, "enquiries_new") >= 1);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 內容與FAQ概況_草稿排程數_未翻譯數_熱門與負評()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            var before = await DashboardAsync(admin);
            await BizTest.ExecuteSqlAsync(
                """
                DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
                DECLARE @cat uniqueidentifier = (SELECT TOP 1 id FROM article_categories ORDER BY row_seq);
                DECLARE @a1 uniqueidentifier = NEWID(), @a2 uniqueidentifier = NEWID(), @a3 uniqueidentifier = NEWID();
                -- 草稿（只有繁中）、排程（繁中＋英文）、本月已發布（繁中＋英文）
                INSERT INTO articles (id, club_id, slug, article_category_id, status) VALUES (@a1, @club, N'zz-dash-a1', @cat, N'draft');
                INSERT INTO articles_i18n (article_id, locale, title) VALUES (@a1, N'zh-Hant', N'ZZDASH 草稿');
                INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at) VALUES (@a2, @club, N'zz-dash-a2', @cat, N'scheduled', DATEADD(day, 5, SYSUTCDATETIME()));
                INSERT INTO articles_i18n (article_id, locale, title) VALUES (@a2, N'zh-Hant', N'ZZDASH 排程'), (@a2, N'en', N'ZZDASH scheduled');
                INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at) VALUES (@a3, @club, N'zz-dash-a3', @cat, N'published', SYSUTCDATETIME());
                INSERT INTO articles_i18n (article_id, locale, title) VALUES (@a3, N'zh-Hant', N'ZZDASH 已發布'), (@a3, N'en', N'ZZDASH published');
                -- FAQ：瀏覽數極高、👎 多於 👍 且達 3 個（負評提醒）；另一題 👎 只有 2 個（不提醒）
                DECLARE @f1 uniqueidentifier = NEWID(), @f2 uniqueidentifier = NEWID();
                INSERT INTO faqs (id, club_id, slug, status, view_count, helpful_count, unhelpful_count) VALUES (@f1, @club, N'zz-dash-f1', N'published', 2000000000, 1, 5);
                INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@f1, N'zh-Hant', N'ZZDASH 負評題目', N'答');
                INSERT INTO faqs (id, club_id, slug, status, view_count, helpful_count, unhelpful_count) VALUES (@f2, @club, N'zz-dash-f2', N'published', 0, 0, 2);
                INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@f2, N'zh-Hant', N'ZZDASH 少量負評', N'答');
                """);

            var after = await DashboardAsync(admin);
            Assert.Equal(before.Content!.DraftCount + 1, after.Content!.DraftCount);
            Assert.Equal(before.Content.ScheduledCount + 1, after.Content.ScheduledCount);
            Assert.Equal(before.Content.PublishedThisMonth + 1, after.Content.PublishedThisMonth);

            var beforeEn = before.Content.Untranslated.Single(u => u.Locale == "en");
            var afterEn = after.Content.Untranslated.Single(u => u.Locale == "en");
            Assert.Equal(beforeEn.Count + 1, afterEn.Count); // 只有草稿那篇缺英文
            Assert.Equal(
                (beforeEn.ByType.FirstOrDefault(t => t.Type == "article")?.Count ?? 0) + 1,
                afterEn.ByType.Single(t => t.Type == "article").Count);

            Assert.Equal("ZZDASH 負評題目", after.Faq!.TopQuestions[0].Question);
            Assert.Contains(after.Faq.NegativeFeedback, f => f.Question == "ZZDASH 負評題目" && f.UnhelpfulCount == 5);
            Assert.DoesNotContain(after.Faq.NegativeFeedback, f => f.Question == "ZZDASH 少量負評");
            Assert.True(after.Faq.TopQuestions.Count <= 10);
            Assert.True(after.Faq.NegativeFeedback.Count <= 10);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 近期行程_十四天內_異常提醒_名額未滿_沒場地_沒教練_超過十四天的不出現()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        try
        {
            await BizTest.ExecuteSqlAsync(
                """
                DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
                DECLARE @t1 uniqueidentifier = NEWID(), @t2 uniqueidentifier = NEWID();
                INSERT INTO trials (id, club_id, trial_on, capacity, enrolled_count) VALUES (@t1, @club, DATEADD(day, 5, CAST(SYSUTCDATETIME() AS date)), 10, 3);
                INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (@t1, N'zh-Hant', N'ZZDASH 近期試訓');
                INSERT INTO trials (id, club_id, trial_on) VALUES (@t2, @club, DATEADD(day, 40, CAST(SYSUTCDATETIME() AS date)));
                INSERT INTO trials_i18n (trial_id, locale, audience) VALUES (@t2, N'zh-Hant', N'ZZDASH 太遠的試訓');
                DECLARE @prog uniqueidentifier = NEWID();
                INSERT INTO programs (id, club_id, slug, status) VALUES (@prog, @club, N'zz-dash-prog', N'published');
                INSERT INTO programs_i18n (program_id, locale, name) VALUES (@prog, N'zh-Hant', N'ZZDASH 近期營隊');
                INSERT INTO sessions (id, club_id, program_id, status, start_on, capacity, enrolled_count)
                VALUES (NEWID(), @club, @prog, N'開放', DATEADD(day, 6, CAST(SYSUTCDATETIME() AS date)), 20, 5);
                """);

            var view = await DashboardAsync(admin);
            var trial = view.Upcoming.Single(u => u.Source == "trial" && u.Title.Contains("近期試訓"));
            Assert.Contains(trial.Warnings, w => w.Contains("名額未滿") && w.Contains("3／10"));
            Assert.Contains("尚未設定地點", trial.Warnings);
            Assert.DoesNotContain(view.Upcoming, u => u.Title.Contains("太遠的試訓"));

            var session = view.Upcoming.Single(u => u.Source == "session" && u.Title == "ZZDASH 近期營隊");
            Assert.Contains("尚未指派教練", session.Warnings);
            Assert.Contains(session.Warnings, w => w.StartsWith("名額未滿"));
            Assert.Contains("尚未設定地點", session.Warnings);

            // 依日期排序，且不超過上限
            Assert.Equal(view.Upcoming.Select(u => u.Date).OrderBy(d => d).ToList(), view.Upcoming.Select(u => u.Date).ToList());
            Assert.True(view.Upcoming.Count <= AdminDashboardRepository.UpcomingLimit);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 轉換概況_週月趨勢_欄位數量固定_序列依權限_不合法週期400()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var editor = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        try
        {
            var week = await BizTest.ReadAsync<AdminDashboardConversionDto>(await admin.GetAsync(Url + "/conversion"));
            Assert.Equal("week", week.Period);
            Assert.Equal(8, week.Buckets.Count);
            Assert.All(week.Buckets, b => Assert.Equal(DayOfWeek.Monday, b.Start!.Value.DayOfWeek));
            var month = await BizTest.ReadAsync<AdminDashboardConversionDto>(await admin.GetAsync(Url + "/conversion?period=month"));
            Assert.Equal(6, month.Buckets.Count);
            Assert.All(month.Buckets, b => Assert.Equal(1, b.Start!.Value.Day));
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Url + "/conversion?period=year")).StatusCode);

            // 造一筆本週的詢問與一筆提案下載，總數與本週 bucket 各 +1
            await BizTest.ExecuteSqlAsync(
                """
                DECLARE @club uniqueidentifier = (SELECT id FROM clubs WHERE code = N'tcrfc');
                INSERT INTO enquiries (id, club_id, form_id, status, internal_note)
                VALUES (NEWID(), @club, (SELECT TOP 1 id FROM forms WHERE club_id = @club AND form_code = N'general_contact'), N'新進', N'ZZDASH');
                INSERT INTO enquiries (id, club_id, form_id, status, internal_note)
                VALUES (NEWID(), @club, (SELECT TOP 1 id FROM forms WHERE club_id = @club AND form_code = N'proposal_download'), N'新進', N'ZZDASH');
                """);
            var after = await BizTest.ReadAsync<AdminDashboardConversionDto>(await admin.GetAsync(Url + "/conversion"));
            Assert.Equal(week.Totals.Enquiries + 1, after.Totals.Enquiries);
            Assert.Equal(week.Totals.ProposalDownloads + 1, after.Totals.ProposalDownloads);
            Assert.Equal(week.Buckets[^1].Enquiries + 1, after.Buckets[^1].Enquiries);
            Assert.Contains(after.Forms, f => f.FormCode == "general_contact" && f.Count >= 1);
            Assert.DoesNotContain(after.Forms, f => f.Count <= 0);

            // 內容編輯沒有詢問／報名／會員權限：這些序列是 null（不是 0）
            var editorView = await BizTest.ReadAsync<AdminDashboardConversionDto>(await editor.GetAsync(Url + "/conversion"));
            Assert.Null(editorView.Totals.Enquiries);
            Assert.Null(editorView.Totals.Registrations);
            Assert.Null(editorView.Totals.NewMembers);
            Assert.All(editorView.Buckets, b => Assert.Null(b.NewPaidMemberships));
            Assert.Empty(editorView.Forms);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task 流量概況_GA4未串接_回尚未串接訊息_不影響其他端點()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var anonymous = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url + "/traffic")).StatusCode);
        var traffic = await BizTest.ReadAsync<AdminDashboardTrafficDto>(await admin.GetAsync(Url + "/traffic"));
        Assert.False(traffic.Configured);
        Assert.Null(traffic.Overview);
        Assert.Contains("尚未串接", traffic.Message);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(Url)).StatusCode);
    }
}
