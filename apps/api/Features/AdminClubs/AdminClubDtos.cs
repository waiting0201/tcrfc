namespace Tcrfc.Api.Features.AdminClubs;

/// <summary>單一語系的俱樂部名稱與簡介（<c>clubs_i18n</c>）。</summary>
public sealed record AdminClubLocaleContent
{
    public required string Name { get; init; }
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
/// 🔴 標誌／favicon／OG 圖三組欄位本輪刻意唯讀（<c>*_key</c> 只回傳目前的值，不接受更新）——
/// 另一位 backend agent 同時在改 <c>BlobImageStorageService</c> 與圖片上傳共用元件，任務指示
/// 明確要求避免動到它。J4 俱樂部品牌圖片的「選檔即時預覽、儲存才上傳」流程之後應比照
/// <c>Features/AdminNews</c> 的 multipart 契約另外補上，見 apps/api/README.md 的說明。
/// </summary>
public sealed record AdminClubDetailDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Domain { get; init; }
    public string? LogoLightKey { get; init; }
    public string? LogoDarkKey { get; init; }
    public string? FaviconKey { get; init; }
    public string? OgImageKey { get; init; }
    public string? BrandColor { get; init; }
    public string? BrandSecondaryColor { get; init; }
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
    public string? BrandColor { get; init; }
    public string? BrandSecondaryColor { get; init; }
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
    public string? BrandColor { get; init; }
    public string? BrandSecondaryColor { get; init; }
    public string? InvoiceTitle { get; init; }
    public string? TaxId { get; init; }
    public required bool IsCollectingSubject { get; init; }
    public required string DefaultLocale { get; init; }
    public required int SortOrder { get; init; }
    public string? Status { get; init; }
}
