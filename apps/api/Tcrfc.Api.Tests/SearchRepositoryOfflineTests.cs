using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Search;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// G-02 全站搜尋的離線測試（不需要資料庫）：關鍵字解析、摘錄、各類別 EF 查詢能翻譯成 SQL。
/// 命中與否、可見性、語系回退等資料相依的行為見 <c>SearchPublicTests</c>（需要資料庫）。
/// </summary>
public sealed class SearchRepositoryOfflineTests
{
    [Fact]
    public void ParseQuery_全形轉半形_小寫_壓縮空白_拆關鍵字()
    {
        var (query, tokens) = SearchRepository.ParseQuery("  ＦＥＥ　　Camp  ");
        Assert.Equal("fee camp", query);
        Assert.Equal(["fee", "camp"], tokens);
    }

    [Fact]
    public void ParseQuery_重複關鍵字去除_最多五個()
    {
        var (_, tokens) = SearchRepository.ParseQuery("a1 a1 b2 c3 d4 e5 f6 g7");
        Assert.Equal(["a1", "b2", "c3", "d4", "e5"], tokens);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("a  ")]
    public void ParseQuery_空白或單一拉丁字元_400(string? input)
        => Assert.Throws<PublicValidationException>(() => SearchRepository.ParseQuery(input));

    [Fact]
    public void ParseQuery_單一中文字放行_過長400()
    {
        Assert.Equal("磐", SearchRepository.ParseQuery("磐").Query);
        Assert.Throws<PublicValidationException>(() => SearchRepository.ParseQuery(new string('字', SearchRepository.MaxQueryLength + 1)));
    }

    [Fact]
    public void ParseQuery_LIKE萬用字元只是普通字元_不被拒絕也不被展開()
    {
        var (query, tokens) = SearchRepository.ParseQuery("100% [a_b]");
        Assert.Equal("100% [a_b]", query);
        Assert.Equal(["100%", "[a_b]"], tokens);
    }

    [Fact]
    public void BuildSnippet_圍繞命中處截取_去除標記_前後加省略號()
    {
        var text = "<p>" + new string('前', 80) + "報名費用說明" + new string('後', 200) + "</p>";
        var snippet = SearchRepository.BuildSnippet(text, ["費用"]);
        Assert.NotNull(snippet);
        Assert.StartsWith("…", snippet);
        Assert.EndsWith("…", snippet);
        Assert.Contains("費用", snippet);
        Assert.DoesNotContain("<", snippet);
        Assert.True(snippet!.Length <= 120 + 2);
    }

    [Fact]
    public void BuildSnippet_沒有命中_取開頭_空白回傳null()
    {
        Assert.Equal("短文字", SearchRepository.BuildSnippet("短文字", ["不存在"]));
        Assert.Null(SearchRepository.BuildSnippet("   ", ["x"]));
        Assert.Null(SearchRepository.BuildSnippet(null, ["x"]));
        Assert.Null(SearchRepository.BuildSnippet("<br/>", ["x"]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(SearchTypes.News)]
    [InlineData(SearchTypes.Faq)]
    [InlineData(SearchTypes.Program)]
    [InlineData(SearchTypes.Player)]
    [InlineData(SearchTypes.Coach)]
    [InlineData(SearchTypes.Charity)]
    public async Task 各類別查詢都能翻譯成SQL(string? type)
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        var repository = new SearchRepository(db, new UnavailableImagePublicUrlResolver());
        var scope = ClubScopeTestFactory.Create(Guid.NewGuid(), "tcrfc");
        await OfflineQueryTranslation.AssertTranslatesAsync(
            () => repository.SearchAsync(scope, "費用 camp", type, "en", 1, 20, CancellationToken.None));
    }

    [Fact]
    public async Task 不合法的分類_400_不碰資料庫()
    {
        await using var db = OfflineQueryTranslation.CreateContext();
        var repository = new SearchRepository(db, new UnavailableImagePublicUrlResolver());
        var scope = ClubScopeTestFactory.Create(Guid.NewGuid(), "tcrfc");
        await Assert.ThrowsAsync<PublicValidationException>(
            () => repository.SearchAsync(scope, "費用", "members", "zh", 1, 20, CancellationToken.None));
    }
}
