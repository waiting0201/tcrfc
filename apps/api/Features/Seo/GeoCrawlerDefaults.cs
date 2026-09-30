namespace Tcrfc.Api.Features.Seo;

/// <summary>
/// `GEO-02`（AI 爬蟲授權，S1-12b，主站規劃書 §7）的預設值與**強制排除路徑**——單一來源，
/// 供 <c>Features/AdminSeo/AdminGeoCrawlerRepository</c>（後台讀寫）與
/// <see cref="SeoRepository"/>（公開端點，<c>apps/web</c> 的 <c>robots.txt</c> 消費）共用，
/// 避免兩處各自維護一份、日久漂移。
/// </summary>
public static class GeoCrawlerDefaults
{
    /// <summary>
    /// 後台尚未設定過（<c>settings</c> 沒有 <c>seo.crawler_agents</c> 這一列）時的預設使用者代理
    /// 清單——規劃書 §7 `GEO-02` 條文原文列的五個範例（`GPTBot`／`ClaudeBot`／`PerplexityBot`／
    /// `Google-Extended`／`CCBot`），全部預設 <c>Allowed = true</c>（「全站允許爬取」）。
    /// 這份清單只是**初次進入後台畫面時的建議值**，不是程式碼寫死的強制清單——管理員儲存後就會
    /// 換成 <c>settings</c> 裡的真實值，之後的預設值無論怎麼改都不影響已經儲存過的俱樂部。
    /// </summary>
    public static readonly IReadOnlyList<CrawlerAgentDefault> DefaultUserAgents =
    [
        new("GPTBot", true),
        new("ClaudeBot", true),
        new("PerplexityBot", true),
        new("Google-Extended", true),
        new("CCBot", true),
    ];

    /// <summary>
    /// 🔴 **站台目前啟用（或規劃中）的語系前綴**——強制排除路徑要對「所有語系版本」都生效，
    /// 不能只擋中文版留下英文版的漏洞。2026-09-25（協調者驗收退回）明確指示：**不要等
    /// `/en/` 真的上線才回頭補**，對目前還不存在的路徑輸出 <c>Disallow:</c> 沒有任何副作用
    /// （沒有頁面可以被誤擋，也不影響任何爬蟲的正常爬取），但「等上線才補」這種事後記得的模式
    /// 正是個資防線最容易漏的地方——所以現在就展開兩個語系，`/en/` 頁面尚未存在時這幾行只是
    /// robots.txt 裡「目前用不到但無害」的規則，`/en/` 頁面一旦真的上線，這裡完全不需要再改。
    /// </summary>
    private static readonly string[] Locales = ["zh", "en"];

    /// <summary>
    /// 🔴 **語系無關的相對路徑片段**——不含語系前綴，<see cref="GetMandatoryExcludePaths"/>
    /// 會依 <see cref="Locales"/> 展開成 <c>/zh/…</c>／<c>/en/…</c> 兩份。逐項路徑來源見下方
    /// <see cref="GetMandatoryExcludePaths"/> 的檔頭說明（那裡保留完整的規劃書條文與 docs 對照，
    /// 這裡只放片段本身，不重複說明）。
    /// </summary>
    private static readonly string[] LocalizedSegments =
    [
        "member/",
        "join/player/",
        "join/academy/",
        "join/camp-registration/",
        "join/international-player/",
        "join/partnership/",
        "join/media/",
        "join/general/",
        "order/lookup/",
    ];

    /// <summary>
    /// 俱樂部專屬的語系無關路徑片段——**未成年學員／球員照片所在頁面**（見下方檔頭說明）。
    /// 兩個俱樂部共用同一套前台路由（<c>apps/web/app/pages/zh/…</c>，藍鯨容器只是換
    /// <c>NUXT_PUBLIC_CLUB</c>），所以藍鯨的青年隊（U15／U12）頁面路徑與磐石學院相同，
    /// 不是另有一組藍鯨網址。
    /// - 兩隊皆有：<c>academy/teams/</c>（梯隊球員名單與照片）、<c>academy/life/</c>（訓練與比賽
    ///   影像；藍鯨目前為「肖像同意到位後公布」空狀態，但頁面就是為未成年影像而設，先擋不等
    ///   照片上架才補）。
    /// - 僅 <c>tcrfc</c>：<c>programs/childrens-training/</c>、<c>programs/summer-camp/</c>、
    ///   <c>programs/specialist/</c>——三頁皆有兒童／學員照片牆，藍鯨版目前對應區塊未渲染
    ///   （<c>isTcrfc</c> 分支），藍鯨若日後補上學員照片，須回頭在此加上（否則就把這三頁
    ///   一併放進共用清單）。
    /// </summary>
    private static readonly Dictionary<string, string[]> ClubLocalizedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tcrfc"] =
        [
            "academy/teams/",
            "academy/life/",
            "programs/childrens-training/",
            "programs/summer-camp/",
            "programs/specialist/",
        ],
        ["bw"] =
        [
            "academy/teams/",
            "academy/life/",
        ],
    };

    /// <summary>
    /// 🔴 **強制排除路徑（個資防線，不是 SEO 設定）**——docs/14-invariants.md：「這條排除是個資
    /// 防線，不是 SEO 設定，不得為了『讓 AI 多抓一點』而放寬」。這份清單**由程式碼寫死**，
    /// 不存在 <c>settings</c>，後台完全沒有任何 API 能讀到「目前的強制清單」再把它整批覆蓋掉——
    /// <c>AdminGeoCrawlerRepository</c> 的寫入方法只接受、只儲存「後台自行再加的路徑」
    /// （<c>seo.crawler_extra_exclude_paths</c>），最終輸出（<see cref="SeoRepository.GetCrawlerSettingsAsync"/>）
    /// 一律是這裡回傳的清單 ∪ 後台加的清單，程式碼結構上就不存在「移除強制路徑」這個操作。
    ///
    /// 對照規劃書 §7 `GEO-02`／docs/05-i18n-seo.md §3：會員中心、七類表單、訂單查詢、
    /// `/m/&lt;token&gt;` 會員卡驗證頁、未成年與學員照片路徑。逐項路徑來源（皆為
    /// <see cref="LocalizedSegments"/> 的語系無關片段，實際輸出依 <see cref="Locales"/> 展開成
    /// <c>/zh/…</c>／<c>/en/…</c> 兩份）：
    /// - 會員中心：docs/01-site-architecture.md §1「MEMBER 會員中心」，<c>apps/web</c> 現有路由
    ///   <c>/zh/member/</c>（<c>apps/web/app/pages/zh/member/</c>）。
    /// - 七類表單（10.1–10.7）：docs/02-frontend-spec.md §10，對應 <c>apps/web</c> 現有路由
    ///   <c>apps/web/app/pages/zh/join/{player,academy,camp-registration,international-player,
    ///   partnership,media,general}/</c>。⚠️ **不含** <c>/zh/join/</c>（單元入口頁）、
    ///   <c>/zh/join/location/</c>（Location &amp; Map 附屬頁）、<c>/zh/join/contact/</c>
    ///   （Contact Information 附屬頁）——這三頁是靜態資訊頁，不收集個資，規劃書「七類表單」
    ///   明確只指會收件的那七頁。
    /// - 訂單查詢：docs/01-site-architecture.md §5「URL 規則」，<c>/zh/order/lookup/</c>。
    /// - 會員卡驗證頁：規劃書行 1679，路徑本身固定是 <c>/m/</c>（**不含語系前綴，不展開**——
    ///   `/m/&lt;token&gt;` 本身就是站在語系目錄之外的短網址，docs/14 既有敘述如此）。
    /// - 未成年學員照片：見 <see cref="ClubLocalizedSegments"/>。<c>tcrfc</c>：<c>academy/teams/</c>
    ///   （U15／U14／U12 梯隊名單，<c>players.portrait_consent_status</c> 未同意者本來就不回傳
    ///   照片，這裡是 defense-in-depth 第二層，擋頁面而非單張圖片）、<c>academy/life/</c>、
    ///   三個課程頁。<c>bw</c>（U15／U12）：<c>academy/teams/</c>、<c>academy/life/</c>——
    ///   藍鯨與磐石共用同一套路由（2026-09-30 修正：先前誤以為藍鯨「尚未建置對應頁面」，
    ///   實際 <c>apps/web</c> 早已用同一個 <c>teams.vue</c> 渲染藍鯨青年隊名單）。
    ///
    /// ✅ **2026-09-25（協調者驗收退回）：`/en/` 版本現在就一併輸出，不留成「上線時再補」的
    /// 已知缺口**——站上目前只有 <c>/zh/</c> 頁面是既有事實，但對不存在的 <c>/en/…</c> 路徑輸出
    /// <c>Disallow:</c> 沒有任何副作用（不會誤擋任何真實頁面，也不影響任何爬蟲的正常運作），
    /// 反而是「等事後才記得補」最容易在個資防線上出漏洞的模式。見 <see cref="Locales"/> 的展開。
    /// </summary>
    public static IReadOnlyList<string> GetMandatoryExcludePaths(string clubCode)
    {
        var segments = new List<string>(LocalizedSegments);

        if (ClubLocalizedSegments.TryGetValue(clubCode, out var clubSegments))
        {
            segments.AddRange(clubSegments);
        }

        var paths = new List<string>();
        foreach (var locale in Locales)
        {
            foreach (var segment in segments)
            {
                paths.Add($"/{locale}/{segment}");
            }
        }

        paths.Add("/m/");

        return paths;
    }
}

/// <summary>單一 AI 使用者代理的允許／拒絕設定。</summary>
public sealed record CrawlerAgentDefault(string UserAgent, bool Allowed);
