using Tcrfc.Api.Features.Partners;

namespace Tcrfc.Api.Features.CharityImpact;

/// <summary>受贈公益團體（前台 11.2／11.3／11.4 共用）。</summary>
public sealed record PublicCharityOrgDto
{
    public required string Slug { get; init; }
    public string? Name { get; init; }
    public string? Intro { get; init; }
    public string? LogoUrl { get; init; }

    /// <summary>標誌寬高（像素）與替代文字（當前語系，英文空白回退繁中）；沒有標誌時三者皆 <c>null</c>。</summary>
    public int? LogoWidth { get; init; }
    public int? LogoHeight { get; init; }
    public string? LogoAlt { get; init; }
    public string? WebsiteUrl { get; init; }
}

public sealed record PublicCharityImageDto
{
    public required string ImageUrl { get; init; }
    public string? ThumbUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }

    /// <summary>圖片替代文字（當前語系，英文空白回退中文）；沒填為 <c>null</c>。</summary>
    public string? Alt { get; init; }
}

/// <summary>11.2 慈善計畫列表項目。只回已發布的計畫，排序＝置頂優先、排序值、開始日（新到舊）。</summary>
public sealed record CharityProgramListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? Name { get; init; }
    public string? TargetAudience { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }

    /// <summary><c>ongoing</c>（進行中）／<c>completed</c>（已完成）。</summary>
    public required string Progress { get; init; }
    public required bool IsPinned { get; init; }
    public string? CoverUrl { get; init; }

    /// <summary>封面寬高（像素）與替代文字（當前語系，英文空白回退繁中）；沒有封面時三者皆 <c>null</c>。</summary>
    public int? CoverWidth { get; init; }
    public int? CoverHeight { get; init; }
    public string? CoverAlt { get; init; }
    public string? CharityName { get; init; }
}

public sealed record CharityLinkedItemDto
{
    public required string Slug { get; init; }
    public string? Name { get; init; }
    public string? LogoDarkUrl { get; init; }
    public string? LogoLightUrl { get; init; }
    public int? LogoDarkWidth { get; init; }
    public int? LogoDarkHeight { get; init; }
    public int? LogoLightWidth { get; init; }
    public int? LogoLightHeight { get; init; }
    public string? LogoAlt { get; init; }
}

public sealed record CharityArticleLinkDto
{
    public required string Slug { get; init; }
    public string? Title { get; init; }
}

/// <summary>11.2 慈善計畫詳情：計畫緣起（區塊 JSON 字串）、受贈公益團體、捐助內容、圖集、贊助夥伴、關聯報導。</summary>
public sealed record CharityProgramDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? Name { get; init; }
    public string? TargetAudience { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public required string Progress { get; init; }
    public string? CoverUrl { get; init; }

    /// <summary>封面寬高（像素）與替代文字（當前語系，英文空白回退繁中）；沒有封面時三者皆 <c>null</c>。</summary>
    public int? CoverWidth { get; init; }
    public int? CoverHeight { get; init; }
    public string? CoverAlt { get; init; }

    /// <summary>緣起與內容（區塊編輯器 JSON 字串，前台解析後渲染）。</summary>
    public string? Content { get; init; }

    /// <summary>捐助內容（文字描述）。</summary>
    public string? DonationContent { get; init; }
    public PublicCharityOrgDto? Charity { get; init; }
    public required IReadOnlyList<PublicCharityImageDto> Images { get; init; }
    public required IReadOnlyList<CharityLinkedItemDto> Partners { get; init; }
    public required IReadOnlyList<CharityLinkedItemDto> Sponsors { get; init; }
    public required IReadOnlyList<CharityArticleLinkDto> Articles { get; init; }
}

/// <summary>11.3 事蹟紀錄。三項核心資料：公益團體名稱（<see cref="CharityName"/>）、捐助內容（<see cref="DonationContent"/>）、
/// 活動圖片（<see cref="ImageUrl"/>＋<see cref="Images"/>）。</summary>
public sealed record ImpactRecordDto
{
    public required Guid Id { get; init; }
    public DateOnly? HappenedOn { get; init; }
    public string? CharityName { get; init; }
    public string? CharityLogoUrl { get; init; }
    public int? CharityLogoWidth { get; init; }
    public int? CharityLogoHeight { get; init; }
    public string? CharityLogoAlt { get; init; }
    public string? DonationContent { get; init; }
    public string? Location { get; init; }
    public string? BriefDescription { get; init; }
    public string? ImageUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }

    /// <summary>活動圖片（主圖）替代文字（當前語系，英文空白回退繁中）；沒有主圖時為 <c>null</c>。</summary>
    public string? ImageAlt { get; init; }
    public required IReadOnlyList<PublicCharityImageDto> Images { get; init; }
    public string? ProgramSlug { get; init; }
    public string? ProgramName { get; init; }
}

public sealed record ImpactMetricDto
{
    public string? Name { get; init; }
    public string? Unit { get; init; }
    public int? Value { get; init; }

    /// <summary>所屬計畫（空＝全站層級）。</summary>
    public string? ProgramSlug { get; init; }
}

/// <summary>11.4 影響力數據：後台標記為公開的統計項目（金額類預設不公開），加上系統自動彙整的
/// 合作公益團體數、累計捐助項次與服務地區。</summary>
public sealed record ImpactSummaryDto
{
    public required IReadOnlyList<ImpactMetricDto> Metrics { get; init; }
    public required int CharityCount { get; init; }

    /// <summary>累計捐助項次＝事蹟紀錄筆數。</summary>
    public required int DonationItemCount { get; init; }
    public required IReadOnlyList<string> Regions { get; init; }

    /// <summary>以團體名稱 Logo 牆／列表呈現的合作公益團體。</summary>
    public required IReadOnlyList<PublicCharityOrgDto> Charities { get; init; }
}

public sealed record CharityCtaDto
{
    /// <summary>慈善捐款平台網址（後台 B5 設定，不寫死在版型）。<c>null</c>＝尚未設定，前台不顯示球迷捐款按鈕。</summary>
    public string? DonationUrl { get; init; }

    /// <summary>球迷捐款導流文案，已依請求語系回退；中文文案必點明收受者「台灣足球策略發展協會」。</summary>
    public string? DonationCta { get; init; }
    public string? FanCta { get; init; }
}

/// <summary>
/// 慈善平台「從官網更新清單」讀取的公益團體與慈善計畫清單（K-2，2026-11-13）。
/// 🔴 只含<b>已公開</b>者的識別與名稱：沒有聯絡窗口、沒有金額、沒有任何個資。<c>Id</c>（主站主鍵）是慈善平台複本的對照鍵——
/// <c>Slug</c> 後台可以改，只給顯示與除錯用，不可當鍵。
/// </summary>
public sealed record CharityDirectoryDto
{
    public required IReadOnlyList<CharityDirectoryCharityDto> Charities { get; init; }
    public required IReadOnlyList<CharityDirectoryProgramDto> Programs { get; init; }
}

public sealed record CharityDirectoryCharityDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }

    /// <summary>目前只輸出已公開者，固定 <c>published</c>；欄位保留，讓慈善平台不必推測「清單裡有的就是公開」。</summary>
    public required string Status { get; init; }
    public required string NameZh { get; init; }

    /// <summary>英文名稱；主站沒有填就是 <c>null</c>（不以中文頂替）。</summary>
    public string? NameEn { get; init; }
}

public sealed record CharityDirectoryProgramDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }

    /// <summary>所屬公益團體的 <see cref="CharityDirectoryCharityDto.Id"/>；一定出現在同一份回應的 <c>Charities</c> 裡。</summary>
    public required Guid CharityId { get; init; }
    public required string Status { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
}
