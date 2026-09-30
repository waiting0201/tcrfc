namespace Tcrfc.Api.Features.Partners;

/// <summary>公開的「共同參與的公益計畫」連結（規劃書 §3.11：慈善計畫可標記贊助夥伴，於夥伴頁顯示）。
/// 只含已發布的計畫；詳情頁在 11.2 慈善計畫（<c>/api/v1/{club}/charity/programs/{slug}</c>）。</summary>
public sealed record PartnerCharityProgramDto
{
    public required string Slug { get; init; }
    public string? Name { get; init; }
}

/// <summary>09.1 合作夥伴。合作期間涵蓋今天（沒填起訖視為進行中）的夥伴才會列出。
/// Logo 提供深底／淺底兩版完整網址（沒有上傳的版本為 <c>null</c>，前台自行回退另一版）。</summary>
public sealed record PartnerDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }

    /// <summary>夥伴類型顯示文字（策略夥伴／國際夥伴／訓練夥伴／教育夥伴／品牌夥伴，或該俱樂部自訂的類型）。</summary>
    public string? PartnerType { get; init; }
    public string? Country { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public string? WebsiteUrl { get; init; }
    public required bool ShowInFooter { get; init; }
    public required bool ShowOnHome { get; init; }
    public required int SortOrder { get; init; }
    public string? Name { get; init; }

    /// <summary>合作內容。</summary>
    public string? Content { get; init; }
    public string? LogoDarkUrl { get; init; }
    public string? LogoLightUrl { get; init; }
    public required IReadOnlyList<PartnerCharityProgramDto> CharityPrograms { get; init; }
}
