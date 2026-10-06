using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Clubs;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Features.Players;
using Tcrfc.Api.Features.Staff;
using Tcrfc.Api.Features.Teams;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// GEO-05（S1-12c／S1-12f）：本輪把 <c>SchemaEligible</c> 布林值接上另外四種公開端點
/// （<c>clubs</c>／<c>teams</c>／<c>players</c>／<c>staff</c>），並在 <c>ArticleDetailDto</c>
/// 補上 <c>BreadcrumbSchemaEligible</c>。<see cref="SchemaCompletenessTests"/> 已經驗證判斷邏輯
/// 本身（<c>SchemaRequiredFields.IsComplete</c>）對不對，這裡驗證的是「資料庫查出來的值有沒有
/// 正確餵進那個判斷」——跟 <c>AdminSeoSchemaCompletenessTests</c> 對後台報表端點的分工一樣。
///
/// 🔴 這裡用 <see cref="ApiFixture"/>（<c>NoOpQueryCache</c>），不需要處理快取失效——直接寫資料庫、
/// 直接讀公開端點就會反映最新值，理由見 <c>ApiFixture</c> 檔頭註解。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PublicSchemaEligibilityTests(ApiFixture fixture)
{
    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    private static async Task ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(RequireConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Club_有名稱與網域_Organization合格_標誌不是資料庫必填欄位()
    {
        using var client = fixture.CreateClient();

        var club = await client.GetFromJsonAsync<ClubDto>("/api/v1/clubs/tcrfc", TestJson.Options);

        Assert.NotNull(club);
        // 🔴 主站規劃書 v3.20：標誌由前台靜態資產輸出，clubs 已無 logo／favicon／品牌色欄位；
        // Organization 必填只剩名稱與網域（名稱缺漏時回退俱樂部代碼，網域為 NOT NULL），兩個俱樂部恆合格。
        Assert.True(club!.SchemaEligible);
    }

    [Fact]
    public async Task Club_公開DTO不再含標誌與品牌欄位()
    {
        using var client = fixture.CreateClient();

        var json = await client.GetStringAsync("/api/v1/clubs/tcrfc");

        foreach (var removed in new[] { "logoUrl", "logoDarkUrl", "faviconUrl", "logoLightKey", "logoDarkKey", "faviconKey", "brandColor", "brandSecondaryColor" })
        {
            Assert.DoesNotContain($"\"{removed}\"", json);
        }

        Assert.Contains("\"ogImageKey\"", json); // og_image_* 仍保留
    }

    [Fact]
    public async Task Team_有隊名與俱樂部網域_SportsTeam合格_識別圖片不影響合格()
    {
        using var client = fixture.CreateClient();

        var teams = await client.GetFromJsonAsync<IReadOnlyList<TeamDto>>("/api/v1/tcrfc/teams", TestJson.Options);
        var d1 = teams?.SingleOrDefault(t => t.Code == "D1");

        Assert.NotNull(d1);
        Assert.True(d1!.SchemaEligible);

        // hero_key 只影響 HeroUrl（球隊頁 Hero 版位），不影響 SportsTeam 合格與否（標誌已不是必填欄位）。
        try
        {
            await ExecuteAsync(
                "UPDATE teams SET hero_key = @Key WHERE club_id = (SELECT id FROM clubs WHERE code = 'tcrfc') AND code = 'D1'",
                ("@Key", "brand/tcrfc/test-team-hero.webp"));
            var again = await client.GetFromJsonAsync<IReadOnlyList<TeamDto>>("/api/v1/tcrfc/teams", TestJson.Options);
            Assert.True(again!.Single(t => t.Code == "D1").SchemaEligible);
        }
        finally
        {
            await ExecuteAsync(
                "UPDATE teams SET hero_key = NULL WHERE club_id = (SELECT id FROM clubs WHERE code = 'tcrfc') AND code = 'D1'");
        }
    }

    [Fact]
    public async Task Players_種子資料皆有姓名_Person全數合格()
    {
        using var client = fixture.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=200", TestJson.Options);

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Items);
        Assert.All(result.Items, p => Assert.True(p.SchemaEligible));
        // 種子資料的球員照片皆未設定同意（見 db/seed README「已知落差」），PhotoUrl 應維持 null，
        // 不因為新增這個欄位就悄悄繞過肖像同意的 fail-closed 規則。
        Assert.All(result.Items, p => Assert.Null(p.PhotoUrl));
    }

    [Fact]
    public async Task Staff_種子資料皆有姓名_Person全數合格()
    {
        using var client = fixture.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResult<StaffDto>>(
            "/api/v1/tcrfc/staff?pageSize=100", TestJson.Options);

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Items);
        Assert.All(result.Items, s => Assert.True(s.SchemaEligible));
        Assert.All(result.Items, s => Assert.Null(s.PhotoUrl));
    }

    [Fact]
    public async Task Article_有標題與網址_BreadcrumbList合格()
    {
        using var client = fixture.CreateClient();

        var list = await client.GetFromJsonAsync<PagedResult<ArticleListItemDto>>(
            "/api/v1/tcrfc/news?pageSize=1", TestJson.Options);
        Assert.NotNull(list);
        var slug = Assert.Single(list!.Items).Slug;

        var article = await client.GetFromJsonAsync<ArticleDetailDto>($"/api/v1/tcrfc/news/{slug}", TestJson.Options);

        Assert.NotNull(article);
        Assert.True(article!.BreadcrumbSchemaEligible);
    }
}
