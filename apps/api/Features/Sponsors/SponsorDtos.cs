using Tcrfc.Api.Features.Partners;

namespace Tcrfc.Api.Features.Sponsors;

public sealed record SponsorActivationImageDto
{
    public required string ImageUrl { get; init; }
    public string? ThumbUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
}

/// <summary>贊助活動紀錄（前台 9.2）。</summary>
public sealed record SponsorActivationDto
{
    public required Guid Id { get; init; }
    public string? Title { get; init; }
    public DateOnly? HappenedOn { get; init; }

    /// <summary>成效摘要。</summary>
    public string? ResultSummary { get; init; }
    public required IReadOnlyList<SponsorActivationImageDto> Images { get; init; }
}

/// <summary>贊助故事（案例文章）。只列已發布的文章；文章詳情走 07 新聞端點。</summary>
public sealed record SponsorStoryDto
{
    public required string Slug { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
}

/// <summary>09.2 贊助商。**不輸出**聯絡窗口、合約日期與到期提醒（商務內部資料）。合約已結束的贊助商不會列出。</summary>
public sealed record SponsorDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }

    /// <summary>未翻譯標示（App 規劃書 §2.5）：請求英文而英文名稱是空的，回應是回退的繁中時為 true；請求繁中恆為 false。</summary>
    public required bool IsFallbackLocale { get; init; }

    /// <summary>主贊助／官方贊助／支持夥伴。</summary>
    public string? Tier { get; init; }
    public required int SortOrder { get; init; }
    public string? Name { get; init; }

    /// <summary>贊助內容。</summary>
    public string? Content { get; init; }
    public string? LogoDarkUrl { get; init; }
    public string? LogoLightUrl { get; init; }
    public required IReadOnlyList<SponsorStoryDto> Stories { get; init; }
    public required IReadOnlyList<SponsorActivationDto> Activations { get; init; }

    /// <summary>共同參與的公益計畫（已發布）。</summary>
    public required IReadOnlyList<PartnerCharityProgramDto> CharityPrograms { get; init; }
}

/// <summary>9.4 贊助方案卡片。價格區間只有後台設為公開（<c>is_price_public</c>）時才輸出，否則兩欄皆為 <c>null</c>。</summary>
public sealed record SponsorPackageDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }

    /// <summary>未翻譯標示（App 規劃書 §2.5）：請求英文而英文名稱是空的，回應是回退的繁中時為 true；請求繁中恆為 false。</summary>
    public required bool IsFallbackLocale { get; init; }
    public required int SortOrder { get; init; }
    public string? Name { get; init; }
    public string? Content { get; init; }

    /// <summary>權益清單（純文字，一行一項）。</summary>
    public string? BenefitList { get; init; }
    public string? Audience { get; init; }
    public int? PriceMin { get; init; }
    public int? PriceMax { get; init; }
}
