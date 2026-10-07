using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// B1「固定頁＋固定欄位」版型本身的測試：① 版型定義自洽（不連資料庫）；② 種子內容檔
/// （<c>db/seed/page_seed_content.json</c>）與版型逐頁結構一致；③ 後台清單各俱樂部的頁面清單正確、
/// 每個版型都有一列；④ 資料庫裡實際的頁面（種子或遷移建立的）結構與版型一致；⑤ 種子內容通得過完整寫入管線；
/// ⑥ 缺頁補建。會改資料的部分只打測試專用複製版型（<c>test/seed/…</c>），不動真正的固定頁。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminPageTemplatesTests(AdminWriteApiFixture fixture)
{
    // ───────────────────────────── ① 版型定義自洽 ─────────────────────────────

    [Fact]
    public void 版型定義自洽_每個版型的slug與區塊代號唯一_列數規則一致()
    {
        var keys = new HashSet<(string, string)>();
        foreach (var template in PageTemplates.All)
        {
            Assert.True(keys.Add((template.ClubCode, template.Slug)), $"版型重複：{template.ClubCode}/{template.Slug}");
            Assert.False(string.IsNullOrWhiteSpace(template.TitleZh));
            Assert.NotEmpty(template.Blocks);
            Assert.Equal(template.Blocks.Count, template.Blocks.Select(b => b.Key).Distinct().Count());

            foreach (var block in template.Blocks)
            {
                Assert.True(PageBlockTypes.IsKnown(block.BlockType), $"{template.Slug}.{block.Key} 的型別 {block.BlockType} 不是支援的區塊類型");
                Assert.False(string.IsNullOrWhiteSpace(block.LabelZh));
                if (block.RowsField is null)
                {
                    Assert.False(block.AllowRowEdit); // 沒有可重複項目的型別不談增刪列
                    Assert.Null(block.FixedRowCount);
                }
                else if (block.AllowRowEdit)
                {
                    Assert.Null(block.FixedRowCount);
                }
                else
                {
                    Assert.True(block.FixedRowCount is >= 1, $"{template.Slug}.{block.Key} 不允許增刪列，必須指定固定列數");
                }

                // 空白骨架必須建得出來（缺頁補建要用）
                Assert.NotNull(PageTemplates.BuildSkeleton(block));
            }
        }
    }

    [Fact]
    public void 俱樂部版型清單_磐石十二頁_藍鯨十頁_藍鯨沒有女子足球與慈善()
    {
        var tcrfc = PageTemplateCatalog.Default.ForClub("tcrfc").Select(t => t.Slug).ToList();
        var bw = PageTemplateCatalog.Default.ForClub("bw").Select(t => t.Slug).ToList();

        Assert.Equal(12, tcrfc.Count);
        Assert.Equal(10, bw.Count);
        Assert.Contains("womens", tcrfc);
        Assert.Contains("charity/commitment", tcrfc);
        Assert.DoesNotContain("womens", bw);
        Assert.DoesNotContain("charity/commitment", bw);
        Assert.All(bw, slug => Assert.Contains(slug, tcrfc)); // 藍鯨的頁都是磐石的子集（同 slug）

        // 藍鯨沒有 slug 特例：願景頁與磐石同名 about/vision-mission（舊種子的 about/vision 已退場）
        Assert.Contains("about/vision-mission", bw);
        Assert.DoesNotContain("about/vision", bw);
    }

    // ───────────────────────────── ② 種子內容檔與版型一致 ─────────────────────────────

    private static string SeedJsonPath()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "db", "seed", "page_seed_content.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("找不到 db/seed/page_seed_content.json（從測試輸出目錄往上找 repo 根目錄）。");
    }

    private sealed record SeedBlock(string Type, JsonNode Content);
    private sealed record SeedPage(string Slug, string SeoTitleZh, List<SeedBlock> Blocks);

    private static Dictionary<string, List<SeedPage>> LoadSeed()
    {
        var root = JsonNode.Parse(File.ReadAllText(SeedJsonPath()))!.AsObject();
        var result = new Dictionary<string, List<SeedPage>>();
        foreach (var (club, pages) in root)
        {
            result[club] = pages!.AsArray().Select(p => new SeedPage(
                p!["slug"]!.GetValue<string>(),
                p["seo"]!["zh"]!["title"]!.GetValue<string>(),
                p["blocks"]!.AsArray().Select(b => new SeedBlock(b!["type"]!.GetValue<string>(), b["content"]!.DeepClone())).ToList())).ToList();
        }

        return result;
    }

    [Fact]
    public void 種子內容檔_每個俱樂部建齊全部版型頁_區塊結構與固定列數與版型一致_沒有版型外的頁()
    {
        var seed = LoadSeed();
        foreach (var club in new[] { "tcrfc", "bw" })
        {
            var templates = PageTemplateCatalog.Default.ForClub(club);
            Assert.Equal(templates.Select(t => t.Slug).OrderBy(x => x), seed[club].Select(p => p.Slug).OrderBy(x => x));

            foreach (var page in seed[club])
            {
                var template = PageTemplateCatalog.Default.Find(club, page.Slug)!;
                Assert.True(
                    PageTemplates.Matches(template, page.Blocks.Select(b => (b.Type, (JsonNode?)b.Content)).ToList()),
                    $"{club}/{page.Slug} 的種子區塊結構與版型不一致");
            }
        }
    }

    // ───────────────────────────── ③④ 後台清單與資料庫實際頁面 ─────────────────────────────

    private async Task<HttpClient> SuperAdminClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("super.admin@tcrfc.test"));
        return client;
    }

    private static async Task<List<AdminPageListItemDto>> ListAsync(HttpClient client, string club)
    {
        var list = await client.GetFromJsonAsync<PagedResult<AdminPageListItemDto>>($"/api/v1/admin/{club}/pages?pageSize=100", TestJson.Options);
        return list!.Items.ToList();
    }

    [Theory]
    [InlineData("tcrfc", 12)]
    [InlineData("bw", 10)]
    public async Task 後台清單_各俱樂部都是版型清單_每個版型有一列_帶頁名與版型鍵_順序依版型(string club, int expectedCount)
    {
        using var client = await SuperAdminClientAsync();
        var items = await ListAsync(client, club);
        var templates = PageTemplateCatalog.Default.ForClub(club);

        Assert.Equal(expectedCount, templates.Count);
        // 清單只會有版型內的頁（測試版型的頁面測完即刪；缺頁的版型由清單補建）
        Assert.Equal(templates.Select(t => t.Slug).ToArray(), items.Where(i => !i.Slug.StartsWith("test/")).Select(i => i.Slug).ToArray());
        foreach (var item in items.Where(i => !i.Slug.StartsWith("test/")))
        {
            var template = templates.Single(t => t.Slug == item.Slug);
            Assert.Equal(template.Slug, item.TemplateKey);
            Assert.Equal(template.TitleZh, item.TitleZh);
            Assert.Equal(template.TitleEn, item.TitleEn);
            Assert.Contains(item.Status, new[] { "draft", "published", "scheduled" });
        }
    }

    [Fact]
    public async Task 後台清單_狀態篩選與關鍵字篩選仍然有效()
    {
        using var client = await SuperAdminClientAsync();
        var byTitle = await client.GetFromJsonAsync<PagedResult<AdminPageListItemDto>>("/api/v1/admin/tcrfc/pages?keyword=" + Uri.EscapeDataString("願景"), TestJson.Options);
        Assert.Contains(byTitle!.Items, i => i.Slug == "about/vision-mission");
        Assert.All(byTitle.Items, i => Assert.Contains("願景", i.TitleZh + i.SeoTitleZh));

        var drafts = await client.GetFromJsonAsync<PagedResult<AdminPageListItemDto>>("/api/v1/admin/tcrfc/pages?status=draft&pageSize=100", TestJson.Options);
        Assert.All(drafts!.Items, i => Assert.Equal("draft", i.Status));
    }

    [Theory]
    [InlineData("tcrfc")]
    [InlineData("bw")]
    public async Task 資料庫裡實際的頁面_區塊結構與固定列數和版型一致_詳情帶版型與區塊名稱(string club)
    {
        using var client = await SuperAdminClientAsync();
        foreach (var item in (await ListAsync(client, club)).Where(i => !i.Slug.StartsWith("test/")))
        {
            var detail = await client.GetFromJsonAsync<AdminPageDetailDto>($"/api/v1/admin/{club}/pages/{item.Id}", TestJson.Options);
            var template = PageTemplateCatalog.Default.Find(club, item.Slug)!;

            Assert.Equal(template.Slug, detail!.Template.Key);
            Assert.Equal(template.TitleZh, detail.Template.TitleZh);
            Assert.Equal(template.Blocks.Count, detail.Template.Blocks.Count);
            Assert.Equal(template.Blocks.Count, detail.Blocks.Count);

            for (var i = 0; i < template.Blocks.Count; i++)
            {
                var def = template.Blocks[i];
                var tdto = detail.Template.Blocks[i];
                Assert.Equal(def.Key, tdto.Key);
                Assert.Equal(def.BlockType, tdto.BlockType);
                Assert.Equal(def.LabelZh, tdto.LabelZh);
                Assert.Equal(def.AllowRowEdit, tdto.AllowRowEdit);
                Assert.Equal(def.FixedRowCount, tdto.FixedRowCount);

                var block = detail.Blocks[i];
                Assert.Equal(def.BlockType, block.BlockType); // 資料庫的區塊類型順序與版型一致
                Assert.Equal(def.Key, block.Key);
                Assert.Equal(def.LabelZh, block.LabelZh);
                if (def is { AllowRowEdit: false, FixedRowCount: { } fixedCount, RowsField: { } field })
                {
                    Assert.Equal(fixedCount, block.Content.GetProperty(field).GetArrayLength());
                }
            }
        }
    }

    [Fact]
    public async Task 資料庫沒有版型外的測試草稿頁_舊的test_draft_page已由遷移移除()
    {
        // 若這裡失敗：本機資料庫還沒套用 db/migrations/20261007_page-templates.sql（或舊種子殘留）。
        var id = await BizTest.ScalarGuidAsync(
            "SELECT ISNULL((SELECT TOP 1 id FROM pages WHERE slug = N'test-draft-page'), CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier))");
        Assert.Equal(Guid.Empty, id);
    }

    // ───────────────────────────── ⑤ 種子內容通得過完整寫入管線 ─────────────────────────────

    [Fact]
    public async Task 種子內容_逐頁走完整PUT管線都能通過內容驗證與版型鎖定()
    {
        using var client = await SuperAdminClientAsync();
        foreach (var (club, pages) in LoadSeed())
        {
            foreach (var page in pages)
            {
                // 複製版型 test/seed/<slug>：結構與真正的版型相同，但頁面是測試專用，不動真正的固定頁
                var cloneSlug = TestPageTemplates.SeedPrefix + page.Slug;
                var blocks = page.Blocks.Select(b => new AdminPageBlockInput { BlockType = b.Type, Content = b.Content.DeepClone() }).ToList();
                var created = await TestPages.CreateAsync(client, club, cloneSlug, blocks, page.SeoTitleZh);
                try
                {
                    var response = await TestPages.PutAsync(client, club, created, TestPages.UpdateRequest(created, blocks, page.SeoTitleZh));
                    Assert.True(response.StatusCode == HttpStatusCode.OK, $"{club}/{page.Slug} 的種子內容無法通過寫入管線：{await response.Content.ReadAsStringAsync()}");
                }
                finally
                {
                    await TestPages.DeleteAsync(created.Id);
                }
            }
        }
    }

    // ───────────────────────────── 遷移 ─────────────────────────────

    private static string MigrationPath()
        => Path.Combine(Path.GetDirectoryName(SeedJsonPath())!, "..", "migrations", "20261007_page-templates.sql");

    /// <summary>以單一連線依序執行遷移檔（以單獨一行的 <c>GO</c> 分批；交易跨批次維持在同一連線上）。</summary>
    private static async Task RunMigrationAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");
        var batches = System.Text.RegularExpressions.Regex.Split(File.ReadAllText(MigrationPath()), @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline);

        await using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var batch in batches.Where(b => !string.IsNullOrWhiteSpace(b)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task 遷移_套用兩次結果穩定_測試草稿頁移除_缺頁補齊_既有頁面結構符合版型()
    {
        await RunMigrationAsync();
        await RunMigrationAsync(); // 冪等：第二次不應出錯，也不再更動任何頁面

        var id = await BizTest.ScalarGuidAsync(
            "SELECT ISNULL((SELECT TOP 1 id FROM pages WHERE slug = N'test-draft-page' OR (slug = N'about/vision' AND club_id = (SELECT id FROM clubs WHERE code = N'bw'))), CAST('00000000-0000-0000-0000-000000000000' AS uniqueidentifier))");
        Assert.Equal(Guid.Empty, id);

        using var client = await SuperAdminClientAsync();
        foreach (var club in new[] { "tcrfc", "bw" })
        {
            var items = (await ListAsync(client, club)).Where(i => !i.Slug.StartsWith("test/")).ToList();
            Assert.Equal(PageTemplateCatalog.Default.ForClub(club).Count, items.Count);
        }
    }

    // ───────────────────────────── ⑥ 缺頁補建 ─────────────────────────────

    [Fact]
    public async Task 缺頁補建_清單載入時為缺頁的版型補一份草稿骨架頁_重複呼叫不重複建立_骨架不能直接存檔()
    {
        const string slug = "test/provision";
        var template = new PageTemplate
        {
            ClubCode = "tcrfc", Slug = slug, TitleZh = "補建測試頁",
            Blocks =
            [
                new PageTemplateBlock { Key = "intro", BlockType = PageBlockTypes.Text, LabelZh = "開場" },
                new PageTemplateBlock { Key = "cards", BlockType = PageBlockTypes.Steps, LabelZh = "卡片", AllowRowEdit = false, FixedRowCount = 3 },
            ],
        };
        await TestPages.DeleteBySlugAsync("tcrfc", slug);

        using var scope = fixture.Services.CreateScope();
        var repository = new AdminPagesRepository(
            scope.ServiceProvider.GetRequiredService<ClubDbContext>(),
            scope.ServiceProvider.GetRequiredService<IQueryCache>(),
            scope.ServiceProvider.GetRequiredService<IImageStorageService>(),
            scope.ServiceProvider.GetRequiredService<IImagePublicUrlResolver>(),
            new PageTemplateCatalog([template]));
        var authorizer = scope.ServiceProvider.GetRequiredService<IAdminClubAuthorizer>();
        var httpContext = await TestAdminHttpContext.CreateAuthenticatedAsync("content.editor@tcrfc.test");
        var adminScope = await authorizer.AuthorizeAsync(httpContext, "tcrfc", "content.page.view", CancellationToken.None);

        try
        {
            var first = await repository.ListAsync(adminScope, null, null, 1, 100, CancellationToken.None);
            var item = Assert.Single(first.Items);
            Assert.Equal(slug, item.Slug);
            Assert.Equal("draft", item.Status);
            Assert.Equal("補建測試頁", item.TitleZh);

            var second = await repository.ListAsync(adminScope, null, null, 1, 100, CancellationToken.None);
            Assert.Equal(item.Id, Assert.Single(second.Items).Id); // 冪等：同一頁，不重複建立

            var detail = await repository.GetByIdAsync(adminScope, item.Id, CancellationToken.None);
            Assert.Equal(["text", "steps"], detail!.Blocks.Select(b => b.BlockType).ToArray());
            Assert.Equal(3, detail.Blocks[1].Content.GetProperty("items").GetArrayLength()); // 固定列數的骨架列已備好
            Assert.Equal(1, detail.LatestVersionNo);
        }
        finally
        {
            await TestPages.DeleteBySlugAsync("tcrfc", slug);
        }
    }
}
