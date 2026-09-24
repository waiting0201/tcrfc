namespace Tcrfc.Api.Features.Home;

/// <summary>公開讀取：單則 Hero 輪播。依語系回退（<see cref="Localization.RequestLocale.Pick"/>），
/// 只回目前在上架期間內的列（見 <c>HomeRepository.ListBannersAsync</c>）。</summary>
public sealed record BannerDto
{
    public required Guid Id { get; init; }
    public required string ImageKey { get; init; }
    public required int SortOrder { get; init; }
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
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
