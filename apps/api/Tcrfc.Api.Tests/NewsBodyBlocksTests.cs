using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 新聞內文區塊（v3.28）的純邏輯測試——不需要資料庫、不需要 Azurite：驗證、zh／en 拆合往返、舊格式轉換、
/// 圖片上傳補償與寬度下限、封面焦點驗證。HTTP 端對端見 <see cref="AdminNewsBodyBlocksApiTests"/>。
/// </summary>
public sealed class NewsBodyBlocksTests
{
    private sealed class FakeImageStorage(int width = 1600, int height = 900) : IImageStorageService
    {
        public List<string> Uploaded { get; } = [];
        public List<string> Deleted { get; } = [];

        public Task<UploadedImageInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
        {
            var key = $"{objectKeyPrefix}/{Guid.NewGuid():N}.webp";
            Uploaded.Add(key);
            return Task.FromResult(new UploadedImageInfo(key, width, height, rawBytes.Length));
        }

        public Task DeleteAsync(string? mainObjectKey, CancellationToken cancellationToken)
        {
            if (mainObjectKey is not null)
            {
                Deleted.Add(mainObjectKey);
            }

            return Task.CompletedTask;
        }
    }

    private static readonly IReadOnlyDictionary<string, (int Width, int Height)> NoKnown = new Dictionary<string, (int, int)>();

    private static JsonObject Bi(string zh, string? en = null) => new() { ["zh"] = zh, ["en"] = en };

    private static NewsBlockInput Block(string type, JsonObject content) => new() { BlockType = type, Content = content };

    private static BlockImageUploader Uploader(IImageStorageService storage, params (string Field, int Bytes)[] files)
    {
        var collection = new FormFileCollection();
        foreach (var (field, bytes) in files)
        {
            collection.Add(new FormFile(new MemoryStream(new byte[bytes]), 0, bytes, field, "x.png"));
        }

        return new BlockImageUploader(storage, collection, "tcrfc/articles/a1/body", NewsBodyBlocks.MinImageWidth);
    }

    private static async Task<AdminArticleValidationException> AssertRejectsAsync(
        IReadOnlyList<NewsBlockInput> blocks, IReadOnlyDictionary<string, (int, int)>? known = null, BlockImageUploader? uploader = null)
        => await Assert.ThrowsAsync<AdminArticleValidationException>(() =>
            NewsBodyBlocks.ResolveAsync(blocks, uploader ?? Uploader(new FakeImageStorage()), known ?? NoKnown, CancellationToken.None));

    // ───────────────────────────── 拆合往返 ─────────────────────────────

    [Fact]
    public async Task 七種區塊_拆成zh與en兩份_結構順序圖片key相同_合併後與輸入一致()
    {
        var known = new Dictionary<string, (int, int)> { ["k/one.webp"] = (1600, 2000), ["k/g1.webp"] = (1200, 800), ["k/g2.webp"] = (900, 1200) };
        var blocks = new[]
        {
            Block("text", new JsonObject { ["body"] = Bi("第一段\n\n第二段", "Para one\n\nPara two") }),
            Block("heading", new JsonObject { ["text"] = Bi("小標", "Heading") }),
            Block("quote", new JsonObject { ["text"] = Bi("引言", "Quote"), ["attribution"] = Bi("某人", "Someone") }),
            Block("list", new JsonObject { ["ordered"] = true, ["items"] = new JsonArray(Bi("甲", "A"), Bi("乙", "B")) }),
            Block("image", new JsonObject
            {
                ["image"] = new JsonObject { ["key"] = "k/one.webp", ["width"] = 1, ["height"] = 1, ["altZh"] = "圖一", ["altEn"] = "Pic one" },
                ["caption"] = Bi("說明", "Caption"),
            }),
            Block("gallery", new JsonObject
            {
                ["images"] = new JsonArray(
                    new JsonObject { ["key"] = "k/g1.webp", ["altZh"] = "甲圖", ["altEn"] = null },
                    new JsonObject { ["key"] = "k/g2.webp", ["altZh"] = "乙圖", ["altEn"] = "B pic" }),
            }),
            Block("video_embed", new JsonObject { ["provider"] = "youtube", ["videoId"] = "dQw4w9WgXcQ", ["caption"] = Bi("影片", "Video") }),
        };

        var resolved = await NewsBodyBlocks.ResolveAsync(blocks, Uploader(new FakeImageStorage()), known, CancellationToken.None);

        Assert.NotNull(resolved.ZhJson);
        Assert.NotNull(resolved.EnJson);
        Assert.Equal(["k/one.webp", "k/g1.webp", "k/g2.webp"], resolved.ImageKeys);

        var zh = JsonNode.Parse(resolved.ZhJson!)!["blocks"]!.AsArray();
        var en = JsonNode.Parse(resolved.EnJson!)!["blocks"]!.AsArray();
        Assert.Equal(7, zh.Count);
        Assert.Equal(zh.Select(b => b!["type"]!.GetValue<string>()), en.Select(b => b!["type"]!.GetValue<string>()));
        Assert.Equal(["paragraph", "heading", "quote", "list", "image", "gallery", "video"], zh.Select(b => b!["type"]!.GetValue<string>()));

        // 契約 B2 欄位
        Assert.Equal("第一段\n\n第二段", zh[0]!["text"]!.GetValue<string>());
        Assert.Equal("某人", zh[2]!["cite"]!.GetValue<string>());
        Assert.True(zh[3]!["ordered"]!.GetValue<bool>());
        Assert.Equal("B", en[3]!["items"]![1]!.GetValue<string>());
        Assert.Equal("k/one.webp", zh[4]!["key"]!.GetValue<string>());
        Assert.Equal(1600, zh[4]!["width"]!.GetValue<int>()); // 寬高以伺服器記錄為準，不採信客戶端的 1
        Assert.Equal("圖一", zh[4]!["alt"]!.GetValue<string>());
        Assert.Equal("Pic one", en[4]!["alt"]!.GetValue<string>());
        Assert.Equal("Caption", en[4]!["caption"]!.GetValue<string>());
        Assert.Null(en[5]!["images"]![0]!["alt"]); // en 缺 alt → null
        Assert.Equal("k/g2.webp", en[5]!["images"]![1]!["key"]!.GetValue<string>()); // 圖片 key 兩份相同
        Assert.Equal("youtube", en[6]!["provider"]!.GetValue<string>());

        // 讀回後台形狀
        var admin = NewsBodyBlocks.ToAdminBlocks(resolved.ZhJson, resolved.EnJson, k => $"https://cdn/{k}", k => $"https://cdn/{k}.t");
        Assert.Equal(7, admin.Count);
        Assert.Equal(["text", "heading", "quote", "list", "image", "gallery", "video_embed"], admin.Select(b => b["blockType"]!.GetValue<string>()));
        Assert.Equal("Para one\n\nPara two", admin[0]["content"]!["body"]!["en"]!.GetValue<string>());
        Assert.Equal("Someone", admin[2]["content"]!["attribution"]!["en"]!.GetValue<string>());
        Assert.Equal("乙", admin[3]["content"]!["items"]![1]!["zh"]!.GetValue<string>());
        var img = admin[4]["content"]!["image"]!;
        Assert.Equal("k/one.webp", img["key"]!.GetValue<string>());
        Assert.Equal("https://cdn/k/one.webp", img["url"]!.GetValue<string>());
        Assert.Equal("https://cdn/k/one.webp.t", img["thumbUrl"]!.GetValue<string>());
        Assert.Equal("圖一", img["altZh"]!.GetValue<string>());
        Assert.Equal("Pic one", img["altEn"]!.GetValue<string>());
        Assert.Equal("Caption", admin[4]["content"]!["caption"]!["en"]!.GetValue<string>());
        Assert.Null(admin[5]["content"]!["images"]![0]!["altEn"]);
        Assert.Equal("dQw4w9WgXcQ", admin[6]["content"]!["videoId"]!.GetValue<string>());
    }

    [Fact]
    public async Task 英文全空_en存NULL_讓前台回退中文()
    {
        var blocks = new[] { Block("text", new JsonObject { ["body"] = Bi("只有中文") }), Block("heading", new JsonObject { ["text"] = Bi("標題") }) };
        var resolved = await NewsBodyBlocks.ResolveAsync(blocks, Uploader(new FakeImageStorage()), NoKnown, CancellationToken.None);
        Assert.NotNull(resolved.ZhJson);
        Assert.Null(resolved.EnJson);
    }

    [Fact]
    public async Task 英文部分有內容_en保留完整結構_空欄位為空字串()
    {
        var blocks = new[]
        {
            Block("text", new JsonObject { ["body"] = Bi("中一", "En one") }),
            Block("text", new JsonObject { ["body"] = Bi("中二") }),
        };
        var resolved = await NewsBodyBlocks.ResolveAsync(blocks, Uploader(new FakeImageStorage()), NoKnown, CancellationToken.None);
        var en = JsonNode.Parse(resolved.EnJson!)!["blocks"]!.AsArray();
        Assert.Equal(2, en.Count);
        Assert.Equal("", en[1]!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task 空清單_內文清空_兩份皆NULL()
    {
        var resolved = await NewsBodyBlocks.ResolveAsync([], Uploader(new FakeImageStorage()), NoKnown, CancellationToken.None);
        Assert.Null(resolved.ZhJson);
        Assert.Null(resolved.EnJson);
        Assert.Empty(resolved.ImageKeys);
    }

    [Fact]
    public async Task 外層欄位type別名可用()
    {
        var blocks = new[] { new NewsBlockInput { Type = "heading", Content = new JsonObject { ["text"] = Bi("小標") } } };
        var resolved = await NewsBodyBlocks.ResolveAsync(blocks, Uploader(new FakeImageStorage()), NoKnown, CancellationToken.None);
        Assert.Contains("\"heading\"", resolved.ZhJson);
    }

    // ───────────────────────────── 舊格式轉換 ─────────────────────────────

    [Fact]
    public void 舊text格式_轉成單一text區塊_zh與en各自填入()
    {
        var admin = NewsBodyBlocks.ToAdminBlocks("{\"text\":\"舊內文\"}", "{\"text\":\"Old body\"}", k => k, k => k);
        var only = Assert.Single(admin);
        Assert.Equal("text", only["blockType"]!.GetValue<string>());
        Assert.Equal("舊內文", only["content"]!["body"]!["zh"]!.GetValue<string>());
        Assert.Equal("Old body", only["content"]!["body"]!["en"]!.GetValue<string>());
    }

    [Fact]
    public void 舊純文字與JSON字串_也轉成text區塊()
    {
        Assert.Single(NewsBodyBlocks.ToAdminBlocks("純文字內文", null, k => k, k => k));
        Assert.Single(NewsBodyBlocks.ToAdminBlocks("\"字串純量\"", null, k => k, k => k));
    }

    [Fact]
    public void 無內文或未知形狀_回空陣列()
    {
        Assert.Empty(NewsBodyBlocks.ToAdminBlocks(null, null, k => k, k => k));
        Assert.Empty(NewsBodyBlocks.ToAdminBlocks("{\"foo\":1}", null, k => k, k => k));
    }

    [Fact]
    public void 英文骨架對不上_略過英文_不丟例外()
    {
        var zh = "{\"blocks\":[{\"type\":\"paragraph\",\"text\":\"中\"}]}";
        var en = "{\"blocks\":[{\"type\":\"heading\",\"text\":\"X\"}]}";
        var admin = NewsBodyBlocks.ToAdminBlocks(zh, en, k => k, k => k);
        Assert.Equal("", Assert.Single(admin)["content"]!["body"]!["en"]!.GetValue<string>());
    }

    [Fact]
    public void ExtractImageKeys_涵蓋image與gallery_舊格式回空()
    {
        var json = "{\"blocks\":[{\"type\":\"image\",\"key\":\"a\"},{\"type\":\"gallery\",\"images\":[{\"key\":\"b\"},{\"key\":\"c\"}]},{\"type\":\"paragraph\",\"text\":\"x\"}]}";
        Assert.Equal(["a", "b", "c"], NewsBodyBlocks.ExtractImageKeys(json));
        Assert.Empty(NewsBodyBlocks.ExtractImageKeys("{\"text\":\"x\"}"));
        Assert.Empty(NewsBodyBlocks.ExtractImageKeys(null));
        Assert.Equal(("b", 0), (NewsBodyBlocks.KnownImages(json).Keys.ElementAt(1), 0));
    }

    // ───────────────────────────── 驗證 ─────────────────────────────

    [Fact]
    public async Task 未知型別_拒絕()
    {
        var ex = await AssertRejectsAsync([Block("cta", new JsonObject())]);
        Assert.Contains("不是支援的區塊類型", ex.Message);
        Assert.Contains("bodyBlocks[0].type", ex.FieldErrors.Keys);
    }

    [Fact]
    public async Task 型別缺漏或內容不是物件_拒絕()
    {
        await AssertRejectsAsync([new NewsBlockInput { Content = new JsonObject() }]);
        await AssertRejectsAsync([new NewsBlockInput { BlockType = "text", Content = null }]);
        await AssertRejectsAsync([Block("text", new JsonObject())]); // 缺 body
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("abc/../x")]
    [InlineData("<script>")]
    [InlineData("a\"onload=1")]
    public async Task 影片代碼含不合法字元_拒絕(string videoId)
    {
        var ex = await AssertRejectsAsync([Block("video_embed", new JsonObject { ["provider"] = "youtube", ["videoId"] = videoId })]);
        Assert.Contains("bodyBlocks[0].videoId", ex.FieldErrors.Keys);
    }

    [Fact]
    public async Task 影音來源不在白名單_拒絕()
    {
        var ex = await AssertRejectsAsync([Block("video_embed", new JsonObject { ["provider"] = "evil", ["videoId"] = "abc" })]);
        Assert.Contains("bodyBlocks[0].provider", ex.FieldErrors.Keys);
    }

    [Fact]
    public async Task 中文必填_小標_段落_清單項目()
    {
        await AssertRejectsAsync([Block("heading", new JsonObject { ["text"] = Bi("  ", "en") })]);
        await AssertRejectsAsync([Block("list", new JsonObject { ["items"] = new JsonArray() })]);
        await AssertRejectsAsync([Block("list", new JsonObject { ["items"] = new JsonArray(Bi("")) })]);
    }

    [Fact]
    public async Task 圖片中文替代文字必填_既有圖片key必須存在()
    {
        var known = new Dictionary<string, (int, int)> { ["k/a.webp"] = (1000, 800) };
        var noAlt = Block("image", new JsonObject { ["image"] = new JsonObject { ["key"] = "k/a.webp", ["altZh"] = "" } });
        var ex = await AssertRejectsAsync([noAlt], known);
        Assert.Contains("bodyBlocks[0].image.altZh", ex.FieldErrors.Keys);

        var foreign = Block("image", new JsonObject { ["image"] = new JsonObject { ["key"] = "other-club/articles/x/cover/y.webp", ["altZh"] = "外來圖" } });
        var ex2 = await AssertRejectsAsync([foreign], known);
        Assert.Contains("已經不存在", ex2.Message);
    }

    [Fact]
    public async Task 區塊數與圖片數超過上限_拒絕()
    {
        var many = Enumerable.Range(0, NewsBodyBlocks.MaxBlocks + 1)
            .Select(_ => Block("heading", new JsonObject { ["text"] = Bi("x") })).ToList();
        await AssertRejectsAsync(many);

        var known = Enumerable.Range(0, 31).ToDictionary(i => $"k/{i}.webp", _ => (1000, 800));
        var gallery = Block("gallery", new JsonObject
        {
            ["images"] = new JsonArray(Enumerable.Range(0, 31).Select(i => (JsonNode)new JsonObject { ["key"] = $"k/{i}.webp", ["altZh"] = "圖" }).ToArray()),
        });
        await AssertRejectsAsync([gallery], known); // 單一圖集 > 30
    }

    // ───────────────────────────── 圖片上傳 ─────────────────────────────

    [Fact]
    public async Task 新圖上傳_物件鍵含區塊索引與路徑_回寫key寬高()
    {
        var storage = new FakeImageStorage(1600, 900);
        var uploader = Uploader(storage, ("file:1:images:0", 10), ("file:1:images:1", 10));
        var blocks = new[]
        {
            Block("heading", new JsonObject { ["text"] = Bi("x") }),
            Block("gallery", new JsonObject
            {
                ["images"] = new JsonArray(
                    new JsonObject { ["pendingUpload"] = true, ["altZh"] = "甲" },
                    new JsonObject { ["pendingUpload"] = true, ["altZh"] = "乙" }),
            }),
        };

        var resolved = await NewsBodyBlocks.ResolveAsync(blocks, uploader, NoKnown, CancellationToken.None);

        Assert.Equal(2, resolved.ImageKeys.Count);
        Assert.All(storage.Uploaded, k => Assert.StartsWith("tcrfc/articles/a1/body/1/images-", k));
        Assert.Equal(storage.Uploaded, uploader.UploadedKeys);
        Assert.Equal(storage.Uploaded, resolved.ImageKeys);
    }

    [Fact]
    public async Task 圖片寬度低於800_拒絕_已上傳的物件可補償刪除()
    {
        var storage = new FakeImageStorage(width: 799, height: 2000);
        var uploader = Uploader(storage, ("file:0:image", 10));
        var blocks = new[] { Block("image", new JsonObject { ["image"] = new JsonObject { ["pendingUpload"] = true, ["altZh"] = "窄圖" } }) };

        await Assert.ThrowsAsync<ImageTooNarrowException>(() =>
            NewsBodyBlocks.ResolveAsync(blocks, uploader, NoKnown, CancellationToken.None));

        Assert.Single(uploader.UploadedKeys);
        await uploader.RollbackAsync();
        Assert.Equal(storage.Uploaded, storage.Deleted);
    }

    [Fact]
    public async Task 標記待上傳卻沒有對應檔案_拒絕()
    {
        var uploader = Uploader(new FakeImageStorage()); // 沒有任何檔案
        var blocks = new[] { Block("image", new JsonObject { ["image"] = new JsonObject { ["pendingUpload"] = true, ["altZh"] = "圖" } }) };
        var ex = await AssertRejectsAsync(blocks, uploader: uploader);
        Assert.Contains("還沒有選擇檔案", ex.Message);
    }

    [Fact]
    public async Task 空檔案_拒絕為圖片錯誤()
    {
        var uploader = Uploader(new FakeImageStorage(), ("file:0:image", 0));
        var blocks = new[] { Block("image", new JsonObject { ["image"] = new JsonObject { ["pendingUpload"] = true, ["altZh"] = "圖" } }) };
        await Assert.ThrowsAsync<EmptyImageException>(() =>
            NewsBodyBlocks.ResolveAsync(blocks, uploader, NoKnown, CancellationToken.None));
    }

    // ───────────────────────────── 封面焦點 ─────────────────────────────

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("0", "0", true)]
    [InlineData("100", "100", true)]
    [InlineData("33.33", "66.67", true)]
    [InlineData("50", null, false)]
    [InlineData(null, "50", false)]
    [InlineData("-0.01", "50", false)]
    [InlineData("50", "100.01", false)]
    [InlineData("10.123", "20", false)]
    public void 封面焦點驗證_同null或同在0到100且最多兩位小數(string? x, string? y, bool ok)
    {
        decimal? dx = x is null ? null : decimal.Parse(x, System.Globalization.CultureInfo.InvariantCulture);
        decimal? dy = y is null ? null : decimal.Parse(y, System.Globalization.CultureInfo.InvariantCulture);
        if (ok)
        {
            CoverFocalPolicy.Validate(dx, dy);
        }
        else
        {
            var ex = Assert.Throws<AdminArticleValidationException>(() => CoverFocalPolicy.Validate(dx, dy));
            Assert.Contains("coverFocalX", ex.FieldErrors.Keys);
        }
    }
}
