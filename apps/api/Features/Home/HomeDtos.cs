namespace Tcrfc.Api.Features.Home;

/// <summary>公開讀取：單則 Hero 輪播。依語系回退（<see cref="Localization.RequestLocale.Pick"/>），
/// 只回目前在上架期間內的列（見 <c>HomeRepository.ListBannersAsync</c>）。</summary>
public sealed record BannerDto
{
    public required Guid Id { get; init; }

    /// <summary>素材種類（S1-7a）：<c>image</c>／<c>video</c>。本輪後台只能寫入 <c>image</c>，
    /// 前台仍先接住這個欄位，供之後開放影片時不需要再改契約。</summary>
    public required string MediaType { get; init; }
    public required string ImageKey { get; init; }

    /// <summary>供前台輸出 <c>&lt;img width height&gt;</c> 預留版面，避免版面跳動（docs/14 圖片
    /// 欄位組通則）。<c>MediaType=video</c> 時代表海報格（poster）尺寸。</summary>
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }

    /// <summary>僅 <see cref="MediaType"/>＝<c>video</c> 時有值。本輪一律為 <c>null</c>。</summary>
    public string? VideoKey { get; init; }

    /// <summary>
    /// <see cref="ImageKey"/> 完整可公開存取網址（E-64 修正，2026-09-29）——比照
    /// <c>Features/Staff/StaffDto.PhotoUrl</c>／<c>Features/Players/PlayerDto.PhotoUrl</c> 的既有
    /// 慣例，由 <see cref="Tcrfc.Api.Images.IImagePublicUrlResolver"/> 算出，供前台直接放進
    /// <c>&lt;img src&gt;</c>。<c>MediaType=video</c> 時這是海報格（poster）的網址。
    /// <c>null</c>＝沒有物件鍵可用。</summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// <see cref="VideoKey"/> 完整可公開存取網址（E-64 修正）。影片走獨立的 Blob 容器，由
    /// <see cref="Tcrfc.Api.Videos.IVideoPublicUrlResolver"/> 算出——不能沿用
    /// <see cref="ImageUrl"/> 的計算方式（見該介面檔頭「容器不同」的說明）。僅
    /// <see cref="MediaType"/>＝<c>video</c> 時有值，本輪一律為 <c>null</c>（同 <see cref="VideoKey"/>）。
    /// </summary>
    public string? VideoUrl { get; init; }

    public required int SortOrder { get; init; }
    public string? Title { get; init; }
    public string? Subtitle { get; init; }

    /// <summary>圖片替代文字（S1-7a，無障礙與 GEO 用）。</summary>
    public string? ImageAlt { get; init; }
    public string? Cta1Label { get; init; }
    public string? Cta1Url { get; init; }
    public string? Cta2Label { get; init; }
    public string? Cta2Url { get; init; }
}

/// <summary>公開讀取：單一首頁區塊的開關、排序與精選指定。<see cref="NameZh"/>／<see cref="NameEn"/>
/// 不回傳——這是後台編輯畫面用的標籤，前台自己知道每個 <see cref="SectionCode"/> 要渲染成什麼樣子，
/// 不需要伺服器端給人類可讀名稱（跟 <c>AdminHomeSectionDto</c> 的使用情境不同）。</summary>
public sealed record HomeSectionDto
{
    public required string SectionCode { get; init; }
    public required bool IsEnabled { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>只有 <c>hero</c> 區塊可能有值，見 <c>AdminHomeSectionsRepository</c> 的說明。
    /// 前台若要顯示這則指定的輪播內容，另打 <c>GET /api/v1/{club}/banners</c> 交叉比對即可，
    /// 不在這裡展開完整輪播內容，避免同一份資料在兩個端點各自序列化一次。</summary>
    public Guid? FeaturedBannerId { get; init; }
}

/// <summary>首頁「五大核心價值」的一項（規劃書 §1.2、§3.1）。圖示由前台依 <see cref="Code"/> 對應；名稱中英文同時回傳（區塊要求「中英名稱」並陳）。</summary>
public sealed record CoreValueDto
{
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public required string NameEn { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>「了解更多」連結的頁面代稱（B1 頁面 <c>about/philosophy</c>＝2.3 足球理念）；前台依語系與站台組成實際網址。</summary>
    public string LearnMorePageSlug { get; init; } = "about/philosophy";
}
