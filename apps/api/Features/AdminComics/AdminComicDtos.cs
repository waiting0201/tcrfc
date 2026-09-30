namespace Tcrfc.Api.Features.AdminComics;

// ───────────── 企劃設定（8.1 About 世界觀說明頁）─────────────

public sealed record AdminComicAboutLocaleContent
{
    public string? Title { get; init; }

    /// <summary>世界觀說明（純文字或編輯器輸出的文字，≤ 20000 字）。</summary>
    public string? Body { get; init; }
}

public sealed record AdminComicAboutDto
{
    public required AdminComicAboutLocaleContent Zh { get; init; }
    public AdminComicAboutLocaleContent? En { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record UpdateAdminComicAboutRequest
{
    public required AdminComicAboutLocaleContent Zh { get; init; }
    public AdminComicAboutLocaleContent? En { get; init; }
}

// ───────────── 角色 ─────────────

public sealed record AdminComicCharacterLocaleContent
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

public sealed record AdminComicCharacterContentInput
{
    public required AdminComicCharacterLocaleContent Zh { get; init; }
    public AdminComicCharacterLocaleContent? En { get; init; }
}

public sealed record AdminComicCharacterDto
{
    public required Guid Id { get; init; }

    /// <summary>關聯的真實球員（選填，作為角色原型）。</summary>
    public Guid? PlayerId { get; init; }
    public string? PlayerName { get; init; }
    public string? ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public required int SortOrder { get; init; }
    public required AdminComicCharacterLocaleContent Zh { get; init; }
    public AdminComicCharacterLocaleContent? En { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminComicCharacterRequest
{
    public Guid? PlayerId { get; init; }

    /// <summary>省略＝新增時排在最後、更新時維持不變。</summary>
    public int? SortOrder { get; init; }
    public bool RemoveImage { get; init; }
    public required AdminComicCharacterContentInput Content { get; init; }
}

// ───────────── 集數 ─────────────

public sealed record AdminComicEpisodeLocaleContent
{
    public required string Title { get; init; }
}

public sealed record AdminComicEpisodeContentInput
{
    public required AdminComicEpisodeLocaleContent Zh { get; init; }
    public AdminComicEpisodeLocaleContent? En { get; init; }
}

public sealed record AdminComicPageDto
{
    public required Guid Id { get; init; }
    public required string ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public required int SortOrder { get; init; }
}

public sealed record AdminComicEpisodeListItemDto
{
    public required Guid Id { get; init; }
    public required int EpisodeNo { get; init; }
    public string? CoverKey { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbUrl { get; init; }
    public DateOnly? PublishedOn { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }

    /// <summary>系統自動判定的最新集數（已發布且發布日不晚於今天的最大集數），不是人工勾選。</summary>
    public required bool IsLatest { get; init; }
    public required int ViewCount { get; init; }
    public required int PageCount { get; init; }
    public string? TitleZh { get; init; }
    public string? TitleEn { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminComicEpisodeDetailDto
{
    public required Guid Id { get; init; }
    public required int EpisodeNo { get; init; }
    public string? CoverKey { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbUrl { get; init; }
    public DateOnly? PublishedOn { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required bool IsLatest { get; init; }
    public required int ViewCount { get; init; }
    public required AdminComicEpisodeLocaleContent Zh { get; init; }
    public AdminComicEpisodeLocaleContent? En { get; init; }
    public required IReadOnlyList<AdminComicPageDto> Pages { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminComicEpisodeRequest
{
    public required int EpisodeNo { get; init; }
    public DateOnly? PublishedOn { get; init; }

    /// <summary><c>draft</c>（不公開）或 <c>published</c>（公開，需至少一張內頁）。</summary>
    public required string Status { get; init; }
    public bool RemoveCover { get; init; }
    public required AdminComicEpisodeContentInput Content { get; init; }
}
