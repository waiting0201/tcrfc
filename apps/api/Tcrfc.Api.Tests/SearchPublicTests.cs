using System.Net;
using Tcrfc.Api.Features.Search;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// G-02 全站搜尋（公開端點）。🔴 本檔寫於沒有資料庫憑證的工作樹，<b>尚未實跑</b>（docs/18 E-121）；
/// 關鍵字解析、摘錄與查詢翻譯另有不需資料庫的測試（<c>SearchRepositoryOfflineTests</c>、<c>SiteBackendOfflineTranslationTests</c>）。
/// 每個測試用自己的唯一關鍵字（<c>ZZSEARCH＋8 碼</c>）造資料，互不干擾，finally 依 id 清除（i18n 側表由外鍵串聯刪除）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class SearchPublicTests(AdminWriteApiFixture fixture)
{
    private sealed class SearchData
    {
        public string Tag { get; } = "ZZSEARCH" + Guid.NewGuid().ToString("N")[..8];
        public List<Guid> Articles { get; } = [];
        public List<Guid> Faqs { get; } = [];
        public List<Guid> Programs { get; } = [];
        public List<Guid> Staff { get; } = [];
        public List<Guid> Players { get; } = [];
        public List<Guid> Charities { get; } = [];
        public List<Guid> CharityPrograms { get; } = [];
        public List<Guid> Records { get; } = [];

        public async Task CleanupAsync()
        {
            await Delete("impact_records", Records);
            await Delete("charity_programs", CharityPrograms);
            await Delete("charities", Charities);
            await Delete("players", Players);
            await Delete("staff", Staff);
            await Delete("programs", Programs);
            await Delete("faqs", Faqs);
            await Delete("articles", Articles);
        }

        private static async Task Delete(string table, List<Guid> ids)
        {
            foreach (var id in ids)
            {
                await BizTest.ExecuteSqlAsync($"DELETE FROM {table} WHERE id = @I", ("@I", id));
            }
        }
    }

    private static Task<Guid?> ClubOrNull(string? code) => code is null ? Task.FromResult<Guid?>(null) : ClubIdAsync(code);

    private static async Task<Guid?> ClubIdAsync(string code) => await C1Test.ClubIdAsync(code);

    private static async Task<Guid> AddArticleAsync(
        SearchData d, string? club, string titleZh, string? titleEn = null, string? summaryZh = null,
        string status = "published", DateTime? publishedAt = null)
    {
        var id = Guid.NewGuid();
        var clubId = await ClubOrNull(club);
        await BizTest.ExecuteSqlAsync(
            """
            INSERT INTO articles (id, club_id, slug, article_category_id, status, published_at)
            VALUES (@I, @Club, @Slug, (SELECT TOP 1 id FROM article_categories ORDER BY row_seq), @S, @P);
            INSERT INTO articles_i18n (article_id, locale, title, summary) VALUES (@I, N'zh-Hant', @Zh, @Sum);
            IF @En IS NOT NULL INSERT INTO articles_i18n (article_id, locale, title) VALUES (@I, N'en', @En);
            """,
            ("@I", id), ("@Club", clubId), ("@Slug", "zz-search-" + id.ToString("N")[..12]), ("@S", status),
            ("@P", publishedAt ?? DateTime.UtcNow.AddDays(-1)), ("@Zh", titleZh), ("@Sum", summaryZh), ("@En", titleEn));
        d.Articles.Add(id);
        return id;
    }

    private static async Task<Guid> AddFaqAsync(SearchData d, string club, string questionZh, string? questionEn = null, string answerZh = "答案內容", string status = "published")
    {
        var id = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            """
            INSERT INTO faqs (id, club_id, slug, status) VALUES (@I, @Club, @Slug, @S);
            INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@I, N'zh-Hant', @Q, @A);
            IF @QE IS NOT NULL INSERT INTO faqs_i18n (faq_id, locale, question, answer) VALUES (@I, N'en', @QE, N'English answer');
            """,
            ("@I", id), ("@Club", await ClubIdAsync(club)), ("@Slug", "zz-search-" + id.ToString("N")[..12]), ("@S", status),
            ("@Q", questionZh), ("@A", answerZh), ("@QE", questionEn));
        d.Faqs.Add(id);
        return id;
    }

    private static async Task<Guid> AddProgramAsync(SearchData d, string club, string nameZh, string status = "published")
    {
        var id = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            """
            INSERT INTO programs (id, club_id, slug, status) VALUES (@I, @Club, @Slug, @S);
            INSERT INTO programs_i18n (program_id, locale, name, intro) VALUES (@I, N'zh-Hant', @N, N'課程簡介');
            """,
            ("@I", id), ("@Club", await ClubIdAsync(club)), ("@Slug", "zz-search-" + id.ToString("N")[..12]), ("@S", status), ("@N", nameZh));
        d.Programs.Add(id);
        return id;
    }

    private static async Task<Guid> AddStaffAsync(SearchData d, string? club, string nameZh, string consent = "not_consented", string? photoKey = null)
    {
        var id = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            """
            INSERT INTO staff (id, club_id, portrait_consent_status, photo_key) VALUES (@I, @Club, @C, @P);
            INSERT INTO staff_i18n (staff_id, locale, name, title) VALUES (@I, N'zh-Hant', @N, N'助理教練');
            """,
            ("@I", id), ("@Club", await ClubOrNull(club)), ("@C", consent), ("@P", photoKey), ("@N", nameZh));
        d.Staff.Add(id);
        return id;
    }

    private static async Task<Guid?> AddPlayerAsync(SearchData d, string club, string nameZh)
    {
        var clubId = await ClubIdAsync(club);
        var team = await C1Test.ScalarAsync<Guid?>("SELECT TOP 1 id FROM teams WHERE club_id = @C ORDER BY row_seq", ("@C", clubId));
        if (team is null)
        {
            return null;
        }

        var id = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            """
            INSERT INTO players (id, club_id, team_id, slug) VALUES (@I, @Club, @T, N'zz-search-' + LEFT(CONVERT(nvarchar(36), @I), 8));
            INSERT INTO players_i18n (player_id, locale, name, bio) VALUES (@I, N'zh-Hant', @N, N'球員簡介');
            """,
            ("@I", id), ("@Club", clubId), ("@T", team), ("@N", nameZh));
        d.Players.Add(id);
        return id;
    }

    private async Task<SearchResponseDto> SearchAsync(string club, string query, string? type = null, string lang = "zh", int? page = null, int? pageSize = null)
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        var url = $"/api/v1/{club}/search?q={Uri.EscapeDataString(query)}&lang={lang}"
                  + (type is null ? string.Empty : $"&type={type}") + (page is null ? string.Empty : $"&page={page}")
                  + (pageSize is null ? string.Empty : $"&pageSize={pageSize}");
        return await BizTest.ReadAsync<SearchResponseDto>(await client.GetAsync(url));
    }

    [Fact]
    public async Task 新聞可見性_只回已發布且到時間_本俱樂部或共同_草稿排程未來發布與別站專屬都不出現()
    {
        var d = new SearchData();
        try
        {
            var visible = await AddArticleAsync(d, "tcrfc", $"{d.Tag} 公開新聞");
            var shared = await AddArticleAsync(d, null, $"{d.Tag} 共同新聞");
            var draft = await AddArticleAsync(d, "tcrfc", $"{d.Tag} 草稿", status: "draft");
            var scheduled = await AddArticleAsync(d, "tcrfc", $"{d.Tag} 排程", status: "scheduled", publishedAt: DateTime.UtcNow.AddDays(3));
            var future = await AddArticleAsync(d, "tcrfc", $"{d.Tag} 未來發布", publishedAt: DateTime.UtcNow.AddDays(3));
            var bwOnly = await AddArticleAsync(d, "bw", $"{d.Tag} 藍鯨專屬");

            var tcrfc = await SearchAsync("tcrfc", d.Tag, SearchTypes.News);
            var ids = tcrfc.Items.Select(i => i.Id).ToHashSet();
            Assert.Equal(new HashSet<Guid> { visible, shared }, ids);
            Assert.DoesNotContain(draft, ids);
            Assert.DoesNotContain(scheduled, ids);
            Assert.DoesNotContain(future, ids);
            Assert.DoesNotContain(bwOnly, ids);
            Assert.All(tcrfc.Items, i => Assert.Equal(SearchTypes.News, i.Type));
            Assert.All(tcrfc.Items, i => Assert.False(string.IsNullOrEmpty(i.Slug)));
            Assert.All(tcrfc.Items, i => Assert.False(string.IsNullOrEmpty(i.CategoryCode)));

            var bw = await SearchAsync("bw", d.Tag, SearchTypes.News);
            Assert.Equal(new HashSet<Guid> { shared, bwOnly }, bw.Items.Select(i => i.Id).ToHashSet());
        }
        finally
        {
            await d.CleanupAsync();
        }
    }

    [Fact]
    public async Task 多個關鍵字要全部命中_標題命中排在內文命中前面()
    {
        var d = new SearchData();
        try
        {
            var both = await AddArticleAsync(d, "tcrfc", $"{d.Tag} alpha beta");
            var onlyAlpha = await AddArticleAsync(d, "tcrfc", $"{d.Tag} alpha");
            var inBody = await AddArticleAsync(d, "tcrfc", "沒有關鍵字的標題", summaryZh: $"摘要裡有 {d.Tag} alpha 的字");

            var two = await SearchAsync("tcrfc", $"{d.Tag} alpha beta", SearchTypes.News);
            Assert.Equal([both], two.Items.Select(i => i.Id).ToList());
            Assert.Equal(3, two.Tokens.Count);

            var one = await SearchAsync("tcrfc", $"{d.Tag} alpha", SearchTypes.News);
            Assert.Equal(3, one.TotalCount);
            Assert.Equal(inBody, one.Items.Last().Id); // 只有內文命中的排最後
            Assert.Contains(d.Tag, one.Items.Last().Snippet, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(onlyAlpha, one.Items.Select(i => i.Id));
        }
        finally
        {
            await d.CleanupAsync();
        }
    }

    [Fact]
    public async Task 分類篩選與命中數_各類別可見性_課程與FAQ草稿不出現_教練可含共同資料()
    {
        var d = new SearchData();
        try
        {
            var article = await AddArticleAsync(d, "tcrfc", $"{d.Tag} 新聞");
            var faq = await AddFaqAsync(d, "tcrfc", $"{d.Tag} 常見問題？");
            await AddFaqAsync(d, "tcrfc", $"{d.Tag} 草稿題目", status: "draft");
            var program = await AddProgramAsync(d, "tcrfc", $"{d.Tag} 營隊");
            await AddProgramAsync(d, "tcrfc", $"{d.Tag} 草稿課程", status: "draft");
            await AddProgramAsync(d, "bw", $"{d.Tag} 藍鯨課程");
            var staff = await AddStaffAsync(d, null, $"{d.Tag} 教練");
            var player = await AddPlayerAsync(d, "tcrfc", $"{d.Tag} 球員");

            var all = await SearchAsync("tcrfc", d.Tag);
            var facets = all.Facets.ToDictionary(f => f.Type, f => f.Count);
            Assert.Equal(1, facets[SearchTypes.News]);
            Assert.Equal(1, facets[SearchTypes.Faq]);
            Assert.Equal(1, facets[SearchTypes.Program]);
            Assert.Equal(1, facets[SearchTypes.Coach]);
            Assert.Equal(player is null ? 0 : 1, facets[SearchTypes.Player]);
            Assert.Equal(0, facets[SearchTypes.Charity]);
            Assert.False(all.IsEmpty);
            Assert.Contains(all.Items, i => i.Id == article && i.Type == SearchTypes.News);
            Assert.Contains(all.Items, i => i.Id == faq && i.Type == SearchTypes.Faq);
            Assert.Contains(all.Items, i => i.Id == program && i.Type == SearchTypes.Program);
            Assert.Contains(all.Items, i => i.Id == staff && i.Type == SearchTypes.Coach);

            // 分類篩選：只回該類，命中數不受影響
            var onlyFaq = await SearchAsync("tcrfc", d.Tag, SearchTypes.Faq);
            Assert.Equal([faq], onlyFaq.Items.Select(i => i.Id).ToList());
            Assert.Equal(1, onlyFaq.Facets.Single(f => f.Type == SearchTypes.News).Count);

            // 藍鯨看不到磐石專屬的新聞／FAQ／課程，但看得到共同教練與自己的課程
            var bw = await SearchAsync("bw", d.Tag);
            var bwFacets = bw.Facets.ToDictionary(f => f.Type, f => f.Count);
            Assert.Equal(0, bwFacets[SearchTypes.News]);
            Assert.Equal(0, bwFacets[SearchTypes.Faq]);
            Assert.Equal(1, bwFacets[SearchTypes.Program]);
            Assert.Equal(1, bwFacets[SearchTypes.Coach]);
        }
        finally
        {
            await d.CleanupAsync();
        }
    }

    [Fact]
    public async Task 語系_英文只搜到有英文的_缺英文回退繁中並標示_繁中請求不搜英文欄位()
    {
        var d = new SearchData();
        try
        {
            var english = $"EnglishWord{d.Tag}";
            var withEn = await AddFaqAsync(d, "tcrfc", $"{d.Tag} 有英文版的題目", questionEn: $"{english} question");
            var zhOnly = await AddFaqAsync(d, "tcrfc", $"{d.Tag} 只有繁中的題目");

            var en = await SearchAsync("tcrfc", d.Tag, lang: "en");
            var enWith = en.Items.Single(i => i.Id == withEn);
            var enZhOnly = en.Items.Single(i => i.Id == zhOnly);
            Assert.Equal($"{english} question", enWith.Title);
            Assert.False(enWith.IsFallbackLocale);
            Assert.Equal($"{d.Tag} 只有繁中的題目", enZhOnly.Title);
            Assert.True(enZhOnly.IsFallbackLocale);

            // 英文詞：英文請求找得到，繁中請求不會去比對英文欄位
            Assert.Equal([withEn], (await SearchAsync("tcrfc", english, lang: "en")).Items.Select(i => i.Id).ToList());
            Assert.Empty((await SearchAsync("tcrfc", english, lang: "zh")).Items);
        }
        finally
        {
            await d.CleanupAsync();
        }
    }

    [Fact]
    public async Task 輸入驗證_空白與單一字母400_不合法分類400_不存在俱樂部404_萬用字元只是普通字元()
    {
        using var client = await BizTest.ClientAsync(fixture, null);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/tcrfc/search")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/tcrfc/search?q=%20%20")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/tcrfc/search?q=a")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/tcrfc/search?q=%E8%B2%BB%E7%94%A8&type=members")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/no-such-club/search?q=%E8%B2%BB%E7%94%A8")).StatusCode);

        // % 與 _ 不是萬用字元：兩個百分號只會找含「%%」字面的內容，不會變成全部命中
        var percent = await SearchAsync("tcrfc", "%%");
        Assert.True(percent.IsEmpty);
        var underscore = await SearchAsync("tcrfc", "__");
        Assert.True(underscore.IsEmpty);
        Assert.Empty(percent.Items);
    }

    [Fact]
    public async Task 分頁_每頁筆數與總數_超出頁數回空()
    {
        var d = new SearchData();
        try
        {
            for (var i = 1; i <= 3; i++)
            {
                await AddArticleAsync(d, "tcrfc", $"{d.Tag} 分頁 {i}", publishedAt: DateTime.UtcNow.AddHours(-i));
            }

            var p1 = await SearchAsync("tcrfc", d.Tag, SearchTypes.News, page: 1, pageSize: 2);
            var p2 = await SearchAsync("tcrfc", d.Tag, SearchTypes.News, page: 2, pageSize: 2);
            var p3 = await SearchAsync("tcrfc", d.Tag, SearchTypes.News, page: 3, pageSize: 2);
            Assert.Equal(2, p1.Items.Count);
            Assert.Single(p2.Items);
            Assert.Empty(p3.Items);
            Assert.Equal(3, p1.TotalCount);
            Assert.Empty(p1.Items.Select(i => i.Id).Intersect(p2.Items.Select(i => i.Id)));
        }
        finally
        {
            await d.CleanupAsync();
        }
    }

    [Fact]
    public async Task 教練與球員照片_未取得肖像同意一律不輸出照片網址()
    {
        var d = new SearchData();
        try
        {
            await AddStaffAsync(d, "tcrfc", $"{d.Tag} 未同意", consent: "not_consented", photoKey: "zz-search/photo.webp");
            var result = await SearchAsync("tcrfc", d.Tag, SearchTypes.Coach);
            var item = Assert.Single(result.Items);
            Assert.Null(item.ImageUrl);
        }
        finally
        {
            await d.CleanupAsync();
        }
    }

    [Fact]
    public async Task 慈善_已發布計畫與事蹟紀錄都會被搜到_草稿計畫不會()
    {
        var d = new SearchData();
        try
        {
            var clubId = await ClubIdAsync("tcrfc");
            var charity = Guid.NewGuid();
            var published = Guid.NewGuid();
            var draft = Guid.NewGuid();
            var record = Guid.NewGuid();
            await BizTest.ExecuteSqlAsync(
                """
                INSERT INTO charities (id, club_id, slug) VALUES (@Ch, NULL, @Slug);
                INSERT INTO charities_i18n (charity_id, locale, name) VALUES (@Ch, N'zh-Hant', @Name);
                INSERT INTO charity_programs (id, club_id, slug, charity_id, status) VALUES (@P1, @Club, @Slug1, @Ch, 'published');
                INSERT INTO charity_programs_i18n (charity_program_id, locale, name) VALUES (@P1, N'zh-Hant', @N1);
                INSERT INTO charity_programs (id, club_id, slug, charity_id, status) VALUES (@P2, @Club, @Slug2, @Ch, 'draft');
                INSERT INTO charity_programs_i18n (charity_program_id, locale, name) VALUES (@P2, N'zh-Hant', @N2);
                INSERT INTO impact_records (id, club_id, charity_id) VALUES (@R, @Club, @Ch);
                INSERT INTO impact_records_i18n (impact_record_id, locale, location, brief_description) VALUES (@R, N'zh-Hant', N'台中', @Brief);
                """,
                ("@Ch", charity), ("@Slug", "zz-search-ch-" + charity.ToString("N")[..8]), ("@Name", $"{d.Tag} 受贈單位"), ("@Club", clubId),
                ("@P1", published), ("@Slug1", "zz-search-p-" + published.ToString("N")[..8]), ("@N1", $"{d.Tag} 公開計畫"),
                ("@P2", draft), ("@Slug2", "zz-search-p-" + draft.ToString("N")[..8]), ("@N2", $"{d.Tag} 草稿計畫"),
                ("@R", record), ("@Brief", $"{d.Tag} 事蹟內容"));
            d.Charities.Add(charity);
            d.CharityPrograms.Add(published);
            d.CharityPrograms.Add(draft);
            d.Records.Add(record);

            var result = await SearchAsync("tcrfc", d.Tag, SearchTypes.Charity);
            Assert.Equal(2, result.TotalCount);
            Assert.Contains(result.Items, i => i.Id == published && i.SubType == "program" && !string.IsNullOrEmpty(i.Slug));
            Assert.Contains(result.Items, i => i.Id == record && i.SubType == "record");
            Assert.DoesNotContain(result.Items, i => i.Id == draft);
        }
        finally
        {
            await d.CleanupAsync();
        }
    }
}
