namespace Tcrfc.Api.Features.AdminSponsors;

/// <summary>E2 贊助商單一語系內容（<c>sponsors_i18n</c>）：名稱與贊助內容。</summary>
public sealed record AdminSponsorLocaleContent
{
    public required string Name { get; init; }
    public string? Content { get; init; }

    /// <summary>標誌替代文字（§4.0 圖片欄位組，逐語系；深色／淺色兩版是同一個標誌，共用這一欄）。對應 <c>sponsors_i18n.logo_alt</c>。</summary>
    public string? LogoAlt { get; init; }
}

public sealed record AdminSponsorContentInput
{
    public required AdminSponsorLocaleContent Zh { get; init; }
    public AdminSponsorLocaleContent? En { get; init; }
}

public sealed record AdminSponsorListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? Tier { get; init; }
    public DateOnly? ContractStartOn { get; init; }
    public DateOnly? ContractEndOn { get; init; }
    public DateOnly? ExpiryAlertOn { get; init; }

    /// <summary>合約狀態：<c>none</c>（沒填合約結束日）／<c>active</c>／<c>alert</c>（已到提醒日、合約尚未結束）／
    /// <c>expired</c>（合約已結束）。到期提醒（規劃書 E2）依這個欄位在畫面上標示。</summary>
    public required string ContractStatus { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactEmail { get; init; }
    public required int SortOrder { get; init; }
    public string? LogoDarkKey { get; init; }
    public string? LogoDarkUrl { get; init; }
    public string? LogoDarkThumbUrl { get; init; }
    public int? LogoDarkWidth { get; init; }
    public int? LogoDarkHeight { get; init; }
    public int? LogoLightWidth { get; init; }
    public int? LogoLightHeight { get; init; }
    public string? LogoLightKey { get; init; }
    public string? LogoLightUrl { get; init; }
    public string? LogoLightThumbUrl { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required int PackageCount { get; init; }
    public required int ActivationCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminSponsorPackageRefDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? NameZh { get; init; }
}

public sealed record AdminSponsorArticleRefDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? TitleZh { get; init; }
    public required string Status { get; init; }
}

public sealed record AdminSponsorDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? Tier { get; init; }
    public DateOnly? ContractStartOn { get; init; }
    public DateOnly? ContractEndOn { get; init; }
    public DateOnly? ExpiryAlertOn { get; init; }
    public required string ContractStatus { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactEmail { get; init; }
    public required int SortOrder { get; init; }
    public string? LogoDarkKey { get; init; }
    public string? LogoDarkUrl { get; init; }
    public string? LogoLightKey { get; init; }
    public string? LogoLightUrl { get; init; }
    public int? LogoDarkWidth { get; init; }
    public int? LogoDarkHeight { get; init; }
    public int? LogoLightWidth { get; init; }
    public int? LogoLightHeight { get; init; }
    public required AdminSponsorLocaleContent Zh { get; init; }
    public AdminSponsorLocaleContent? En { get; init; }
    public required IReadOnlyList<AdminSponsorPackageRefDto> Packages { get; init; }

    /// <summary>贊助故事（關聯文章，規劃書 E2「贊助故事」）。</summary>
    public required IReadOnlyList<AdminSponsorArticleRefDto> Articles { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminSponsorRequest
{
    public string? Slug { get; init; }

    /// <summary>主贊助／官方贊助／支持夥伴（規劃書 E2 三個等級，中文字面，資料庫 CHECK 同）。</summary>
    public string? Tier { get; init; }
    public DateOnly? ContractStartOn { get; init; }
    public DateOnly? ContractEndOn { get; init; }
    public DateOnly? ExpiryAlertOn { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactEmail { get; init; }
    public int SortOrder { get; init; }
    public required AdminSponsorContentInput Content { get; init; }

    /// <summary>省略（null）＝維持不變；空陣列＝清空；有值＝整份取代（比照 B2 新聞標籤的既有語意）。</summary>
    public IReadOnlyList<Guid>? PackageIds { get; init; }
    public IReadOnlyList<Guid>? ArticleIds { get; init; }
    public bool RemoveLogoDark { get; init; }
    public bool RemoveLogoLight { get; init; }
}

// ───────────── 贊助方案（9.4 九種方案卡片）─────────────

public sealed record AdminSponsorPackageLocaleContent
{
    public required string Name { get; init; }
    public string? Content { get; init; }

    /// <summary>權益清單（純文字，一行一項，前台自行斷行成清單）。</summary>
    public string? BenefitList { get; init; }
    public string? Audience { get; init; }
}

public sealed record AdminSponsorPackageContentInput
{
    public required AdminSponsorPackageLocaleContent Zh { get; init; }
    public AdminSponsorPackageLocaleContent? En { get; init; }
}

public sealed record AdminSponsorPackageListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public int? PriceMin { get; init; }
    public int? PriceMax { get; init; }
    public required bool IsPricePublic { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required int SponsorCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminSponsorPackageDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public int? PriceMin { get; init; }
    public int? PriceMax { get; init; }
    public required bool IsPricePublic { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required AdminSponsorPackageLocaleContent Zh { get; init; }
    public AdminSponsorPackageLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminSponsorPackageRequest
{
    public string? Slug { get; init; }
    public int? PriceMin { get; init; }
    public int? PriceMax { get; init; }

    /// <summary>省略時預設 <c>true</c>（資料庫預設）。規劃書 E2「價格區間（可設定不公開）」。</summary>
    public bool IsPricePublic { get; init; } = true;
    public int SortOrder { get; init; }

    /// <summary><c>draft</c>（不顯示）或 <c>published</c>（顯示）。</summary>
    public required string Status { get; init; }
    public required AdminSponsorPackageContentInput Content { get; init; }
}

// ───────────── 贊助活動（Activations）─────────────

public sealed record AdminActivationLocaleContent
{
    public required string Title { get; init; }
    public string? ResultSummary { get; init; }
}

public sealed record AdminActivationContentInput
{
    public required AdminActivationLocaleContent Zh { get; init; }
    public AdminActivationLocaleContent? En { get; init; }
}

public sealed record AdminActivationImageDto
{
    public required Guid Id { get; init; }
    public required string ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ThumbUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }

    /// <summary>圖集圖片替代文字（中文／英文；<c>sponsor_activation_images.image_alt_zh／image_alt_en</c>），用 <c>PUT .../images/{imageId}</c> 修改。</summary>
    public string? AltZh { get; init; }
    public string? AltEn { get; init; }
    public required int SortOrder { get; init; }
}

public sealed record AdminActivationDto
{
    public required Guid Id { get; init; }
    public required Guid SponsorId { get; init; }
    public DateOnly? HappenedOn { get; init; }
    public required int SortOrder { get; init; }
    public required AdminActivationLocaleContent Zh { get; init; }
    public AdminActivationLocaleContent? En { get; init; }
    public required IReadOnlyList<AdminActivationImageDto> Images { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminActivationRequest
{
    public DateOnly? HappenedOn { get; init; }
    public int SortOrder { get; init; }
    public required AdminActivationContentInput Content { get; init; }
}
