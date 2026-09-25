namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// `GEO-01`（<c>llms.txt</c> 維護，S1-12a，主站規劃書 §7／§4.8）——後台讀寫的五個自由文字區塊。
/// **不新增資料型別**（docs/14-invariants.md「不新增資料型別，設定存 <c>Setting</c>」），沿用
/// <c>settings</c>／<c>settings_i18n</c>，比照既有 <see cref="AdminSeoSettingsDto"/> 同一套
/// 「zh／en 雙欄位」慣例（CLAUDE.md 全域規定 4）。
///
/// 🔴 **五個欄位皆可為空（本輪判斷，規劃書沒有指定必填）**：跟 <c>seo.title_template</c>／
/// <c>seo.default_description</c>（會直接決定頁面 <c>&lt;title&gt;</c>，留空會讓前台顯示空白）
/// 不同，這五個欄位空白時前台（<c>apps/web</c> 的 <c>llms.txt</c>／<c>llms-en.txt</c> 路由）
/// 有安全的內建預設文字可以回退（沿用骨架階段既有的固定文案），管理員可以先不填、之後再逐步補齊，
/// 不會因為「還沒填」而讓 <c>/llms.txt</c> 輸出壞掉或消失。CLAUDE.md 全域規定 4「英文可空但欄位
/// 必須存在」在這裡的落實方式是：五個欄位各自都有 <c>Zh</c>／<c>En</c> 兩個屬性，不是省略英文欄位。
///
/// 「代表頁清單」刻意設計成**管理員自行維護的自由文字**（不是自動從 <c>getEnabledSiteUnits()</c>
/// 算出來的清單）——規劃書 `GEO-01` 明文「由後台 <c>H</c> 模組維護」，若改成後端自動產生，就不是
/// 「後台維護」而是「開發者寫死在程式碼」，管理員也就沒有實際可編輯的東西。管理員可以直接用
/// Markdown 清單語法撰寫（例如 <c>- [關於我們](/zh/about/)</c>），前台原樣輸出，不額外解析。
/// </summary>
public sealed record AdminLlmsContentDto
{
    /// <summary>站點定位——一到兩句話說明這個站是誰、做什麼。</summary>
    public string? PositioningZh { get; init; }
    public string? PositioningEn { get; init; }

    /// <summary>代表頁清單——管理員自行撰寫的頁面清單文字（建議 Markdown 清單語法）。</summary>
    public string? KeyPagesZh { get; init; }
    public string? KeyPagesEn { get; init; }

    /// <summary>事實摘要——成立年份、主場、聯賽等重要事實的濃縮摘要（`GEO-03`／`GEO-04` 事實
    /// 應與前台明文一致，不得矛盾，但本欄位本身不是「唯一來源」，只是給 AI 系統的摘要引用）。</summary>
    public string? FactsSummaryZh { get; init; }
    public string? FactsSummaryEn { get; init; }

    /// <summary>授權與引用方式。</summary>
    public string? LicenseZh { get; init; }
    public string? LicenseEn { get; init; }

    /// <summary>聯絡窗口。</summary>
    public string? ContactZh { get; init; }
    public string? ContactEn { get; init; }
}

/// <summary>更新 <c>llms.txt</c> 內容的請求，形狀與 <see cref="AdminLlmsContentDto"/> 一致
/// （純文字，沒有圖片欄位，不需要 <c>multipart/form-data</c>）。整份送出語意比照
/// <see cref="AdminSeoSettingsDto"/> 檔頭——省略某個 <c>*En</c> 欄位＝清空既有英文值，不是
/// 「維持不變」。</summary>
public sealed record UpdateLlmsContentRequest
{
    public string? PositioningZh { get; init; }
    public string? PositioningEn { get; init; }
    public string? KeyPagesZh { get; init; }
    public string? KeyPagesEn { get; init; }
    public string? FactsSummaryZh { get; init; }
    public string? FactsSummaryEn { get; init; }
    public string? LicenseZh { get; init; }
    public string? LicenseEn { get; init; }
    public string? ContactZh { get; init; }
    public string? ContactEn { get; init; }
}
