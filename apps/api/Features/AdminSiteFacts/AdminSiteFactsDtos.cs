namespace Tcrfc.Api.Features.AdminSiteFacts;

/// <summary>
/// `I` 網站設定——`GEO-03`（事實單一來源）／`GEO-04`（結構化資料與明文雙重呈現）承載的站台事實：
/// 成立年份與成立日期、首季頭銜、所屬聯賽（中英全名與簡稱）、梯隊組成（年齡層代碼＋敘述）、
/// 主場與場地（名稱、地址）、聯絡方式（電話、營業時間）。**不新增資料表**——純量與逐語系事實存於
/// 既有 <c>settings</c>／<c>settings_i18n</c>（<c>setting_group='site'</c>，鍵詞彙見
/// <see cref="AdminSiteFactsRepository"/> 檔頭），主場場地本身沿用既有 <c>venues</c>／
/// <c>venues_i18n</c>（<c>Venue</c> 不帶 <c>club_id</c>，見 docs/12 §4.7「S1-12d」）。
///
/// 🔴 **整份取代語意**（比照 <c>Features/AdminSeo/AdminSeoSettingsDto</c> 既有慣例）：呼叫端一律
/// 送出完整表單內容，省略欄位＝清空該欄位，不是「維持不變」。<see cref="HomeVenues"/> 亦同——
/// 送出的清單就是這個俱樂部之後唯一承認的主場清單，清單裡沒出現的既有場地不會被刪除
/// （<c>Venue</c> 列本身可能仍被其他資料引用），只是不再被視為這個俱樂部的主場。
/// </summary>
public sealed record AdminSiteFactsDto
{
    /// <summary>成立年（西元，字串形式，供明文組句），必填。</summary>
    public string? FoundedYear { get; init; }

    /// <summary>ISO 8601 日期。確切成立月日未核實時為 <c>null</c>——不臆測（GEO-05「資料不足
    /// 時不輸出」同一原則延伸到這裡）。</summary>
    public string? FoundingDateIso { get; init; }

    public string? FoundingDateDisplayZh { get; init; }
    public string? FoundingDateDisplayEn { get; init; }

    /// <summary>成立當年拿下的頭銜（可為空——不是每個俱樂部都有「成立當年奪冠」這筆事實）。</summary>
    public string? FoundingTitleZh { get; init; }
    public string? FoundingTitleEn { get; init; }

    public string? LeagueNameZh { get; init; }
    public string? LeagueNameEn { get; init; }

    /// <summary>聯賽常用簡稱（如「木蘭聯賽」），可為空。</summary>
    public string? LeagueShortNameZh { get; init; }
    public string? LeagueShortNameEn { get; init; }

    /// <summary>梯隊組成的簡短敘述（體系層級文字，例如「一線隊與足球學院三個梯隊並行的發展體系」）。</summary>
    public string? SquadStructureZh { get; init; }
    public string? SquadStructureEn { get; init; }

    /// <summary>梯隊年齡層代碼清單（不含一線隊本身），依顯示順序。例：<c>["U15", "U14", "U12"]</c>。
    /// 這是「有哪些年齡層」這個事實本身的單一來源——不查 <c>teams</c> 表即時算，理由是磐石目前
    /// 尚未建立對應的 <c>Team</c> 列（球員名單與肖像同意未到位，見 STATUS.md），即時查詢會得到
    /// 空清單而非正確答案，見 apps/api/README.md「S1-12d」節。</summary>
    public required IReadOnlyList<string> SquadCodes { get; init; }

    /// <summary>主場場地清單，依顯示順序，第一筆為主要主場（<see cref="Contact"/> 的地址取自
    /// 這一筆）。</summary>
    public required IReadOnlyList<AdminSiteFactVenueDto> HomeVenues { get; init; }

    /// <summary>聯絡電話。目前兩俱樂部皆未核實，維持 <c>null</c>，不放佔位假資料。</summary>
    public string? ContactPhone { get; init; }

    public string? ContactHoursZh { get; init; }
    public string? ContactHoursEn { get; init; }

    /// <summary>台中藍鯨官方網站網址（主站規劃書 §3.6「06 女子足球」入口頁「前往台中藍鯨官網」
    /// 按鈕的連結目標）。**概念上只屬於台中磐石（<c>tcrfc</c>）這個俱樂部的設定**——06 單元是
    /// 主站專屬單元，藍鯨官網本身沒有這個單元（見 <c>docs/13-blue-whale-site.md</c> §6「不設 06」），
    /// 因此藍鯨（<c>bw</c>）俱樂部範圍下這個鍵預期恆為 <c>null</c>。這不是資料庫層級強制的規則，
    /// 是沿用既有「有些站台事實不是每個俱樂部都有」的既有原則（比照 <see cref="FoundingTitleZh"/>）——
    /// 沒有另外加俱樂部白名單檢查，見 <c>apps/api/README.md</c>「S1-12d」節「藍鯨官網網址」小節。
    /// <c>null</c>＝尚未設定；有值時必為 <c>https://</c> 開頭的絕對網址（見
    /// <see cref="AdminSiteFactsRepository.ValidateBlueWhaleSiteUrl"/>）。</summary>
    public string? BlueWhaleSiteUrl { get; init; }
}

/// <summary>既有 <c>Venue</c> 列（可能同時被其他資料引用，例如賽事、梯次的地點）。</summary>
public sealed record AdminSiteFactVenueDto
{
    public required Guid Id { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? Address { get; init; }
}

/// <summary>整份取代（見上方檔頭「整份取代語意」）。JSON PUT，沒有圖片欄位——與
/// <c>Features/AdminSeo</c> 系列（<c>multipart/form-data</c>）不同，這裡不需要處理檔案上傳。</summary>
public sealed record UpdateSiteFactsRequest
{
    public string? FoundedYear { get; init; }
    public string? FoundingDateIso { get; init; }
    public string? FoundingDateDisplayZh { get; init; }
    public string? FoundingDateDisplayEn { get; init; }
    public string? FoundingTitleZh { get; init; }
    public string? FoundingTitleEn { get; init; }
    public string? LeagueNameZh { get; init; }
    public string? LeagueNameEn { get; init; }
    public string? LeagueShortNameZh { get; init; }
    public string? LeagueShortNameEn { get; init; }
    public string? SquadStructureZh { get; init; }
    public string? SquadStructureEn { get; init; }
    public IReadOnlyList<string>? SquadCodes { get; init; }
    public IReadOnlyList<UpdateSiteFactVenueRequest>? HomeVenues { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactHoursZh { get; init; }
    public string? ContactHoursEn { get; init; }
    public string? BlueWhaleSiteUrl { get; init; }
}

/// <summary><see cref="Id"/> 有值＝更新既有 <c>Venue</c> 列（找不到則回 400）；
/// <c>null</c>＝新增一筆場地。</summary>
public sealed record UpdateSiteFactVenueRequest
{
    public Guid? Id { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? Address { get; init; }
}
