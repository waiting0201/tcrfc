using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminCalendar;
using Tcrfc.Api.Features.AdminForms;
using Tcrfc.Api.Features.AdminMatches;
using Tcrfc.Api.Features.AdminPlayers;
using Tcrfc.Api.Features.AdminSeasons;
using Tcrfc.Api.Features.AdminSeo;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Features.AdminSiteFacts;
using Tcrfc.Api.Features.AdminTeams;
using Tcrfc.Api.Features.Email;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Features.Shop;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// docs/23 後台欄位串接稽核的後端修正（A-1／A-2／A-3／A-4／A-6／A-12／B-12／B-13／B-16／B-22／E-1／E-2）。
/// 打真正的 HTTP 管線與本機測試庫；測試資料一律自己建、自己清，種子資料只讀不改（會動到的設定先讀原值再寫回同值）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AuditWiringFixesTests(AdminWriteApiFixture fixture)
{
    // ═════════════════════════ A-1 烏龍球 ═════════════════════════

    [Theory]
    [InlineData(null, true, null)]
    [InlineData("", true, null)]
    [InlineData("  ", true, null)]
    [InlineData("header", true, "header")]
    [InlineData("頭槌", true, "header")]
    [InlineData("點球", true, "penalty")]
    [InlineData("烏龍球", true, "own_goal")]
    [InlineData("OWN_GOAL", true, "own_goal")]
    [InlineData("free_kick", true, "free_kick")]
    [InlineData("其他", true, "other")]
    [InlineData("遠射神功", false, null)]
    public void 進球類型_寫入正規化與值域(string? raw, bool ok, string? expected)
    {
        Assert.Equal(ok, MatchGoalTypes.TryNormalize(raw, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("own_goal", true)]
    [InlineData("烏龍球", true)] // 舊資料是自由文字
    [InlineData("烏龍", true)]
    [InlineData("Own Goal", true)]
    [InlineData("penalty", false)]
    [InlineData("頭槌", false)]
    [InlineData(null, false)]
    public void 進球類型_讀取端認得舊資料的烏龍球(string? stored, bool expected)
        => Assert.Equal(expected, MatchGoalTypes.IsOwnGoal(stored));

    [Fact]
    public async Task 烏龍球_自動彙總不算球員進球_進球類型寫入收斂值域()
    {
        using var client = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        var teamId = await B1Test.TeamIdAsync("D1");
        var seasonId = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
        Guid? matchId = null;
        Guid? playerA = null, playerB = null;
        try
        {
            playerA = await CreatePlayerAsync(client, teamId, 91, "烏龍測試甲");
            playerB = await CreatePlayerAsync(client, teamId, 92, "烏龍測試乙");

            // 不合法的進球類型 → 400，錯誤標在 goals[1].goalType。
            var bad = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", new CreateAdminMatchRequest
            {
                SeasonId = seasonId, TeamIds = [teamId], MatchOn = new DateOnly(2090, 5, 1), Opponent = "烏龍測試對手", Status = "played", ScoreHome = 1, ScoreAway = 1,
                Goals = [new AdminMatchGoalInput { PlayerId = playerA.Value }, new AdminMatchGoalInput { PlayerId = playerA.Value, GoalType = "亂寫類型" }],
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            var errors = (await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            Assert.True(errors.TryGetProperty("goals[1].goalType", out _));

            var create = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", new CreateAdminMatchRequest
            {
                SeasonId = seasonId, TeamIds = [teamId], MatchOn = new DateOnly(2090, 5, 1), Opponent = "烏龍測試對手", Status = "played", ScoreHome = 3, ScoreAway = 1,
                Goals =
                [
                    new AdminMatchGoalInput { PlayerId = playerA.Value, Minute = 10 },                          // 一般進球
                    new AdminMatchGoalInput { PlayerId = playerA.Value, Minute = 20, GoalType = "烏龍球" },      // 中文寫法 → own_goal
                    new AdminMatchGoalInput { PlayerId = playerA.Value, Minute = 30, GoalType = "own_goal" },
                    new AdminMatchGoalInput { PlayerId = playerB.Value, Minute = 40, GoalType = "頭槌" },       // → header，算進球
                ],
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var created = await create.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options);
            matchId = created!.Id;
            Assert.Equal(["own_goal", "own_goal"], created.Goals.Where(g => g.GoalType == "own_goal").Select(g => g.GoalType));
            Assert.Contains(created.Goals, g => g.GoalType == "header");

            // 公開球員數據：甲只有 1 個進球（烏龍球 2 個不算），乙 1 個；兩人都有出賽（有進球紀錄的場次）。
            var stats = await client.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/stats/players?season=2026-27", TestJson.Options);
            // 2090 年的賽事不在 2026-27 球季期間也無妨：彙總只看賽事所屬球季，不看日期。
            var a = stats.GetProperty("items").EnumerateArray().First(i => i.GetProperty("playerId").GetGuid() == playerA);
            var b = stats.GetProperty("items").EnumerateArray().First(i => i.GetProperty("playerId").GetGuid() == playerB);
            Assert.Equal(1, a.GetProperty("goals").GetInt32());
            Assert.Equal(1, a.GetProperty("appearances").GetInt32());
            Assert.Equal(1, b.GetProperty("goals").GetInt32());
        }
        finally
        {
            if (matchId is Guid m)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/matches/{m}");
            }

            await DeletePlayersAsync(playerA, playerB);
        }
    }

    // ═════════════════════════ A-2 球員狀態（公開端只回現役）═════════════════════════

    [Fact]
    public async Task 球員狀態_離隊外借不出現在公開列表詳情與搜尋_現役輸出status()
    {
        using var client = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        var teamId = await B1Test.TeamIdAsync("D1");
        Guid? playerId = null;
        try
        {
            playerId = await CreatePlayerAsync(client, teamId, 93, "狀態測試球員");

            var active = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/players/{playerId}", TestJson.Options);
            Assert.Equal("active", active.GetProperty("status").GetString());

            foreach (var status in new[] { "departed", "loan", "overseas" })
            {
                await BizTest.ExecuteSqlAsync("UPDATE players SET status = @S WHERE id = @Id", ("@S", status), ("@Id", playerId));
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/tcrfc/players/{playerId}")).StatusCode);
                var list = await client.GetFromJsonAsync<PagedResult<JsonElement>>("/api/v1/tcrfc/players?team=D1&pageSize=100", TestJson.Options);
                Assert.DoesNotContain(list!.Items, p => p.GetProperty("id").GetGuid() == playerId);
            }

            // 尚未填寫狀態（舊資料 NULL）視同現役。
            await BizTest.ExecuteSqlAsync("UPDATE players SET status = NULL WHERE id = @Id", ("@Id", playerId));
            var legacy = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/players/{playerId}", TestJson.Options);
            Assert.Equal("active", legacy.GetProperty("status").GetString());
        }
        finally
        {
            await DeletePlayersAsync(playerId);
        }
    }

    // ═════════════════════════ A-3 表單欄位鎖定 ═════════════════════════

    [Theory]
    [InlineData(nameof(FormCatalog.JoinPlayer))]
    [InlineData(nameof(FormCatalog.AcademyChildrenTraining))]
    [InlineData(nameof(FormCatalog.CampRegistration))]
    [InlineData(nameof(FormCatalog.InternationalPlayerEnquiry))]
    [InlineData(nameof(FormCatalog.PartnershipSponsorship))]
    [InlineData(nameof(FormCatalog.MediaEnquiry))]
    [InlineData(nameof(FormCatalog.GeneralContact))]
    [InlineData(nameof(FormCatalog.ProposalDownload))]
    public void 表單欄位鎖定_七類表單與提案下載都在名單內(string constName)
    {
        var code = (string)typeof(FormCatalog).GetField(constName)!.GetValue(null)!;
        Assert.True(FormCatalog.AreFieldsLocked(code));
    }

    [Fact]
    public void 表單欄位鎖定_捐助洽詢不鎖()
        => Assert.False(FormCatalog.AreFieldsLocked(FormCatalog.DonationEnquiry));

    [Fact]
    public async Task 鎖定的表單_新增刪除欄位與改代碼類型必填選項都回400欄位錯誤_題目文字仍可改()
    {
        using var client = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var formId = await FormIdAsync(client, FormCatalog.PartnershipSponsorship);
        var detail = await client.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        var select = detail!.Fields.First(f => f.FieldKey == "enquiry_type");
        var name = detail.Fields.First(f => f.FieldKey == "name");
        var fieldsUrl = $"/api/v1/admin/tcrfc/forms/{formId}/fields";

        async Task<HashSet<string>> ErrorKeysAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToHashSet();
        }

        UpdateAdminFormFieldRequest Same(AdminFormFieldDto f) => new()
        {
            FieldKey = f.FieldKey, FieldType = f.FieldType, LabelZh = f.LabelZh, LabelEn = f.LabelEn, IsRequired = f.IsRequired,
            ValidationRule = f.ValidationRule, Options = f.Options, OptionLabelsEn = f.OptionLabelsEn, IsSummary = f.IsSummary, SortOrder = f.SortOrder,
        };

        // 新增、刪除
        Assert.Contains("fieldKey", await ErrorKeysAsync(await client.PostAsJsonAsync(fieldsUrl, new CreateAdminFormFieldRequest
        {
            FieldKey = "extra_field", FieldType = "text", LabelZh = "多的欄位",
        }, TestJson.WriteOptions)));
        Assert.Contains("fieldId", await ErrorKeysAsync(await client.DeleteAsync($"{fieldsUrl}/{name.Id}")));

        // 改代碼、類型、必填、選項
        Assert.Contains("fieldKey", await ErrorKeysAsync(await client.PutAsJsonAsync($"{fieldsUrl}/{name.Id}", Same(name) with { FieldKey = "name2" }, TestJson.WriteOptions)));
        Assert.Contains("fieldType", await ErrorKeysAsync(await client.PutAsJsonAsync($"{fieldsUrl}/{name.Id}", Same(name) with { FieldType = "textarea" }, TestJson.WriteOptions)));
        Assert.Contains("isRequired", await ErrorKeysAsync(await client.PutAsJsonAsync($"{fieldsUrl}/{name.Id}", Same(name) with { IsRequired = !name.IsRequired }, TestJson.WriteOptions)));
        Assert.Contains("options", await ErrorKeysAsync(await client.PutAsJsonAsync($"{fieldsUrl}/{select.Id}", Same(select) with
        {
            Options = [.. select.Options!, "新增選項"], OptionLabelsEn = null,
        }, TestJson.WriteOptions)));

        // 資料沒有被改到
        var after = await client.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        Assert.Equal(detail.Fields.Count, after!.Fields.Count);
        Assert.Equal(select.Options, after.Fields.First(f => f.FieldKey == "enquiry_type").Options);

        // 結構不變、只改題目文字（中英）→ 允許，驗完還原
        var relabeled = await client.PutAsJsonAsync($"{fieldsUrl}/{name.Id}", Same(name) with { LabelZh = name.LabelZh + "（測）", LabelEn = "Name (test)" }, TestJson.WriteOptions);
        try
        {
            Assert.Equal(HttpStatusCode.OK, relabeled.StatusCode);
        }
        finally
        {
            await client.PutAsJsonAsync($"{fieldsUrl}/{name.Id}", Same(name), TestJson.WriteOptions);
        }
    }

    [Fact]
    public async Task 鎖定的表單_收件通知_自動回覆_驗證碼_導向頁仍可修改()
    {
        using var client = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var formId = await FormIdAsync(client, FormCatalog.MediaEnquiry);
        var original = await client.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        try
        {
            var response = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}", new UpdateAdminFormRequest
            {
                NotifyEmails = "media-lock@example.test", CaptchaEnabled = !original!.CaptchaEnabled, RedirectPath = "/zh/media-thanks",
                AutoReplyBodyZh = "鎖定測試中文", AutoReplyBodyEn = "lock test en",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await RestoreFormAsync(client, formId, original!);
        }
    }

    // ═════════════════════════ B-16 導向頁輸出 ═════════════════════════

    [Fact]
    public async Task 送出後導向頁_只接受站內相對路徑_公開表單定義輸出redirectPath()
    {
        using var client = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var formId = await FormIdAsync(client, FormCatalog.DonationEnquiry);
        var original = await client.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        UpdateAdminFormRequest With(string? redirect) => new() { NotifyEmails = original!.NotifyEmails, CaptchaEnabled = original.CaptchaEnabled, RedirectPath = redirect };
        try
        {
            foreach (var bad in new[] { "https://evil.example/x", "//evil.example", "/\\evil.example", "javascript:alert(1)", "thanks" })
            {
                var response = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}", With(bad), TestJson.WriteOptions);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("redirectPath", out _));
            }

            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}", With("/zh/thanks"), TestJson.WriteOptions)).StatusCode);
            var publicForm = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/forms/{FormCatalog.DonationEnquiry}", TestJson.Options);
            Assert.Equal("/zh/thanks", publicForm.GetProperty("redirectPath").GetString());

            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}", With(null), TestJson.WriteOptions)).StatusCode);
            var cleared = await client.GetFromJsonAsync<JsonElement>($"/api/v1/tcrfc/forms/{FormCatalog.DonationEnquiry}", TestJson.Options);
            Assert.Equal(JsonValueKind.Null, cleared.GetProperty("redirectPath").ValueKind);
        }
        finally
        {
            await RestoreFormAsync(client, formId, original!);
        }
    }

    [Theory]
    [InlineData("/zh/thanks", "/zh/thanks")]
    [InlineData("/", "/")]
    [InlineData("//evil.example", null)]
    [InlineData("/\\evil", null)]
    [InlineData("https://evil.example", null)]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void 導向頁輸出端防線_舊資料即使是完整網址也不輸出(string? stored, string? expected)
        => Assert.Equal(expected, FormsRepository.SafeRedirectPath(stored));

    // ═════════════════════════ A-4 表單通知信 ═════════════════════════

    [Fact]
    public async Task 表單通知信_收件通知寄給多位_自動回覆依語系_電話聯絡方式不寄回覆()
    {
        using var admin = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var anon = fixture.CreateClient();
        var formId = await FormIdAsync(admin, FormCatalog.DonationEnquiry);
        var original = await admin.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var staffA = $"staff-a-{tag}@example.test";
        var staffB = $"staff-b-{tag}@example.test";
        var senderZh = $"zh-{tag}@example.test";
        var senderEn = $"en-{tag}@example.test";
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}", new UpdateAdminFormRequest
            {
                NotifyEmails = $"{staffA}; {staffB}, {staffA}", CaptchaEnabled = false,
                AutoReplyBodyZh = $"中文自動回覆 {tag}", AutoReplyBodyEn = $"English auto reply {tag}",
            }, TestJson.WriteOptions)).StatusCode);

            Task<HttpResponseMessage> Submit(string contact, string? lang) => anon.PostAsJsonAsync("/api/v1/tcrfc/forms/donation_enquiry/submissions", new SubmitFormRequest
            {
                Answers = new Dictionary<string, string> { ["name"] = $"通知測試{tag}", ["contact"] = contact, ["message"] = "想了解捐助方式", ["privacy_consent"] = "true" },
                Lang = lang,
            }, TestJson.WriteOptions);

            Assert.Equal(HttpStatusCode.OK, (await Submit(senderZh, null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Submit(senderEn, "en")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Submit("0912-345-678", "zh")).StatusCode);

            var sent = MemberTestDoubles.Email.Sent;
            // 收件通知：兩位收件人（重複的只寄一次）× 三筆送出；內容含題目與答案、不含同意條款欄位。
            Assert.Equal(3, sent.Count(m => m.To == staffA && m.Kind == "form_notify"));
            Assert.Equal(3, sent.Count(m => m.To == staffB && m.Kind == "form_notify"));
            var notify = sent.Last(m => m.To == staffA && m.Kind == "form_notify");
            Assert.Contains("想了解捐助方式", notify.TextBody);
            Assert.DoesNotContain("隱私權政策", notify.TextBody);

            // 自動回覆：依 lang 選內文；電話型聯絡方式（不是 Email）不寄。
            Assert.Contains($"中文自動回覆 {tag}", sent.Single(m => m.To == senderZh && m.Kind == "form_auto_reply").TextBody);
            Assert.Contains($"English auto reply {tag}", sent.Single(m => m.To == senderEn && m.Kind == "form_auto_reply").TextBody);
            Assert.DoesNotContain(sent, m => m.Kind == "form_auto_reply" && m.To.Contains("0912"));
        }
        finally
        {
            await RestoreFormAsync(admin, formId, original!);
            await BizTest.ExecuteSqlAsync("""
                DELETE a FROM enquiry_answers a JOIN enquiry_answers b ON b.enquiry_id = a.enquiry_id AND b.value = @Tag;
                DELETE e FROM enquiries e WHERE NOT EXISTS (SELECT 1 FROM enquiry_answers x WHERE x.enquiry_id = e.id)
                  AND e.form_id = @F AND e.created_at > DATEADD(minute, -10, SYSUTCDATETIME());
                """, ("@Tag", $"通知測試{tag}"), ("@F", formId));
        }
    }

    [Fact]
    public async Task 表單通知信_沒設定收件人與自動回覆內文就不寄_寄信失敗不影響送出()
    {
        using var admin = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        var formId = await FormIdAsync(admin, FormCatalog.DonationEnquiry);
        var original = await admin.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        var tag = Guid.NewGuid().ToString("N")[..8];
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}", new UpdateAdminFormRequest
            {
                NotifyEmails = $"boom-{tag}@example.test", CaptchaEnabled = false, AutoReplyBodyZh = "會失敗的自動回覆",
            }, TestJson.WriteOptions)).StatusCode);

            // 寄信一律丟例外的測試主機：送出仍須成功，詢問仍須寫入。
            using var factory = fixture.WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<IEmailSender>();
                s.AddSingleton<IEmailSender, ThrowingEmailSender>();
            }));
            using var anon = factory.CreateClient();
            var response = await anon.PostAsJsonAsync("/api/v1/tcrfc/forms/donation_enquiry/submissions", new SubmitFormRequest
            {
                Answers = new Dictionary<string, string> { ["name"] = $"失敗測試{tag}", ["contact"] = $"fail-{tag}@example.test", ["privacy_consent"] = "true" },
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, await ShopTest.CountAsync(
                "SELECT COUNT(*) FROM enquiry_answers WHERE value = @V", ("@V", $"fail-{tag}@example.test")));
        }
        finally
        {
            await RestoreFormAsync(admin, formId, original!);
            await BizTest.ExecuteSqlAsync("""
                DECLARE @E TABLE (id uniqueidentifier);
                INSERT @E SELECT enquiry_id FROM enquiry_answers WHERE value = @V;
                DELETE FROM enquiry_answers WHERE enquiry_id IN (SELECT id FROM @E);
                DELETE FROM enquiries WHERE id IN (SELECT id FROM @E);
                """, ("@V", $"fail-{tag}@example.test"));
        }
    }

    private sealed class ThrowingEmailSender : IEmailSender
    {
        public bool IsConfigured => true;

        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
            => throw new InvalidOperationException("寄信供應商故障（測試用）");
    }

    // ═════════════════════════ A-6 訂單詳情的買家 Email 與發票資料 ═════════════════════════

    [Fact]
    public async Task 訂單詳情_補買家Email與發票開立資料_Email與載具遮罩_統編與捐贈碼不遮罩()
    {
        using var admin = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        using var service = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var business = await BizTest.ClientAsync(fixture, "business.sponsorship@tcrfc.test");
        var made = await ShopTest.CreateProductAsync(admin, "tcrfc", "inv-detail", ("M", 200, 10));
        try
        {
            var order = await ShopTest.CreateOrderAsync(service, "tcrfc", "onsite_pickup", (made.Variants[0].Id, 1));
            string protectedCarrier;
            using (var scope = fixture.Services.CreateScope())
            {
                protectedCarrier = scope.ServiceProvider.GetRequiredService<ShopInvoiceService>().ProtectCarrier("/ABC+123");
            }

            await BizTest.ExecuteSqlAsync("UPDATE orders SET buyer_email = N'buyer.test@example.com' WHERE id = @O", ("@O", order.Id));
            await BizTest.ExecuteSqlAsync("""
                INSERT INTO store_invoices (id, club_id, order_id, carrier_type, carrier_id_encrypted, issue_status, void_status)
                SELECT NEWID(), club_id, id, 'mobile_barcode', @C, 'pending', 'none' FROM orders WHERE id = @O
                """, ("@O", order.Id), ("@C", protectedCarrier));

            var full = await GetOrderAsync(service, order.Id);
            Assert.Equal("buyer.test@example.com", full.BuyerEmail);
            Assert.Equal("mobile_barcode", full.Invoice!.Type);
            Assert.Equal("手機條碼載具", full.Invoice.TypeLabel);
            Assert.Equal("/ABC+123", full.Invoice.CarrierId);
            Assert.Equal("待開立", full.Invoice.IssueStatusLabel);
            Assert.Equal("未作廢", full.Invoice.VoidStatusLabel);

            var masked = await GetOrderAsync(business, order.Id);
            Assert.True(masked.IsMasked);
            Assert.Equal("b***@example.com", masked.BuyerEmail);
            Assert.Equal("/A****23", masked.Invoice!.CarrierId);

            // 公司戶與捐贈：統編、捐贈碼是公開資訊，不遮罩。
            await BizTest.ExecuteSqlAsync("UPDATE store_invoices SET carrier_type = NULL, carrier_id_encrypted = NULL, tax_id = N'12345678' WHERE order_id = @O", ("@O", order.Id));
            var tax = (await GetOrderAsync(business, order.Id)).Invoice!;
            Assert.Equal("tax_id", tax.Type);
            Assert.Equal("12345678", tax.TaxId);
            Assert.Null(tax.CarrierId);
            await BizTest.ExecuteSqlAsync("UPDATE store_invoices SET tax_id = NULL, donation_code = N'168001' WHERE order_id = @O", ("@O", order.Id));
            var donation = (await GetOrderAsync(business, order.Id)).Invoice!;
            Assert.Equal("donation", donation.Type);
            Assert.Equal("168001", donation.DonationCode);
        }
        finally
        {
            await ShopTest.CleanupAsync(made.ProductId);
        }
    }

    [Theory]
    [InlineData("/ABC+123", "/A****23")]
    [InlineData("AB12345678901234", "AB************34")]
    [InlineData("ABCD", "***")]
    [InlineData(null, null)]
    public void 載具號碼遮罩(string? carrier, string? expected) => Assert.Equal(expected, PiiMasking.MaskCarrier(carrier));

    // ═════════════════════════ A-12 賽季管理 ═════════════════════════

    [Fact]
    public async Task 賽季管理_新增編輯刪除_代碼唯一_期間不可重疊_結束須晚於開始()
    {
        using var client = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        var code = "T" + Guid.NewGuid().ToString("N")[..6];
        Guid? id = null;
        try
        {
            // 格式與日期驗證（400，欄位鍵）
            var badCode = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seasons", new CreateAdminSeasonRequest { Code = "含 空白!", StartOn = new DateOnly(2091, 1, 1), EndOn = new DateOnly(2091, 12, 31) }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, badCode.StatusCode);
            Assert.True((await badCode.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("code", out _));
            var badRange = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seasons", new CreateAdminSeasonRequest { Code = code, StartOn = new DateOnly(2091, 6, 1), EndOn = new DateOnly(2091, 6, 1) }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, badRange.StatusCode);
            Assert.True((await badRange.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("endOn", out _));

            var created = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seasons", new CreateAdminSeasonRequest { Code = code, StartOn = new DateOnly(2091, 1, 1), EndOn = new DateOnly(2091, 12, 31) }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var dto = await created.Content.ReadFromJsonAsync<AdminSeasonDto>(TestJson.Options);
            id = dto!.Id;
            Assert.False(dto.InUse);

            // 代碼重複（不分大小寫）→ 409 code；期間重疊 → 409 startOn
            var dup = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seasons", new CreateAdminSeasonRequest { Code = code.ToLowerInvariant(), StartOn = new DateOnly(2093, 1, 1), EndOn = new DateOnly(2093, 12, 31) }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
            Assert.True((await dup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("code", out _));
            var overlap = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seasons", new CreateAdminSeasonRequest { Code = code + "x", StartOn = new DateOnly(2091, 12, 31), EndOn = new DateOnly(2092, 6, 30) }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
            Assert.True((await overlap.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("startOn", out _));

            // 緊接著的下一年不算重疊；編輯自己時不跟自己比
            var next = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seasons", new CreateAdminSeasonRequest { Code = code + "n", StartOn = new DateOnly(2092, 1, 1), EndOn = new DateOnly(2092, 12, 31) }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Created, next.StatusCode);
            var nextId = (await next.Content.ReadFromJsonAsync<AdminSeasonDto>(TestJson.Options))!.Id;
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/seasons/{nextId}")).StatusCode);

            var updated = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/seasons/{id}", new UpdateAdminSeasonRequest { Code = code, StartOn = new DateOnly(2091, 2, 1), EndOn = new DateOnly(2091, 11, 30) }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            Assert.Equal(new DateOnly(2091, 2, 1), (await updated.Content.ReadFromJsonAsync<AdminSeasonDto>(TestJson.Options))!.StartOn);

            // 清單（含舊的下拉用欄位）與詳情
            var list = await client.GetFromJsonAsync<List<AdminSeasonDto>>("/api/v1/admin/tcrfc/seasons", TestJson.Options);
            Assert.Contains(list!, s => s.Id == id && s.Code == code);
            Assert.Contains(list!, s => s.Code == "2026-27");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/admin/tcrfc/seasons/{id}")).StatusCode);
        }
        finally
        {
            if (id is Guid sid)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/seasons/{sid}");
            }
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin/tcrfc/seasons/{id}")).StatusCode);
    }

    [Fact]
    public async Task 賽季管理_被引用就不能刪_回409並說明被誰引用_俱樂部隔離與列級授權()
    {
        using var client = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        var teamId = await B1Test.TeamIdAsync("D1");
        var code = "R" + Guid.NewGuid().ToString("N")[..6];
        Guid? seasonId = null;
        Guid? playerId = null;
        try
        {
            var created = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/seasons", new CreateAdminSeasonRequest { Code = code, StartOn = new DateOnly(2095, 1, 1), EndOn = new DateOnly(2095, 12, 31) }, TestJson.WriteOptions);
            seasonId = (await created.Content.ReadFromJsonAsync<AdminSeasonDto>(TestJson.Options))!.Id;
            playerId = await CreatePlayerAsync(client, teamId, 94, "賽季引用測試");

            // 賽事、球員賽季數據引用賽季 → 不能刪
            var match = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", new CreateAdminMatchRequest
            {
                SeasonId = seasonId.Value, TeamIds = [teamId], MatchOn = new DateOnly(2095, 3, 1), Opponent = "賽季引用對手", Status = "scheduled",
            }, TestJson.WriteOptions);
            var matchId = (await match.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options))!.Id;
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/players/{playerId}/season-stats/{seasonId}",
                new SetAdminPlayerSeasonStatRequest { Appearances = 1, Goals = 0, Assists = 0, YellowCards = 0, RedCards = 0 }, TestJson.WriteOptions)).StatusCode);

            var blocked = await client.DeleteAsync($"/api/v1/admin/tcrfc/seasons/{seasonId}");
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            var message = (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!;
            Assert.Contains("賽事 1 筆", message);
            Assert.Contains("球員賽季數據 1 筆", message);
            var detail = await client.GetFromJsonAsync<AdminSeasonDto>($"/api/v1/admin/tcrfc/seasons/{seasonId}", TestJson.Options);
            Assert.True(detail!.InUse);
            Assert.Contains(detail.Usage, u => u.Label == "賽事" && u.Count == 1);

            await client.DeleteAsync($"/api/v1/admin/tcrfc/matches/{matchId}");
            await client.DeleteAsync($"/api/v1/admin/tcrfc/players/{playerId}/season-stats/{seasonId}");
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/admin/tcrfc/seasons/{seasonId}")).StatusCode);
            seasonId = null;
        }
        finally
        {
            await DeletePlayersAsync(playerId);
            if (seasonId is Guid sid)
            {
                await BizTest.ExecuteSqlAsync("DELETE FROM matches WHERE season_id = @S; DELETE FROM seasons WHERE id = @S", ("@S", sid));
            }
        }

        // 沒有 bw 授權 → 403；學院限定帳號（有 team.match.* 但只限學院）不能寫賽季 → 403；唯讀角色可看不可寫
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/bw/seasons")).StatusCode);
        using var academy = await BizTest.ClientAsync(fixture, "academy.manager@tcrfc.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await academy.PostAsJsonAsync("/api/v1/admin/bw/seasons",
            new CreateAdminSeasonRequest { Code = "ACAD1", StartOn = new DateOnly(2096, 1, 1), EndOn = new DateOnly(2096, 12, 31) }, TestJson.WriteOptions)).StatusCode);
        using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/v1/admin/tcrfc/seasons")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/v1/admin/tcrfc/seasons",
            new CreateAdminSeasonRequest { Code = "VIEW1", StartOn = new DateOnly(2096, 1, 1), EndOn = new DateOnly(2096, 12, 31) }, TestJson.WriteOptions)).StatusCode);
    }

    // ═════════════════════════ B-13 球員賽季數據手動輸入 ═════════════════════════

    [Fact]
    public async Task 球員賽季數據_手動設定與清除_清除後回到自動彙總_公開端以手動為準()
    {
        using var client = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        var teamId = await B1Test.TeamIdAsync("D1");
        var seasonId = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
        Guid? playerId = null;
        try
        {
            playerId = await CreatePlayerAsync(client, teamId, 95, "手動數據測試");
            var url = $"/api/v1/admin/tcrfc/players/{playerId}/season-stats";

            // 一開始：每個賽季一列，沒有手動、沒有自動
            var initial = await client.GetFromJsonAsync<AdminPlayerSeasonStatsDto>(url, TestJson.Options);
            var row = initial!.Items.Single(i => i.SeasonId == seasonId);
            Assert.Equal("none", row.Source);
            Assert.Null(row.Manual);

            // 範圍驗證：負數、過大 → 400，欄位鍵對應
            var bad = await client.PutAsJsonAsync($"{url}/{seasonId}", new SetAdminPlayerSeasonStatRequest { Appearances = 1, Goals = -1, Assists = 0, YellowCards = 0, RedCards = 0 }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            Assert.True((await bad.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("goals", out _));
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"{url}/{seasonId}",
                new SetAdminPlayerSeasonStatRequest { Appearances = 10000, Goals = 0, Assists = 0, YellowCards = 0, RedCards = 0 }, TestJson.WriteOptions)).StatusCode);

            // 設定 → manual；再改一次是整組取代
            var set = await client.PutAsJsonAsync($"{url}/{seasonId}", new SetAdminPlayerSeasonStatRequest { Appearances = 12, Goals = 5, Assists = 3, YellowCards = 2, RedCards = 1 }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, set.StatusCode);
            var manual = (await set.Content.ReadFromJsonAsync<AdminPlayerSeasonStatDto>(TestJson.Options))!;
            Assert.Equal("manual", manual.Source);
            Assert.Equal(5, manual.Manual!.Goals);
            Assert.Equal(3, manual.Manual.Assists);

            var publicStats = await client.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/stats/players?season=2026-27", TestJson.Options);
            var pub = publicStats.GetProperty("items").EnumerateArray().First(i => i.GetProperty("playerId").GetGuid() == playerId);
            Assert.Equal("manual", pub.GetProperty("source").GetString());
            Assert.Equal(5, pub.GetProperty("goals").GetInt32());
            Assert.Equal(3, pub.GetProperty("assists").GetInt32());

            // 清除 → 回到自動彙總（沒有賽事就是 none）；重複清除也是 204
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{url}/{seasonId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{url}/{seasonId}")).StatusCode);
            var cleared = (await client.GetFromJsonAsync<AdminPlayerSeasonStatsDto>(url, TestJson.Options))!.Items.Single(i => i.SeasonId == seasonId);
            Assert.Equal("none", cleared.Source);
            var publicAfter = await client.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/stats/players?season=2026-27", TestJson.Options);
            Assert.DoesNotContain(publicAfter.GetProperty("items").EnumerateArray(), i => i.GetProperty("playerId").GetGuid() == playerId);

            // 不存在的球員／賽季 → 404；跨俱樂部的球員 → 404
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin/tcrfc/players/{Guid.NewGuid()}/season-stats")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{url}/{Guid.NewGuid()}",
                new SetAdminPlayerSeasonStatRequest { Appearances = 1, Goals = 0, Assists = 0, YellowCards = 0, RedCards = 0 }, TestJson.WriteOptions)).StatusCode);

            // 權限：唯讀角色可看不可改
            using var viewer = await BizTest.ClientAsync(fixture, "viewer@tcrfc.test");
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync($"{url}/{seasonId}",
                new SetAdminPlayerSeasonStatRequest { Appearances = 1, Goals = 0, Assists = 0, YellowCards = 0, RedCards = 0 }, TestJson.WriteOptions)).StatusCode);
        }
        finally
        {
            await DeletePlayersAsync(playerId);
        }
    }

    // ═════════════════════════ B-12 草稿賽事系列名稱不出現在公開賽程 ═════════════════════════

    [Fact]
    public async Task 公開賽程_草稿賽事系列的名稱與代碼不輸出_發布後才輸出()
    {
        using var client = await BizTest.ClientAsync(fixture, "team.manager@tcrfc.test");
        var teamId = await B1Test.TeamIdAsync("D1");
        var seasonId = await B1Test.SeasonIdAsync("tcrfc", "2026-27");
        var compCode = "CD" + Guid.NewGuid().ToString("N")[..6];
        Guid? competitionId = null, matchId = null;
        try
        {
            competitionId = Guid.NewGuid();
            var clubId = await B1Test.ClubIdAsync("tcrfc");
            await BizTest.ExecuteSqlAsync("""
                INSERT INTO competitions (id, club_id, season_id, code, status) VALUES (@C, @Club, @S, @Code, 'draft');
                INSERT INTO competitions_i18n (competition_id, locale, name) VALUES (@C, N'zh-Hant', N'草稿賽事名稱測試');
                """, ("@C", competitionId), ("@Club", clubId), ("@S", seasonId), ("@Code", compCode));
            var match = await client.PostAsJsonAsync("/api/v1/admin/tcrfc/matches", new CreateAdminMatchRequest
            {
                SeasonId = seasonId, TeamIds = [teamId], CompetitionId = competitionId, MatchOn = new DateOnly(2090, 6, 1), Opponent = "草稿賽事對手", Status = "scheduled",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Created, match.StatusCode);
            matchId = (await match.Content.ReadFromJsonAsync<AdminMatchDetailDto>(TestJson.Options))!.Id;

            JsonElement Find(JsonElement page) => page.GetProperty("items").EnumerateArray().First(i => i.GetProperty("id").GetGuid() == matchId);
            var draft = Find(await client.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/schedule?team=D1&season=2026-27&pageSize=200&to=2090-12-31", TestJson.Options));
            Assert.DoesNotContain("草稿賽事名稱測試", draft.GetRawText());
            Assert.DoesNotContain(compCode, draft.GetRawText());

            await BizTest.ExecuteSqlAsync("UPDATE competitions SET status = 'published' WHERE id = @C", ("@C", competitionId));
            var published = Find(await client.GetFromJsonAsync<JsonElement>("/api/v1/tcrfc/schedule?team=D1&season=2026-27&pageSize=200&to=2090-12-31", TestJson.Options));
            Assert.Contains("草稿賽事名稱測試", published.GetRawText());
        }
        finally
        {
            if (matchId is Guid m)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/matches/{m}");
            }

            await BizTest.ExecuteSqlAsync("DELETE FROM competitions_i18n WHERE competition_id = @C; DELETE FROM competitions WHERE id = @C", ("@C", competitionId));
        }
    }

    // ═════════════════════════ E-1 追蹤碼格式、E-2 行事曆連結 ═════════════════════════

    [Theory]
    [InlineData("ga4MeasurementId", "G-AB12CD34'); alert(1);//")]
    [InlineData("ga4MeasurementId", "UA-12345-1")]
    [InlineData("gtmContainerId", "GTM-AB12</script>")]
    [InlineData("gtmContainerId", "G-ABCD123")]
    [InlineData("metaPixelId", "123abc")]
    [InlineData("metaPixelId", "1234567890123456'+x")]
    [InlineData("lineTagId", "abc def")]
    [InlineData("lineTagId", "a';alert(1)//")]
    public async Task 追蹤碼_格式不符回400並標在對應欄位(string field, string value)
    {
        using var client = await BizTest.ClientAsync(fixture, "super.admin@tcrfc.test");
        var payload = new Dictionary<string, object?>
        {
            ["titleTemplateZh"] = "{title}｜測試", ["defaultDescriptionZh"] = "測試描述", [field] = value,
        };
        using var form = new MultipartFormDataContent();
        var json = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8);
        json.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(json, "payload");
        var response = await client.PutAsync("/api/v1/admin/tcrfc/seo/settings", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task 行事曆活動連結_只接受http_https或站內路徑()
    {
        using var client = await BizTest.ClientAsync(fixture, "content.editor@tcrfc.test");
        CreateAdminCalendarCustomEventRequest Req(string? url) => new()
        {
            StartsAt = new DateTime(2090, 10, 15, 10, 0, 0, DateTimeKind.Utc), IsAllDay = false, IsPublic = true, CtaUrl = url,
            Content = new AdminCalendarEventContentInput { Zh = new AdminCalendarEventLocaleContent { Title = "連結驗證測試" } },
        };

        foreach (var bad in new[] { "javascript:alert(1)", "data:text/html,x", "//evil.example", "ftp://x.example/a", "evil.example/page" })
        {
            var response = await client.PostAsync("/api/v1/admin/tcrfc/calendar/custom-events", AdminArticleMultipart.Build(Req(bad)));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("ctaUrl", out _));
        }

        foreach (var ok in new[] { "https://example.com/register", "http://example.com", "/zh/schedule" })
        {
            var response = await client.PostAsync("/api/v1/admin/tcrfc/calendar/custom-events", AdminArticleMultipart.Build(Req(ok)));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<AdminCalendarCustomEventDetailDto>(TestJson.Options);
            Assert.Equal(ok, created!.CtaUrl);
            await client.DeleteAsync($"/api/v1/admin/tcrfc/calendar/custom-events/{created.Id}");
        }
    }

    // ═════════════════════════ B-22 後台寫入後失效公開快取 ═════════════════════════

    private sealed class RecordingCache : IQueryCache
    {
        private readonly List<string> _invalidated = [];
        public IReadOnlyList<string> Invalidated { get { lock (_invalidated) { return _invalidated.ToList(); } } }

        public Task<T> GetOrCreateAsync<T>(string entity, string club, string locale, string qualifier, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
            => factory(cancellationToken);

        public Task InvalidateAsync(string entity, string club, CancellationToken cancellationToken)
        {
            lock (_invalidated)
            {
                _invalidated.Add($"{entity}:{club}");
            }

            return Task.CompletedTask;
        }
    }

    private WebApplicationFactory<Program> WithRecordingCache(RecordingCache cache) => fixture.WithWebHostBuilder(b => b.ConfigureServices(s =>
    {
        s.RemoveAll<IQueryCache>();
        s.AddSingleton<IQueryCache>(cache);
    }));

    [Fact]
    public async Task 快取失效_表單_SEO設定_網站事實_球隊改名()
    {
        var cache = new RecordingCache();
        using var factory = WithRecordingCache(cache);
        using var admin = await BizTest.ClientAsync(factory, "super.admin@tcrfc.test");

        // 表單：寫回同值
        var formId = await FormIdAsync(admin, FormCatalog.DonationEnquiry);
        var form = await admin.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        await RestoreFormAsync(admin, formId, form!);
        Assert.Contains("forms:tcrfc", cache.Invalidated);

        // SEO 設定：寫回同值
        var seo = await admin.GetFromJsonAsync<AdminSeoSettingsDto>("/api/v1/admin/tcrfc/seo/settings", TestJson.Options);
        var seoResponse = await admin.PutAsync("/api/v1/admin/tcrfc/seo/settings", AdminArticleMultipart.Build(new UpdateSeoSettingsRequest
        {
            TitleTemplateZh = seo!.TitleTemplateZh, TitleTemplateEn = seo.TitleTemplateEn, DefaultDescriptionZh = seo.DefaultDescriptionZh, DefaultDescriptionEn = seo.DefaultDescriptionEn,
            RobotsCustomRules = seo.RobotsCustomRules, Ga4MeasurementId = seo.Ga4MeasurementId, GtmContainerId = seo.GtmContainerId, MetaPixelId = seo.MetaPixelId, LineTagId = seo.LineTagId,
        }));
        Assert.Equal(HttpStatusCode.OK, seoResponse.StatusCode);
        Assert.Contains("seo-settings:tcrfc", cache.Invalidated);

        // 網站事實：寫回同值
        var facts = await admin.GetFromJsonAsync<AdminSiteFactsDto>("/api/v1/admin/tcrfc/site-facts", TestJson.Options);
        var factsResponse = await admin.PutAsJsonAsync("/api/v1/admin/tcrfc/site-facts", new UpdateSiteFactsRequest
        {
            FoundedYear = facts!.FoundedYear, FoundingDateIso = facts.FoundingDateIso, FoundingDateDisplayZh = facts.FoundingDateDisplayZh, FoundingDateDisplayEn = facts.FoundingDateDisplayEn,
            FoundingTitleZh = facts.FoundingTitleZh, FoundingTitleEn = facts.FoundingTitleEn, LeagueNameZh = facts.LeagueNameZh, LeagueNameEn = facts.LeagueNameEn,
            LeagueShortNameZh = facts.LeagueShortNameZh, LeagueShortNameEn = facts.LeagueShortNameEn, SquadStructureZh = facts.SquadStructureZh, SquadStructureEn = facts.SquadStructureEn,
            SquadCodes = facts.SquadCodes,
            HomeVenues = facts.HomeVenues.Select(v => new UpdateSiteFactVenueRequest { Id = v.Id, NameZh = v.NameZh, NameEn = v.NameEn, Address = v.Address }).ToList(),
            ContactPhone = facts.ContactPhone, ContactHoursZh = facts.ContactHoursZh, ContactHoursEn = facts.ContactHoursEn, BlueWhaleSiteUrl = facts.BlueWhaleSiteUrl,
        }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.OK, factsResponse.StatusCode);
        Assert.Contains("site-facts:tcrfc", cache.Invalidated);

        // 球隊改名：players／staff／schedule／honors 一併失效
        var code = "Z" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        try
        {
            var create = await admin.PostAsync("/api/v1/admin/tcrfc/teams", AdminArticleMultipart.Build(new CreateAdminTeamRequest
            {
                Code = code, Type = "academy", Gender = "men", Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = "快取測試梯隊" } },
            }));
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            var team = (await create.Content.ReadFromJsonAsync<AdminTeamDetailDto>(TestJson.Options))!;
            var update = await admin.PutAsync($"/api/v1/admin/tcrfc/teams/{team.Id}", AdminArticleMultipart.Build(new UpdateAdminTeamRequest
            {
                Code = code, Type = "academy", Gender = "men", Content = new AdminTeamContentInput { Zh = new AdminTeamLocaleContent { Name = "快取測試梯隊（改名）" } },
            }));
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            foreach (var entity in new[] { "teams", "players", "staff", "schedule", "honors" })
            {
                Assert.Contains($"{entity}:tcrfc", cache.Invalidated);
            }
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("DELETE FROM teams_i18n WHERE team_id IN (SELECT id FROM teams WHERE code = @C); DELETE FROM teams WHERE code = @C", ("@C", code));
        }
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private static async Task<AdminOrderDetailDto> GetOrderAsync(HttpClient client, Guid id)
        => await BizTest.ReadAsync<AdminOrderDetailDto>(await client.GetAsync($"/api/v1/admin/tcrfc/shop/orders/{id}"));

    private static async Task<Guid> FormIdAsync(HttpClient admin, string formCode)
    {
        var forms = await admin.GetFromJsonAsync<List<AdminFormListItemDto>>("/api/v1/admin/tcrfc/forms", TestJson.Options);
        return forms!.First(f => f.FormCode == formCode).Id;
    }

    /// <summary>把表單設定寫回原值（只含設定，不動欄位）。</summary>
    private static async Task RestoreFormAsync(HttpClient admin, Guid formId, AdminFormDetailDto original)
        => Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}", new UpdateAdminFormRequest
        {
            NotifyEmails = original.NotifyEmails, CaptchaEnabled = original.CaptchaEnabled, RedirectPath = original.RedirectPath,
            AutoReplyBodyZh = original.AutoReplyBodyZh, AutoReplyBodyEn = original.AutoReplyBodyEn,
        }, TestJson.WriteOptions)).StatusCode);

    private static async Task<Guid> CreatePlayerAsync(HttpClient client, Guid teamId, int shirtNo, string name)
    {
        var response = await client.PostAsync("/api/v1/admin/tcrfc/players", AdminArticleMultipart.Build(new CreateAdminPlayerRequest
        {
            TeamId = teamId, ShirtNo = shirtNo, Status = "active",
            Content = new AdminPlayerContentInput { Zh = new AdminPlayerLocaleContent { Name = name } },
        }));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AdminPlayerDetailDto>(TestJson.Options))!.Id;
    }

    private static async Task DeletePlayersAsync(params Guid?[] ids)
    {
        foreach (var id in ids.Where(i => i is not null))
        {
            // 比賽進球／牌／名單、賽季數據都掛在球員底下；先清這些再刪球員。
            await BizTest.ExecuteSqlAsync("""
                DELETE FROM match_goals WHERE player_id = @Id;
                DELETE FROM match_cards WHERE player_id = @Id;
                DELETE FROM match_lineups WHERE player_id = @Id;
                DELETE FROM player_season_stats WHERE player_id = @Id;
                DELETE FROM players_i18n WHERE player_id = @Id;
                DELETE FROM players WHERE id = @Id;
                """, ("@Id", id));
        }
    }
}
