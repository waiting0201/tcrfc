using Tcrfc.Api.Features.Honors;
using Tcrfc.Api.Features.Partners;
using Tcrfc.Api.Features.Sponsors;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 守門測試（唯讀）：官網「後台沒資料就顯示寫死內容」的後備拿掉之前，真實內容必須已經在資料庫裡
/// （種子區段 60b，<c>db/seed/fallback_content_seed.py</c>；已有資料的庫走 <c>db/migrations/20261113_fallback-content-import.sql</c>）。
/// 這裡走公開端點（Repository 的顯示條件：合作期間、已發布、俱樂部分區），確認每一項搬進去的資料「官網真的讀得到」。
/// 庫沒灌這一段或被人改掉會先紅燈，不要把它當成會自己修復的測試。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class FallbackContentSeedTests(AdminWriteApiFixture fixture)
{
    [Fact]
    public async Task 磐石國際夥伴三筆_官網讀得到_中英名稱與地區欄位正確()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var type = Uri.EscapeDataString("國際夥伴");
        foreach (var lang in new[] { "zh", "en" })
        {
            var list = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync($"/api/v1/tcrfc/partners?type={type}&lang={lang}"));
            foreach (var (slug, name, country) in new[]
            {
                ("intl-hellas-verona", "Hellas Verona FC", "義大利 Italy"),
                ("intl-rayo-alcobendas", "Rayo Ciudad Alcobendas CF", "西班牙 Spain"),
                ("intl-rot-weiss-ahlen", "Rot-Weiss Ahlen", "德國 Germany"),
            })
            {
                var p = Assert.Single(list, x => x.Slug == slug);
                Assert.Equal(name, p.Name);
                Assert.Equal(country, p.Country); // 3.4 國際路徑頁依 country 比對地區
                Assert.False(p.IsFallbackLocale);
                Assert.False(string.IsNullOrWhiteSpace(p.Content));
                Assert.Null(p.StartOn); // 只知道新聞日期、不知道簽署日，不填
            }
        }

        var en = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync($"/api/v1/tcrfc/partners?type={type}&lang=en"));
        Assert.DoesNotContain(en.Where(p => p.Slug.StartsWith("intl-")), p => System.Text.RegularExpressions.Regex.IsMatch(p.Content ?? "", "[㐀-鿿]")); // 英文頁不得出現中文
        var bw = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync($"/api/v1/bw/partners?type={type}"));
        Assert.DoesNotContain(bw, p => p.Slug.StartsWith("intl-")); // 藍鯨看不到磐石的國際夥伴（E-83）
    }

    [Fact]
    public async Task 藍鯨建教合作學校五所_官網讀得到_磐石看不到()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var type = Uri.EscapeDataString("教育夥伴");
        var zh = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync($"/api/v1/bw/partners?type={type}&lang=zh"));
        var schools = zh.Where(p => p.Slug.StartsWith("school-")).OrderBy(p => p.SortOrder).ToList();
        Assert.Equal(5, schools.Count);
        Assert.Equal("國立臺灣體育運動大學女子足球隊", schools[0].Name);
        Assert.Contains(schools, p => p.Name == "臺中市篤行國小女子足球隊");
        Assert.All(schools, p => Assert.Contains("建教合作", p.Content));
        Assert.All(schools, p => Assert.Null(p.StartOn)); // 年份只寫在合作內容文字，不編造精確日期
        Assert.Contains("2015", schools.Single(p => p.Slug == "school-wuquan-jhs").Content);

        var en = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync($"/api/v1/bw/partners?type={type}&lang=en"));
        var enSchools = en.Where(p => p.Slug.StartsWith("school-")).ToList();
        Assert.Equal(5, enSchools.Count);
        Assert.All(enSchools, p => Assert.False(p.IsFallbackLocale));

        var tcrfc = await BizTest.ReadAsync<List<PartnerDto>>(await client.GetAsync($"/api/v1/tcrfc/partners?type={type}"));
        Assert.DoesNotContain(tcrfc, p => p.Slug.StartsWith("school-"));
    }

    [Fact]
    public async Task 贊助方案名稱_藍鯨八張_磐石至少九張同名方案_價格不公開()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var bw = await BizTest.ReadAsync<List<SponsorPackageDto>>(await client.GetAsync("/api/v1/bw/sponsor-packages?lang=zh"));
        var names = bw.OrderBy(p => p.SortOrder).Select(p => p.Name).ToList();
        Assert.Equal(
            ["俱樂部贊助", "青年隊贊助", "球隊贊助", "營隊贊助", "國際計畫贊助", "商品合作", "球迷會贊助", "場館冠名"],
            names.Take(8).ToList());
        Assert.DoesNotContain(bw, p => p.Name == "漫畫內容合作"); // 藍鯨不設漫畫（藍鯨規劃書 §2.1）
        Assert.All(bw.Where(p => p.Slug.StartsWith("package-")), p => { Assert.Null(p.PriceMin); Assert.Null(p.PriceMax); });

        var bwEn = await BizTest.ReadAsync<List<SponsorPackageDto>>(await client.GetAsync("/api/v1/bw/sponsor-packages?lang=en"));
        Assert.Contains(bwEn, p => p.Name == "Youth Team Sponsorship" && !p.IsFallbackLocale);

        // 磐石：本機種子已有同名的【測試】方案（名稱＝規劃書方案名），區段 60b 以「同名不補」避免出現 18 張卡
        var tcrfc = await BizTest.ReadAsync<List<SponsorPackageDto>>(await client.GetAsync("/api/v1/tcrfc/sponsor-packages?lang=zh"));
        foreach (var name in new[] { "俱樂部贊助", "學院贊助", "球隊贊助", "營隊贊助", "國際計畫贊助", "漫畫內容合作", "商品合作", "球迷會贊助" })
        {
            Assert.Single(tcrfc, p => p.Name == name);
        }
    }

    [Fact]
    public async Task 磐石成立首年榮譽_官網讀得到_英文名稱存在()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var zh = await BizTest.ReadAsync<List<AchievementDto>>(await client.GetAsync("/api/v1/tcrfc/achievements?lang=zh"));
        var h = Assert.Single(zh, a => a.Year == 2024 && a.CompetitionName == "全國乙級聯賽");
        Assert.Equal("冠軍", h.Placing);
        Assert.Equal("D1", h.TeamCode);

        var en = await BizTest.ReadAsync<List<AchievementDto>>(await client.GetAsync("/api/v1/tcrfc/achievements?lang=en"));
        var he = Assert.Single(en, a => a.Id == h.Id);
        Assert.Equal("National Second Division", he.CompetitionName);
        Assert.Equal("Champions", he.Placing);
    }
}
