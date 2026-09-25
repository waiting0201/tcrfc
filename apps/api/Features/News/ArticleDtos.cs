using Tcrfc.Api.Features.Seo;

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

    /// <summary>Meta Keywords（S1-12 新增）。對應 <c>articles_i18n.seo_keywords</c>。</summary>
    public string? SeoKeywords { get; init; }

    /// <summary>OG 圖片完整網址（S1-12 驗收退回後補做）。已套用完整優先序：**這篇文章專屬的 OG
    /// 圖片 &gt; 全站預設 OG 圖片（<c>Club.OgImageKey</c>） &gt; 這篇文章的封面圖片
    /// （<c>cover_key</c>）**。<c>null</c>＝三層都沒有圖片可用。</summary>
    public string? OgImageUrl { get; init; }

    public int? OgImageWidth { get; init; }
    public int? OgImageHeight { get; init; }
    public string? OgImageAlt { get; init; }

    /// <summary>手動覆寫 canonical（S1-12 新增）。<c>null</c>＝前台沿用自動產生的 canonical。</summary>
    public string? CanonicalPath { get; init; }

    /// <summary>單頁 noindex 開關（S1-12 新增）。與全站上線前 noindex 是兩個機制，見
    /// <c>db/club-schema.sql</c> 對 <c>articles.is_noindex</c> 的註解。</summary>
    public required bool IsNoindex { get; init; }

    public required bool IsShared { get; init; }

    /// <summary>GEO-05（S1-12c／S1-12f）：這篇文章的資料是否足以輸出 BreadcrumbList 結構化資料
    /// （<see cref="SchemaType.BreadcrumbList"/> 要求「這一頁本身有沒有可用的標題與網址」，見
    /// <see cref="SchemaRequiredFields"/> 檔頭「BreadcrumbList」段——本專案目前沒有頁面階層資料表，
    /// 只能檢查這個最小前提）。判斷條件單一來源見 <see cref="SchemaRequiredFields"/>，這裡不重新
    /// 判斷一次（E-39）。<c>Title</c>／<c>Slug</c> 皆為 Article 既有必填內容，本欄位在有這篇文章時
    /// 實務上恆為 <c>true</c>，仍照單一來源機制走，不因為「反正都會是 true」就省略這個檢查。</summary>
    public required bool BreadcrumbSchemaEligible { get; init; }

    /// <summary>標籤（S1-5 新增）。前台詳情頁規格明文列出「標籤」是顯示項目之一（規劃書 3.7）。</summary>
    public required IReadOnlyList<ArticleTagDto> Tags { get; init; }

    /// <summary>核心價值標籤（S1-5 新增），值域見 <c>value_tag_links.value_tag</c> 的 CHECK 約束。</summary>
    public required IReadOnlyList<string> CoreValueTags { get; init; }

    /// <summary>關聯（S1-5 新增，球員／球隊／賽事／課程／夥伴）。</summary>
    public required IReadOnlyList<ArticleRelationDto> Relations { get; init; }

    /// <summary>
    /// GEO-05／S1-12c：這篇文章的資料是否足以輸出 <c>Article</c> Schema
    /// （<see cref="SchemaType.Article"/> 必填欄位齊全——標題、發布時間、圖片，見
    /// <see cref="SchemaRequiredFields"/> 檔頭「必填欄位怎麼訂出來的」）。前台
    /// （<c>app/pages/zh/news/[slug]/index.vue</c>）改讀這個欄位決定輸不輸出 <c>Article</c> 的
    /// JSON-LD，不再自己判斷「有沒有發布時間」——判斷條件只在 <see cref="SchemaRequiredFields"/>
    /// 宣告一次（E-39）。
    /// </summary>
    public required bool SchemaEligible { get; init; }
}
