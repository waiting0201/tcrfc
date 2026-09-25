namespace Tcrfc.Api.Features.Forms;

/// <summary>
/// 9 個固定 `form_code` 目錄（7 類表單 ＋ 提案下載 ＋ 捐助洽詢，主站規劃書 §3.10／§4.7 G2，
/// docs/12-database-schema.md §4.6）。**表單本身是固定目錄，不是可由後台自由新增的資料**——
/// G1「表單設計器」只能編輯既有 9 筆 <c>forms</c> 的設定與底下的動態欄位（<c>form_fields</c>），
/// 沒有「新增一種表單類型」的端點，見 <c>Features/AdminForms/AdminFormsEndpoints.cs</c> 檔頭。
///
/// 🔴 **代碼字串本身是本輪（S1-10）判斷**：規劃書 §3.10 只用中文標題列出這 9 種，未定義程式用
/// 代碼，見 docs/12-database-schema.md §4.6 附註「Form.form_code 九碼目錄拍板」的完整說明。
/// `db/seed/generate-club-seed-sql.py` 各自宣告一份同樣的九個代碼字面值（既有慣例，見該檔
/// `HOME_SECTIONS` 段的檔頭說明：「C# 與本腳本各自宣告一份同樣的代碼，靠命名一致與 code review
/// 維持同步，不是自動化比對」）——改這裡的字串一定要同步改種子腳本，否則種子資料的
/// `form_code` 會跟這裡對不上，导致 G2 依類別過濾的查詢永遠找不到對應的表單。
///
/// ⚠️ `DonationEnquiry`（捐助洽詢）**規劃書全文未曾定義這個表單的實際欄位**——只在 G2 收件匣
/// 分頁清單（行 1163）與 `Enquiry` 型別說明兩處被提及，見 `apps/api/README.md`「S1-10」段
/// 「規劃書沒寫清楚、本輪自行判斷的地方」。
/// </summary>
public static class FormCatalog
{
    public const string JoinPlayer = "join_player"; // 10.1 加入球隊
    public const string AcademyChildrenTraining = "academy_children_training"; // 10.2 加入學院／兒童訓練
    public const string CampRegistration = "camp_registration"; // 10.3 營隊報名
    public const string InternationalPlayerEnquiry = "international_player_enquiry"; // 10.4 國際球員詢問
    public const string PartnershipSponsorship = "partnership_sponsorship"; // 10.5 合作夥伴與贊助洽詢
    public const string MediaEnquiry = "media_enquiry"; // 10.6 媒體詢問
    public const string GeneralContact = "general_contact"; // 10.7 一般聯絡
    public const string ProposalDownload = "proposal_download"; // 9.4 CTA 提案簡介下載
    public const string DonationEnquiry = "donation_enquiry"; // 捐助洽詢（規劃書未定義欄位，見上方說明）

    /// <summary>依規劃書 §3.10 表格順序（10.1–10.7），再接提案下載與捐助洽詢。</summary>
    public static readonly IReadOnlyList<string> AllCodes =
    [
        JoinPlayer, AcademyChildrenTraining, CampRegistration, InternationalPlayerEnquiry,
        PartnershipSponsorship, MediaEnquiry, GeneralContact, ProposalDownload, DonationEnquiry,
    ];

    /// <summary>學院／課程管理角色（矩陣「課程類詢問」）能看到的 <c>form_code</c> 集合。</summary>
    public static readonly IReadOnlySet<string> CourseCategoryCodes =
        new HashSet<string>(StringComparer.Ordinal) { AcademyChildrenTraining, CampRegistration };

    /// <summary>商務／贊助角色（矩陣「合作／贊助類詢問」）能看到的 <c>form_code</c> 集合。
    /// 🔴 <see cref="ProposalDownload"/> 併入本類是本輪判斷（提案下載本質是贊助洽詢的前導動作，
    /// 規劃書未明文歸類，見任務回報），不是規劃書逐字要求。</summary>
    public static readonly IReadOnlySet<string> PartnershipCategoryCodes =
        new HashSet<string>(StringComparer.Ordinal) { PartnershipSponsorship, ProposalDownload };

    /// <summary>公關／媒體角色（矩陣「媒體類詢問」）能看到的 <c>form_code</c> 集合。</summary>
    public static readonly IReadOnlySet<string> MediaCategoryCodes =
        new HashSet<string>(StringComparer.Ordinal) { MediaEnquiry };

    public static bool IsKnownCode(string formCode) => AllCodes.Contains(formCode, StringComparer.Ordinal);

    /// <summary>顯示名稱對照——**不是資料庫欄位**，純粹是規劃書 §3.10 固定表格的中英名稱字面值，
    /// 供 CSV 匯出（docs/14-invariants.md「CSV 匯出的欄位標題同此規則」，不得顯示英文技術代碼）與
    /// 後台清單 API 的便利欄位使用，不違反 2026-09-22「不建 forms_i18n.name」的拍板——那條約束的是
    /// **資料庫儲存**，不是「程式碼裡能不能有一個固定字典」。</summary>
    private static readonly IReadOnlyDictionary<string, (string Zh, string En)> DisplayNames =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            [JoinPlayer] = ("加入球隊", "Join as a Player"),
            [AcademyChildrenTraining] = ("加入學院／兒童訓練", "Academy & Children's Training"),
            [CampRegistration] = ("營隊報名", "Camp Registration"),
            [InternationalPlayerEnquiry] = ("國際球員詢問", "International Player Enquiries"),
            [PartnershipSponsorship] = ("合作夥伴與贊助洽詢", "Partnership & Sponsorship"),
            [MediaEnquiry] = ("媒體詢問", "Media Enquiries"),
            [GeneralContact] = ("一般聯絡", "General Contact"),
            [ProposalDownload] = ("提案簡介下載", "Sponsorship Deck Download"),
            [DonationEnquiry] = ("捐助洽詢", "Donation Enquiry"),
        };

    public static string DisplayNameZh(string formCode) => DisplayNames.TryGetValue(formCode, out var names) ? names.Zh : formCode;
    public static string DisplayNameEn(string formCode) => DisplayNames.TryGetValue(formCode, out var names) ? names.En : formCode;
}
