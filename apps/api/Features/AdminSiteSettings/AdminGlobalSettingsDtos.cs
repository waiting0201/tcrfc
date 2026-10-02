namespace Tcrfc.Api.Features.AdminSiteSettings;

public sealed record AdminBrandSettingsDto
{
    public string? LogoLightUrl { get; init; }

    public string? LogoDarkUrl { get; init; }

    public string? FaviconUrl { get; init; }

    /// <summary><c>#RRGGBB</c>，沒設定為 <c>null</c>。</summary>
    public string? BrandColor { get; init; }

    public string? BrandSecondaryColor { get; init; }
}

public sealed record AdminPolicySettingDto
{
    /// <summary>URL 片段：<c>cookie</c>／<c>privacy</c>／<c>member-terms</c>。</summary>
    public required string Code { get; init; }

    public required string Title { get; init; }

    /// <summary>純文字內文（空行分段）。</summary>
    public string? BodyZh { get; init; }

    public string? BodyEn { get; init; }

    public DateTime? UpdatedAt { get; init; }
}

public sealed record AdminMaintenanceSettingsDto
{
    public required bool Enabled { get; init; }

    public string? MessageZh { get; init; }

    public string? MessageEn { get; init; }

    public DateTime? UpdatedAt { get; init; }
}

/// <summary>I3 全域設定：Logo、品牌色、Favicon、Cookie 政策、隱私權政策、會員條款、維護模式（規劃書 §4.9）。</summary>
public sealed record AdminGlobalSettingsDto
{
    public required AdminBrandSettingsDto Brand { get; init; }

    public required IReadOnlyList<AdminPolicySettingDto> Policies { get; init; }

    public required AdminMaintenanceSettingsDto Maintenance { get; init; }
}

public sealed record AdminPolicyInput
{
    public string? BodyZh { get; init; }

    public string? BodyEn { get; init; }
}

/// <summary>整份取代（圖片三格例外：不帶檔案且未勾選移除＝維持原圖）。以 <c>multipart/form-data</c> 送出：<c>payload</c>（本型別的 JSON）＋選填檔案欄位
/// <c>logoLight</c>／<c>logoDark</c>／<c>favicon</c>。</summary>
public sealed record UpdateAdminGlobalSettingsRequest
{
    public string? BrandColor { get; init; }

    public string? BrandSecondaryColor { get; init; }

    public bool RemoveLogoLight { get; init; }

    public bool RemoveLogoDark { get; init; }

    public bool RemoveFavicon { get; init; }

    public AdminPolicyInput? CookiePolicy { get; init; }

    public AdminPolicyInput? PrivacyPolicy { get; init; }

    public AdminPolicyInput? MemberTerms { get; init; }

    public bool MaintenanceEnabled { get; init; }

    public string? MaintenanceMessageZh { get; init; }

    public string? MaintenanceMessageEn { get; init; }
}
