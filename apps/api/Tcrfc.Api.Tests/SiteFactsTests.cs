using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminSiteFacts;
using Tcrfc.Api.Features.SiteFacts;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-12d：`I` 網站設定——`GEO-03`／`GEO-04` 站台事實的後台讀寫與公開讀取。打真正的 HTTP 管線與
/// 真正的 <c>tcrfc_club</c>，不 mock，跟這個測試專案既有的紀律一致（比照 <c>AdminSeoTests</c>）。
///
/// 🔴 <see cref="站台事實_系統管理員_可讀可寫_完整輪替後還原()"/> 會寫入共用的
/// <c>settings</c>／<c>settings_i18n</c>／<c>venues</c>／<c>venues_i18n</c>——測試前先讀出目前的
/// <see cref="AdminSiteFactsDto"/>，測試後（含斷言失敗時）用 <c>finally</c> 把同一份內容 PUT 回去，
/// 不能只靠「假設種子資料一定長這樣」，否則跟其他也會讀這批鍵的測試（例如種子資料驗證）產生
/// E-62 那一類互相干擾。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class SiteFactsTests(AdminWriteApiFixture fixture)
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

    // ───────────────────────────── 後台：權限 ─────────────────────────────

    [Fact]
    public async Task 後台_未登入_擋下()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/tcrfc/site-facts")).StatusCode);
    }

    [Fact]
    public async Task 後台_內容編輯角色_沒有sysadminonly權限_403()
    {
        // 規劃書 §6 權限矩陣沒有「網站設定」欄，本輪比照 seo.*／system.* 既有先例判斷
        // sysadmin_only（見 docs/12b §7.4「S1-12d 新增」）——非超管角色即使被指派了角色，
        // PermissionChecker 仍會擋下。
        using var client = await CreateContentEditorClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/tcrfc/site-facts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(
            "/api/v1/admin/tcrfc/site-facts",
            new UpdateSiteFactsRequest { FoundedYear = "2024", FoundingDateDisplayZh = "x", LeagueNameZh = "x", SquadStructureZh = "x" },
            TestJson.WriteOptions)).StatusCode);
    }

    // ───────────────────────────── 後台：讀取現況（種子資料） ─────────────────────────────

    [Fact]
    public async Task 後台_系統管理員_可讀取種子資料()
    {
        using var client = await CreateSuperAdminClientAsync();

        var response = await client.GetAsync("/api/v1/admin/tcrfc/site-facts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AdminSiteFactsDto>(TestJson.Options);

        Assert.NotNull(dto);
        Assert.Equal("2024", dto!.FoundedYear);
        Assert.Equal("全國乙級聯賽冠軍", dto.FoundingTitleZh);
        Assert.Equal("企業甲級聯賽", dto.LeagueNameZh);
        Assert.Equal("Enterprise Premier League", dto.LeagueNameEn);
        Assert.Equal(["U15", "U14", "U12"], dto.SquadCodes);
        var venue = Assert.Single(dto.HomeVenues);
        Assert.Equal("西屯足球場", venue.NameZh);
        Assert.Equal("Xitun Football Field", venue.NameEn);
        Assert.Equal("台中市北屯區崇平路二段景谷巷 11 弄 41 號", venue.Address);
        Assert.Equal("https://bw-stg.tcrfc.tw", dto.BlueWhaleSiteUrl); // 主站規劃書 §3.6，種子見 db/seed。
    }

    [Fact]
    public async Task 後台_藍鯨主場依主要主場排序_太原在前豐原在後()
    {
        using var client = await CreateSuperAdminClientAsync();

        var dto = await (await client.GetAsync("/api/v1/admin/bw/site-facts"))
            .Content.ReadFromJsonAsync<AdminSiteFactsDto>(TestJson.Options);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.HomeVenues.Count);
        Assert.Equal("台中北屯太原足球場", dto.HomeVenues[0].NameZh);
        Assert.Equal("台中市立豐原體育場", dto.HomeVenues[1].NameZh);
        Assert.Equal(["U15", "U12"], dto.SquadCodes);
        Assert.Null(dto.FoundingTitleZh); // bw 沒有「成立當年奪冠」這筆事實。
        Assert.Null(dto.BlueWhaleSiteUrl); // 概念上只屬於 tcrfc，種子沒有種給 bw。
    }

    // ───────────────────────────── 後台：完整寫入輪替 ─────────────────────────────

    [Fact]
    public async Task 站台事實_系統管理員_可讀可寫_完整輪替後還原()
    {
        using var client = await CreateSuperAdminClientAsync();

        var original = await (await client.GetAsync("/api/v1/admin/tcrfc/site-facts"))
            .Content.ReadFromJsonAsync<AdminSiteFactsDto>(TestJson.Options);
        Assert.NotNull(original);

        try
        {
            // 缺中文必填欄位 → 400（HomeVenues 仍照原樣送出，只是把 SquadStructureZh 拿掉）。
            var invalidRequest = ToUpdateRequest(original!) with { SquadStructureZh = null };
            var invalidResponse = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/site-facts", invalidRequest, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

            var marker = Guid.NewGuid().ToString("N");
            var updateRequest = ToUpdateRequest(original!) with
            {
                ContactPhone = $"04-0000-{marker[..4]}",
                ContactHoursZh = "平日 09:00–18:00",
                LeagueShortNameZh = "測試簡稱",
                BlueWhaleSiteUrl = "https://bw-test.tcrfc.tw",
            };

            var updateResponse = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/site-facts", updateRequest, TestJson.WriteOptions);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            var updated = await updateResponse.Content.ReadFromJsonAsync<AdminSiteFactsDto>(TestJson.Options);
            Assert.Equal(updateRequest.ContactPhone, updated!.ContactPhone);
            Assert.Equal("平日 09:00–18:00", updated.ContactHoursZh);
            Assert.Null(updated.ContactHoursEn); // 省略英文＝清空既有英文列（目前種子資料本來就沒有）。
            Assert.Equal("測試簡稱", updated.LeagueShortNameZh);
            Assert.Equal("https://bw-test.tcrfc.tw", updated.BlueWhaleSiteUrl);
            // 主場場地引用清單原樣保留（送出時帶著既有 Id）。
            var venue = Assert.Single(updated.HomeVenues);
            Assert.Equal(original!.HomeVenues[0].Id, venue.Id);
            Assert.Equal("西屯足球場", venue.NameZh);

            // 再讀一次，確認真的落地而不是回應內容剛好對得上。
            var reread = await (await client.GetAsync("/api/v1/admin/tcrfc/site-facts"))
                .Content.ReadFromJsonAsync<AdminSiteFactsDto>(TestJson.Options);
            Assert.Equal(updateRequest.ContactPhone, reread!.ContactPhone);

            // 公開端點（不需要登入）應該看得到同一份值——測試環境是 NoOpQueryCache，立即反映。
            using var publicClient = fixture.CreateClient();
            var publicFacts = await (await publicClient.GetAsync("/api/v1/tcrfc/site-facts"))
                .Content.ReadFromJsonAsync<PublicSiteFactsDto>(TestJson.Options);
            Assert.Equal(updateRequest.ContactPhone, publicFacts!.Contact.Phone);
            Assert.Equal("平日 09:00–18:00", publicFacts.Contact.Hours);
            Assert.Equal("測試簡稱", publicFacts.League.ShortName);
            Assert.Equal("https://bw-test.tcrfc.tw", publicFacts.BlueWhaleSiteUrl);
        }
        finally
        {
            var restoreRequest = ToUpdateRequest(original!);
            var restoreResponse = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/site-facts", restoreRequest, TestJson.WriteOptions);
            Assert.True(restoreResponse.IsSuccessStatusCode, $"還原站台事實失敗：{restoreResponse.StatusCode}");
        }
    }

    [Theory]
    [InlineData("http://bw-stg.tcrfc.tw")] // 不接受 http（規格要求 https）。
    [InlineData("bw-stg.tcrfc.tw")] // 不是絕對網址（沒有 scheme）。
    [InlineData("javascript:alert(1)")] // 有 scheme 但不是 https，同樣要擋。
    public async Task 藍鯨官網網址不是https_回400(string invalidUrl)
    {
        using var client = await CreateSuperAdminClientAsync();
        var original = await (await client.GetAsync("/api/v1/admin/tcrfc/site-facts"))
            .Content.ReadFromJsonAsync<AdminSiteFactsDto>(TestJson.Options);

        var request = ToUpdateRequest(original!) with { BlueWhaleSiteUrl = invalidUrl };
        var response = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/site-facts", request, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 找不到指定的既有場地Id_回400()
    {
        using var client = await CreateSuperAdminClientAsync();
        var original = await (await client.GetAsync("/api/v1/admin/tcrfc/site-facts"))
            .Content.ReadFromJsonAsync<AdminSiteFactsDto>(TestJson.Options);

        var request = ToUpdateRequest(original!) with
        {
            HomeVenues = [new UpdateSiteFactVenueRequest { Id = Guid.NewGuid(), NameZh = "不存在的場地" }],
        };

        var response = await client.PutAsJsonAsync("/api/v1/admin/tcrfc/site-facts", request, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static UpdateSiteFactsRequest ToUpdateRequest(AdminSiteFactsDto dto) => new()
    {
        FoundedYear = dto.FoundedYear,
        FoundingDateIso = dto.FoundingDateIso,
        FoundingDateDisplayZh = dto.FoundingDateDisplayZh,
        FoundingDateDisplayEn = dto.FoundingDateDisplayEn,
        FoundingTitleZh = dto.FoundingTitleZh,
        FoundingTitleEn = dto.FoundingTitleEn,
        LeagueNameZh = dto.LeagueNameZh,
        LeagueNameEn = dto.LeagueNameEn,
        LeagueShortNameZh = dto.LeagueShortNameZh,
        LeagueShortNameEn = dto.LeagueShortNameEn,
        SquadStructureZh = dto.SquadStructureZh,
        SquadStructureEn = dto.SquadStructureEn,
        SquadCodes = dto.SquadCodes,
        HomeVenues = dto.HomeVenues.Select(v => new UpdateSiteFactVenueRequest
        {
            Id = v.Id,
            NameZh = v.NameZh,
            NameEn = v.NameEn,
            Address = v.Address,
        }).ToList(),
        ContactPhone = dto.ContactPhone,
        ContactHoursZh = dto.ContactHoursZh,
        ContactHoursEn = dto.ContactHoursEn,
        BlueWhaleSiteUrl = dto.BlueWhaleSiteUrl,
    };

    // ───────────────────────────── 公開端點 ─────────────────────────────

    [Fact]
    public async Task 公開端點_預設中文_成立年份主場聯賽梯隊皆正確()
    {
        using var client = fixture.CreateClient();

        var dto = await (await client.GetAsync("/api/v1/tcrfc/site-facts"))
            .Content.ReadFromJsonAsync<PublicSiteFactsDto>(TestJson.Options);

        Assert.NotNull(dto);
        Assert.Equal("2024", dto!.FoundedYear);
        Assert.Null(dto.FoundingDateIso); // 確切成立月日未核實。
        Assert.Equal("2024 年創立", dto.FoundedDisplay);
        Assert.Equal("全國乙級聯賽冠軍", dto.FoundingTitle);
        Assert.Equal("企業甲級聯賽", dto.League.Name);
        Assert.Null(dto.League.ShortName);
        Assert.Equal(["U15", "U14", "U12"], dto.SquadCodes);

        var venue = Assert.Single(dto.Venues);
        Assert.Equal("西屯足球場", venue.Name);
        Assert.Equal("台中市北屯區崇平路二段景谷巷 11 弄 41 號", venue.Address);
        Assert.True(venue.IsHomeGround);

        Assert.Equal(venue.Address, dto.Contact.Address);
        // 電話與營業時間沒有核實資料，種子放的是明顯的測試值（db/seed/README.md「測試值清單」）；
        // 這裡只驗證「有值會被公開端點帶出」，不綁定測試值本身，正式資料替換後測試仍成立。
        Assert.False(string.IsNullOrWhiteSpace(dto.Contact.Phone));
        Assert.False(string.IsNullOrWhiteSpace(dto.Contact.Hours));
        Assert.Equal("https://bw-stg.tcrfc.tw", dto.BlueWhaleSiteUrl);
    }

    [Fact]
    public async Task 公開端點_lang等於en_解析英文並在缺英文時回退中文()
    {
        using var client = fixture.CreateClient();

        var dto = await (await client.GetAsync("/api/v1/tcrfc/site-facts?lang=en"))
            .Content.ReadFromJsonAsync<PublicSiteFactsDto>(TestJson.Options);

        Assert.NotNull(dto);
        Assert.Equal("Enterprise Premier League", dto!.League.Name); // 有英文值就用英文。
        Assert.Equal("2024 年創立", dto.FoundedDisplay); // 沒有英文值時回退中文。

        var venue = Assert.Single(dto.Venues);
        Assert.Equal("Xitun Football Field", venue.Name); // 場地本身有英文名稱。
        Assert.Equal("台中市北屯區崇平路二段景谷巷 11 弄 41 號", venue.Address); // 場地沒有英文地址，回退中文。
    }

    [Fact]
    public async Task 公開端點_俱樂部隔離_藍鯨看不到磐石的事實()
    {
        using var client = fixture.CreateClient();

        var tcrfc = await (await client.GetAsync("/api/v1/tcrfc/site-facts"))
            .Content.ReadFromJsonAsync<PublicSiteFactsDto>(TestJson.Options);
        var bw = await (await client.GetAsync("/api/v1/bw/site-facts"))
            .Content.ReadFromJsonAsync<PublicSiteFactsDto>(TestJson.Options);

        Assert.NotEqual(tcrfc!.League.Name, bw!.League.Name);
        Assert.Equal("台灣木蘭足球聯賽", bw.League.Name);
        Assert.Equal("木蘭聯賽", bw.League.ShortName);
        Assert.Equal(["U15", "U12"], bw.SquadCodes);
        Assert.Equal(2, bw.Venues.Count);
        Assert.Equal("台中北屯太原足球場", bw.Venues[0].Name); // 主要主場排在第一筆。
        Assert.Equal(bw.Venues[0].Address, bw.Contact.Address);
        Assert.Null(bw.FoundingTitle); // bw 沒有這筆事實，且不得沾到 tcrfc 的值。
        Assert.Null(bw.BlueWhaleSiteUrl); // 概念上只屬於 tcrfc，bw 不得沾到 tcrfc 的值。
        Assert.Equal("https://bw-stg.tcrfc.tw", tcrfc.BlueWhaleSiteUrl);
    }
}
