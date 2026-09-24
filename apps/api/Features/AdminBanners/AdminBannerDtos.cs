namespace Tcrfc.Api.Features.AdminBanners;

/// <summary>單一語系可編輯內容。<c>banners_i18n</c> 六欄全部可為 <c>null</c>（db/club-schema.sql，
/// S1-7a 新增 <see cref="ImageAlt"/>）。</summary>
public sealed record AdminBannerLocaleContent
{
    public string? Title { get; init; }
    public string? Subtitle { get; init; }

    /// <summary>圖片替代文字（S1-7a，docs/14 圖片欄位組通則，無障礙與 GEO 用）。</summary>
    public string? ImageAlt { get; init; }
    public string? Cta1Label { get; init; }
    public string? Cta1Url { get; init; }
    public string? Cta2Label { get; init; }
    public string? Cta2Url { get; init; }
}

/// <summary><c>Zh</c> 必填（雙語規則本身不要求標題非空白——<c>banners_i18n</c> 的欄位本來就全部
/// 可為 <c>null</c>，一張只靠視覺的輪播圖不一定需要文字），<c>En</c> 可省略。</summary>
public sealed record AdminBannerContentInput
{
    public required AdminBannerLocaleContent Zh { get; init; }
    public AdminBannerLocaleContent? En { get; init; }
}

/// <summary>
/// 🔴 這是 <c>payload</c> 這個 multipart 欄位的 JSON 內容，不含圖片／影片鍵——圖片透過同一次
/// 請求的 <c>file</c> 欄位、影片透過 <c>video</c> 欄位一起送出，比照 <c>Features/AdminNews</c>
/// 的既有契約（見 apps/api/README.md「多檔案 multipart 契約」段的既有說明）。
/// </summary>
public sealed record CreateBannerRequest
{
    /// <summary>素材種類（S1-7a 新增欄位，v3.14 開放 <c>video</c>）。省略時預設 <c>image</c>。
    /// <c>image</c>：只需要 <c>file</c>（輪播圖）。<c>video</c>：<c>file</c>（海報格 poster，
    /// 仍是必填——「必須搭配海報圖」，docs/17-deployment.md §6）＋ <c>video</c>（影片檔案，
    /// MP4／H.264／AAC，上限 50 MB，見 <c>Tcrfc.Api.Videos.VideoUploadOptions</c>），兩者缺一
    /// 都是 400。見 <c>AdminBannersRepository.ValidateMediaType</c>、
    /// <c>AdminBannersEndpoints</c>「影片模式的欄位互斥檢查」。</summary>
    public string? MediaType { get; init; }

    /// <summary>上架起訖時間（規劃書 B3「上架期間」）。皆可為 <c>null</c>＝不限制起訖，
    /// 一建立就可能出現在前台（只要 <c>EndAt</c> 也是 <c>null</c> 或還沒到）。</summary>
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }

    public required int SortOrder { get; init; }

    public required AdminBannerContentInput Content { get; init; }
}

/// <summary>
/// 更新請求。圖片是否更換由「這次請求有沒有帶 <c>file</c>」決定，影片同理看 <c>video</c>
/// （見 <c>AdminBannersEndpoints</c>）——<c>banners.image_key</c> 是 <c>NOT NULL</c>，不像文章
/// 封面圖有「移除」這個選項，只有「換一張」或「維持原圖」兩態；<c>video_key</c> 則會在切回
/// <c>image</c> 模式時被清空並刪除舊物件，見 <c>AdminBannersRepository.UpdateAsync</c>。
/// </summary>
public sealed record UpdateBannerRequest
{
    public string? MediaType { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public required int SortOrder { get; init; }
    public required AdminBannerContentInput Content { get; init; }
}

public sealed record AdminBannerListItemDto
{
    public required Guid Id { get; init; }
    public required string MediaType { get; init; }
    public required string ImageKey { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public string? VideoKey { get; init; }

    /// <summary>草稿／發布（v3.14）。新增時一律是 <c>draft</c>，見
    /// <see cref="AdminBannersRepository.CreateAsync"/>；透過 <c>/publish</c>／<c>/unpublish</c>
    /// 兩支專用端點切換，不是這個 DTO 對應的 Create／Update 請求的欄位。</summary>
    public required string Status { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public required int SortOrder { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public string? TitleZh { get; init; }
    public string? TitleEn { get; init; }
}

public sealed record AdminBannerDetailDto
{
    public required Guid Id { get; init; }
    public required string MediaType { get; init; }
    public required string ImageKey { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public string? VideoKey { get; init; }
    public required string Status { get; init; }
    public DateTime? StartAt { get; init; }
    public DateTime? EndAt { get; init; }
    public required int SortOrder { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required AdminBannerLocaleContent Zh { get; init; }
    public AdminBannerLocaleContent? En { get; init; }
}
