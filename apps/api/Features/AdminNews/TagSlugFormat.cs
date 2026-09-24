using System.Text.RegularExpressions;

namespace Tcrfc.Api.Features.AdminNews;

/// <summary>
/// 標籤網址名稱（<c>tags.slug</c>）的格式驗證（S1-5 新增）。
///
/// 🔴 跟 <see cref="SlugPolicy"/>（文章網址名稱）刻意分開、不共用同一份規則：
/// 文章網址名稱要擋「跟 07 單元分類 landing 頁撞名」這件事，標籤沒有對應的路由頁面（規劃書
/// B2 只講「標籤」，前台 07 單元的篩選目前是查詢參數 <c>?tag=</c>，不是獨立路由），沒有保留字
/// 需要擋。格式規則本身沿用同一條 kebab-case 正規表示式，是因為標籤 slug 之後很可能被用在
/// 同一種查詢參數的位置，維持 URL 安全與大小寫一致的形狀是合理的最低要求，不是抄錯檔案。
/// </summary>
internal static class TagSlugFormat
{
    private static readonly Regex Format = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>驗證不合格一律丟 <see cref="AdminArticleValidationException"/>（400）。</summary>
    public static void Validate(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new AdminArticleValidationException("標籤的網址名稱為必填欄位。");
        }

        if (!Format.IsMatch(slug))
        {
            throw new AdminArticleValidationException(
                $"標籤的網址名稱「{slug}」格式不正確：只能使用小寫英文字母、數字與連字號（-）組成，" +
                "開頭與結尾不能是連字號，也不能連續出現兩個連字號，請修改後再試一次。");
        }
    }
}
