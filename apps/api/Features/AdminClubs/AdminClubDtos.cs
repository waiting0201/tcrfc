namespace Tcrfc.Api.Features.AdminClubs;

/// <summary>單一語系的俱樂部名稱與簡介（<c>clubs_i18n</c>）。</summary>
public sealed record AdminClubLocaleContent
{
    public required string Name { get; init; }

    /// <summary>簡稱（<c>clubs_i18n.short_name</c>，最多 32 字）。磐石中文「台中磐石」、英文「Taichung Rock FC」、藍鯨中文「台中藍鯨」；
    /// **藍鯨英文不由開發端填值**（B-5，客戶尚未指定英文全名），之後由後台人員在這裡填寫。省略／空白＝沒有簡稱（公開端點退回繁中簡稱或全名）。</summary>
    public string? ShortName { get; init; }
    public string? Description { get; init; }
}

public sealed record AdminClubContentInput
{
    public required AdminClubLocaleContent Zh { get; init; }
    public AdminClubLocaleContent? En { get; init; }
}

public sealed record AdminClubListItemDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Domain { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required string DefaultLocale { get; init; }
    public required bool IsCollectingSubject { get; init; }
    public required int SortOrder { get; init; }
    public string? Status { get; init; }
}

/// <summary>
/// 🔴 主站規劃書 v3.20：標誌、Favicon、品牌主色與輔色由前台靜態資產與 CSS 定義，後台不設定——本 DTO 與建立／更新請求都不含這些欄位
/// （<c>og_image_*</c> 仍保留，由 H 模組全站預設 OG 圖編輯）。
/// </summary>
public sealed record AdminClubDetailDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Domain { get; init; }
    public string? OgImageKey { get; init; }
    public string? OgImageUrl { get; init; }
    public string? OgImageThumbUrl { get; init; }
    public string? InvoiceTitle { get; init; }
    public string? TaxId { get; init; }
    public required bool IsCollectingSubject { get; init; }
    public required string DefaultLocale { get; init; }
    public required int SortOrder { get; init; }
    public string? Status { get; init; }
    public required AdminClubLocaleContent Zh { get; init; }
    public AdminClubLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record CreateAdminClubRequest
{
    public required string Code { get; init; }
    public required string Domain { get; init; }
    public required AdminClubContentInput Content { get; init; }
    public string? InvoiceTitle { get; init; }
    public string? TaxId { get; init; }
    public bool IsCollectingSubject { get; init; } = true;
    public string DefaultLocale { get; init; } = "zh-Hant";
    public int SortOrder { get; init; }
}

public sealed record UpdateAdminClubRequest
{
    public required string Domain { get; init; }
    public required AdminClubContentInput Content { get; init; }
    public string? InvoiceTitle { get; init; }
    public string? TaxId { get; init; }
    public required bool IsCollectingSubject { get; init; }
    public required string DefaultLocale { get; init; }
    public required int SortOrder { get; init; }
    public string? Status { get; init; }
}
