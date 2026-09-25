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
    public async Task Club_種子資料沒有隊徽物件鍵_Organization不合格()
    {
        using var client = fixture.CreateClient();

        var club = await client.GetFromJsonAsync<ClubDto>("/api/v1/clubs/tcrfc", TestJson.Options);

        Assert.NotNull(club);
        // 🔴 已知現況（見 ClubDto.SchemaEligible 檔頭）：clubs.logo_light_key 目前沒有任何寫入路徑，
        // 種子資料恆為 null，這裡如實反映「缺漏者不輸出」，不是本次判斷有誤。
        Assert.False(club!.SchemaEligible);
        Assert.Null(club.LogoUrl);
    }

    [Fact]
    public async Task Club_補上隊徽物件鍵後_Organization合格且LogoUrl有值()
    {
        try
        {
            await ExecuteAsync(
                "UPDATE clubs SET logo_light_key = @Key WHERE code = 'tcrfc'",
                ("@Key", "brand/tcrfc/test-logo.webp"));

            using var client = fixture.CreateClient();
            var club = await client.GetFromJsonAsync<ClubDto>("/api/v1/clubs/tcrfc", TestJson.Options);

            Assert.NotNull(club);
            Assert.True(club!.SchemaEligible);
            // 🔴 ApiFixture 沒有接真實 Azurite，IImagePublicUrlResolver 綁的是
            // UnavailableImagePublicUrlResolver（一律回傳 null），所以這裡不斷言 LogoUrl 非空——
            // Resolve() 本身的解析邏輯已由 AdminSeoImageTests（真實 Azurite）驗證過，這裡只驗證
            // SchemaEligible 有沒有正確吃到新寫入的 logo_light_key。
        }
        finally
        {
            await ExecuteAsync("UPDATE clubs SET logo_light_key = NULL WHERE code = 'tcrfc'");
        }
    }

    [Fact]
    public async Task Team_種子資料沒有識別圖片_SportsTeam不合格()
    {
        using var client = fixture.CreateClient();

        var teams = await client.GetFromJsonAsync<IReadOnlyList<TeamDto>>("/api/v1/tcrfc/teams", TestJson.Options);
        var d1 = teams?.SingleOrDefault(t => t.Code == "D1");

        Assert.NotNull(d1);
        // 🔴 已知現況（見 TeamDto.SchemaEligible 檔頭）：teams.hero_key 與 clubs.logo_light_key
        // 種子資料皆為 null，兩者擇一都拿不到值，如實反映不合格。
        Assert.False(d1!.SchemaEligible);
        Assert.Null(d1.LogoUrl);
    }

    [Fact]
    public async Task Team_補上球隊識別圖片後_SportsTeam合格()
    {
        try
        {
            await ExecuteAsync(
                "UPDATE teams SET hero_key = @Key WHERE club_id = (SELECT id FROM clubs WHERE code = 'tcrfc') AND code = 'D1'",
                ("@Key", "brand/tcrfc/test-team-hero.webp"));

            using var client = fixture.CreateClient();
            var teams = await client.GetFromJsonAsync<IReadOnlyList<TeamDto>>("/api/v1/tcrfc/teams", TestJson.Options);
            var d1 = teams?.SingleOrDefault(t => t.Code == "D1");

            Assert.NotNull(d1);
            Assert.True(d1!.SchemaEligible); // LogoUrl 不斷言非空，理由同 Club 那組測試
        }
        finally
        {
            await ExecuteAsync(
                "UPDATE teams SET hero_key = NULL WHERE club_id = (SELECT id FROM clubs WHERE code = 'tcrfc') AND code = 'D1'");
        }
    }

    [Fact]
    public async Task Team_球隊自己沒有識別圖片但俱樂部有隊徽時_擇一合格()
    {
        try
        {
            await ExecuteAsync(
                "UPDATE clubs SET logo_light_key = @Key WHERE code = 'tcrfc'",
                ("@Key", "brand/tcrfc/test-logo.webp"));

            using var client = fixture.CreateClient();
            var teams = await client.GetFromJsonAsync<IReadOnlyList<TeamDto>>("/api/v1/tcrfc/teams", TestJson.Options);
            var d1 = teams?.SingleOrDefault(t => t.Code == "D1");

            Assert.NotNull(d1);
            Assert.Null(d1!.HeroKey); // 球隊自己沒有，回退用俱樂部隊徽
            Assert.True(d1.SchemaEligible); // LogoUrl 不斷言非空，理由同上
        }
        finally
        {
            await ExecuteAsync("UPDATE clubs SET logo_light_key = NULL WHERE code = 'tcrfc'");
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
