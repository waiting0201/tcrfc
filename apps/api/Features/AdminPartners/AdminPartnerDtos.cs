namespace Tcrfc.Api.Features.AdminPartners;

/// <summary>E1 夥伴單一語系內容（<c>partners_i18n</c>）。<c>Name</c> 必填（中文），<c>Content</c> 是「合作內容」。</summary>
public sealed record AdminPartnerLocaleContent
{
    public required string Name { get; init; }
    public string? Content { get; init; }

    /// <summary>標誌替代文字（§4.0 圖片欄位組，逐語系；深色／淺色兩版是同一個標誌，共用這一欄）。對應 <c>partners_i18n.logo_alt</c>。</summary>
    public string? LogoAlt { get; init; }
}

public sealed record AdminPartnerContentInput
{
    public required AdminPartnerLocaleContent Zh { get; init; }

    /// <summary>省略（null）＝沒有英文版；更新時省略會移除既有英文版（比照 C3 教練）。</summary>
    public AdminPartnerLocaleContent? En { get; init; }
}

public sealed record AdminPartnerListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? PartnerType { get; init; }
    public string? Country { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public string? WebsiteUrl { get; init; }
    public required bool ShowInFooter { get; init; }
    public required bool ShowOnHome { get; init; }
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

    /// <summary>合作期間是否涵蓋今天（沒填起訖＝視為進行中）。</summary>
    public required bool IsActive { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminPartnerDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? PartnerType { get; init; }
    public string? Country { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public string? WebsiteUrl { get; init; }
    public required bool ShowInFooter { get; init; }
    public required bool ShowOnHome { get; init; }
    public required int SortOrder { get; init; }
    public string? LogoDarkKey { get; init; }
    public string? LogoDarkUrl { get; init; }
    public string? LogoLightKey { get; init; }
    public string? LogoLightUrl { get; init; }
    public int? LogoDarkWidth { get; init; }
    public int? LogoDarkHeight { get; init; }
    public int? LogoLightWidth { get; init; }
    public int? LogoLightHeight { get; init; }
    public required AdminPartnerLocaleContent Zh { get; init; }
    public AdminPartnerLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>建立與更新共用的請求（檔案欄位 <c>logoDark</c>／<c>logoLight</c> 在 multipart 的檔案欄位）。</summary>
public sealed record UpsertAdminPartnerRequest
{
    /// <summary>省略＝自動產生（英文名稱轉出，沒有英文名稱就用隨機字串）。更新時省略＝維持不變。</summary>
    public string? Slug { get; init; }

    public string? PartnerType { get; init; }
    public string? Country { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public string? WebsiteUrl { get; init; }
    public bool ShowInFooter { get; init; }
    public bool ShowOnHome { get; init; }
    public int SortOrder { get; init; }
    public required AdminPartnerContentInput Content { get; init; }

    /// <summary>true＝移除目前的深色底版本 Logo（不可同時夾帶新檔案）。</summary>
    public bool RemoveLogoDark { get; init; }
    public bool RemoveLogoLight { get; init; }
}

public sealed record AdminPartnerTypesDto
{
    /// <summary>規劃書 §3.9 9.1 的五種標準類型。</summary>
    public required IReadOnlyList<string> StandardTypes { get; init; }

    /// <summary>這個俱樂部資料裡實際用過的類型（可能含標準類型以外的值，例如藍鯨的「指導單位」）。</summary>
    public required IReadOnlyList<string> UsedTypes { get; init; }
}
