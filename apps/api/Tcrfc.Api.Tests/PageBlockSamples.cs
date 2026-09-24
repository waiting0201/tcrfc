using System.Text.Json.Nodes;
using Tcrfc.Api.Features.AdminPages;

namespace Tcrfc.Api.Tests;

/// <summary>
/// B1 頁面 12 種區塊型別的測試樣本產生器——每種型別各提供一個「合法」與若干「刻意缺欄位」的版本，
/// 供 <c>AdminPagesBlockValidationTests</c> 逐型別驗證。雙語文字欄位一律用
/// <see cref="Bilingual"/> 組出 <c>{zh, en}</c> 物件，形狀對應
/// <c>Features/AdminPages/PageBlockContentProcessor.cs</c> 的驗證規則。
/// </summary>
internal static class PageBlockSamples
{
    public static JsonObject Bilingual(string zh, string? en = "英文內容") => new() { ["zh"] = zh, ["en"] = en };

    public static AdminPageBlockInput Text(string body = "測試內文") => new()
    {
        BlockType = PageBlockTypes.Text,
        Content = new JsonObject { ["body"] = Bilingual(body) },
    };

    public static AdminPageBlockInput TextInvalid() => new()
    {
        BlockType = PageBlockTypes.Text,
        Content = new JsonObject(), // 缺 body
    };

    /// <summary>圖文左右，圖片欄位標示待上傳（<c>pendingUpload</c>），呼叫端須同時夾檔案
    /// （欄位名 <c>file:{blockIndex}:image</c>）。</summary>
    public static AdminPageBlockInput TextImagePending(string altZh = "圖片說明") => new()
    {
        BlockType = PageBlockTypes.TextImage,
        Content = new JsonObject
        {
            ["body"] = Bilingual("圖文左右內文"),
            ["imagePosition"] = "left",
            ["image"] = new JsonObject { ["pendingUpload"] = true, ["altZh"] = altZh, ["altEn"] = "image alt" },
        },
    };

    public static AdminPageBlockInput TextImageInvalidMissingImage() => new()
    {
        BlockType = PageBlockTypes.TextImage,
        Content = new JsonObject { ["body"] = Bilingual("內文"), ["imagePosition"] = "left" }, // 缺 image
    };

    public static AdminPageBlockInput GalleryPending(int count = 2) => new()
    {
        BlockType = PageBlockTypes.Gallery,
        Content = new JsonObject
        {
            ["images"] = new JsonArray(Enumerable.Range(0, count)
                .Select(i => (JsonNode)new JsonObject { ["pendingUpload"] = true, ["altZh"] = $"第 {i} 張", ["altEn"] = $"image {i}" })
                .ToArray()),
        },
    };

    public static AdminPageBlockInput GalleryInvalidEmpty() => new()
    {
        BlockType = PageBlockTypes.Gallery,
        Content = new JsonObject { ["images"] = new JsonArray() }, // 至少要 1 張
    };

    public static AdminPageBlockInput VideoEmbed() => new()
    {
        BlockType = PageBlockTypes.VideoEmbed,
        Content = new JsonObject { ["provider"] = "youtube", ["videoId"] = "abc123" },
    };

    public static AdminPageBlockInput VideoEmbedInvalidProvider() => new()
    {
        BlockType = PageBlockTypes.VideoEmbed,
        Content = new JsonObject { ["provider"] = "not-a-real-provider", ["videoId"] = "abc123" },
    };

    public static AdminPageBlockInput Quote() => new()
    {
        BlockType = PageBlockTypes.Quote,
        Content = new JsonObject { ["text"] = Bilingual("這是一段引言") },
    };

    public static AdminPageBlockInput QuoteInvalid() => new()
    {
        BlockType = PageBlockTypes.Quote,
        Content = new JsonObject(), // 缺 text
    };

    public static AdminPageBlockInput Cta() => new()
    {
        BlockType = PageBlockTypes.Cta,
        Content = new JsonObject
        {
            ["text"] = Bilingual("加入我們"),
            ["buttonLabel"] = Bilingual("立即報名"),
            ["buttonUrl"] = "/zh/join/",
        },
    };

    public static AdminPageBlockInput CtaInvalid() => new()
    {
        BlockType = PageBlockTypes.Cta,
        Content = new JsonObject { ["text"] = Bilingual("加入我們") }, // 缺 buttonLabel／buttonUrl
    };

    public static AdminPageBlockInput AccordionFaq() => new()
    {
        BlockType = PageBlockTypes.AccordionFaq,
        Content = new JsonObject
        {
            ["items"] = new JsonArray(new JsonObject { ["question"] = Bilingual("怎麼加入？"), ["answer"] = Bilingual("填表單") }),
        },
    };

    public static AdminPageBlockInput AccordionFaqInvalidEmpty() => new()
    {
        BlockType = PageBlockTypes.AccordionFaq,
        Content = new JsonObject { ["items"] = new JsonArray() },
    };

    public static AdminPageBlockInput Timeline() => new()
    {
        BlockType = PageBlockTypes.Timeline,
        Content = new JsonObject
        {
            ["items"] = new JsonArray(new JsonObject { ["date"] = "2024-01", ["title"] = Bilingual("成立") }),
        },
    };

    public static AdminPageBlockInput TimelineInvalidMissingDate() => new()
    {
        BlockType = PageBlockTypes.Timeline,
        Content = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["title"] = Bilingual("成立") }) },
    };

    public static AdminPageBlockInput Steps() => new()
    {
        BlockType = PageBlockTypes.Steps,
        Content = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["title"] = Bilingual("第一步：填表") }) },
    };

    public static AdminPageBlockInput StepsInvalidEmpty() => new()
    {
        BlockType = PageBlockTypes.Steps,
        Content = new JsonObject { ["items"] = new JsonArray() },
    };

    public static AdminPageBlockInput StatCards() => new()
    {
        BlockType = PageBlockTypes.StatCards,
        Content = new JsonObject
        {
            ["items"] = new JsonArray(new JsonObject { ["value"] = "5000+", ["label"] = Bilingual("累積學員") }),
        },
    };

    public static AdminPageBlockInput StatCardsInvalidMissingValue() => new()
    {
        BlockType = PageBlockTypes.StatCards,
        Content = new JsonObject { ["items"] = new JsonArray(new JsonObject { ["label"] = Bilingual("累積學員") }) },
    };

    public static AdminPageBlockInput Table() => new()
    {
        BlockType = PageBlockTypes.Table,
        Content = new JsonObject
        {
            ["headers"] = new JsonArray(Bilingual("項目"), Bilingual("數量")),
            ["rows"] = new JsonArray(new JsonArray("學員", "120")),
        },
    };

    public static AdminPageBlockInput TableInvalidColumnMismatch() => new()
    {
        BlockType = PageBlockTypes.Table,
        Content = new JsonObject
        {
            ["headers"] = new JsonArray(Bilingual("項目"), Bilingual("數量")),
            ["rows"] = new JsonArray(new JsonArray("只有一欄")), // 欄數與標題不符
        },
    };

    public static AdminPageBlockInput FileDownload() => new()
    {
        BlockType = PageBlockTypes.FileDownload,
        Content = new JsonObject { ["label"] = Bilingual("招生簡章"), ["fileUrl"] = "https://example.test/brochure.pdf" },
    };

    public static AdminPageBlockInput FileDownloadInvalid() => new()
    {
        BlockType = PageBlockTypes.FileDownload,
        Content = new JsonObject { ["label"] = Bilingual("招生簡章") }, // 缺 fileUrl
    };

    public static AdminPageBlockInput UnknownType() => new()
    {
        BlockType = "not-a-real-block-type",
        Content = new JsonObject(),
    };
}
