using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 慈善後台的欄位錯誤（ProblemDetails <c>errors</c>），比照主站 <c>AdminFieldErrorsTests</c>。
/// 欄位鍵必須與 apps/admin-charity 各編輯頁 <c>FormField field=</c>（雙語 <c>field="name"</c> → <c>nameZh</c>）一致；
/// 鍵只在程式內對照，不得出現在訊息文字裡。涵蓋項目、店家、站台設定與系統信樣板、帳號、角色，
/// 以及 <c>CharityConflictException</c> 的 <c>errors</c> 通道（帳號／角色代碼重複）。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminFieldErrorsTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Projects = AdminBase + "/projects";
    private const string Stores = AdminBase + "/stores";
    private const string Settings = AdminBase + "/settings";
    private const string Templates = AdminBase + "/email-templates";
    private const string Channels = AdminBase + "/payment-channels";
    private const string Accounts = AdminBase + "/accounts";
    private const string Roles = AdminBase + "/roles";

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private async Task<HttpClient> SysAsync() => fx.CreateClientFor(await fx.CreateAdminAsync(true));

    private static string NewUsername() => $"ct-fe{Guid.NewGuid():N}@{CharityApiFixture.TestEmailDomain}";

    private static string NewRoleCode() => $"ctrole_{Guid.NewGuid():N}"[..20];

    /// <summary>斷言狀態碼，並回傳 errors（鍵 → 第一則訊息）；同時斷言鍵格式合法、值為單元素陣列、鍵不出現在訊息裡。</summary>
    private static async Task<Dictionary<string, string>> ErrorsAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("errors", out var errors), $"回應沒有 errors：{body}");
        var map = new Dictionary<string, string>();
        foreach (var property in errors.EnumerateObject())
        {
            Assert.Matches(FieldKey.Pattern(), property.Name);
            Assert.Equal(1, property.Value.GetArrayLength());
            var message = property.Value[0].GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(message));
            Assert.DoesNotContain(property.Name, message, StringComparison.Ordinal);
            map[property.Name] = message;
        }

        // detail 不變，仍是同一則訊息
        Assert.Equal(body.GetProperty("detail").GetString(), map.Values.First());
        return map;
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient c, string url, object body) => c.PostAsJsonAsync(url, body, TestJson.WriteOptions);

    private static Task<HttpResponseMessage> PutAsync(HttpClient c, string url, object body) => c.PutAsJsonAsync(url, body, TestJson.WriteOptions);

    private static object Project(Action<Dictionary<string, object?>>? tweak = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["slug"] = $"ct-{Guid.NewGuid():N}"[..16], ["nameZh"] = "CT欄位錯誤項目", ["invoiceMode"] = "b2c_invoice", ["minAmount"] = 100, ["maxAmount"] = 10000,
        };
        tweak?.Invoke(body);
        return body;
    }

    // ───────────────────────────── 項目 ─────────────────────────────

    [Fact]
    public async Task 項目_必填與長度與雙語鍵()
    {
        using var sys = await SysAsync();

        Assert.Contains("nameZh", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["nameZh"] = "")), HttpStatusCode.BadRequest));
        Assert.Contains("nameEn", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["nameEn"] = new string('x', 129))), HttpStatusCode.BadRequest));
        Assert.Contains("oneLinerEn", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["oneLinerEn"] = new string('x', 256))), HttpStatusCode.BadRequest));
        Assert.Contains("coverAltZh", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["coverAltZh"] = new string('x', 256))), HttpStatusCode.BadRequest));
        Assert.Contains("invoiceMode", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["invoiceMode"] = null)), HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task 項目_金額與分潤與撥付對象鍵()
    {
        using var sys = await SysAsync();

        Assert.Contains("minAmount", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["minAmount"] = 0)), HttpStatusCode.BadRequest));
        Assert.Contains("maxAmount", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["maxAmount"] = -5)), HttpStatusCode.BadRequest));
        Assert.Contains("minAmount", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => { b["minAmount"] = 500; b["maxAmount"] = 100; })), HttpStatusCode.BadRequest));
        Assert.Contains("amountOptions", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["amountOptions"] = new[] { 100, 100 })), HttpStatusCode.BadRequest));
        Assert.Contains("amountOptions", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["amountOptions"] = new[] { 99999 })), HttpStatusCode.BadRequest));
        Assert.Contains("projectSharePct", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["projectSharePct"] = 101m)), HttpStatusCode.BadRequest));
        Assert.Contains("charityRefCode", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["charityRefCode"] = "no-such-charity")), HttpStatusCode.BadRequest));
        Assert.Contains("charityProgramRefCode", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["charityProgramRefCode"] = "no-such-program")), HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task 項目_網址名稱重複_409帶slug鍵()
    {
        using var sys = await SysAsync();
        var slug = $"ct-{Guid.NewGuid():N}"[..16];
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(sys, Projects, Project(b => b["slug"] = slug))).StatusCode);

        Assert.Contains("slug", await ErrorsAsync(await PostAsync(sys, Projects, Project(b => b["slug"] = slug)), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task 項目內文_超長_帶對應語言的鍵()
    {
        using var sys = await SysAsync();
        var created = await PostAsync(sys, Projects, Project());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var errors = await ErrorsAsync(await PutAsync(sys, $"{Projects}/{id}/content", new { fundUsageEn = new string('字', 5_001) }), HttpStatusCode.BadRequest);
        Assert.Contains("fundUsageEn", errors);
    }

    // ───────────────────────────── 店家 ─────────────────────────────

    [Fact]
    public async Task 店家_必填長度日期狀態分潤鍵()
    {
        using var sys = await SysAsync();

        Assert.Contains("nameZh", await ErrorsAsync(await PostAsync(sys, Stores, new { nameZh = "" }), HttpStatusCode.BadRequest));
        Assert.Contains("logoAltEn", await ErrorsAsync(await PostAsync(sys, Stores, new { nameZh = "CT店家欄位", logoAltEn = new string('x', 256) }), HttpStatusCode.BadRequest));
        Assert.Contains("address", await ErrorsAsync(await PostAsync(sys, Stores, new { nameZh = "CT店家欄位", address = new string('x', 501) }), HttpStatusCode.BadRequest));
        Assert.Contains("contactPhone", await ErrorsAsync(await PostAsync(sys, Stores, new { nameZh = "CT店家欄位", contactPhone = new string('1', 33) }), HttpStatusCode.BadRequest));
        Assert.Contains("endOn", await ErrorsAsync(await PostAsync(sys, Stores, new { nameZh = "CT店家欄位", startOn = "2026-05-01", endOn = "2026-04-01" }), HttpStatusCode.BadRequest));
        Assert.Contains("status", await ErrorsAsync(await PostAsync(sys, Stores, new { nameZh = "CT店家欄位", status = "bogus" }), HttpStatusCode.BadRequest));
        Assert.Contains("storeSharePct", await ErrorsAsync(await PostAsync(sys, Stores, new { nameZh = "CT店家欄位", storeSharePct = 100.5m }), HttpStatusCode.BadRequest));
    }

    // ───────────────────────────── 站台設定、系統信樣板、金流憑證 ─────────────────────────────

    [Fact]
    public async Task 站台設定_文案超長與網址與金額範圍鍵()
    {
        using var sys = await SysAsync();

        Assert.Contains("homeIntroZh", await ErrorsAsync(await PutAsync(sys, Settings, new { homeIntroZh = new string('字', 2001) }), HttpStatusCode.BadRequest));
        Assert.Contains("privacyPolicyEn", await ErrorsAsync(await PutAsync(sys, Settings, new { privacyPolicyEn = new string('x', 100_001) }), HttpStatusCode.BadRequest));
        Assert.Contains("clubSiteUrl", await ErrorsAsync(await PutAsync(sys, Settings, new { clubSiteUrl = "not-a-url" }), HttpStatusCode.BadRequest));
        Assert.Contains("defaultMinAmount", await ErrorsAsync(await PutAsync(sys, Settings, new { defaultMinAmount = 0 }), HttpStatusCode.BadRequest));
        Assert.Contains("defaultMaxAmount", await ErrorsAsync(await PutAsync(sys, Settings, new { defaultMinAmount = 500, defaultMaxAmount = 100 }), HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task 系統信樣板_主旨本文與英文成對鍵()
    {
        using var sys = await SysAsync();
        var url = $"{Templates}/invoice_issued";

        Assert.Contains("subjectZh", await ErrorsAsync(await PutAsync(sys, url, new { subjectZh = "", bodyZh = "有本文沒主旨" }), HttpStatusCode.BadRequest));
        Assert.Contains("bodyZh", await ErrorsAsync(await PutAsync(sys, url, new { bodyZh = "   " }), HttpStatusCode.BadRequest));
        Assert.Contains("bodyEn", await ErrorsAsync(await PutAsync(sys, url, new { subjectEn = "只有主旨" }), HttpStatusCode.BadRequest));
        Assert.Contains("subjectEn", await ErrorsAsync(await PutAsync(sys, url, new { bodyEn = "only body" }), HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task 金流憑證_必填與環境鍵()
    {
        using var sys = await SysAsync();

        Assert.Contains("credential", await ErrorsAsync(await PutAsync(sys, $"{Channels}/line_pay/credential", new { environment = "sandbox", credential = "" }), HttpStatusCode.BadRequest));
        Assert.Contains("environment", await ErrorsAsync(await PutAsync(sys, $"{Channels}/line_pay/credential", new { environment = "nope", credential = "x" }), HttpStatusCode.BadRequest));
        Assert.Contains("environment", await ErrorsAsync(await PutAsync(sys, $"{Channels}/line_pay/environment", new { environment = "sandbox", confirm = false }), HttpStatusCode.BadRequest));
    }

    // ───────────────────────────── 帳號 ─────────────────────────────

    [Fact]
    public async Task 帳號_驗證鍵與帳號重複_409帶username鍵()
    {
        using var sys = await SysAsync();
        var username = NewUsername();

        Assert.Contains("username", await ErrorsAsync(await PostAsync(sys, Accounts, new { username = "a b", displayName = "x", initialPassword = "InitialPass-123" }), HttpStatusCode.BadRequest));
        Assert.Contains("displayName", await ErrorsAsync(await PostAsync(sys, Accounts, new { username, displayName = "", initialPassword = "InitialPass-123" }), HttpStatusCode.BadRequest));
        Assert.Contains("initialPassword", await ErrorsAsync(await PostAsync(sys, Accounts, new { username, displayName = "x", initialPassword = "short" }), HttpStatusCode.BadRequest));
        Assert.Contains("roleCodes", await ErrorsAsync(await PostAsync(sys, Accounts, new { username, displayName = "x", initialPassword = "InitialPass-123", roleCodes = new[] { "no_such_role" } }), HttpStatusCode.BadRequest));

        Assert.Equal(HttpStatusCode.Created, (await PostAsync(sys, Accounts, new { username, displayName = "x", initialPassword = "InitialPass-123" })).StatusCode);
        Assert.Contains("username", await ErrorsAsync(await PostAsync(sys, Accounts, new { username, displayName = "x", initialPassword = "InitialPass-123" }), HttpStatusCode.Conflict));
    }

    // ───────────────────────────── 角色 ─────────────────────────────

    [Fact]
    public async Task 角色_驗證鍵與代碼重複_409帶code鍵()
    {
        using var sys = await SysAsync();
        var code = NewRoleCode();

        Assert.Contains("code", await ErrorsAsync(await PostAsync(sys, Roles, new { code = "Bad-Code", nameZh = "x" }), HttpStatusCode.BadRequest));
        Assert.Contains("nameZh", await ErrorsAsync(await PostAsync(sys, Roles, new { code, nameZh = "" }), HttpStatusCode.BadRequest));

        Assert.Equal(HttpStatusCode.Created, (await PostAsync(sys, Roles, new { code, nameZh = "CT角色" })).StatusCode);
        Assert.Contains("code", await ErrorsAsync(await PostAsync(sys, Roles, new { code, nameZh = "CT角色" }), HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task 角色權限_查無權限碼_400帶permissions鍵()
    {
        using var sys = await SysAsync();
        var created = await PostAsync(sys, Roles, new { code = NewRoleCode(), nameZh = "CT角色" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await PutAsync(sys, $"{Roles}/{id}/permissions", new { permissions = new[] { new { permissionCode = "no.such.perm", scopeType = "all" } } });
        Assert.Contains("permissions", await ErrorsAsync(response, HttpStatusCode.BadRequest));
    }

    // ───────────────────────────── 沒有欄位歸屬 ─────────────────────────────

    [Fact]
    public async Task 沒有欄位歸屬的衝突_不帶errors_仍是原本的標題與訊息()
    {
        using var sys = await SysAsync();
        // 只有一位啟用中的最高管理員時停用自己會被擋（SetStatus 路徑不帶欄位）。這裡改用較穩定的：重產店家網址未確認屬驗證錯誤且沒有欄位。
        var created = await PostAsync(sys, Stores, new { nameZh = $"CT店家{Guid.NewGuid():N}"[..14] });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var response = await PostAsync(sys, $"{Stores}/{id}/regenerate-slug", new { confirm = false });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.TryGetProperty("errors", out _));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("detail").GetString()));
    }
}
