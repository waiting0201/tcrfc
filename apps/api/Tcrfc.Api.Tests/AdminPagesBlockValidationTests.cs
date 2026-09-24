using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 逐一驗證 <c>Features/AdminPages/PageBlockContentProcessor.cs</c> 對 12 種區塊型別的內容驗證——
/// 每種型別各一個「合法內容應該成功建立」與「刻意缺欄位應該回 400」的配對，另外驗證不支援的區塊
/// 型別本身也回 400。**不含圖片欄位的解析**（<see cref="PageBlockTypes.TextImage"/>／
/// <see cref="PageBlockTypes.Gallery"/> 只驗證「完全沒有圖片欄位」這種結構性缺漏——真的上傳圖片
/// 見 <c>AdminPagesImageTests</c>，那組測試需要 Azurite，跟本檔的 fixture 不同）。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminPagesBlockValidationTests(AdminWriteApiFixture fixture)
{
    private static string UniqueSlug() => $"admin-write-page-block-{Guid.NewGuid():N}";

    private async Task<HttpClient> ContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    private static CreatePageRequest Request(AdminPageBlockInput block) => new()
    {
        Slug = UniqueSlug(),
        Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "區塊驗證測試" } },
        Blocks = [block],
    };

    public static IEnumerable<object[]> ValidBlocks()
    {
        yield return [PageBlockSamples.Text()];
        yield return [PageBlockSamples.VideoEmbed()];
        yield return [PageBlockSamples.Quote()];
        yield return [PageBlockSamples.Cta()];
        yield return [PageBlockSamples.AccordionFaq()];
        yield return [PageBlockSamples.Timeline()];
        yield return [PageBlockSamples.Steps()];
        yield return [PageBlockSamples.StatCards()];
        yield return [PageBlockSamples.Table()];
        yield return [PageBlockSamples.FileDownload()];
    }

    public static IEnumerable<object[]> InvalidBlocks()
    {
        yield return [PageBlockSamples.TextInvalid()];
        yield return [PageBlockSamples.TextImageInvalidMissingImage()];
        yield return [PageBlockSamples.GalleryInvalidEmpty()];
        yield return [PageBlockSamples.VideoEmbedInvalidProvider()];
        yield return [PageBlockSamples.QuoteInvalid()];
        yield return [PageBlockSamples.CtaInvalid()];
        yield return [PageBlockSamples.AccordionFaqInvalidEmpty()];
        yield return [PageBlockSamples.TimelineInvalidMissingDate()];
        yield return [PageBlockSamples.StepsInvalidEmpty()];
        yield return [PageBlockSamples.StatCardsInvalidMissingValue()];
        yield return [PageBlockSamples.TableInvalidColumnMismatch()];
        yield return [PageBlockSamples.FileDownloadInvalid()];
        yield return [PageBlockSamples.UnknownType()];
    }

    [Theory]
    [MemberData(nameof(ValidBlocks))]
    public async Task 合法區塊內容_建立成功(AdminPageBlockInput block)
    {
        using var client = await ContentEditorClientAsync();
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(Request(block)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        Assert.Single(created!.Blocks);
        Assert.Equal(block.BlockType, created.Blocks[0].BlockType);

        await client.DeleteAsync($"/api/v1/admin/tcrfc/pages/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(created.UpdatedAt.ToString("o"))}");
    }

    [Theory]
    [MemberData(nameof(InvalidBlocks))]
    public async Task 不合法區塊內容_回400(AdminPageBlockInput block)
    {
        using var client = await ContentEditorClientAsync();
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(Request(block)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 圖文左右_缺少替代文字_回400()
    {
        using var client = await ContentEditorClientAsync();
        var block = PageBlockSamples.TextImagePending(altZh: "   "); // 空白視同未填
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(Request(block)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 圖文左右_標示待上傳但沒有夾檔案_回400()
    {
        using var client = await ContentEditorClientAsync();
        // 沒有帶對應的 file:0:image，PageBlockContentProcessor 應該擋下。
        var block = PageBlockSamples.TextImagePending();
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(Request(block)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task 空陣列區塊_允許建立空白草稿()
    {
        using var client = await ContentEditorClientAsync();
        var request = new CreatePageRequest
        {
            Slug = UniqueSlug(),
            Seo = new AdminPageSeoInput { Zh = new AdminPageSeoLocaleContent { SeoTitle = "空白草稿" } },
            Blocks = [],
        };
        var response = await client.PostAsync("/api/v1/admin/tcrfc/pages", AdminPageMultipart.Build(request));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options);
        Assert.NotNull(created);
        Assert.Empty(created!.Blocks);

        await client.DeleteAsync($"/api/v1/admin/tcrfc/pages/{created.Id}?expectedUpdatedAt={Uri.EscapeDataString(created.UpdatedAt.ToString("o"))}");
    }
}
