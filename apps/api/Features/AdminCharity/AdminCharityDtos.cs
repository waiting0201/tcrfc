namespace Tcrfc.Api.Features.AdminCharity;

// ═════════════ 公益團體（charities）═════════════

public sealed record AdminCharityOrgLocaleContent
{
    public required string Name { get; init; }
    public string? Intro { get; init; }
}

public sealed record AdminCharityOrgContentInput
{
    public required AdminCharityOrgLocaleContent Zh { get; init; }
    public AdminCharityOrgLocaleContent? En { get; init; }
}

public sealed record AdminCharityOrgListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }

    /// <summary>true＝兩隊共同資料，本俱樂部範圍端點只能檢視、不可編輯。</summary>
    public required bool IsShared { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? LogoKey { get; init; }
    public string? LogoUrl { get; init; }
    public string? LogoThumbUrl { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required int ProgramCount { get; init; }
    public required int RecordCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminCharityOrgProgramRefDto
{
    public required Guid Id { get; init; }
    public string? NameZh { get; init; }
    public required string Status { get; init; }
}

public sealed record AdminCharityOrgRecordRefDto
{
    public required Guid Id { get; init; }
    public DateOnly? HappenedOn { get; init; }
    public string? DonationContentZh { get; init; }
}

public sealed record AdminCharityOrgDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required bool IsShared { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? LogoKey { get; init; }
    public string? LogoUrl { get; init; }
    public required AdminCharityOrgLocaleContent Zh { get; init; }
    public AdminCharityOrgLocaleContent? En { get; init; }

    /// <summary>合作紀錄（規劃書 B5「公益團體資料」）：這個團體受贈的計畫與事蹟，唯讀彙整。</summary>
    public required IReadOnlyList<AdminCharityOrgProgramRefDto> Programs { get; init; }
    public required IReadOnlyList<AdminCharityOrgRecordRefDto> Records { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminCharityOrgRequest
{
    public string? Slug { get; init; }
    public string? WebsiteUrl { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public required AdminCharityOrgContentInput Content { get; init; }
    public bool RemoveLogo { get; init; }
}

// ═════════════ 慈善計畫（charity_programs）═════════════

public sealed record AdminCharityProgramLocaleContent
{
    public required string Name { get; init; }
    public string? TargetAudience { get; init; }

    /// <summary>緣起與內容：區塊編輯器整段 JSON（只驗證語法，不驗證區塊結構，理由同 P1 課程）。</summary>
    public string? Content { get; init; }

    /// <summary>捐助內容（文字描述）。</summary>
    public string? DonationContent { get; init; }
}

public sealed record AdminCharityProgramContentInput
{
    public required AdminCharityProgramLocaleContent Zh { get; init; }
    public AdminCharityProgramLocaleContent? En { get; init; }
}

public sealed record AdminCharityProgramListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required bool IsShared { get; init; }

    /// <summary><c>draft</c>（不公開）／<c>published</c>（公開）。</summary>
    public required string Status { get; init; }

    /// <summary>前台顯示的進行狀態：<c>ongoing</c>（進行中，沒填結束日或結束日尚未到）／<c>completed</c>（已完成）。</summary>
    public required string Progress { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsPinned { get; init; }
    public required Guid CharityId { get; init; }
    public string? CharityNameZh { get; init; }
    public string? CoverKey { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbUrl { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminCharityLinkRefDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? Title { get; init; }
}

public sealed record AdminGalleryImageDto
{
    public required Guid Id { get; init; }
    public required string ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ThumbUrl { get; init; }
    public required int SortOrder { get; init; }
}

public sealed record AdminCharityProgramDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required bool IsShared { get; init; }
    public required string Status { get; init; }
    public required string Progress { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsPinned { get; init; }
    public required Guid CharityId { get; init; }
    public string? CharityNameZh { get; init; }
    public string? CoverKey { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbUrl { get; init; }
    public required AdminCharityProgramLocaleContent Zh { get; init; }
    public AdminCharityProgramLocaleContent? En { get; init; }

    /// <summary>贊助夥伴（E1 夥伴）。</summary>
    public required IReadOnlyList<AdminCharityLinkRefDto> Partners { get; init; }

    /// <summary>贊助夥伴（E2 贊助商）。</summary>
    public required IReadOnlyList<AdminCharityLinkRefDto> Sponsors { get; init; }

    /// <summary>關聯報導（7.7 新聞）。</summary>
    public required IReadOnlyList<AdminCharityLinkRefDto> Articles { get; init; }

    /// <summary>活動圖片藝廊（不含封面）。</summary>
    public required IReadOnlyList<AdminGalleryImageDto> Images { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminCharityProgramRequest
{
    public string? Slug { get; init; }
    public required Guid CharityId { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }

    /// <summary><c>draft</c>／<c>published</c>。</summary>
    public required string Status { get; init; }
    public int SortOrder { get; init; }
    public bool IsPinned { get; init; }
    public required AdminCharityProgramContentInput Content { get; init; }

    /// <summary>省略＝維持不變；空陣列＝清空；有值＝整份取代。三個關聯欄位語意相同。</summary>
    public IReadOnlyList<Guid>? PartnerIds { get; init; }
    public IReadOnlyList<Guid>? SponsorIds { get; init; }
    public IReadOnlyList<Guid>? ArticleIds { get; init; }
    public bool RemoveCover { get; init; }
}

// ═════════════ 事蹟紀錄（impact_records）═════════════

public sealed record AdminImpactRecordLocaleContent
{
    /// <summary>捐助內容（必填，例如「足球 50 顆、訓練背心 100 件」）。</summary>
    public required string DonationContent { get; init; }
    public string? Location { get; init; }
    public string? BriefDescription { get; init; }
}

public sealed record AdminImpactRecordContentInput
{
    public required AdminImpactRecordLocaleContent Zh { get; init; }
    public AdminImpactRecordLocaleContent? En { get; init; }
}

public sealed record AdminImpactRecordListItemDto
{
    public required Guid Id { get; init; }
    public required bool IsShared { get; init; }
    public required Guid CharityId { get; init; }
    public string? CharityNameZh { get; init; }
    public Guid? CharityProgramId { get; init; }
    public string? ProgramNameZh { get; init; }
    public DateOnly? HappenedOn { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsPinned { get; init; }
    public string? ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public string? DonationContentZh { get; init; }
    public string? LocationZh { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminImpactRecordDetailDto
{
    public required Guid Id { get; init; }
    public required bool IsShared { get; init; }
    public required Guid CharityId { get; init; }
    public string? CharityNameZh { get; init; }
    public Guid? CharityProgramId { get; init; }
    public string? ProgramNameZh { get; init; }
    public DateOnly? HappenedOn { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsPinned { get; init; }
    public string? ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public required AdminImpactRecordLocaleContent Zh { get; init; }
    public AdminImpactRecordLocaleContent? En { get; init; }

    /// <summary>其他活動圖片（可多張，不含主圖）。</summary>
    public required IReadOnlyList<AdminGalleryImageDto> Images { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminImpactRecordRequest
{
    /// <summary>公益團體（必填）。</summary>
    public required Guid CharityId { get; init; }

    /// <summary>所屬慈善計畫（選填）。</summary>
    public Guid? CharityProgramId { get; init; }
    public DateOnly? HappenedOn { get; init; }
    public int SortOrder { get; init; }
    public bool IsPinned { get; init; }
    public required AdminImpactRecordContentInput Content { get; init; }
}

// ═════════════ 影響力數據（impact_metrics）═════════════

public sealed record AdminImpactMetricLocaleContent
{
    public required string Name { get; init; }

    /// <summary>單位（如「人」「場」「元」）。</summary>
    public string? Unit { get; init; }
}

public sealed record AdminImpactMetricContentInput
{
    public required AdminImpactMetricLocaleContent Zh { get; init; }
    public AdminImpactMetricLocaleContent? En { get; init; }
}

public sealed record AdminImpactMetricDto
{
    public required Guid Id { get; init; }
    public required bool IsShared { get; init; }

    /// <summary>所屬計畫；空＝全站層級的統計項目。</summary>
    public Guid? CharityProgramId { get; init; }
    public string? ProgramNameZh { get; init; }
    public int? Value { get; init; }

    /// <summary>是否公開。**金額類項目預設不公開**：建立時省略此欄位即為 <c>false</c>，
    /// 要公開必須明確送 <c>true</c>。</summary>
    public required bool IsPublic { get; init; }
    public required int SortOrder { get; init; }
    public required AdminImpactMetricLocaleContent Zh { get; init; }
    public AdminImpactMetricLocaleContent? En { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminImpactMetricRequest
{
    public Guid? CharityProgramId { get; init; }
    public int? Value { get; init; }
    public bool IsPublic { get; init; }
    public int SortOrder { get; init; }
    public required AdminImpactMetricContentInput Content { get; init; }
}

// ═════════════ 捐款導流與參與方式設定 ═════════════

public sealed record AdminCharityBilingualTextDto
{
    public string? Zh { get; init; }
    public string? En { get; init; }
}

/// <summary>捐款導流設定與參與方式設定（規劃書 B5「捐款導流設定」「參與方式設定」，CH-6）。
/// 整份取代語意：PUT 送什麼就存什麼，未送的欄位視為清空。</summary>
public sealed record AdminCharitySettingsDto
{
    /// <summary>慈善捐款平台網址（https）。不寫死在前台版型，一律由這裡設定。空白＝前台不顯示球迷捐款按鈕。</summary>
    public string? DonationUrl { get; init; }

    /// <summary>捐款導流按鈕文案。🔴 設定了 <see cref="DonationUrl"/> 時，中文文案必須點明捐款由
    /// 「台灣足球策略發展協會」收受（規劃書 §3.11，不得讓使用者誤以為是捐給台中磐石）。</summary>
    public AdminCharityBilingualTextDto? DonationCta { get; init; }

    /// <summary>「企業合作公益專案」按鈕文案與導向（站內路徑，例如 <c>/zh/join/partnership/</c>，或 https 網址）。</summary>
    public AdminCharityBilingualTextDto? CorporateCta { get; init; }
    public string? CorporateUrl { get; init; }

    /// <summary>「球迷捐款」按鈕文案，導向固定為 <see cref="DonationUrl"/>。</summary>
    public AdminCharityBilingualTextDto? FanCta { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
