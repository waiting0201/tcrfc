using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcrfc.Api.Features.AdminPages;

namespace Tcrfc.Api.Tests;

/// <summary>
/// B1「固定頁＋固定欄位」的測試接縫：測試主機把版型目錄換成「正式版型＋測試專用版型」
/// （<see cref="IPageTemplateCatalog"/>），會改資料的測試（寫入、版本、發布、圖片上傳）一律打
/// <c>test/…</c> 測試頁（<see cref="TestPages"/> 以 SQL 建立、測完刪除），<b>不動真正的固定頁</b>。
///
/// 測試專用版型（磐石與藍鯨各一份，缺頁時不自動補建：<see cref="PageTemplate.ProvisionWhenMissing"/> ＝ false）：
/// <list type="bullet">
/// <item><c>test/basic</c>：文字、引言、步驟（固定 2 列）、時間軸（可增刪列）——寫入／版本／發布／結構驗證用。</item>
/// <item><c>test/all-types</c>：12 種區塊型別各一個（依序）——內容驗證逐型別驗證用。</item>
/// <item><c>test/images</c>：圖文左右、CTA、圖片藝廊——圖片上傳與補償交易用（Azurite fixture）。</item>
/// <item><c>test/seed/&lt;正式 slug&gt;</c>：每個正式版型的複製——驗證種子內容能通過完整寫入管線，卻不動真正的頁面。</item>
/// </list>
/// </summary>
internal static class TestPageTemplates
{
    public const string Basic = "test/basic";
    public const string AllTypes = "test/all-types";
    public const string Images = "test/images";
    public const string SeedPrefix = "test/seed/";

    private static PageTemplateBlock B(string key, string type, string label, bool rowEdit = false, int? fixedRows = null)
        => new() { Key = key, BlockType = type, LabelZh = label, AllowRowEdit = rowEdit, FixedRowCount = fixedRows };

    public static IReadOnlyList<PageTemplate> TestTemplates { get; } = Build();

    private static List<PageTemplate> Build()
    {
        var list = new List<PageTemplate>();
        foreach (var club in new[] { "tcrfc", "bw" })
        {
            list.Add(new PageTemplate
            {
                ClubCode = club, Slug = Basic, TitleZh = "測試頁（基本）", ProvisionWhenMissing = false,
                Blocks =
                [
                    B("intro", PageBlockTypes.Text, "開場"),
                    B("quote", PageBlockTypes.Quote, "引言"),
                    B("cards", PageBlockTypes.Steps, "卡片", rowEdit: false, fixedRows: 2),
                    B("years", PageBlockTypes.Timeline, "年表", rowEdit: true),
                ],
            });

            list.Add(new PageTemplate
            {
                ClubCode = club, Slug = AllTypes, TitleZh = "測試頁（全部型別）", ProvisionWhenMissing = false,
                Blocks = PageBlockTypes.All.Select(t => B(t, t, t, rowEdit: PageTemplates.RowsFieldOf(t) is not null)).ToList(),
            });

            list.Add(new PageTemplate
            {
                ClubCode = club, Slug = Images, TitleZh = "測試頁（圖片）", ProvisionWhenMissing = false,
                Blocks =
                [
                    B("photo", PageBlockTypes.TextImage, "圖文"),
                    B("action", PageBlockTypes.Cta, "行動呼籲"),
                    B("gallery", PageBlockTypes.Gallery, "藝廊", rowEdit: true),
                ],
            });
        }

        foreach (var real in PageTemplates.All)
        {
            list.Add(real with { Slug = SeedPrefix + real.Slug, ProvisionWhenMissing = false });
        }

        return list;
    }

    public static IPageTemplateCatalog Catalog { get; } = new PageTemplateCatalog(PageTemplates.All.Concat(TestTemplates).ToList());

    public static void Register(IServiceCollection services)
    {
        services.RemoveAll<IPageTemplateCatalog>();
        services.AddSingleton(Catalog);
    }
}

/// <summary>測試頁的建立與清除（SQL 直寫，模擬「已存在的固定頁」）與常用請求組裝。</summary>
internal static class TestPages
{
    /// <summary><c>test/basic</c> 的合法區塊清單（文字、引言、步驟 2 列、時間軸 <paramref name="timelineItems"/> 列）。</summary>
    public static List<AdminPageBlockInput> BasicBlocks(string text = "第一版內文", int timelineItems = 1) =>
    [
        PageBlockSamples.Text(text),
        PageBlockSamples.Quote(),
        PageBlockSamples.StepsOf(2),
        PageBlockSamples.TimelineOf(timelineItems),
    ];

    /// <summary>
    /// 建立（或重建）指定俱樂部的測試頁，狀態草稿，帶第 1 版快照與預覽權杖。同 slug 的殘留列先刪。
    /// 回傳 GET 後台詳情（含並行權杖 <c>UpdatedAt</c>）。
    /// </summary>
    public static async Task<AdminPageDetailDto> CreateAsync(
        HttpClient adminClient, string club, string slug, IReadOnlyList<AdminPageBlockInput> blocks, string seoTitle = "版本測試")
    {
        var id = Guid.NewGuid();
        await DeleteBySlugAsync(club, slug);

        var sql = new System.Text.StringBuilder();
        var parameters = new List<(string, object?)> { ("@Id", id), ("@Club", club), ("@Slug", slug), ("@Title", seoTitle) };
        sql.Append("""
            DECLARE @clubId uniqueidentifier = (SELECT id FROM clubs WHERE code = @Club);
            INSERT INTO pages (id, club_id, slug, status) VALUES (@Id, @clubId, @Slug, N'draft');
            INSERT INTO pages_i18n (page_id, locale, seo_title) VALUES (@Id, N'zh-Hant', @Title);

            """);
        for (var i = 0; i < blocks.Count; i++)
        {
            sql.Append($"INSERT INTO page_blocks (id, page_id, block_type, content, sort_order) VALUES (NEWID(), @Id, @T{i}, @C{i}, {i});\n");
            parameters.Add(($"@T{i}", blocks[i].BlockType));
            parameters.Add(($"@C{i}", blocks[i].Content.ToJsonString()));
        }

        var snapshot = new JsonObject
        {
            ["seo"] = new JsonObject { ["zh"] = new JsonObject { ["seoTitle"] = seoTitle, ["seoDescription"] = null } },
            ["blocks"] = new JsonArray(blocks.Select(b => (JsonNode)new JsonObject { ["blockType"] = b.BlockType, ["content"] = b.Content.DeepClone() }).ToArray()),
        };
        sql.Append("INSERT INTO page_versions (id, page_id, version_no, snapshot, preview_token) VALUES (NEWID(), @Id, 1, @Snap, @Token);");
        parameters.Add(("@Snap", snapshot.ToJsonString()));
        parameters.Add(("@Token", Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()));

        await BizTest.ExecuteSqlAsync(sql.ToString(), parameters.ToArray());

        var response = await adminClient.GetAsync($"/api/v1/admin/{club}/pages/{id}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;
    }

    public static Task DeleteAsync(Guid id) => BizTest.ExecuteSqlAsync("DELETE FROM pages WHERE id = @Id", ("@Id", id));

    public static Task DeleteBySlugAsync(string club, string slug) => BizTest.ExecuteSqlAsync(
        "DELETE p FROM pages p JOIN clubs c ON c.id = p.club_id WHERE c.code = @Club AND p.slug = @Slug", ("@Club", club), ("@Slug", slug));

    public static UpdatePageRequest UpdateRequest(
        AdminPageDetailDto page, IReadOnlyList<AdminPageBlockInput> blocks, string seoTitle = "改過的標題") => new()
    {
        Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = seoTitle } },
        Blocks = blocks,
        ExpectedUpdatedAt = page.UpdatedAt,
    };

    public static Task<HttpResponseMessage> PutAsync(HttpClient client, string club, AdminPageDetailDto page, UpdatePageRequest request, IReadOnlyDictionary<string, byte[]>? files = null)
        => client.PutAsync($"/api/v1/admin/{club}/pages/{page.Id}", AdminPageMultipart.Build(request, files));

    /// <summary>讀 400 回應的 <c>errors</c> 鍵集合（沒有 errors 回空）。</summary>
    public static async Task<Dictionary<string, string>> ErrorKeysAsync(HttpResponseMessage response)
    {
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var result = new Dictionary<string, string>();
        if (body.TryGetProperty("errors", out var errors))
        {
            foreach (var property in errors.EnumerateObject())
            {
                result[property.Name] = property.Value[0].GetString() ?? "";
            }
        }

        return result;
    }
}
