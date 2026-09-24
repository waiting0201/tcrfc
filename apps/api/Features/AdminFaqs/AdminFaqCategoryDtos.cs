namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>單一語系的分類名稱。</summary>
public sealed record AdminFaqCategoryLocaleContent
{
    public string? Name { get; init; }
}

/// <summary><c>Zh</c> 必填，<c>En</c> 可省略（CLAUDE.md 全域規定 4）。</summary>
public sealed record AdminFaqCategoryContentInput
{
    public required AdminFaqCategoryLocaleContent Zh { get; init; }
    public AdminFaqCategoryLocaleContent? En { get; init; }
}

/// <summary>
/// 建立／更新常見問題主題分類的請求。<c>faq_categories</c> 沒有 <c>club_id</c>（規劃書 B4
/// 「主題分類管理」對應十個全站共用主題，同 <c>article_categories</c> 的共用主檔設計），
/// 因此沒有俱樂部路由段，端點走 <see cref="Security.IAdminSystemAuthorizer"/>。
/// </summary>
public sealed record CreateAdminFaqCategoryRequest
{
    /// <summary>對應 <c>faq_categories.slug</c>，格式見 <see cref="FaqSlugPolicy"/>。</summary>
    public required string Slug { get; init; }

    public required int SortOrder { get; init; }

    /// <summary>S1-7a 新增：省略時預設 <c>true</c>（新增分類預設啟用）。</summary>
    public bool IsEnabled { get; init; } = true;

    public required AdminFaqCategoryContentInput Content { get; init; }
}

public sealed record UpdateAdminFaqCategoryRequest
{
    public required string Slug { get; init; }

    public required int SortOrder { get; init; }

    /// <summary>S1-7a 新增：軟停用開關。<c>false</c>＝從公開分類導覽消失（見
    /// <c>Features/Faqs/FaqsRepository.ListCategoriesAsync</c>），既有題目與關聯不受影響、
    /// 可隨時改回 <c>true</c> 重新啟用——取代先前用 DELETE 湊「停用」的做法（DELETE 仍保留，
    /// 但現在是真正的刪除，見 <c>AdminFaqCategoriesRepository.DeleteAsync</c>）。</summary>
    public required bool IsEnabled { get; init; }

    public required AdminFaqCategoryContentInput Content { get; init; }
}

public sealed record AdminFaqCategoryListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsEnabled { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }

    /// <summary>目前掛在這個分類底下的常見問題筆數（不分狀態），供刪除前的提醒用。</summary>
    public required int FaqCount { get; init; }

    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminFaqCategoryDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsEnabled { get; init; }
    public required AdminFaqCategoryLocaleContent Zh { get; init; }
    public AdminFaqCategoryLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
