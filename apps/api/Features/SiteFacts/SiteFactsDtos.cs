namespace Tcrfc.Api.Features.SiteFacts;

/// <summary>
/// 公開讀取的站台事實（`GEO-03`／`GEO-04`，S1-12d）：成立年份、主場與場地、所屬聯賽、
/// 梯隊組成、聯絡方式。前台（<c>apps/web</c>）用來組頁面明文與結構化資料（<c>Organization</c>／
/// <c>SportsTeam</c> 的 <c>foundingDate</c>／<c>memberOf</c>／<c>location</c> 等欄位），
/// 兩者讀同一個來源，滿足 <c>GEO-04</c>「結構化資料與明確文字同時呈現、數值一致」。
///
/// 🔴 **人類語言欄位依 <c>?lang=</c> 解析成單一語系字串**（比照 <c>Features/Players</c>／
/// <c>Features/Schedule</c> 既有慣例，跟 <c>Features/Seo</c> 一次回傳 zh／en 兩份的慣例不同——
/// <c>Features/Seo</c> 的設定值是後台編輯表單直接消費雙欄位，這裡是給一般頁面顯示用的公開內容）。
/// 中文缺漏時的回退規則見 <c>Localization/RequestLocale.Pick</c>：請求語系有值就用，否則退回中文，
/// 兩者都沒有回傳 <c>null</c>。
///
/// ⚠️ **JSON-LD 需要不受 <c>lang</c> 影響的中文全名時**（例如 <c>SportsTeam.memberOf.name</c>
/// 規劃書明文「JSON-LD 一律用中文全名」），呼叫端另外用 <c>?lang=zh</c> 呼叫一次本端點取值即可——
/// 這支端點有快取，多呼叫一次成本很低，不需要在回應裡同時塞兩種語系的重複欄位。
/// </summary>
public sealed record PublicSiteFactsDto
{
    /// <summary>成立年（西元，字串形式，供明文組句）。</summary>
    public string? FoundedYear { get; init; }

    /// <summary>ISO 8601 日期，供 JSON-LD <c>foundingDate</c> 使用；確切月日未核實時為
    /// <c>null</c>（GEO-05「資料不足時不輸出」同一原則），與 <c>lang</c> 無關。</summary>
    public string? FoundingDateIso { get; init; }

    /// <summary>「＿＿年創立」這類完整顯示句，依 <c>lang</c> 解析。</summary>
    public string? FoundedDisplay { get; init; }

    /// <summary>成立當年拿下的頭銜，依 <c>lang</c> 解析，<c>null</c>＝沒有這筆事實。</summary>
    public string? FoundingTitle { get; init; }

    public required PublicSiteFactLeagueDto League { get; init; }

    /// <summary>主場與場地清單，依顯示順序（第一筆為主要主場）。</summary>
    public required IReadOnlyList<PublicSiteFactVenueDto> Venues { get; init; }

    /// <summary>梯隊組成敘述，依 <c>lang</c> 解析。</summary>
    public string? SquadStructureSummary { get; init; }

    /// <summary>梯隊年齡層代碼清單（不含一線隊本身），與 <c>lang</c> 無關。</summary>
    public required IReadOnlyList<string> SquadCodes { get; init; }

    public required PublicSiteFactContactDto Contact { get; init; }

    /// <summary>台中藍鯨官方網站網址（主站規劃書 §3.6「06 女子足球」入口頁「前往台中藍鯨官網」
    /// 按鈕的連結目標），與 <c>lang</c> 無關（網址本身不需要逐語系）。**概念上只屬於台中磐石
    /// （<c>tcrfc</c>）**——藍鯨官網本身沒有 06 單元（見 <c>docs/13-blue-whale-site.md</c> §6），
    /// 因此以 <c>bw</c> 呼叫本端點時這個欄位預期恆為 <c>null</c>。<c>null</c>＝尚未設定，
    /// 呼叫端應保留既有預設值（例如 <c>apps/web</c> 目前的 <c>NUXT_PUBLIC_BLUE_WHALE_SITE_URL</c>
    /// staging 預設）而不是顯示空白連結。</summary>
    public string? BlueWhaleSiteUrl { get; init; }

    /// <summary>社群連結（C-2）。全部可空，<c>null</c>＝該平台未設定，前台不應顯示該圖示。</summary>
    public required PublicSiteFactSocialDto Social { get; init; }

    /// <summary>頁尾品牌簡介，依 <c>lang</c> 解析（缺英文回退中文）；<c>null</c>＝未設定，前台保留既有預設文案。</summary>
    public string? FooterBlurb { get; init; }
}

public sealed record PublicSiteFactSocialDto
{
    public string? Facebook { get; init; }
    public string? Instagram { get; init; }
    public string? Youtube { get; init; }
    public string? Line { get; init; }
}

/// <summary>各部門窗口（對外公開資訊）。名稱依 <c>lang</c> 解析。</summary>
public sealed record PublicSiteFactDepartmentDto
{
    public required string Name { get; init; }
    public string? Email { get; init; }
    public string? PhoneExtension { get; init; }
}

public sealed record PublicSiteFactLeagueDto
{
    /// <summary>官方全名，依 <c>lang</c> 解析。JSON-LD <c>memberOf.name</c> 一律用中文全名
    /// （見上方檔頭說明），不是這個依 <c>lang</c> 解析出來的值。</summary>
    public string? Name { get; init; }

    /// <summary>常用簡稱，依 <c>lang</c> 解析，<c>null</c>＝沒有這筆事實。</summary>
    public string? ShortName { get; init; }
}

public sealed record PublicSiteFactVenueDto
{
    /// <summary>場地名稱，依 <c>lang</c> 解析。</summary>
    public required string Name { get; init; }

    /// <summary>地址，依 <c>lang</c> 解析，<c>null</c>＝尚未公開／未核實（不放佔位假地址）。</summary>
    public string? Address { get; init; }

    /// <summary>是否為主場——這支端點只回傳「這個俱樂部登記的主場清單」，因此恆為
    /// <c>true</c>；保留這個欄位是為了跟前台既有 <c>SiteFactVenue.isHomeGround</c>
    /// 介面形狀相容，也讓日後若這支端點擴充成回傳非主場場地時不需要改前台型別。</summary>
    public required bool IsHomeGround { get; init; }
}

public sealed record PublicSiteFactContactDto
{
    /// <summary>取自主場地址（第一筆主場場地，見 <see cref="PublicSiteFactsDto.Venues"/>）的
    /// 計算值，不是獨立儲存的欄位——避免同一個地址在兩處各寫一份（GEO-03）。</summary>
    public string? Address { get; init; }

    /// <summary>聯絡電話，與 <c>lang</c> 無關，目前兩俱樂部皆未核實，恆為 <c>null</c>。</summary>
    public string? Phone { get; init; }

    /// <summary>營業時間，依 <c>lang</c> 解析，目前兩俱樂部皆未核實，恆為 <c>null</c>。</summary>
    public string? Hours { get; init; }

    /// <summary>聯絡 Email，與 <c>lang</c> 無關；<c>null</c>＝未設定。</summary>
    public string? Email { get; init; }

    /// <summary>各部門窗口，依顯示順序；未設定為空陣列。</summary>
    public required IReadOnlyList<PublicSiteFactDepartmentDto> Departments { get; init; }
}
