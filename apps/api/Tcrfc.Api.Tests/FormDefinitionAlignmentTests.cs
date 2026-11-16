using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Features.AdminForms;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// docs/23 W-3 的後端配套：前台七張表單改讀後台表單定義後，種子／系統定義的題目文字、選項顯示文字、必填設定
/// 必須與頁面（apps/web/app/pages/zh/join/*）原本定稿的一致；後台可存選項的中文顯示文字（規劃書 v3.35 4.7 G1）。
/// 打真正的 HTTP 管線與本機測試庫；會寫的測試自己建（<see cref="UnlockedTestForm"/>）或讀原值寫回原值。
///
/// 🔴 公開送出端點依 IP 分區限流（PermitLimit=10／5 分鐘，同一個測試主機共用一個分區），所以送出類測試用
/// <c>WithWebHostBuilder</c> 開一個獨立主機（獨立限流狀態），不佔用 <see cref="AdminWriteApiFixture"/> 共用主機的額度。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class FormDefinitionAlignmentTests(AdminWriteApiFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<PublicFormDto> GetPublicAsync(HttpClient client, string club, string formCode, string lang)
    {
        var form = await client.GetFromJsonAsync<PublicFormDto>($"/api/v1/{club}/forms/{formCode}?lang={lang}", Json);
        Assert.NotNull(form);
        return form!;
    }

    private static PublicFormFieldDto Field(PublicFormDto form, string key) => form.Fields.Single(f => f.FieldKey == key);

    // ═════════════════════════ 題目與選項文字：以頁面措辭為準 ═════════════════════════

    // (表單, 欄位, 中文題目, 英文題目)——頁面的 fl() 退路（中、英）逐一對應；兩個俱樂部的頁面措辭相同。
    public static TheoryData<string, string, string, string> PageLabels => new()
    {
        { "join_player", "birth_date", "出生日期", "Date of birth" },
        { "join_player", "position", "場上位置", "Playing position" },
        { "join_player", "experience", "足球經歷簡述", "Football experience" },
        { "join_player", "video_url", "比賽或訓練影片連結", "Match or training video link" },
        { "academy_children_training", "enrollment_category", "想報名的項目", "Program you would like to join" },
        { "academy_children_training", "location_preference", "偏好受訓地點", "Preferred training location" },
        { "academy_children_training", "birth_date", "學員出生日期", "Student's date of birth" },
        { "academy_children_training", "experience", "足球或運動經歷", "Football or sports experience" },
        { "academy_children_training", "health_status", "特殊健康狀況說明", "Special health conditions" },
        { "camp_registration", "session_choice", "希望報名的梯次", "Preferred session" },
        { "camp_registration", "name", "學員姓名", "Student's name" },
        { "camp_registration", "birth_date", "學員出生日期", "Student's date of birth" },
        { "international_player_enquiry", "name", "姓名", "Full Name" },
        { "international_player_enquiry", "nationality", "國籍", "Nationality" },
        { "international_player_enquiry", "passport_no", "護照號碼", "Passport Number" },
        { "international_player_enquiry", "experience", "足球經歷簡述", "Career Summary" },
        { "international_player_enquiry", "video_url", "影片連結", "Video Highlight Link" },
        { "international_player_enquiry", "visa_status", "簽證狀態", "Current Visa Status" },
        { "partnership_sponsorship", "enquiry_type", "這次想洽詢的是", "This enquiry is about" },
        { "partnership_sponsorship", "company", "公司／機構名稱", "Company / organisation name" },
        { "partnership_sponsorship", "industry", "產業別", "Industry" },
        { "partnership_sponsorship", "budget_range", "預算區間", "Budget range" },
        { "partnership_sponsorship", "name", "聯絡人姓名", "Contact name" },
        { "media_enquiry", "media_name", "媒體／機構名稱", "Media outlet / organisation name" },
        { "media_enquiry", "name", "記者／聯絡人姓名", "Journalist / contact name" },
        { "media_enquiry", "topic", "採訪主題／需求說明", "Interview topic / requirements" },
        { "media_enquiry", "deadline", "期望採訪日期／截稿日", "Preferred interview date / deadline" },
        { "general_contact", "name", "姓名", "Name" },
        { "general_contact", "contact", "Email", "Email" },
        { "general_contact", "subject", "主旨", "Subject" },
        { "general_contact", "message", "內容", "Message" },
    };

    [Theory]
    [MemberData(nameof(PageLabels))]
    public async Task 公開定義_題目文字中英都是頁面定稿的措辭_兩個俱樂部一致(string formCode, string fieldKey, string zh, string en)
    {
        using var client = fixture.CreateClient();
        foreach (var club in new[] { "tcrfc", "bw" })
        {
            Assert.Equal(zh, Field(await GetPublicAsync(client, club, formCode, "zh"), fieldKey).Label);
            Assert.Equal(en, Field(await GetPublicAsync(client, club, formCode, "en"), fieldKey).Label);
        }
    }

    [Fact]
    public async Task 公開定義_磐石報名項目_顯示文字是頁面的梯隊與班別簡稱_送出值不變()
    {
        using var client = fixture.CreateClient();
        string[] values = ["學院 U10", "學院 U12", "學院 U14", "學院 U16", "學院 U18", "兒童混齡班", "兒童初學班", "兒童技巧發展班", "專項訓練"];

        var zh = Field(await GetPublicAsync(client, "tcrfc", FormCatalog.AcademyChildrenTraining, "zh"), "enrollment_category");
        Assert.Equal(values, zh.Options);
        Assert.Equal(["U10 梯隊", "U12 梯隊", "U14 梯隊", "U16 梯隊", "U18 梯隊", "混齡班", "初學班", "技巧發展班", "專項訓練"], zh.OptionLabels);

        var en = Field(await GetPublicAsync(client, "tcrfc", FormCatalog.AcademyChildrenTraining, "en"), "enrollment_category");
        Assert.Equal(values, en.Options);
        Assert.Equal(["U10 squad", "U12 squad", "U14 squad", "U16 squad", "U18 squad", "Mixed-age class", "Beginner class", "Skills development class", "Specialist training"], en.OptionLabels);
    }

    [Fact]
    public async Task 公開定義_藍鯨報名項目_顯示文字稱青年隊_不出現學院或Academy()
    {
        using var client = fixture.CreateClient();
        var zh = Field(await GetPublicAsync(client, "bw", FormCatalog.AcademyChildrenTraining, "zh"), "enrollment_category");
        var en = Field(await GetPublicAsync(client, "bw", FormCatalog.AcademyChildrenTraining, "en"), "enrollment_category");

        // 送出值沿用（收件匣既有資料與後端驗證依賴），只有顯示文字對齊藍鯨頁面（E-233：藍鯨稱青年隊）。
        Assert.Equal(["學院 U12", "學院 U14", "學院 U15", "兒童混齡班", "兒童初學班", "兒童技巧發展班", "專項訓練"], zh.Options);
        Assert.Equal(["U12 青年隊", "U14 青年隊", "U15 青年隊", "混齡班", "初學班", "技巧發展班", "專項訓練"], zh.OptionLabels);
        Assert.Equal(["U12 Youth team", "U14 Youth team", "U15 Youth team", "Mixed-age class", "Beginner class", "Skills development class", "Specialist training"], en.OptionLabels);
        Assert.DoesNotContain(zh.OptionLabels!.Concat(en.OptionLabels!), l => l.Contains("學院") || l.Contains("Academy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task 公開定義_洽詢類型_顯示文字是頁面的措辭_送出值不變()
    {
        using var client = fixture.CreateClient();
        foreach (var club in new[] { "tcrfc", "bw" })
        {
            var zh = Field(await GetPublicAsync(client, club, FormCatalog.PartnershipSponsorship, "zh"), "enquiry_type");
            var en = Field(await GetPublicAsync(client, club, FormCatalog.PartnershipSponsorship, "en"), "enquiry_type");
            Assert.Equal(["合作夥伴", "贊助", "兩者"], zh.Options);
            Assert.Equal(["合作夥伴關係", "贊助方案", "兩者都想了解"], zh.OptionLabels);
            Assert.Equal(["A partnership", "A sponsorship package", "Both"], en.OptionLabels);
        }
    }

    // ═════════════════════════ 必填：以頁面為準 ═════════════════════════

    // 各表單「必填欄位」的完整集合（頁面標必填者）；反方向（定義必填、頁面選填）盤點為 0 項，
    // 所以用集合相等斷言，之後有人把任何欄位改必填或改選填都會在這裡現形。
    public static TheoryData<string, string> RequiredFieldSets => new()
    {
        { "join_player", "name,birth_date,position,experience,video_url,contact,privacy_consent" },
        { "academy_children_training", "enrollment_category,name,birth_date,contact,privacy_consent" },
        { "camp_registration", "session_choice,name,birth_date,health_declaration,contact,privacy_consent" },
        { "international_player_enquiry", "name,nationality,experience,video_url,visa_status,contact,privacy_consent" },
        { "partnership_sponsorship", "enquiry_type,company,cooperation_direction,name,contact,privacy_consent" },
        { "media_enquiry", "media_name,name,topic,deadline,contact,privacy_consent" },
        { "general_contact", "name,contact,subject,message,privacy_consent" },
    };

    [Theory]
    [MemberData(nameof(RequiredFieldSets))]
    public async Task 公開定義_必填欄位集合等於頁面標必填的欄位(string formCode, string expectedCsv)
    {
        using var client = fixture.CreateClient();
        var expected = expectedCsv.Split(',').Order().ToArray();
        foreach (var club in new[] { "tcrfc", "bw" })
        {
            var form = await GetPublicAsync(client, club, formCode, "zh");
            Assert.Equal(expected, form.Fields.Where(f => f.IsRequired).Select(f => f.FieldKey).Order().ToArray());
        }
    }

    [Theory]
    [InlineData("join_player", "position")]
    [InlineData("join_player", "experience")]
    [InlineData("join_player", "video_url")]
    [InlineData("international_player_enquiry", "experience")]
    [InlineData("international_player_enquiry", "video_url")]
    [InlineData("international_player_enquiry", "visa_status")]
    [InlineData("media_enquiry", "deadline")]
    [InlineData("partnership_sponsorship", "cooperation_direction")]
    public async Task 必填生效_原本選填現在必填的欄位留空回400_欄位錯誤鍵指向該欄位(string formCode, string emptyKey)
    {
        using var factory = fixture.WithWebHostBuilder(_ => { });
        using var client = factory.CreateClient();

        var answers = ValidAnswers(formCode);
        answers[emptyKey] = "";
        var response = await client.PostAsJsonAsync($"/api/v1/tcrfc/forms/{formCode}/submissions",
            new SubmitFormRequest { Answers = answers }, TestJson.WriteOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(emptyKey, body);
    }

    [Fact]
    public async Task 必填生效_七張表單填齊頁面必填欄位就能送出_沒有誤擋()
    {
        using var factory = fixture.WithWebHostBuilder(_ => { });
        using var client = factory.CreateClient();
        // 只送一張（join_player）：公開送出端點依 IP 限流，其餘表單的「填齊可送出」由上方集合相等斷言涵蓋。
        var tag = Guid.NewGuid().ToString("N")[..8];
        var answers = ValidAnswers("join_player");
        answers["contact"] = $"zz-test-align-{tag}@example.test";
        var response = await client.PostAsJsonAsync("/api/v1/tcrfc/forms/join_player/submissions",
            new SubmitFormRequest { Answers = answers }, TestJson.WriteOptions);
        try
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await BizTest.ExecuteSqlAsync("""
                DECLARE @E TABLE (id uniqueidentifier);
                INSERT @E SELECT enquiry_id FROM enquiry_answers WHERE value = @V;
                DELETE FROM enquiry_answers WHERE enquiry_id IN (SELECT id FROM @E);
                DELETE FROM enquiries WHERE id IN (SELECT id FROM @E);
                """, ("@V", $"zz-test-align-{tag}@example.test"));
        }
    }

    private static Dictionary<string, string> ValidAnswers(string formCode) => formCode switch
    {
        "join_player" => new()
        {
            ["name"] = "測試", ["birth_date"] = "2000-01-01", ["position"] = "門將 GK", ["experience"] = "測試經歷",
            ["video_url"] = "https://example.test/v", ["contact"] = "zz@example.test", ["privacy_consent"] = "true",
        },
        "international_player_enquiry" => new()
        {
            ["name"] = "Test", ["nationality"] = "Spain", ["experience"] = "x", ["video_url"] = "https://example.test/v",
            ["visa_status"] = "arc", ["contact"] = "zz@example.test", ["privacy_consent"] = "true",
        },
        "media_enquiry" => new()
        {
            ["media_name"] = "測試媒體", ["name"] = "記者", ["topic"] = "主題", ["deadline"] = "2027-01-01",
            ["contact"] = "zz@example.test", ["privacy_consent"] = "true",
        },
        "partnership_sponsorship" => new()
        {
            ["enquiry_type"] = "贊助", ["company"] = "測試公司", ["cooperation_direction"] = "構想", ["name"] = "聯絡人",
            ["contact"] = "zz@example.test", ["privacy_consent"] = "true",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(formCode)),
    };

    // ═════════════════════════ 後台：選項的中文顯示文字 ═════════════════════════

    [Fact]
    public async Task 後台_可新增帶中文選項顯示文字的欄位_公開端點中文回中文顯示文字_英文沒設定時回退成送出值()
    {
        using var admin = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var anon = fixture.CreateClient();
        await using var testForm = await UnlockedTestForm.CreateAsync();
        var url = $"/api/v1/admin/tcrfc/forms/{testForm.Id}/fields";

        var create = await admin.PostAsJsonAsync(url, new CreateAdminFormFieldRequest
        {
            FieldKey = "test_channel", FieldType = "select", LabelZh = "管道",
            Options = ["email", "phone"], OptionLabelsZh = ["電子郵件", "電話"],
        }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<AdminFormFieldDto>(TestJson.Options))!;
        Assert.Equal(["email", "phone"], created.Options);
        Assert.Equal(["電子郵件", "電話"], created.OptionLabelsZh);
        Assert.Null(created.OptionLabelsEn);

        // 後台詳情讀得回來
        var detail = await admin.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{testForm.Id}", TestJson.Options);
        Assert.Equal(["電子郵件", "電話"], detail!.Fields.Single(f => f.FieldKey == "test_channel").OptionLabelsZh);

        // 公開端點：中文回中文顯示文字、送出值不變；英文沒設定 → 回退成送出值（現行行為）
        var zh = Field(await GetPublicAsync(anon, "tcrfc", testForm.Code, "zh"), "test_channel");
        Assert.Equal(["email", "phone"], zh.Options);
        Assert.Equal(["電子郵件", "電話"], zh.OptionLabels);
        var en = Field(await GetPublicAsync(anon, "tcrfc", testForm.Code, "en"), "test_channel");
        Assert.Equal(["email", "phone"], en.OptionLabels);
    }

    [Fact]
    public async Task 後台_中文選項顯示文字驗證_筆數不符_空字串_沒有選項_過長都回400_錯誤鍵optionLabelsZh()
    {
        using var admin = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        await using var testForm = await UnlockedTestForm.CreateAsync();
        var url = $"/api/v1/admin/tcrfc/forms/{testForm.Id}/fields";

        async Task AssertBadAsync(CreateAdminFormFieldRequest request)
        {
            var response = await admin.PostAsJsonAsync(url, request, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("errors").TryGetProperty("optionLabelsZh", out _), "錯誤鍵應為 optionLabelsZh");
        }

        await AssertBadAsync(new CreateAdminFormFieldRequest { FieldKey = "t1", FieldType = "select", LabelZh = "x", Options = ["a", "b"], OptionLabelsZh = ["只有一個"] });
        await AssertBadAsync(new CreateAdminFormFieldRequest { FieldKey = "t2", FieldType = "select", LabelZh = "x", Options = ["a", "b"], OptionLabelsZh = ["甲", "  "] });
        await AssertBadAsync(new CreateAdminFormFieldRequest { FieldKey = "t3", FieldType = "text", LabelZh = "x", OptionLabelsZh = ["甲"] });
        await AssertBadAsync(new CreateAdminFormFieldRequest { FieldKey = "t4", FieldType = "select", LabelZh = "x", Options = ["a", "b"], OptionLabelsZh = [new string('長', 256), "乙"] });
    }

    [Fact]
    public async Task 後台_更新中文選項顯示文字_省略保留_空陣列清除_有內容取代()
    {
        using var admin = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var anon = fixture.CreateClient();
        await using var testForm = await UnlockedTestForm.CreateAsync();
        var baseUrl = $"/api/v1/admin/tcrfc/forms/{testForm.Id}/fields";

        var created = (await (await admin.PostAsJsonAsync(baseUrl, new CreateAdminFormFieldRequest
        {
            FieldKey = "test_choice", FieldType = "select", LabelZh = "選擇", Options = ["a", "b"], OptionLabelsZh = ["甲", "乙"],
        }, TestJson.WriteOptions)).Content.ReadFromJsonAsync<AdminFormFieldDto>(TestJson.Options))!;

        UpdateAdminFormFieldRequest Req(IReadOnlyList<string>? zh, IReadOnlyList<string>? en = null) => new()
        {
            FieldKey = "test_choice", FieldType = "select", LabelZh = "選擇", Options = ["a", "b"],
            OptionLabelsZh = zh, OptionLabelsEn = en, SortOrder = created.SortOrder,
        };

        async Task<AdminFormFieldDto> PutAsync(UpdateAdminFormFieldRequest r)
        {
            var response = await admin.PutAsJsonAsync($"{baseUrl}/{created.Id}", r, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<AdminFormFieldDto>(TestJson.Options))!;
        }

        // 省略（舊版畫面不認得這個欄位）→ 保留
        Assert.Equal(["甲", "乙"], (await PutAsync(Req(null))).OptionLabelsZh);
        // 有內容 → 取代
        Assert.Equal(["丙", "丁"], (await PutAsync(Req(["丙", "丁"]))).OptionLabelsZh);
        Assert.Equal(["丙", "丁"], Field(await GetPublicAsync(anon, "tcrfc", testForm.Code, "zh"), "test_choice").OptionLabels);
        // 英文同時設定，互不影響
        var both = await PutAsync(Req(["丙", "丁"], ["C", "D"]));
        Assert.Equal(["丙", "丁"], both.OptionLabelsZh);
        Assert.Equal(["C", "D"], both.OptionLabelsEn);
        // 空陣列 → 清除，前台回到顯示送出值
        Assert.Null((await PutAsync(Req([]))).OptionLabelsZh);
        Assert.Equal(["a", "b"], Field(await GetPublicAsync(anon, "tcrfc", testForm.Code, "zh"), "test_choice").OptionLabels);
    }

    [Fact]
    public async Task 後台_系統鎖定的表單也能存選項的中文顯示文字_送出值鎖定仍然生效_驗完還原()
    {
        using var admin = await BizTest.ClientAsync(fixture, "customer.service@tcrfc.test");
        using var anon = fixture.CreateClient();
        var forms = await admin.GetFromJsonAsync<List<AdminFormListItemDto>>("/api/v1/admin/tcrfc/forms", TestJson.Options);
        var formId = forms!.First(f => f.FormCode == FormCatalog.PartnershipSponsorship).Id;
        var detail = await admin.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        var select = detail!.Fields.Single(f => f.FieldKey == "enquiry_type");
        var url = $"/api/v1/admin/tcrfc/forms/{formId}/fields/{select.Id}";

        UpdateAdminFormFieldRequest Same(IReadOnlyList<string>? zh, IReadOnlyList<string>? options = null) => new()
        {
            FieldKey = select.FieldKey, FieldType = select.FieldType, LabelZh = select.LabelZh, LabelEn = select.LabelEn, IsRequired = select.IsRequired,
            ValidationRule = select.ValidationRule, Options = options ?? select.Options, OptionLabelsZh = zh, OptionLabelsEn = select.OptionLabelsEn,
            IsSummary = select.IsSummary, SortOrder = select.SortOrder,
        };

        try
        {
            var changed = await admin.PutAsJsonAsync(url, Same(["合作夥伴（改）", "贊助（改）", "兩者（改）"]), TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            var zh = Field(await GetPublicAsync(anon, "tcrfc", FormCatalog.PartnershipSponsorship, "zh"), "enquiry_type");
            Assert.Equal(["合作夥伴", "贊助", "兩者"], zh.Options); // 送出值不動
            Assert.Equal(["合作夥伴（改）", "贊助（改）", "兩者（改）"], zh.OptionLabels);

            // 送出值鎖定仍然生效
            var locked = await admin.PutAsJsonAsync(url, Same(null, [.. select.Options!, "新增選項"]), TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, locked.StatusCode);
        }
        finally
        {
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(url, Same(select.OptionLabelsZh ?? []), TestJson.WriteOptions)).StatusCode);
            // 若原本沒有中文顯示文字（null）則上一行以空陣列清除；原本有值則寫回原值。
        }

        var restored = await admin.GetFromJsonAsync<AdminFormDetailDto>($"/api/v1/admin/tcrfc/forms/{formId}", TestJson.Options);
        Assert.Equal(select.OptionLabelsZh, restored!.Fields.Single(f => f.FieldKey == "enquiry_type").OptionLabelsZh);
    }
}
