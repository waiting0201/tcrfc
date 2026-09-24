namespace Tcrfc.Api.Features.News;

/// <summary>公開讀取用的標籤（S1-5 新增）：<c>Name</c> 已依語系回退挑值，跟公開 API 其他欄位
/// 同一套規則（跟後台 <c>AdminArticleTagDto</c> 分開——後台要看未回退的原始值，公開 API 不需要）。</summary>
public sealed record ArticleTagDto
{
    public required string Slug { get; init; }
    public string? Name { get; init; }
}

/// <summary>公開讀取用的文章關聯（S1-5 新增）：只回傳型別與目標 id，不展開目標實體的內容——
/// 展開哪些欄位屬於各自型別的公開 API 該回答的問題（球員頁、球隊頁……），這裡不重複組裝。</summary>
public sealed record ArticleRelationDto
{
    public required string TargetType { get; init; }
    public required Guid TargetId { get; init; }
}

/// <summary>
/// 新聞列表項目。⛔ 不含 <c>body</c>（完整內容，體積大且列表用不到，見 <see cref="ArticleDetailDto"/>）。
/// <c>articles</c> 不在受限欄位清單內（docs/12b-database-tables.md §8）——公開發布的新聞稿本來就公開。
/// </summary>
public sealed record ArticleListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string CategoryCode { get; init; }
    public string? CategoryName { get; init; }
    public string? CoverKey { get; init; }
    public required bool IsFeatured { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }

    /// <summary>true＝兩隊共同新聞（<c>club_id IS NULL</c>）。目前種子資料無此情形，見 README 驗收紀錄。</summary>
    public required bool IsShared { get; init; }

    /// <summary>標籤（S1-5 新增），供列表頁顯示標籤晶片與「標籤篩選」對照。</summary>
    public required IReadOnlyList<ArticleTagDto> Tags { get; init; }
}

/// <summary>新聞單篇內容。⛔ 只在明確依 slug 查詢單篇時才回傳 <c>Body</c>，列表 API 不帶。</summary>
public sealed record ArticleDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string CategoryCode { get; init; }
    public string? CategoryName { get; init; }
    public string? CoverKey { get; init; }
    public required bool IsFeatured { get; init; }
    public int ViewCount { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }

    /// <summary>原始 JSON 字串（<c>articles_i18n.body</c>，Azure SQL 原生 json 型別）。
    /// 本次種子資料全數為 null（文稿仍是 .gdoc 捷徑，見 db/seed/README.md「已知落差」）——
    /// 回傳 null 是資料現況，不是這支 API 的錯誤。</summary>
    public string? BodyJson { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public required bool IsShared { get; init; }

    /// <summary>標籤（S1-5 新增）。前台詳情頁規格明文列出「標籤」是顯示項目之一（規劃書 3.7）。</summary>
    public required IReadOnlyList<ArticleTagDto> Tags { get; init; }

    /// <summary>核心價值標籤（S1-5 新增），值域見 <c>value_tag_links.value_tag</c> 的 CHECK 約束。</summary>
    public required IReadOnlyList<string> CoreValueTags { get; init; }

    /// <summary>關聯（S1-5 新增，球員／球隊／賽事／課程／夥伴）。</summary>
    public required IReadOnlyList<ArticleRelationDto> Relations { get; init; }
}
