using System.Text.RegularExpressions;

namespace Tcrfc.Api.Features.AdminNews;

/// <summary>
/// 網址名稱（<c>articles.slug</c>）的格式與保留字驗證，建立與更新兩條寫入路徑共用。
///
/// 🔴🔴🔴 <see cref="ReservedSlugs"/> 這份清單的**真實來源其實不在這個檔案裡**，而是
/// <c>apps/web/app/pages/zh/news/*.vue</c> 的路由檔名——07 新聞單元每一個分類 landing 頁
/// 都是一個固定檔名，那個檔名就是它在 <c>/zh/news/{檔名}/</c> 這個網址片段。一篇文章的
/// 網址名稱如果（不分大小寫）剛好等於其中一個，前台扁平路由 <c>/zh/news/{slug}/</c> 就會跟
/// 分類頁撞在一起，使用者點進去的不是文章而是分類清單。
///
/// ⚠️ **這是手動抄的一份，不是自動同步**，因此存在 <c>docs/18-work-errors.md</c> E-36 那一類
/// 「共用真實來源分裂成兩份」的漂移風險：日後 <c>apps/web</c> 只要新增一個 07 單元的分類頁
/// （或替既有分類頁改檔名），這份清單就會悄悄過期——而且過期的方向永遠是「漏擋」，不會有任何
/// 編譯錯誤、測試失敗或執行期例外提醒維護者去補這一份。
///
/// **本次任務邊界內能做的處理，只到這裡為止**：把清單集中在單一檔案、把來源路徑寫死在註解裡，
/// 讓下一個改 <c>apps/web</c> 路由的人至少有機會用「07」「news」「reserved」之類的關鍵字搜到
/// 這個檔案。⛔ **沒有**做到跨專案自動比對（例如讓 CI 讀 <c>apps/web</c> 的實際檔名清單去驗證
/// 這份清單是否過期）——一來 <c>apps/web</c> 本輪由另一個 agent 在改，不得觸碰；二來這需要新增
/// 一種「A 專案的檔案異動觸發 B 專案檢查」的建置步驟，<c>docs/20-cicd.md</c> 目前沒有這種機制，
/// 屬於「需要跨專案改動才能真正解決」的情況，依任務邊界只回報建議、不動手發明。
///
/// **回報給下一位／使用者的建議**（真正根治漂移的做法，其中一種）：
/// 1. 把 9 個分類代碼與其路由片段抽成一份兩邊都讀的共用資料（例如 repo 根目錄
///    <c>content/news-category-slugs.json</c>，或直接讀資料庫既有的 <c>article_categories.code</c>——
///    這 9 個路由片段本來就跟 <c>article_categories.code</c> 的 7.1–7.8 分類代碼同名，是本來就
///    存在的同一份事實，此檔目前重複硬編碼了一份而不是查表，也是可以再收斂的地方；下方
///    <c>ReservedSlugs</c> 的排序刻意跟 <c>db/club-schema.sql</c> 種子的 <c>article_categories</c>
///    插入順序一致，方便日後核對）。
/// 2. 或在 CI 加一道檢查：<c>apps/web</c> 的 07 單元路由檔名異動時，比對這個檔案的清單是否同步，
///    不一致就讓 CI 失敗。
/// 兩者都需要同時改 <c>apps/web</c>（或新增建置管線），本次任務邊界不允許，故只記錄不動手。
/// </summary>
internal static class SlugPolicy
{
    /// <summary>
    /// 07 新聞單元既有的 9 個分類 landing 頁路由片段。
    /// 來源盤點（2026-09-22）：
    /// <c>apps/web/app/pages/zh/news/{club,match,academy,player-stories,international,
    /// camps-events,community,media,article}.vue</c>。
    /// ⛔ 不含 <c>index.vue</c>——那是新聞總覽頁本身的網址（<c>/zh/news/</c>），
    /// 不是 <c>{slug}</c> 這個路由片段可能填入的值，跟單篇文章的網址名稱不會相撞。
    /// </summary>
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "club",
        "match",
        "academy",
        "player-stories",
        "international",
        "camps-events",
        "community",
        "media",
        "article",
    };

    /// <summary>
    /// 只允許小寫英文字母、數字，以及用來分隔詞組的單一連字號；不能開頭／結尾是連字號，
    /// 也不能連續出現兩個以上連字號。這條規則本身就同時擋掉大小寫變形（保留字比對前就先擋掉
    /// 大寫）、斜線、句點、空白——不需要為每一種危險形狀各寫一條規則。
    /// </summary>
    private static readonly Regex SlugFormat = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>
    /// 驗證一個網址名稱是否可用。不合格一律丟 <see cref="AdminArticleValidationException"/>（400），
    /// 訊息用日常中文，說明「哪個欄位、為什麼不行、可以怎麼改」（docs/06 §1 用語對照表：
    /// <c>slug</c> 的介面說法是「網址名稱」，訊息裡不出現英文技術詞）。
    /// </summary>
    public static void Validate(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new AdminArticleValidationException("網址名稱為必填欄位。");
        }

        if (!SlugFormat.IsMatch(slug))
        {
            throw new AdminArticleValidationException(
                $"網址名稱「{slug}」格式不正確：只能使用小寫英文字母、數字與連字號（-）組成，" +
                "開頭與結尾不能是連字號，也不能連續出現兩個連字號（例如大寫字母、空白、斜線、句點都不能出現）。" +
                "請修改後再試一次。");
        }

        if (slug.All(char.IsAsciiDigit))
        {
            throw new AdminArticleValidationException(
                $"網址名稱「{slug}」不能整段只有數字，請加入能代表文章內容的文字（例如日期或分類關鍵字），" +
                "避免之後跟其他以數字排序或分頁用途的網址搞混。");
        }

        if (ReservedSlugs.Contains(slug))
        {
            throw new AdminArticleValidationException(
                $"網址名稱「{slug}」是系統保留給分類頁面使用的名稱，這篇文章不能使用這個名稱，" +
                "請換一個能代表這篇文章內容的網址名稱（例如加上日期或關鍵字，像現有文章慣用的「2024-12-18-club-079」這種寫法）。");
        }
    }
}
