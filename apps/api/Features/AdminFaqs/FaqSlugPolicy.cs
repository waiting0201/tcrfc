using System.Text.RegularExpressions;

namespace Tcrfc.Api.Features.AdminFaqs;

/// <summary>
/// 網址名稱格式驗證，<c>faq_categories.slug</c>／<c>faqs.slug</c> 共用同一套規則
/// （比照 <c>Features/AdminNews/SlugPolicy.cs</c> 的格式規則本體）。
///
/// ⚠️ 沒有搬 <c>SlugPolicy</c> 的保留字清單過來：07 新聞單元的保留字是因為前台走「扁平路由」
/// （<c>/zh/news/{slug}/</c> 跟分類 landing 頁共用同一層路徑，兩者會相撞）。12 FAQ 單元的前台
/// （規劃書 3.12）目前**尚未建置**（`apps/web` 沒有任何 <c>/zh/faq/*</c> 路由，見 STATUS.md
/// S1-18，本輪任務邊界是 <c>apps/api</c>），沒有既有路由可以核對會不會撞名，因此沒有保留字清單
/// 可抄——等前台實際定案路由結構後再補（跟 <c>SlugPolicy</c> 檔頭記錄的漂移風險是同一種情況，
/// 這裡是「還沒有東西可以比對」而不是「比對了但沒抄全」）。
/// </summary>
internal static class FaqSlugPolicy
{
    private static readonly Regex SlugFormat = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>共用給 <c>faqs.slug</c> 與 <c>faq_categories.slug</c>，不合格丟
    /// <see cref="AdminFaqValidationException"/>（400）。<paramref name="fieldLabel"/>
    /// 是給錯誤訊息用的欄位說法（例如「網址名稱」），日常中文、不出現英文技術詞。</summary>
    public static void Validate(string slug, string fieldLabel = "網址名稱")
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new AdminFaqValidationException($"{fieldLabel}為必填欄位。");
        }

        if (!SlugFormat.IsMatch(slug))
        {
            throw new AdminFaqValidationException(
                $"{fieldLabel}「{slug}」格式不正確：只能使用小寫英文字母、數字與連字號（-）組成，" +
                "開頭與結尾不能是連字號，也不能連續出現兩個連字號（例如大寫字母、空白、斜線、句點都不能出現）。" +
                "請修改後再試一次。");
        }
    }
}
