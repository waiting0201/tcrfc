namespace Tcrfc.Api.Features.AdminPress;

public sealed record AdminPressLocaleContent
{
    public required string Title { get; init; }
    public string? Description { get; init; }

    /// <summary>封面圖片替代文字（§4.0 圖片欄位組，逐語系）。對應 <c>press_resources_i18n.cover_alt</c>；有封面圖時前台輸出。</summary>
    public string? CoverAlt { get; init; }
}

public sealed record AdminPressContentInput
{
    public required AdminPressLocaleContent Zh { get; init; }
    public AdminPressLocaleContent? En { get; init; }
}

public sealed record AdminPressListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required bool IsShared { get; init; }

    /// <summary><c>press_release</c>（新聞稿）／<c>brand_kit</c>（品牌識別包）／<c>hires_image</c>（高解析圖）。</summary>
    public required string ResourceType { get; init; }
    public required string Status { get; init; }
    public DateOnly? PublishedOn { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>累計下載次數（唯讀，由前台下載端點累加）。</summary>
    public required int DownloadCount { get; init; }
    public int? FileBytes { get; init; }
    public string? CoverThumbUrl { get; init; }
    public string? TitleZh { get; init; }
    public string? TitleEn { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminPressDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required bool IsShared { get; init; }
    public required string ResourceType { get; init; }
    public required string Status { get; init; }
    public DateOnly? PublishedOn { get; init; }
    public required int SortOrder { get; init; }
    public required int DownloadCount { get; init; }
    public required string FileKey { get; init; }

    /// <summary>資源檔案的完整網址（後台預覽用；部署層未開放容器公開讀取時可能無法直接開啟）。</summary>
    public string? FileUrl { get; init; }
    public int? FileBytes { get; init; }
    public string? CoverKey { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbUrl { get; init; }
    public int? CoverWidth { get; init; }
    public int? CoverHeight { get; init; }
    public required AdminPressLocaleContent Zh { get; init; }
    public AdminPressLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>建立與更新共用。檔案在 multipart 的 <c>file</c>（建立必填；更新省略＝維持原檔）與 <c>cover</c>（選填）。</summary>
public sealed record UpsertAdminPressRequest
{
    public string? Slug { get; init; }
    public required string ResourceType { get; init; }

    /// <summary><c>draft</c>（隱藏）／<c>published</c>（顯示）。</summary>
    public required string Status { get; init; }

    /// <summary>發布日期；狀態為顯示且省略時自動填今天。</summary>
    public DateOnly? PublishedOn { get; init; }
    public int SortOrder { get; init; }
    public required AdminPressContentInput Content { get; init; }
    public bool RemoveCover { get; init; }
}

public sealed record BatchChangePressTypeRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }
    public required string ResourceType { get; init; }
}
