namespace Tcrfc.Api.Features.Comics;

public sealed record ComicAboutPublicDto
{
    public string? Title { get; init; }
    public string? Body { get; init; }
}

public sealed record ComicCharacterPublicDto
{
    public required Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }

    /// <summary>圖片寬高（像素）與替代文字（當前語系，英文空白回退繁中）；沒有圖片時三者皆 <c>null</c>。</summary>
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public string? ImageAlt { get; init; }

    /// <summary>原型球員（關聯真實球員；可為空）。</summary>
    public Guid? PlayerId { get; init; }
}

public sealed record ComicEpisodeListItemDto
{
    public required int EpisodeNo { get; init; }
    public string? Title { get; init; }
    public string? CoverUrl { get; init; }

    /// <summary>封面寬高（像素）與替代文字（當前語系，英文空白回退繁中）；沒有封面時三者皆 <c>null</c>。</summary>
    public int? CoverWidth { get; init; }
    public int? CoverHeight { get; init; }
    public string? CoverAlt { get; init; }
    public string? CoverThumbUrl { get; init; }
    public DateOnly? PublishedOn { get; init; }
    /// <summary>最新集數（置頂區塊、首頁同步曝光）：已發布且已到發布日的<b>最大集數</b>，讀取時即時判定。</summary>
    public required bool IsLatest { get; init; }
    public required int PageCount { get; init; }
}

public sealed record ComicPagePublicDto
{
    public required int PageNo { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }

    /// <summary>內頁替代文字（當前語系，英文空白回退中文）；沒填為 <c>null</c>。</summary>
    public string? Alt { get; init; }
}

/// <summary>集數詳情＝線上閱讀器的資料：全部頁面（依序）與上一集／下一集導覽。全部免費、不需登入、沒有付費牆欄位。</summary>
public sealed record ComicEpisodeDetailDto
{
    public required int EpisodeNo { get; init; }
    public string? Title { get; init; }
    public string? CoverUrl { get; init; }

    /// <summary>封面寬高（像素）與替代文字（當前語系，英文空白回退繁中）；沒有封面時三者皆 <c>null</c>。</summary>
    public int? CoverWidth { get; init; }
    public int? CoverHeight { get; init; }
    public string? CoverAlt { get; init; }
    public DateOnly? PublishedOn { get; init; }
    public required bool IsLatest { get; init; }
    public required IReadOnlyList<ComicPagePublicDto> Pages { get; init; }
    public int? PreviousEpisodeNo { get; init; }
    public int? NextEpisodeNo { get; init; }
}
