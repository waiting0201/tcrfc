namespace Tcrfc.Api.Features.Players;

/// <summary>
/// 球員國籍代碼（2026-11-09 使用者決定）。<c>players.nationality</c>（nvarchar(32)，單一國籍）存
/// <b>ISO 3166-1 alpha-2 大寫代碼</b>，另加英國四地 <c>GB-ENG</c>／<c>GB-SCT</c>／<c>GB-WLS</c>／<c>GB-NIR</c>（足球以四協會參賽）。
/// 國名不入庫、API 只回代碼，由官網與 App 依語系產生（原本後台填中文，英文版因此顯示中文）。
/// 清單硬編碼：<c>RegionInfo</c> 在不同平台的 ICU 資料不完整，不能當驗證依據。
/// </summary>
public static class NationalityCode
{
    public const int MaxLength = 6;

    private static readonly string[] Iso = (
        "AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BV BW BY BZ " +
        "CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE EG EH ER ES ET FI FJ FK FM FO FR " +
        "GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HK HM HN HR HT HU ID IE IL IM IN IO IQ IR IS IT JE JM JO JP " +
        "KE KG KH KI KM KN KP KR KW KY KZ LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME MF MG MH MK ML MM MN MO MP MQ MR MS MT MU MV MW MX MY MZ " +
        "NA NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW " +
        "SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ TC TD TF TG TH TJ TK TL TM TN TO TR TT TV TW TZ " +
        "UA UG UM US UY UZ VA VC VE VG VI VN VU WF WS YE YT ZA ZM ZW").Split(' ');

    private static readonly string[] BritishNations = ["GB-ENG", "GB-SCT", "GB-WLS", "GB-NIR"];

    /// <summary>全部合法代碼（249 個 ISO ＋ 4 個英國四地）。</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(Iso.Concat(BritishNations), StringComparer.Ordinal);

    /// <summary>trim、轉大寫；空白回 null。不檢查是否合法（合法性用 <see cref="IsValid"/>）。</summary>
    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    /// <summary>null／空白視為合法（可為空）；否則正規化後必須在清單內。</summary>
    public static bool IsValid(string? value)
    {
        var v = Normalize(value);
        return v is null || All.Contains(v);
    }
}
