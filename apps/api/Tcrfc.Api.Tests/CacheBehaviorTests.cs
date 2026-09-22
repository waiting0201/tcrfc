using System.Net;
using System.Net.Http.Json;
using StackExchange.Redis;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.News;
using Tcrfc.Api.Features.Players;
using Tcrfc.Api.Features.Schedule;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 五組唯讀 repository 接上 <c>IQueryCache</c> 之後的行為驗證（S0-7d 續作，2026-09-21，使用者拍板）。
/// 用真正在跑的 redis-server（<see cref="RedisEnabledApiFixture"/>），驗證：
/// 1. qualifier 真的涵蓋每個會改變結果的查詢參數（漏一個＝換了篩選條件卻拿到舊結果）。
/// 2. club／locale 維度真的隔離（不是「看程式碼推論應該沒問題」，是直接用 Redis 用戶端核對 key）。
/// 3. 文章單篇 404 不快取。
/// </summary>
[Collection(RedisEnabledCollection.Name)]
public sealed class CacheBehaviorTests(RedisEnabledApiFixture fixture)
{
    [Fact]
    public async Task 同一組參數連打兩次_結果一致()
    {
        using var client = fixture.CreateClient();

        var first = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=5", TestJson.Options);
        var second = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=5", TestJson.Options);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.TotalCount, second!.TotalCount);
        Assert.Equal(first.Items.Select(p => p.Id), second.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task players_換pageSize_拿到不同結果_證明qualifier涵蓋分頁參數()
    {
        using var client = fixture.CreateClient();

        var pageSizeOne = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=1", TestJson.Options);
        var pageSizeTwo = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=2", TestJson.Options);

        Assert.NotNull(pageSizeOne);
        Assert.NotNull(pageSizeTwo);
        // 若 qualifier 漏了 pageSize，這裡 pageSizeTwo 會悄悄變成跟 pageSizeOne 一樣的 1 筆
        // （命中 pageSize=1 那次快取住的結果）。
        Assert.Single(pageSizeOne!.Items);
        Assert.Equal(2, pageSizeTwo!.Items.Count);
    }

    [Fact]
    public async Task players_換team篩選_拿到不同結果_證明qualifier涵蓋team參數()
    {
        using var client = fixture.CreateClient();

        var unfiltered = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=200", TestJson.Options);
        var noSuchTeam = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?team=does-not-exist&pageSize=200", TestJson.Options);

        Assert.NotNull(unfiltered);
        Assert.NotNull(noSuchTeam);
        Assert.True(unfiltered!.TotalCount > 0, "種子資料應該有球員");
        // 若 qualifier 漏了 team，這裡會誤命中「不帶 team」那次的快取，回傳非 0。
        Assert.Equal(0, noSuchTeam!.TotalCount);
    }

    [Fact]
    public async Task articles_換category篩選_拿到不同結果_證明qualifier涵蓋category參數()
    {
        using var client = fixture.CreateClient();

        var all = await client.GetFromJsonAsync<PagedResult<ArticleListItemDto>>(
            "/api/v1/tcrfc/news?pageSize=200", TestJson.Options);
        var matchOnly = await client.GetFromJsonAsync<PagedResult<ArticleListItemDto>>(
            "/api/v1/tcrfc/news?category=match&pageSize=200", TestJson.Options);

        Assert.NotNull(all);
        Assert.NotNull(matchOnly);
        Assert.True(all!.TotalCount > matchOnly!.TotalCount, "分類篩選應該讓筆數變少，否則 qualifier 可能漏了 category");
    }

    [Fact]
    public async Task matches_換season與status篩選_拿到不同結果_證明qualifier涵蓋這兩個參數()
    {
        using var client = fixture.CreateClient();

        var all = await client.GetFromJsonAsync<PagedResult<MatchDto>>(
            "/api/v1/tcrfc/schedule?pageSize=200", TestJson.Options);
        var noSuchSeason = await client.GetFromJsonAsync<PagedResult<MatchDto>>(
            "/api/v1/tcrfc/schedule?season=does-not-exist&pageSize=200", TestJson.Options);
        var noSuchStatus = await client.GetFromJsonAsync<PagedResult<MatchDto>>(
            "/api/v1/tcrfc/schedule?status=does-not-exist&pageSize=200", TestJson.Options);

        Assert.NotNull(all);
        Assert.True(all!.TotalCount > 0, "種子資料應該有賽程");
        Assert.Equal(0, noSuchSeason!.TotalCount);
        Assert.Equal(0, noSuchStatus!.TotalCount);
    }

    [Fact]
    public async Task 文章單篇_不同slug拿到不同內容_不會互相污染()
    {
        using var client = fixture.CreateClient();

        var list = await client.GetFromJsonAsync<PagedResult<ArticleListItemDto>>(
            "/api/v1/tcrfc/news?pageSize=2", TestJson.Options);
        Assert.NotNull(list);
        Assert.True(list!.Items.Count >= 2, "種子資料應該至少有兩篇 tcrfc 新聞");

        var slugA = list.Items[0].Slug;
        var slugB = list.Items[1].Slug;

        var articleA = await client.GetFromJsonAsync<ArticleDetailDto>($"/api/v1/tcrfc/news/{slugA}", TestJson.Options);
        var articleB = await client.GetFromJsonAsync<ArticleDetailDto>($"/api/v1/tcrfc/news/{slugB}", TestJson.Options);

        Assert.NotNull(articleA);
        Assert.NotNull(articleB);
        Assert.NotEqual(articleA!.Id, articleB!.Id);
        Assert.Equal(slugA, articleA.Slug);
        Assert.Equal(slugB, articleB.Slug);
    }

    [Fact]
    public async Task 文章單篇_查無資料的404不寫入快取()
    {
        using var client = fixture.CreateClient();
        var server = fixture.RedisInspector.GetServer(fixture.RedisInspector.GetEndPoints()[0]);

        const string missingSlug = "this-slug-does-not-exist-anywhere";
        var response = await client.GetAsync($"/api/v1/tcrfc/news/{missingSlug}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // 🔴 關鍵斷言：查無資料時 IQueryCache.GetOrCreateAsync 不應該寫入任何 key。
        var keysForMissingSlug = server.Keys(pattern: $"*article-detail*{missingSlug}*").ToArray();
        Assert.Empty(keysForMissingSlug);
    }

    [Fact]
    public async Task key真的依club維度隔離_用redis直接核對()
    {
        using var client = fixture.CreateClient();
        var server = fixture.RedisInspector.GetServer(fixture.RedisInspector.GetEndPoints()[0]);

        // 先把 tcrfc 打熱。
        var tcrfcResult = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=50", TestJson.Options);
        // 再打 bw——如果 club 維度沒隔離，這裡有可能誤命中 tcrfc 那把 key 並拿到同一批球員。
        var bwResult = await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/bw/players?pageSize=50", TestJson.Options);

        Assert.NotNull(tcrfcResult);
        Assert.NotNull(bwResult);
        Assert.True(tcrfcResult!.TotalCount > 0);
        // 🔴 2026-09-22：原本這裡斷言 bw 一定是 0 筆，但 BW-0g（藍鯨舊站資料匯入，見 git log）
        // 是獨立且合法的任務，已經把真實藍鯨球員資料灌進 tcrfc_club_dev，bw 現在也有球員了。
        // 改成驗證「兩邊球員 id 不重疊」——不論資料量怎麼變都能驗證 club_id 範圍真的有隔離，
        // 跟下面「Redis key 本身不重疊」是同一件事的兩種驗證角度（HTTP 回應層 ＋ 快取層）。
        var tcrfcIds = tcrfcResult.Items.Select(p => p.Id).ToHashSet();
        var bwIds = bwResult!.Items.Select(p => p.Id).ToHashSet();
        Assert.Empty(tcrfcIds.Intersect(bwIds));

        // 直接用 Redis 用戶端核對：兩個俱樂部各自有一把不同的 key，且 key 字串本身就標明是哪個俱樂部。
        var playerKeys = server.Keys(pattern: "*:players:*").ToArray();
        var tcrfcKeys = playerKeys.Where(k => k.ToString().Contains(":tcrfc:")).ToArray();
        var bwKeys = playerKeys.Where(k => k.ToString().Contains(":bw:")).ToArray();

        Assert.NotEmpty(tcrfcKeys);
        Assert.NotEmpty(bwKeys);
        Assert.DoesNotContain(tcrfcKeys, k => bwKeys.Contains(k));
    }

    [Fact]
    public async Task key真的依locale維度隔離_中英文各自一把key()
    {
        using var client = fixture.CreateClient();
        var db = fixture.RedisInspector.GetDatabase();
        var server = fixture.RedisInspector.GetServer(fixture.RedisInspector.GetEndPoints()[0]);

        await client.GetFromJsonAsync<Tcrfc.Api.Features.Clubs.ClubDto>("/api/v1/clubs/tcrfc?lang=zh", TestJson.Options);
        await client.GetFromJsonAsync<Tcrfc.Api.Features.Clubs.ClubDto>("/api/v1/clubs/tcrfc?lang=en", TestJson.Options);

        // key 格式是 v{ver}:{club}:{locale}:{entity}:{qualifier}——locale 在 club 之後、entity 之前，
        // 用 "*club-detail*" 撈全部再依 locale 子字串篩選，不要假設 entity 名稱在 key 裡的相對位置。
        var clubDetailKeys = server.Keys(pattern: "*:tcrfc:*:club-detail:*").ToArray();
        var zhKeys = clubDetailKeys.Where(k => k.ToString().Contains(":zh-Hant:")).ToArray();
        var enKeys = clubDetailKeys.Where(k => k.ToString().Contains(":en:")).ToArray();

        Assert.NotEmpty(zhKeys);
        Assert.NotEmpty(enKeys);
        Assert.DoesNotContain(zhKeys, k => enKeys.Contains(k));
    }

    [Fact]
    public async Task 快取的key有TTL兜底_不是永久存活()
    {
        using var client = fixture.CreateClient();
        var db = fixture.RedisInspector.GetDatabase();
        var server = fixture.RedisInspector.GetServer(fixture.RedisInspector.GetEndPoints()[0]);

        await client.GetFromJsonAsync<PagedResult<PlayerDto>>(
            "/api/v1/tcrfc/players?pageSize=7", TestJson.Options);

        var matchingKeys = server.Keys(pattern: "*:players:*:7").ToArray();
        Assert.True(matchingKeys.Length > 0, "應該要有一把命中 pageSize=7 的 key");

        var ttl = await db.KeyTimeToLiveAsync(matchingKeys[0]);
        Assert.NotNull(ttl);
        Assert.True(ttl!.Value.TotalSeconds > 0 && ttl.Value.TotalSeconds <= 300,
            $"TTL 應該落在 (0, 300] 秒之間（預設值），實際是 {ttl.Value.TotalSeconds} 秒");
    }
}
