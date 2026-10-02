using System.Net;
using System.Net.Http.Json;
using System.Text;
using Tcrfc.Api.CharityPlatform.Admin;
using Tcrfc.Api.Common;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;
using static Tcrfc.Api.Tests.CharityTestSupport;

namespace Tcrfc.Api.Tests;

/// <summary>
/// N1 批次匯入店家（規劃書 §6.1）。重點：整批驗證任一列有錯整批不寫入、錯誤列號與 Excel 一致、重複判定讓重複匯入冪等、
/// slug 一律系統產生、分潤欄位非 0 需要獨立授權並逐店寫稽核。店名一律用 <c>CT店家</c> 開頭，測試後依標記清掉。
/// </summary>
[Collection(CharityCollection.Name)]
public sealed class CharityAdminStoreImportTests(CharityApiFixture fx) : IAsyncLifetime
{
    private const string Import = AdminBase + "/stores/import";
    private const string Header = "店家名稱（繁中）,店家名稱（英文）,類別,地址,聯絡人,電話,合作開始日,合作結束日,狀態,店家分潤（%）";

    public Task InitializeAsync()
    {
        fx.ResetDoubles();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await fx.CleanupAsync();

    private async Task<HttpClient> SysAsync() => fx.CreateClientFor(await fx.CreateAdminAsync(true));

    private static string Name() => $"CT店家{Guid.NewGuid():N}"[..16];

    private static string Csv(params string[] rows) => string.Join("\r\n", new[] { Header }.Concat(rows)) + "\r\n";

    private static Task<HttpResponseMessage> PostCsvAsync(HttpClient client, string csv, bool skipDuplicates = false)
        => client.PostAsync($"{Import}{(skipDuplicates ? "?skipDuplicates=true" : string.Empty)}", new StringContent(csv, new UTF8Encoding(true), "text/csv"));

    private static async Task<AdminStoreImportResultDto> ResultAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<AdminStoreImportResultDto>(TestJson.Options))!;

    private Task<int> StoreCountAsync(string name)
        => fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_stores_i18n WHERE name = @n AND locale = N'zh-Hant'", ("@n", name));

    [Fact]
    public async Task 匯入成功_建立店家_slug由系統產生且互不相同_狀態與日期與英文名稱正確套用_寫匯入稽核()
    {
        var a = Name();
        var b = Name();
        var c = Name();
        var admin = await fx.CreateAdminAsync(true);
        using var sys = fx.CreateClientFor(admin);

        var response = await PostCsvAsync(sys, Csv(
            $"{a},Cafe A,餐飲,台中市西區測試路 1 號,王小明,04-2222-3333,2026-10-01,2027-09-30,合作中,0",
            $"{b},,烘焙,台中市北區測試路 2 號,,,,,已停止,",
            $"\"{c}\",Shop C,零售,\"台中市中區測試路 3 號，二樓\",李小華,0912-345-678,2026/10/1,,active,7.5"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ResultAsync(response);
        Assert.Equal(3, result.ImportedCount);
        Assert.Empty(result.Errors);

        var slugs = new List<string>();
        foreach (var name in new[] { a, b, c })
        {
            var slug = await fx.ScalarAsync<string>("SELECT s.store_slug FROM donation_stores s JOIN donation_stores_i18n i ON i.donation_store_id = s.id WHERE i.name = @n", ("@n", name));
            Assert.Matches("^[a-z2-9]{16}$", slug!);
            slugs.Add(slug!);
        }

        Assert.Equal(3, slugs.Distinct().Count());
        Assert.Equal("inactive", await fx.ScalarAsync<string>("SELECT s.status FROM donation_stores s JOIN donation_stores_i18n i ON i.donation_store_id = s.id WHERE i.name = @n", ("@n", b)));
        Assert.Equal(7.5m, await fx.ScalarAsync<decimal>("SELECT s.store_share_pct FROM donation_stores s JOIN donation_stores_i18n i ON i.donation_store_id = s.id WHERE i.name = @n", ("@n", c)));
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM donation_stores_i18n WHERE name = N'Cafe A' AND locale = N'en'"));
        Assert.Equal(new DateTime(2026, 10, 1), await fx.ScalarAsync<DateTime>("SELECT s.start_on FROM donation_stores s JOIN donation_stores_i18n i ON i.donation_store_id = s.id WHERE i.name = @n", ("@n", c)));
        Assert.Equal("台中市中區測試路 3 號，二樓", await fx.ScalarAsync<string>("SELECT s.address FROM donation_stores s JOIN donation_stores_i18n i ON i.donation_store_id = s.id WHERE i.name = @n", ("@n", c)));

        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'store.import_csv'", ("@a", admin.Id)));
        Assert.Equal(1, await fx.ScalarAsync<int>("SELECT COUNT(*) FROM audit_logs WHERE admin_user_id = @a AND action = N'store.share_pct_set'", ("@a", admin.Id))); // 只有分潤非 0 的那一家
    }

    [Fact]
    public async Task 任一列有錯整批不寫入_錯誤逐列回報_列號與Excel一致()
    {
        var good = Name();
        var bad = Name();
        using var sys = await SysAsync();

        var response = await PostCsvAsync(sys, Csv(
            $"{good},,餐飲,地址甲,,,2026-10-01,2027-09-30,合作中,5",         // 第 2 列：正常
            $"{bad},,餐飲,地址乙,,,2026-13-45,,合作中,5",                      // 第 3 列：日期格式錯
            ",,餐飲,地址丙,,,,,合作中,5",                                        // 第 4 列：缺店名
            $"{Name()},,餐飲,地址丁,,,2027-01-01,2026-01-01,合作中,5",         // 第 5 列：結束早於開始
            $"{Name()},,餐飲,地址戊,,,,,不明狀態,5",                            // 第 6 列：狀態錯
            $"{Name()},,餐飲,地址己,,,,,合作中,101",                            // 第 7 列：分潤超出範圍
            $"{Name()},,餐飲,地址庚,,,,,合作中,1.234",                          // 第 8 列：三位小數
            "只有兩欄,,"));                                                       // 第 9 列：欄位數不對

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await ResultAsync(response);
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(new[] { 3, 4, 5, 6, 7, 8, 9 }, result.Errors.Select(e => e.RowNumber).ToArray());
        Assert.Contains("合作開始日", result.Errors[0].Reason);
        Assert.Contains("店家名稱（繁中）", result.Errors[1].Reason);
        Assert.Contains("欄位數不正確", result.Errors[6].Reason);

        // 整批不寫入：連那一列正常的也沒有進資料庫
        Assert.Equal(0, await StoreCountAsync(good));
    }

    [Fact]
    public async Task 表頭不符_空檔_只有表頭_超過上限_都回400()
    {
        using var sys = await SysAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await PostCsvAsync(sys, "")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostCsvAsync(sys, "店名,地址\r\nA,B\r\n")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostCsvAsync(sys, Header + "\r\n")).StatusCode);

        var tooMany = Csv(Enumerable.Range(0, CharityStoresAdminService.MaxImportRows + 1).Select(i => $"{Name()}-{i},,,,,,,,,").ToArray());
        var response = await PostCsvAsync(sys, tooMany);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("500", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task 重複判定_店名與地址相同視為重複_同一份檔案重複匯入不會產生重複店家_skipDuplicates改為略過()
    {
        var a = Name();
        var b = Name();
        using var sys = await SysAsync();
        var file = Csv($"{a},,餐飲,地址甲,,,,,合作中,0", $"{b},,餐飲,地址乙,,,,,合作中,0");

        Assert.Equal(HttpStatusCode.OK, (await PostCsvAsync(sys, file)).StatusCode);

        // 第二次整份重複匯入：預設視為錯誤，整批不寫入（冪等：不會多出重複店家）
        var again = await PostCsvAsync(sys, file);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(new[] { 2, 3 }, (await ResultAsync(again)).Errors.Select(e => e.RowNumber).ToArray());
        Assert.Equal(1, await StoreCountAsync(a));

        // skipDuplicates：重複的略過並回報，其餘合格的照常匯入；店名相同但地址不同不算重複
        var c = Name();
        var mixed = Csv($"{a},,餐飲,地址甲,,,,,合作中,0", $"{c},,餐飲,地址丙,,,,,合作中,0", $"{a},,餐飲,另一個地址,,,,,合作中,0");
        var response = await PostCsvAsync(sys, mixed, skipDuplicates: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ResultAsync(response);
        Assert.Equal(2, result.ImportedCount);                       // c 與「同店名不同地址」的 a
        Assert.Equal(new[] { 2 }, result.Skipped.Select(s => s.RowNumber).ToArray());
        Assert.Equal(2, await StoreCountAsync(a));

        // 檔案內自己重複：第二次出現的列被指出與第幾列重複
        var d = Name();
        var inFile = await PostCsvAsync(sys, Csv($"{d},,,地址,,,,,,", $"{d},,,地址,,,,,,"));
        Assert.Equal(HttpStatusCode.BadRequest, inFile.StatusCode);
        Assert.Contains("第 2 列", (await ResultAsync(inFile)).Errors.Single().Reason);
    }

    [Fact]
    public async Task 分潤欄位非0需要設定分潤權限_沒有時整批403_商務可匯入分潤為0的店家()
    {
        var biz = fx.CreateClientFor(await fx.CreateAdminAsync(false, "business_sponsorship"));
        var withPct = Name();
        var noPct = Name();

        Assert.Equal(HttpStatusCode.Forbidden, (await PostCsvAsync(biz, Csv($"{withPct},,,地址,,,,,,5"))).StatusCode);
        Assert.Equal(0, await StoreCountAsync(withPct));

        Assert.Equal(HttpStatusCode.OK, (await PostCsvAsync(biz, Csv($"{noPct},,,地址,,,,,,0"))).StatusCode);
        Assert.Equal(1, await StoreCountAsync(noPct));
    }

    [Fact]
    public async Task 分潤加項目分潤超過100_該列報錯()
    {
        var (_, _) = await CreateProjectAsync(fx, projectPct: 50m);
        using var sys = await SysAsync();

        var response = await PostCsvAsync(sys, Csv($"{Name()},,,地址,,,,,合作中,60"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("超過 100%", (await ResultAsync(response)).Errors.Single().Reason);
    }

    [Fact]
    public async Task 授權與範本_檢視者與客服不能匯入_未登入401_範本可下載且表頭正確()
    {
        using var viewer = fx.CreateClientFor(await fx.CreateAdminAsync(false, "viewer"));
        using var cs = fx.CreateClientFor(await fx.CreateAdminAsync(false, "customer_service_admin"));
        using var biz = fx.CreateClientFor(await fx.CreateAdminAsync(false, "business_sponsorship"));

        Assert.Equal(HttpStatusCode.Forbidden, (await PostCsvAsync(viewer, Csv())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostCsvAsync(cs, Csv())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostCsvAsync(fx.CreateClient(), Csv())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(Import + "-template")).StatusCode);

        var template = await biz.GetAsync(Import + "-template");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        var text = Encoding.UTF8.GetString(await template.Content.ReadAsByteArrayAsync());
        Assert.Contains(Header, text);

        // 範本內容本身就是可匯入的（表頭＋範例列），驗證契約沒有自相矛盾。
        var example = text.TrimStart('﻿');
        Assert.Equal(HttpStatusCode.OK, (await PostCsvAsync(biz, example.Replace("範例咖啡店", Name()).Replace("5\r\n", "0\r\n"))).StatusCode);
    }

    [Fact]
    public async Task 檔案過大回400_不讀完整個本文()
    {
        using var sys = await SysAsync();
        var huge = new string('A', 1_200_000);

        var response = await PostCsvAsync(sys, huge);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("1 MB", await response.Content.ReadAsStringAsync());
    }
}
