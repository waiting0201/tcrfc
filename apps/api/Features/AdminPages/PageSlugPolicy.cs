using System.Text.RegularExpressions;

namespace Tcrfc.Api.Features.AdminPages;

/// <summary>
/// 頁面網址名稱（<c>pages.slug</c>）的格式驗證。
///
/// 🔴 與 <c>Features/AdminNews/SlugPolicy.cs</c>（新聞的 <c>slug</c>）刻意不同：新聞的網址是扁平的
/// <c>/news/&lt;slug&gt;/</c>，所以要擋「跟既有分類 landing 頁撞名」；B1 頁面本身**就是**
/// 網站的靜態頁面路由（docs/01-site-architecture.md「URL 直接對應網站層級」，例：
/// <c>/zh/academy/join/</c>），slug 允許多層路徑（用 <c>/</c> 分隔），沒有一份「保留字清單」
/// 可以比對——頁面管理本來就是在定義這些路由，不是在避開別人定義好的路由。
///
/// ⚠️ **已知缺口（回報，不是本次任務範圍）**：本驗證不檢查這個 slug 會不會撞到 07 新聞、
/// 08.3 商店、13 行事曆等「資料型內容」自己的路由前綴（例如把頁面的 slug 存成
/// <c>"news"</c> 或 <c>"shop/anything"</c>）。這類跨模組路由衝突偵測需要知道
/// <c>apps/web</c> 的完整路由表，而 <c>apps/web</c> 本輪由另一個 agent 同時在改、
/// 依派工指示不得觸碰，性質與 <c>SlugPolicy.cs</c> 檔頭記錄的「無法做到跨專案自動比對」
/// 是同一種缺口，留給下一輪處理。
/// </summary>
internal static class PageSlugPolicy
{
    /// <summary>
    /// 一或多個路徑片段，用單一 <c>/</c> 分隔；每個片段只能是小寫英文字母、數字與連字號，
    /// 不能以連字號開頭或結尾、不能連續兩個連字號；整個 slug 不得以 <c>/</c> 開頭或結尾、
    /// 不得出現連續 <c>//</c>。
    /// </summary>
    private static readonly Regex SlugFormat = new(
        @"^[a-z0-9]+(-[a-z0-9]+)*(/[a-z0-9]+(-[a-z0-9]+)*)*$", RegexOptions.Compiled);

    public static void Validate(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new AdminPageValidationException("網址名稱為必填欄位。");
        }

        if (!SlugFormat.IsMatch(slug))
        {
            throw new AdminPageValidationException(
                $"網址名稱「{slug}」格式不正確：只能使用小寫英文字母、數字、連字號（-）與斜線（/，用來表示分層路徑）組成，" +
                "不能以斜線或連字號開頭或結尾，也不能出現連續的斜線或連字號（例如大寫字母、空白、句點都不能出現）。" +
                "請修改後再試一次。");
        }
    }
}
