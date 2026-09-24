namespace Tcrfc.Api.Features.AdminHomeSections;

/// <summary>
/// 首頁九大區塊的固定代碼與人類看得懂的名稱（規劃書 §4.2 B3「首頁各區塊開關與排序……
/// （現行區塊：Hero、核心價值、體系導覽卡、最新賽事、近期賽事、最新消息、夥伴 Logo 牆、
/// 商店入口、底部 CTA）」，逐字對應主站規劃書 §3.1 首頁九大區塊表格的九列）。
///
/// 🔴 <c>home_sections.section_code</c> 是固定列舉，**不能透過 API 新增或刪除**——九個區塊在
/// <c>db/seed/generate-club-seed-sql.py</c> 建站時就依此清單為每個俱樂部各種一列，本輪只提供
/// 「開關／排序／指定精選輪播」的 Update，沒有 Create／Delete 端點（見 docs/14-invariants.md
/// 「後台是為了產出前台而存在的」——九個區塊是前台首頁固定的九個位置，不是使用者自訂清單）。
/// 這份清單是 C# 與種子腳本兩邊「各自維護一份同樣的九個代碼」的其中一份，比照
/// <c>Features/AdminNews/SlugPolicy.cs</c> 檔頭記錄的既有慣例（多處字面值常數，各自宣告，
/// 靠命名一致與 code review 維持同步，不是自動化比對）。
/// </summary>
public static class HomeSectionCatalog
{
    public sealed record Entry(string Code, int DefaultSortOrder, string NameZh, string NameEn);

    public static readonly IReadOnlyList<Entry> Entries =
    [
        new("hero", 0, "Hero 輪播", "Hero Carousel"),
        new("core_values", 1, "核心價值", "Core Values"),
        new("ecosystem_nav", 2, "體系導覽卡", "Ecosystem Navigation"),
        new("upcoming_match", 3, "最新賽事", "Upcoming Match"),
        new("recent_fixtures", 4, "近期賽事", "Recent Fixtures"),
        new("latest_news", 5, "最新消息", "Latest News"),
        new("partner_logos", 6, "夥伴 Logo 牆", "Partner Logo Wall"),
        new("shop_entry", 7, "商店入口", "Shop Entry"),
        new("bottom_cta", 8, "底部 CTA", "Bottom CTA"),
    ];

    private static readonly Dictionary<string, Entry> ByCode =
        Entries.ToDictionary(e => e.Code, StringComparer.Ordinal);

    public static Entry? Find(string code) => ByCode.GetValueOrDefault(code);
}
