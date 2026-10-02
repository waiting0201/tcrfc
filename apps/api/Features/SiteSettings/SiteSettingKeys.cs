namespace Tcrfc.Api.Features.SiteSettings;

/// <summary>
/// I 網站設定（規劃書 §4.9）寫進 <c>settings</c>／<c>settings_i18n</c> 的鍵詞彙，<b>後台寫入端與前台讀取端共用同一份</b>（改鍵名只改這裡）。
/// 命名慣例 <c>&lt;setting_group&gt;.&lt;name&gt;</c>，同 <c>site.*</c>（<c>AdminSiteFactsRepository</c>）與 <c>seo.*</c>。
/// 全部是「每俱樂部一份」（<c>settings.club_id</c> 必填）。
/// </summary>
public static class SiteSettingKeys
{
    // ── 政策頁（全域設定）：內文存 settings_i18n.value，純文字 ──────────────────────────────
    public const string GroupPolicy = "policy";
    public const string PolicyCookie = "policy.cookie";
    public const string PolicyPrivacy = "policy.privacy";
    public const string PolicyMemberTerms = "policy.member_terms";

    // ── 維護模式（全域設定）─────────────────────────────────────────────────────────────
    public const string GroupMaintenance = "maintenance";
    /// <summary>單一值：<c>"1"</c>＝開啟、其餘（含沒有這個設定列）＝關閉。</summary>
    public const string MaintenanceEnabled = "maintenance.enabled";
    public const string MaintenanceMessage = "maintenance.message";

    // ── 多語系管理 ──────────────────────────────────────────────────────────────────────
    public const string GroupI18n = "i18n";
    /// <summary>單一值：<c>show_default</c>（未翻譯時顯示繁中，預設）／<c>hide</c>（隱藏該頁）。</summary>
    public const string I18nFallbackMode = "i18n.fallback_mode";
    /// <summary>逐語系：日期格式樣式，例 <c>YYYY/MM/DD</c>、<c>MMM D, YYYY</c>。</summary>
    public const string I18nDateFormat = "i18n.date_format";
    /// <summary>逐語系：數字格式範例字串，例 <c>1,234.56</c>、<c>1.234,56</c>、<c>1 234,56</c>。</summary>
    public const string I18nNumberFormat = "i18n.number_format";

    // ── EDM 平台設定 ────────────────────────────────────────────────────────────────────
    public const string GroupEdm = "edm";
    public const string EdmEnabled = "edm.enabled";
    public const string EdmProvider = "edm.provider";
    public const string EdmListId = "edm.list_id";
    public const string EdmSenderEmail = "edm.sender_email";
    /// <summary>API 金鑰，Data Protection 加密後存放；<b>只寫不讀</b>（任何端點都不回傳，也不回傳部分字元）。</summary>
    public const string EdmApiKeyEncrypted = "edm.api_key_encrypted";

    public const string FallbackShowDefault = "show_default";
    public const string FallbackHide = "hide";

    /// <summary>政策頁代碼（URL 片段）→ 設定鍵與顯示名稱。順序即畫面顯示順序。</summary>
    public static readonly IReadOnlyList<PolicyDefinition> Policies =
    [
        new("cookie", PolicyCookie, "Cookie 政策", "Cookie Policy"),
        new("privacy", PolicyPrivacy, "隱私權政策", "Privacy Policy"),
        new("member-terms", PolicyMemberTerms, "會員條款", "Membership Terms"),
    ];

    public sealed record PolicyDefinition(string Code, string SettingKey, string TitleZh, string TitleEn);
}
