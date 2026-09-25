namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>一筆孤立頁面（S1-12，主站規劃書 §4.8 H「內部連結建議：依網站層級提示未被連結的孤立
/// 頁面」）。<see cref="EntityType"/> 值域 <c>page</c>／<c>article</c>（沿用
/// docs/18-work-errors.md 既有的型別詞彙表單數小寫慣例，見 <c>ArticleRelation.target_type</c>
/// 同一套命名）。</summary>
public sealed record OrphanPageDto
{
    public required string EntityType { get; init; }
    public required Guid Id { get; init; }

    /// <summary>公開網址（<c>page</c> 是 <c>/zh/{slug}/</c>，<c>article</c> 是
    /// <c>/zh/news/{slug}/</c>）——後台介面顯示網址不顯示原始 <c>slug</c> 欄位名稱本身，符合
    /// 「介面不顯示欄位名」通則，這裡的 <c>Path</c> 是完整可點的相對網址不是欄位名。</summary>
    public required string Path { get; init; }

    public string? TitleZh { get; init; }
}

public sealed record OrphanPageReportDto
{
    public required IReadOnlyList<OrphanPageDto> Items { get; init; }
}
