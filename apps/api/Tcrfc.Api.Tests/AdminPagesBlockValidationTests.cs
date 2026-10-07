using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 逐一驗證 <c>Features/AdminPages/PageBlockContentProcessor.cs</c> 對 12 種區塊型別的內容驗證——
/// 固定頁之後區塊結構由版型鎖定，所以這裡打測試專用的 <c>test/all-types</c> 頁（12 種型別各一個，依
/// <see cref="PageBlockTypes.All"/> 順序）：每種型別各一個「合法內容應該存檔成功」與「刻意缺欄位應該回 400」的配對，
/// 另外驗證區塊類型不是版型指定的（含不支援的型別）也回 400。**不含圖片上傳**（真的上傳見 <c>AdminPagesImageTests</c>，
/// 需要 Azurite）：圖文左右／圖片藝廊只驗證「沿用既有圖片鍵」與結構性缺漏。
/// </summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AdminPagesBlockValidationTests(AdminWriteApiFixture fixture)
{
    private async Task<HttpClient> ContentEditorClientAsync()
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", await TestAdminTokens.IssueAccessTokenForSeededUserAsync("content.editor@tcrfc.test"));
        return client;
    }

    /// <summary><c>test/all-types</c> 的 12 個合法區塊（依版型順序）。</summary>
    private static List<AdminPageBlockInput> ValidAll() =>
    [
        PageBlockSamples.Text(),
        PageBlockSamples.TextImageExisting(),
        PageBlockSamples.GalleryExisting(),
        PageBlockSamples.VideoEmbed(),
        PageBlockSamples.Quote(),
        PageBlockSamples.Cta(),
        PageBlockSamples.AccordionFaq(),
        PageBlockSamples.Timeline(),
        PageBlockSamples.Steps(),
        PageBlockSamples.StatCards(),
        PageBlockSamples.Table(),
        PageBlockSamples.FileDownload(),
    ];

    private static int IndexOf(string blockType) => PageBlockTypes.All.ToList().IndexOf(blockType);

    public static IEnumerable<object[]> ValidBlocks()
    {
        yield return [PageBlockSamples.Text()];
        yield return [PageBlockSamples.TextImageExisting()];
        yield return [PageBlockSamples.GalleryExisting()];
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
    }

    private async Task<(HttpClient Client, AdminPageDetailDto Page)> NewAllTypesPageAsync()
    {
        var client = await ContentEditorClientAsync();
        var page = await TestPages.CreateAsync(client, "tcrfc", TestPageTemplates.AllTypes, ValidAll(), "區塊驗證測試");
        return (client, page);
    }

    [Fact]
    public async Task 十二種型別全部合法_存檔成功()
    {
        var (client, page) = await NewAllTypesPageAsync();
        using var _ = client;
        try
        {
            var response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, ValidAll()));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var updated = (await response.Content.ReadFromJsonAsync<AdminPageDetailDto>(TestJson.Options))!;
            Assert.Equal(PageBlockTypes.All, updated.Blocks.Select(b => b.BlockType).ToArray());
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    [Theory]
    [MemberData(nameof(ValidBlocks))]
    public async Task 合法區塊內容_存檔成功(AdminPageBlockInput block)
    {
        var (client, page) = await NewAllTypesPageAsync();
        using var _ = client;
        try
        {
            var blocks = ValidAll();
            blocks[IndexOf(block.BlockType)] = block;
            var response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, blocks));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidBlocks))]
    public async Task 不合法區塊內容_回400且欄位鍵指向該區塊(AdminPageBlockInput block)
    {
        var (client, page) = await NewAllTypesPageAsync();
        using var _ = client;
        try
        {
            var index = IndexOf(block.BlockType);
            var blocks = ValidAll();
            blocks[index] = block;
            var response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, blocks));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var keys = await TestPages.ErrorKeysAsync(response);
            Assert.Contains(keys.Keys, k => k.StartsWith($"blocks[{index}]", StringComparison.Ordinal));
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    [Fact]
    public async Task 不支援的區塊型別_回400鍵為該區塊()
    {
        var (client, page) = await NewAllTypesPageAsync();
        using var _ = client;
        try
        {
            var blocks = ValidAll();
            blocks[0] = PageBlockSamples.UnknownType();
            var response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, blocks));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("blocks[0]", (await TestPages.ErrorKeysAsync(response)).Keys);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    [Fact]
    public async Task 圖文左右_缺少替代文字_回400()
    {
        var (client, page) = await NewAllTypesPageAsync();
        using var _ = client;
        try
        {
            var blocks = ValidAll();
            blocks[IndexOf(PageBlockTypes.TextImage)] = PageBlockSamples.TextImagePending(altZh: "   "); // 空白視同未填
            var response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, blocks));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }

    [Fact]
    public async Task 圖文左右_標示待上傳但沒有夾檔案_回400()
    {
        var (client, page) = await NewAllTypesPageAsync();
        using var _ = client;
        try
        {
            // 沒有帶對應的 file:1:image，PageBlockContentProcessor 應該擋下。
            var blocks = ValidAll();
            blocks[IndexOf(PageBlockTypes.TextImage)] = PageBlockSamples.TextImagePending();
            var response = await TestPages.PutAsync(client, "tcrfc", page, TestPages.UpdateRequest(page, blocks));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await TestPages.DeleteAsync(page.Id);
        }
    }
}
