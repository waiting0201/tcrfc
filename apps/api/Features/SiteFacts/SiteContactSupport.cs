using System.Text.Json;

namespace Tcrfc.Api.Features.SiteFacts;

/// <summary>
/// 各部門窗口的儲存形狀（<c>site.contact_departments</c> 的 <c>setting_value</c>，JSON 陣列，camelCase）。
/// 部門名稱是人類語言，但窗口清單是一個整體（順序、筆數同進退），因此整份存成單一 JSON 值而不是拆成多個
/// 逐語系鍵——沿用既有 json setting 慣例（<c>AdminShop</c> 排除地區、<c>AdminCalendar</c> 主場球隊）。
/// 後台寫入與公開讀取共用，避免兩邊各寫一份解析。
/// </summary>
internal sealed record StoredDepartment(string NameZh, string? NameEn, string? Email, string? PhoneExtension);

internal static class SiteContactSupport
{
    internal const string KeyContactEmail = "site.contact_email";
    internal const string KeyDepartments = "site.contact_departments";
    internal const string KeyFooterBlurb = "site.footer_blurb";
    internal const string KeySocialFacebook = "site.social_facebook";
    internal const string KeySocialInstagram = "site.social_instagram";
    internal const string KeySocialYoutube = "site.social_youtube";
    internal const string KeySocialLine = "site.social_line";

    internal static readonly string[] Keys =
    [
        KeyContactEmail, KeyDepartments, KeyFooterBlurb,
        KeySocialFacebook, KeySocialInstagram, KeySocialYoutube, KeySocialLine,
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static string? Serialize(IReadOnlyList<StoredDepartment> items)
        => items.Count == 0 ? null : JsonSerializer.Serialize(items, Json);

    /// <summary>壞掉的 JSON 一律視為沒有資料，不讓公開頁面因單一設定值損毀而 500。</summary>
    internal static IReadOnlyList<StoredDepartment> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<List<StoredDepartment>>(value, Json) ?? [])
                .Where(d => !string.IsNullOrWhiteSpace(d.NameZh))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
