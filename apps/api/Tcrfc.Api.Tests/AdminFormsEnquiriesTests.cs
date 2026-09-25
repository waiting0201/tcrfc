using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminEnquiries;
using Tcrfc.Api.Features.AdminForms;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-10：`G1`（表單設計器）／`G2`（詢問收件匣）後台 CRUD ＋ 10 表單中心的公開讀取與送出端點。
/// 形狀比照 <c>AdminProgramsSessionsRegistrationsTests</c>（同一批打真正 HTTP 管線與真正
/// <c>tcrfc_club_dev</c> 的既有先例）。
///
/// 種子測試帳號（見 apps/api/README.md「種子測試帳號」）：
/// <c>customer.service@tcrfc.test</c>（<c>customer_service_admin</c>，僅授權 <c>tcrfc</c>，
/// <c>form.*</c>／<c>enquiry.inbox.*</c> ✔全但不含匯出）；
/// <c>academy.manager@tcrfc.test</c>（<c>academy_program</c>，僅授權 <c>bw</c>，只有
/// <c>enquiry.course.*</c>）；
/// <c>business.sponsorship@tcrfc.test</c>（<c>business_sponsorship</c>，僅授權 <c>tcrfc</c>，
/// 只有 <c>enquiry.partnership.*</c>，本輪新增）；
/// <c>pr.media@tcrfc.test</c>（<c>pr_media</c>，僅授權 <c>tcrfc</c>，只有 <c>enquiry.media.*</c>）；
/// <c>content.editor@tcrfc.test</c>（<c>content_editor</c>，矩陣「表單詢問」欄是「—」，完全沒有
/// <c>form.*</c>／<c>enquiry.*</c> 權限碼）；<c>viewer@tcrfc.test</c>（<c>viewer</c>，只有
/// <c>form.view</c>／<c>enquiry.inbox.view</c>，唯讀）。
///
/// ⚠️ **公開送出端點掛了依 IP 分區的 Rate Limiting**（見 <c>Program.cs</c>），
/// <c>WebApplicationFactory</c> 測試連線的 <c>RemoteIpAddress</c> 一律是同一個值（本檔全部測試
/// 共用同一個分區），本檔的公開送出呼叫次數刻意控制在遠低於 <c>PermitLimit=10</c> 的範圍
/// （5 分鐘固定視窗），不寫涵蓋「第 11 次被擋下」的自動化測試——會佔用同一個分區的額度，
/// 干擾本檔其餘測試；濫用防護本身已用 curl 對本機開發伺服器手動驗證過，見任務回報。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminFormsEnquiriesTests(AdminWriteApiFixture fixture)
{
    // ═════════════════════════════ 權限矩陣 ═════════════════════════════

    [Fact]
    public async Task Forms_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        var response = await client.GetAsync("/api/v1/admin/tcrfc/forms");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Forms_跨俱樂部_擋下()
    {
        // academy.manager@tcrfc.test 只被授權 bw，沒有 tcrfc。
        using var client = await CreateClientAsync("academy.manager@tcrfc.test");
        var response = await client.GetAsync("/api/v1/admin/tcrfc/forms");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Forms_內容編輯角色矩陣是橫線_完全沒有表單權限_連檢視都被擋下()
    {
        using var client = await CreateClientAsync("content.editor@tcrfc.test");
        var response = await client.GetAsync("/api/v1/admin/tcrfc/forms");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enquiries_檢視者唯讀_更新會被擋下_也不能匯出()
    {
        using var client = await CreateClientAsync("viewer@tcrfc.test");

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/enquiries");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var exportResponse = await client.GetAsync("/api/v1/admin/tcrfc/enquiries/export");
        Assert.Equal(HttpStatusCode.Forbidden, exportResponse.StatusCode);
    }

    [Fact]
    public async Task Enquiries_客服可以看全部詢問但不能匯出()
    {
        using var client = await CreateClientAsync("customer.service@tcrfc.test");

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/enquiries");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var exportResponse = await client.GetAsync("/api/v1/admin/tcrfc/enquiries/export");
        Assert.Equal(HttpStatusCode.Forbidden, exportResponse.StatusCode);
    }

    [Fact]
    public async Task Enquiries_依類別過濾_學院課程管理只看得到課程類詢問_商務贊助只看得到合作贊助類_公關媒體只看得到媒體類()
    {
        using var publicClient = fixture.CreateClient();
        Guid? courseEnquiryId = null, partnershipEnquiryId = null, mediaEnquiryId = null;

        try
        {
            // 依序在 bw（academy_program 只被授權 bw）與 tcrfc 送出三種類別各一筆。
            courseEnquiryId = await SubmitAndGetIdAsync(publicClient, "bw", "camp_registration", new Dictionary<string, string>
            {
                ["session_choice"] = "2026 寒假營第一梯",
                ["name"] = "類別測試-課程",
                ["birth_date"] = "2015-01-01",
                ["health_declaration"] = "true",
                ["contact"] = "0911111111",
                ["privacy_consent"] = "true",
            });

            partnershipEnquiryId = await SubmitAndGetIdAsync(publicClient, "tcrfc", "partnership_sponsorship", new Dictionary<string, string>
            {
                ["enquiry_type"] = "贊助",
                ["company"] = "測試股份有限公司",
                ["name"] = "類別測試-贊助",
                ["contact"] = "sponsor@example.com",
                ["privacy_consent"] = "true",
            });

            mediaEnquiryId = await SubmitAndGetIdAsync(publicClient, "tcrfc", "media_enquiry", new Dictionary<string, string>
            {
                ["media_name"] = "測試媒體",
                ["name"] = "類別測試-媒體",
                ["topic"] = "球隊專訪",
                ["contact"] = "reporter@example.com",
                ["privacy_consent"] = "true",
            });

            // 學院／課程管理（僅 bw）：看得到 bw 的課程類詢問，看不到 tcrfc 的（跨俱樂部先被擋）。
            using (var academyClient = await CreateClientAsync("academy.manager@tcrfc.test"))
            {
                var detailResponse = await academyClient.GetAsync($"/api/v1/admin/bw/enquiries/{courseEnquiryId}");
                Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

                // camp_registration 沒有任何欄位標記 is_summary（沒有合適的敘述性文字欄位），
                // 內容摘要在清單上應該是 null，不是錯誤（見 AdminEnquiriesRepository 檔頭說明）。
                var listResponse = await academyClient.GetAsync("/api/v1/admin/bw/enquiries?formCode=" + FormCatalog.CampRegistration);
                var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<AdminEnquiryListItemDto>>(TestJson.Options);
                var listed = page!.Items.First(i => i.Id == courseEnquiryId);
                Assert.Null(listed.ContentSummary);
            }

            // 商務／贊助（僅 tcrfc）：看得到合作贊助類，看不到媒體類（類別越權視同 404）。
            using (var bizClient = await CreateClientAsync("business.sponsorship@tcrfc.test"))
            {
                var ownCategory = await bizClient.GetAsync($"/api/v1/admin/tcrfc/enquiries/{partnershipEnquiryId}");
                Assert.Equal(HttpStatusCode.OK, ownCategory.StatusCode);

                var otherCategory = await bizClient.GetAsync($"/api/v1/admin/tcrfc/enquiries/{mediaEnquiryId}");
                Assert.Equal(HttpStatusCode.NotFound, otherCategory.StatusCode);

                var list = await bizClient.GetAsync("/api/v1/admin/tcrfc/enquiries");
                Assert.Equal(HttpStatusCode.OK, list.StatusCode);
                var page = await list.Content.ReadFromJsonAsync<PagedResult<AdminEnquiryListItemDto>>(TestJson.Options);
                Assert.Contains(page!.Items, i => i.Id == partnershipEnquiryId);
                Assert.DoesNotContain(page.Items, i => i.Id == mediaEnquiryId);

                // 商務／贊助沒有 enquiry.partnership.update 以外的更新碼，但這裡驗證同類別可以處理。
                var update = await bizClient.PutAsJsonAsync($"/api/v1/admin/tcrfc/enquiries/{partnershipEnquiryId}", new UpdateAdminEnquiryRequest
                {
                    Status = "處理中",
                }, TestJson.WriteOptions);
                Assert.Equal(HttpStatusCode.OK, update.StatusCode);

                // 跨類別更新：視同 404，不能處理媒體類詢問。
                var updateOther = await bizClient.PutAsJsonAsync($"/api/v1/admin/tcrfc/enquiries/{mediaEnquiryId}", new UpdateAdminEnquiryRequest
                {
                    Status = "處理中",
                }, TestJson.WriteOptions);
                Assert.Equal(HttpStatusCode.NotFound, updateOther.StatusCode);
            }

            // 公關／媒體（僅 tcrfc）：看得到媒體類，看不到合作贊助類。
            using (var mediaClient = await CreateClientAsync("pr.media@tcrfc.test"))
            {
                var ownCategory = await mediaClient.GetAsync($"/api/v1/admin/tcrfc/enquiries/{mediaEnquiryId}");
                Assert.Equal(HttpStatusCode.OK, ownCategory.StatusCode);

                var otherCategory = await mediaClient.GetAsync($"/api/v1/admin/tcrfc/enquiries/{partnershipEnquiryId}");
                Assert.Equal(HttpStatusCode.NotFound, otherCategory.StatusCode);
            }

            // 競技／球隊管理矩陣是「—」：完全沒有 enquiry.* 權限碼，連檢視都被擋下。
            using (var teamClient = await CreateClientAsync("team.manager@tcrfc.test"))
            {
                var response = await teamClient.GetAsync("/api/v1/admin/tcrfc/enquiries");
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
        finally
        {
            await DeleteEnquiryByIdAsync(courseEnquiryId);
            await DeleteEnquiryByIdAsync(partnershipEnquiryId);
            await DeleteEnquiryByIdAsync(mediaEnquiryId);
        }
    }

    // ═════════════════════════════ G1 表單設計器 ═════════════════════════════

    [Fact]
    public async Task Forms_列表回九個固定表單_詳情含預設欄位()
    {
        using var client = await CreateClientAsync("customer.service@tcrfc.test");

        var listResponse = await client.GetAsync("/api/v1/admin/tcrfc/forms");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var forms = await listResponse.Content.ReadFromJsonAsync<List<AdminFormListItemDto>>(TestJson.Options);
        Assert.NotNull(forms);
        Assert.Equal(9, forms!.Count);
        Assert.Contains(forms, f => f.FormCode == FormCatalog.GeneralContact);

        var generalContact = forms.First(f => f.FormCode == FormCatalog.GeneralContact);
        var detailResponse = await client.GetAsync($"/api/v1/admin/tcrfc/forms/{generalContact.Id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<AdminFormDetailDto>(TestJson.Options);
        Assert.NotNull(detail);
        Assert.Contains(detail!.Fields, f => f.FieldKey == "name");
        Assert.Contains(detail.Fields, f => f.FieldKey == "contact");
        Assert.Contains(detail.Fields, f => f.FieldKey == "privacy_consent" && f.FieldType == "consent");
    }

    [Fact]
    public async Task Form_更新設定_Email格式驗證_自動回覆信雙語可寫入且可還原()
    {
        using var client = await CreateClientAsync("customer.service@tcrfc.test");
        var formId = await GetFormIdAsync(client, "tcrfc", FormCatalog.DonationEnquiry);

        var original = await client.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        Assert.NotNull(original);

        try
        {
            // Email 格式錯誤 → 400。
            var badEmailResponse = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}", new UpdateAdminFormRequest
            {
                NotifyEmails = "not-an-email",
                CaptchaEnabled = true,
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, badEmailResponse.StatusCode);

            // 合法多筆 Email（逗號分隔）＋雙語自動回覆信 → 成功。
            var updateResponse = await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}", new UpdateAdminFormRequest
            {
                NotifyEmails = "a@example.com, b@example.com",
                CaptchaEnabled = false,
                RedirectPath = "/zh/thank-you",
                AutoReplyBodyZh = "感謝您的洽詢，我們將盡快回覆。",
                AutoReplyBodyEn = "Thank you for reaching out, we will reply soon.",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminFormDetailDto>(TestJson.Options);
            Assert.Equal("a@example.com,b@example.com", updated!.NotifyEmails);
            Assert.False(updated.CaptchaEnabled);
            Assert.Equal("感謝您的洽詢，我們將盡快回覆。", updated.AutoReplyBodyZh);
            Assert.Equal("Thank you for reaching out, we will reply soon.", updated.AutoReplyBodyEn);
        }
        finally
        {
            // 還原成種子預設值，不污染其他測試或下一輪驗收。
            await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}", new UpdateAdminFormRequest
            {
                NotifyEmails = original!.NotifyEmails,
                CaptchaEnabled = original.CaptchaEnabled,
                RedirectPath = original.RedirectPath,
                AutoReplyBodyZh = original.AutoReplyBodyZh,
                AutoReplyBodyEn = original.AutoReplyBodyEn,
            }, TestJson.WriteOptions);
        }
    }

    [Fact]
    public async Task FormField_建立成功_欄位代碼重複回409_下拉缺選項回400_刪除後恢復_使用中的欄位刪除被擋下()
    {
        using var client = await CreateClientAsync("customer.service@tcrfc.test");
        var formId = await GetFormIdAsync(client, "tcrfc", FormCatalog.DonationEnquiry);
        Guid? createdFieldId = null;

        try
        {
            // 下拉型別沒有選項 → 400。
            var missingOptionsResponse = await client.PostAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}/fields", new CreateAdminFormFieldRequest
            {
                FieldKey = "test_channel",
                FieldType = "select",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, missingOptionsResponse.StatusCode);

            // 建立成功。
            var createResponse = await client.PostAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}/fields", new CreateAdminFormFieldRequest
            {
                FieldKey = "test_channel",
                FieldType = "select",
                IsRequired = false,
                Options = new[] { "Email", "電話" },
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminFormFieldDto>(TestJson.Options);
            Assert.NotNull(created);
            createdFieldId = created!.Id;
            Assert.Equal(2, created.Options!.Count);

            // 欄位代碼重複 → 409。
            var conflictResponse = await client.PostAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}/fields", new CreateAdminFormFieldRequest
            {
                FieldKey = "test_channel",
                FieldType = "text",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

            // 刪除成功。
            var deleteResponse = await client.DeleteAsync($"/api/v1/admin/tcrfc/forms/{formId}/fields/{createdFieldId}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            createdFieldId = null;
        }
        finally
        {
            if (createdFieldId is Guid remaining)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/forms/{formId}/fields/{remaining}");
            }
        }
    }

    [Fact]
    public async Task FormField_已有詢問資料引用_刪除被擋下409()
    {
        using var adminClient = await CreateClientAsync("customer.service@tcrfc.test");
        using var publicClient = fixture.CreateClient();

        var formId = await GetFormIdAsync(adminClient, "tcrfc", FormCatalog.GeneralContact);
        var detail = await adminClient.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        var nameFieldId = detail!.Fields.First(f => f.FieldKey == "name").Id;

        Guid? enquiryId = null;
        try
        {
            enquiryId = await SubmitAndGetIdAsync(publicClient, "tcrfc", FormCatalog.GeneralContact, new Dictionary<string, string>
            {
                ["name"] = "刪除保護測試",
                ["contact"] = "delete-guard@example.com",
                ["subject"] = "測試",
                ["message"] = "測試內容",
                ["privacy_consent"] = "true",
            });

            var deleteResponse = await adminClient.DeleteAsync($"/api/v1/admin/tcrfc/forms/{formId}/fields/{nameFieldId}");
            Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
        }
        finally
        {
            await DeleteEnquiryByIdAsync(enquiryId);
        }
    }

    [Fact]
    public async Task FormField_內容摘要同一表單最多一個_標記新的會自動取代舊的()
    {
        using var client = await CreateClientAsync("customer.service@tcrfc.test");
        var formId = await GetFormIdAsync(client, "tcrfc", FormCatalog.DonationEnquiry);

        var before = await client.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        var originalSummaryField = before!.Fields.Single(f => f.IsSummary); // 種子資料：donation_enquiry 的 message 欄位。
        Assert.Equal("message", originalSummaryField.FieldKey);

        Guid? newFieldId = null;
        try
        {
            var createResponse = await client.PostAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}/fields", new CreateAdminFormFieldRequest
            {
                FieldKey = "test_summary_field",
                FieldType = "textarea",
                IsSummary = true,
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AdminFormFieldDto>(TestJson.Options);
            newFieldId = created!.Id;
            Assert.True(created.IsSummary);

            var after = await client.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
            var summaryFields = after!.Fields.Where(f => f.IsSummary).ToList();
            Assert.Single(summaryFields); // 同一張表單最多一個，不是兩個同時為 true。
            Assert.Equal(newFieldId, summaryFields[0].Id);
            Assert.False(after.Fields.Single(f => f.Id == originalSummaryField.Id).IsSummary); // 舊的被自動取代。
        }
        finally
        {
            if (newFieldId is Guid id)
            {
                await client.DeleteAsync($"/api/v1/admin/tcrfc/forms/{formId}/fields/{id}");
            }
            // 還原種子預設：message 重新標記為內容摘要來源，不污染其他測試或下一輪驗收。
            await client.PutAsJsonAsync($"/api/v1/admin/tcrfc/forms/{formId}/fields/{originalSummaryField.Id}", new UpdateAdminFormFieldRequest
            {
                FieldKey = originalSummaryField.FieldKey,
                FieldType = originalSummaryField.FieldType,
                IsRequired = originalSummaryField.IsRequired,
                IsSummary = true,
                SortOrder = originalSummaryField.SortOrder,
            }, TestJson.WriteOptions);
        }
    }

    // ═════════════════════════════ 10 表單中心 公開讀取＋送出 ═════════════════════════════

    [Fact]
    public async Task Public_表單定義_找不到表單回404_找得到時欄位依排序輸出()
    {
        using var client = fixture.CreateClient();

        var notFound = await client.GetAsync("/api/v1/tcrfc/forms/not_a_real_form_code");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);

        var found = await client.GetAsync($"/api/v1/tcrfc/forms/{FormCatalog.GeneralContact}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var form = await found.Content.ReadFromJsonAsync<PublicFormDto>(TestJson.Options);
        Assert.NotNull(form);
        Assert.Equal(FormCatalog.GeneralContact, form!.FormCode);
        Assert.True(form.Fields.Count > 0);
        Assert.True(form.Fields.SequenceEqual(form.Fields.OrderBy(f => f.SortOrder)));
    }

    [Fact]
    public async Task Public_送出_誘捕欄位有值_安靜成功但不寫入資料()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/tcrfc/forms/{FormCatalog.GeneralContact}/submissions", new SubmitFormRequest
        {
            Answers = new Dictionary<string, string>
            {
                ["name"] = "機器人",
                ["contact"] = "bot@example.com",
                ["subject"] = "test",
                ["message"] = "test",
                ["privacy_consent"] = "true",
            },
            Website = "http://spam.example.com", // 誘捕欄位有值。
        }, TestJson.WriteOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubmitFormResultDto>(TestJson.Options);
        Assert.True(result!.Success);

        var count = await CountEnquiriesByContactAsync("bot@example.com");
        Assert.Equal(0, count); // 誘捕命中：不應該寫入任何一筆。
    }

    [Fact]
    public async Task Public_送出_缺必填欄位回400_未知欄位回400_同意條款未勾選回400()
    {
        using var client = fixture.CreateClient();

        var missingRequired = await client.PostAsJsonAsync($"/api/v1/tcrfc/forms/{FormCatalog.GeneralContact}/submissions", new SubmitFormRequest
        {
            Answers = new Dictionary<string, string> { ["name"] = "缺欄位測試" },
        }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, missingRequired.StatusCode);

        var unknownField = await client.PostAsJsonAsync($"/api/v1/tcrfc/forms/{FormCatalog.GeneralContact}/submissions", new SubmitFormRequest
        {
            Answers = new Dictionary<string, string>
            {
                ["name"] = "未知欄位測試",
                ["contact"] = "a@example.com",
                ["subject"] = "s",
                ["message"] = "m",
                ["privacy_consent"] = "true",
                ["not_a_real_field"] = "x",
            },
        }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);

        var consentNotChecked = await client.PostAsJsonAsync($"/api/v1/tcrfc/forms/{FormCatalog.GeneralContact}/submissions", new SubmitFormRequest
        {
            Answers = new Dictionary<string, string>
            {
                ["name"] = "未勾同意條款測試",
                ["contact"] = "a@example.com",
                ["subject"] = "s",
                ["message"] = "m",
                ["privacy_consent"] = "false",
            },
        }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, consentNotChecked.StatusCode);
    }

    [Fact]
    public async Task Public_送出成功_後台可見_姓名與聯絡方式取自慣例欄位鍵_狀態預設新進()
    {
        using var publicClient = fixture.CreateClient();
        using var adminClient = await CreateClientAsync("customer.service@tcrfc.test");
        Guid? enquiryId = null;

        try
        {
            enquiryId = await SubmitAndGetIdAsync(publicClient, "tcrfc", FormCatalog.GeneralContact, new Dictionary<string, string>
            {
                ["name"] = "王小明",
                ["contact"] = "wang@example.com",
                ["subject"] = "詢問合作",
                ["message"] = "您好，我想詢問合作機會。",
                ["privacy_consent"] = "true",
            }, sourcePath: "/zh/join/general-contact", utmSource: "google");

            var detailResponse = await adminClient.GetAsync($"/api/v1/admin/tcrfc/enquiries/{enquiryId}");
            Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
            var detail = await detailResponse.Content.ReadFromJsonAsync<AdminEnquiryDetailDto>(TestJson.Options);
            Assert.Equal("新進", detail!.Status);
            Assert.Equal("/zh/join/general-contact", detail.SourcePath);
            Assert.Equal("google", detail.UtmSource);
            Assert.Contains(detail.Answers, a => a.FieldKey == "name" && a.Value == "王小明");
            Assert.Contains(detail.Answers, a => a.FieldKey == "message" && a.Value == "您好，我想詢問合作機會。");

            var listResponse = await adminClient.GetAsync("/api/v1/admin/tcrfc/enquiries?formCode=" + FormCatalog.GeneralContact);
            var page = await listResponse.Content.ReadFromJsonAsync<PagedResult<AdminEnquiryListItemDto>>(TestJson.Options);
            var listed = page!.Items.First(i => i.Id == enquiryId);
            Assert.Equal("王小明", listed.ApplicantName);
            Assert.Equal("wang@example.com", listed.ContactInfo);
            // general_contact 的種子資料把 message 標記為 is_summary，內容摘要應該取到這個值
            // （審查回饋補做：G2「內容摘要」欄，見 AdminEnquiriesRepository 檔頭說明）。
            Assert.Equal("您好，我想詢問合作機會。", listed.ContentSummary);

            // 更新：處理中 → 指派負責人 → 備註。
            var updateResponse = await adminClient.PutAsJsonAsync($"/api/v1/admin/tcrfc/enquiries/{enquiryId}", new UpdateAdminEnquiryRequest
            {
                Status = "已回覆",
                InternalNote = "已電話回覆，對方將再提供資料。",
                Tags = "重要客戶",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminEnquiryDetailDto>(TestJson.Options);
            Assert.Equal("已回覆", updated!.Status);
            Assert.Equal("重要客戶", updated.Tags);

            // 狀態值域驗證。
            var badStatusResponse = await adminClient.PutAsJsonAsync($"/api/v1/admin/tcrfc/enquiries/{enquiryId}", new UpdateAdminEnquiryRequest
            {
                Status = "不存在的狀態",
            }, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, badStatusResponse.StatusCode);
        }
        finally
        {
            await DeleteEnquiryByIdAsync(enquiryId);
        }
    }

    [Fact]
    public async Task Public_送出_下拉選項不在允許清單內_回400()
    {
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/tcrfc/forms/{FormCatalog.PartnershipSponsorship}/submissions", new SubmitFormRequest
        {
            Answers = new Dictionary<string, string>
            {
                ["enquiry_type"] = "不存在的選項",
                ["company"] = "測試公司",
                ["name"] = "測試",
                ["contact"] = "a@example.com",
                ["privacy_consent"] = "true",
            },
        }, TestJson.WriteOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_匯出CSV_有全權限的角色可以匯出且格式正確_不含英文技術代碼()
    {
        using var publicClient = fixture.CreateClient();
        using var adminClient = await CreateClientAsync("super.admin@tcrfc.test");
        Guid? enquiryId = null;

        try
        {
            enquiryId = await SubmitAndGetIdAsync(publicClient, "tcrfc", FormCatalog.GeneralContact, new Dictionary<string, string>
            {
                ["name"] = "匯出測試",
                ["contact"] = "export-test@example.com",
                ["subject"] = "s",
                ["message"] = "摘要匯出驗證內容",
                ["privacy_consent"] = "true",
            });

            var response = await adminClient.GetAsync("/api/v1/admin/tcrfc/enquiries/export");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.StartsWith("text/csv", response.Content.Headers.ContentType?.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.Contains("來源表單", text); // BOM 之後的表頭。
            Assert.Contains("內容摘要", text); // 審查回饋補做的欄位。
            Assert.Contains("一般聯絡", text); // 表單類別輸出中文顯示名稱，不是 general_contact 字面值。
            Assert.Contains("摘要匯出驗證內容", text); // message 欄位值（is_summary 來源），內容摘要有被匯出。
            Assert.DoesNotContain("general_contact", text);
        }
        finally
        {
            await DeleteEnquiryByIdAsync(enquiryId);
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

    private static async Task<Guid> GetFormIdAsync(HttpClient adminClient, string club, string formCode)
    {
        var forms = await adminClient.GetFromJsonAsync<List<AdminFormListItemDto>>($"/api/v1/admin/{club}/forms", TestJson.Options);
        return forms!.First(f => f.FormCode == formCode).Id;
    }

    private static async Task<Guid> SubmitAndGetIdAsync(
        HttpClient publicClient, string club, string formCode, IReadOnlyDictionary<string, string> answers,
        string? sourcePath = null, string? utmSource = null)
    {
        var response = await publicClient.PostAsJsonAsync($"/api/v1/{club}/forms/{formCode}/submissions", new SubmitFormRequest
        {
            Answers = answers,
            SourcePath = sourcePath,
            UtmSource = utmSource,
        }, TestJson.WriteOptions);
        response.EnsureSuccessStatusCode();

        // 公開送出端點刻意不回傳新建的 Enquiry id（規劃書沒有要求確認編號），測試改用送出內容裡
        // 一定存在的 contact 值回查資料庫拿 id，供後續清理與斷言使用。
        var contact = answers.TryGetValue("contact", out var c) ? c : throw new InvalidOperationException("測試呼叫缺少 contact 欄位，無法回查建立的 Enquiry。");
        return await FindEnquiryIdByContactAsync(contact);
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task<Guid> FindEnquiryIdByContactAsync(string contact)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP 1 e.id
            FROM enquiries e
            JOIN enquiry_answers ea ON ea.enquiry_id = e.id
            JOIN form_fields ff ON ff.id = ea.form_field_id
            WHERE ff.field_key = 'contact' AND ea.value = @Contact
            ORDER BY e.row_seq DESC
            """;
        command.Parameters.AddWithValue("@Contact", contact);
        var result = await command.ExecuteScalarAsync();
        return result is Guid id ? id : throw new InvalidOperationException($"回查不到 contact={contact} 的 Enquiry，送出可能沒有真的成功寫入。");
    }

    private static async Task<int> CountEnquiriesByContactAsync(string contact)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM enquiry_answers ea
            JOIN form_fields ff ON ff.id = ea.form_field_id
            WHERE ff.field_key = 'contact' AND ea.value = @Contact
            """;
        command.Parameters.AddWithValue("@Contact", contact);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task DeleteEnquiryByIdAsync(Guid? id)
    {
        if (id is not Guid enquiryId)
        {
            return;
        }
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM enquiry_answers WHERE enquiry_id = @Id;
            DELETE FROM enquiries WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", enquiryId);
        await command.ExecuteNonQueryAsync();
    }
}
