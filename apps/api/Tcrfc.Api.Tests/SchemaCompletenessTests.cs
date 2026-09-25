using Tcrfc.Api.Features.Seo;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// <see cref="SchemaRequiredFields"/> 的純邏輯單元測試（不碰 HTTP、不碰資料庫）——GEO-05／
/// S1-12c「必填欄位不得留空；資料不足時不輸出該型別」的判斷邏輯本身，跟任何一張資料表的
/// 實際查詢分開驗證。整合測試（<c>AdminSeoSchemaCompletenessTests</c>）驗證的是「資料庫查出來的
/// 值有沒有正確餵進這個判斷」，這裡驗證的是「判斷本身對不對」。
/// </summary>
public sealed class SchemaCompletenessTests
{
    [Fact]
    public void Article_三個必填欄位都有值_視為完整()
    {
        var values = new Dictionary<string, object?>
        {
            ["headline"] = "標題",
            ["datePublished"] = DateTime.UtcNow,
            ["image"] = "https://example.com/a.webp",
        };

        Assert.True(SchemaRequiredFields.IsComplete(SchemaType.Article, values));
        Assert.Empty(SchemaRequiredFields.GetMissingFields(SchemaType.Article, values));
    }

    [Fact]
    public void Article_缺圖片_列出image一項缺漏()
    {
        var values = new Dictionary<string, object?>
        {
            ["headline"] = "標題",
            ["datePublished"] = DateTime.UtcNow,
            ["image"] = null,
        };

        Assert.False(SchemaRequiredFields.IsComplete(SchemaType.Article, values));
        var missing = SchemaRequiredFields.GetMissingFields(SchemaType.Article, values);
        Assert.Single(missing);
        Assert.Equal("image", missing[0].Key);
    }

    [Fact]
    public void Article_空白字串視同缺漏_不是有值()
    {
        // 字串欄位用 IsNullOrWhiteSpace 判斷，不是只判斷 null——後台編輯欄位打了幾個空白字元
        // 存進去，不應該被誤判為「已填寫」。
        var values = new Dictionary<string, object?>
        {
            ["headline"] = "   ",
            ["datePublished"] = DateTime.UtcNow,
            ["image"] = "https://example.com/a.webp",
        };

        var missing = SchemaRequiredFields.GetMissingFields(SchemaType.Article, values);
        Assert.Single(missing);
        Assert.Equal("headline", missing[0].Key);
    }

    [Fact]
    public void 呼叫端漏傳某個必填欄位鍵_視為缺漏_不是視為有值()
    {
        // 防呆：字典裡完全沒有這個 key（不是「有 key 但值是 null」），一樣要被判定缺漏，
        // 不能因為呼叫端忘記塞值就被放行輸出殘缺 Schema。
        var values = new Dictionary<string, object?>
        {
            ["headline"] = "標題",
            ["datePublished"] = DateTime.UtcNow,
            // 沒有 "image" 這個鍵。
        };

        Assert.False(SchemaRequiredFields.IsComplete(SchemaType.Article, values));
        Assert.Contains(SchemaRequiredFields.GetMissingFields(SchemaType.Article, values), f => f.Key == "image");
    }

    [Fact]
    public void SportsEvent_六個必填欄位都有值_視為完整()
    {
        var values = new Dictionary<string, object?>
        {
            ["matchOn"] = new DateOnly(2026, 10, 1),
            ["kickoff"] = "19:00",
            ["homeAway"] = "HOME",
            ["opponent"] = "測試對手",
            ["venue"] = "測試球場",
            ["competitionName"] = "測試聯賽",
        };

        Assert.True(SchemaRequiredFields.IsComplete(SchemaType.SportsEvent, values));
    }

    [Theory]
    [InlineData("kickoff")]
    [InlineData("homeAway")]
    [InlineData("venue")]
    [InlineData("competitionName")]
    public void SportsEvent_缺任一欄位_視為不完整(string missingKey)
    {
        var values = new Dictionary<string, object?>
        {
            ["matchOn"] = new DateOnly(2026, 10, 1),
            ["kickoff"] = "19:00",
            ["homeAway"] = "HOME",
            ["opponent"] = "測試對手",
            ["venue"] = "測試球場",
            ["competitionName"] = "測試聯賽",
        };
        values[missingKey] = null;

        Assert.False(SchemaRequiredFields.IsComplete(SchemaType.SportsEvent, values));
        Assert.Contains(SchemaRequiredFields.GetMissingFields(SchemaType.SportsEvent, values), f => f.Key == missingKey);
    }

    [Fact]
    public void Organization_與_SportsTeam_都要求name_url_logo三欄()
    {
        var incomplete = new Dictionary<string, object?> { ["name"] = "台中磐石", ["url"] = null, ["logo"] = null };

        Assert.Equal(2, SchemaRequiredFields.GetMissingFields(SchemaType.Organization, incomplete).Count);
        Assert.Equal(2, SchemaRequiredFields.GetMissingFields(SchemaType.SportsTeam, incomplete).Count);
    }

    [Fact]
    public void Person_只要求姓名()
    {
        Assert.True(SchemaRequiredFields.IsComplete(SchemaType.Person, new Dictionary<string, object?> { ["name"] = "王小明" }));
        Assert.False(SchemaRequiredFields.IsComplete(SchemaType.Person, new Dictionary<string, object?> { ["name"] = null }));
    }

    [Fact]
    public void Course_要求名稱與說明()
    {
        var missing = SchemaRequiredFields.GetMissingFields(SchemaType.Course, new Dictionary<string, object?>
        {
            ["name"] = "U12 育成營",
            ["description"] = null,
        });
        Assert.Single(missing);
        Assert.Equal("description", missing[0].Key);
    }

    [Fact]
    public void BreadcrumbList_要求標題與網址()
    {
        Assert.True(SchemaRequiredFields.IsComplete(SchemaType.BreadcrumbList, new Dictionary<string, object?>
        {
            ["name"] = "關於我們",
            ["path"] = "/zh/about/",
        }));
        Assert.False(SchemaRequiredFields.IsComplete(SchemaType.BreadcrumbList, new Dictionary<string, object?>
        {
            ["name"] = null,
            ["path"] = "/zh/about/",
        }));
    }

    [Fact]
    public void FaqPage_要求問題與答案()
    {
        Assert.True(SchemaRequiredFields.IsComplete(SchemaType.FaqPage, new Dictionary<string, object?>
        {
            ["question"] = "怎麼加入球隊？",
            ["answer"] = "請填寫線上表單。",
        }));
        Assert.False(SchemaRequiredFields.IsComplete(SchemaType.FaqPage, new Dictionary<string, object?>
        {
            ["question"] = "怎麼加入球隊？",
            ["answer"] = null,
        }));
    }

    [Fact]
    public void Event_要求名稱_開始時間_地點()
    {
        var missing = SchemaRequiredFields.GetMissingFields(SchemaType.Event, new Dictionary<string, object?>
        {
            ["name"] = "球迷見面會",
            ["startDate"] = null,
            ["location"] = null,
        });
        Assert.Equal(2, missing.Count);
    }

    [Theory]
    [InlineData(SchemaType.Organization, "Organization")]
    [InlineData(SchemaType.SportsTeam, "SportsTeam")]
    [InlineData(SchemaType.Event, "Event")]
    [InlineData(SchemaType.SportsEvent, "SportsEvent")]
    [InlineData(SchemaType.Person, "Person")]
    [InlineData(SchemaType.Article, "Article")]
    [InlineData(SchemaType.Course, "Course")]
    [InlineData(SchemaType.BreadcrumbList, "BreadcrumbList")]
    [InlineData(SchemaType.FaqPage, "FAQPage")]
    public void SchemaTypeCodes_對應schema_org正確字面值(SchemaType type, string expectedCode)
    {
        Assert.Equal(expectedCode, SchemaTypeCodes.ToCode(type));
    }
}
