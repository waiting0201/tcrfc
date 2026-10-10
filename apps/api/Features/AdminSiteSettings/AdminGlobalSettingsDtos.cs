namespace Tcrfc.Api.Features.AdminSiteSettings;

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

/// <summary>I3 全域設定：Cookie 政策、隱私權政策、會員條款、維護模式（規劃書 §4.9）。</summary>
public sealed record AdminGlobalSettingsDto
{
    public required IReadOnlyList<AdminPolicySettingDto> Policies { get; init; }

    public required AdminMaintenanceSettingsDto Maintenance { get; init; }

    /// <summary>目前生效的隱私權政策版本編號（未設定時為預設值 <c>1.0</c>）。前台送出報名／詢問時，伺服器把它連同同意時間留存（v3.25）；
    /// 改了政策內文就該同步改這個編號，之後的送出才會記到新版本。</summary>
    public required string PrivacyPolicyVersion { get; init; }
}

public sealed record AdminPolicyInput
{
    public string? BodyZh { get; init; }

    public string? BodyEn { get; init; }
}

/// <summary>整份取代。純 JSON 請求（v3.20 起沒有 Logo／Favicon／品牌色，也就沒有檔案欄位）。</summary>
public sealed record UpdateAdminGlobalSettingsRequest
{
    public AdminPolicyInput? CookiePolicy { get; init; }

    public AdminPolicyInput? PrivacyPolicy { get; init; }

    public AdminPolicyInput? MemberTerms { get; init; }

    /// <summary>隱私權政策版本編號（≤ 50 字）。<b>省略（null）＝維持不變</b>，不會因為舊版前端沒送就被清掉；空字串＝清除、回到預設值。</summary>
    public string? PrivacyPolicyVersion { get; init; }

    public bool MaintenanceEnabled { get; init; }

    public string? MaintenanceMessageZh { get; init; }

    public string? MaintenanceMessageEn { get; init; }
}
