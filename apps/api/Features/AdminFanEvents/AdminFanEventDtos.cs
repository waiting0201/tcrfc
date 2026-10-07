namespace Tcrfc.Api.Features.AdminFanEvents;

public sealed record AdminFanEventLocaleContent
{
    public required string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>活動地點的文字說明（例如「台中市西屯區某某球場入口」）；選了場地時可以不填。</summary>
    public string? Location { get; init; }

    /// <summary>封面圖片替代文字（§4.0 圖片欄位組，逐語系）。對應 <c>fan_events_i18n.cover_alt</c>；有封面圖時前台輸出。</summary>
    public string? CoverAlt { get; init; }
}

public sealed record AdminFanEventContentInput
{
    public required AdminFanEventLocaleContent Zh { get; init; }
    public AdminFanEventLocaleContent? En { get; init; }
}

public sealed record AdminFanEventImageDto
{
    public required Guid Id { get; init; }
    public required string ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public required int SortOrder { get; init; }
}

public sealed record AdminFanEventArticleDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? TitleZh { get; init; }
    public required string Status { get; init; }
}

public sealed record AdminFanEventListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public DateTime? StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public DateTime? RegistrationDeadlineAt { get; init; }
    public int? Capacity { get; init; }
    public required bool IsPaidMembersOnly { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? CoverKey { get; init; }
    public string? CoverThumbUrl { get; init; }
    public Guid? VenueId { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }

    /// <summary>已報名（含已到場）人數，佔名額。</summary>
    public required int RegisteredCount { get; init; }
    public required int WaitlistCount { get; init; }

    /// <summary>已發布、報名截止日還沒過、名額還有空位。</summary>
    public required bool IsRegistrationOpen { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminFanEventDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public DateTime? StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public DateTime? RegistrationDeadlineAt { get; init; }
    public int? Capacity { get; init; }
    public required bool IsPaidMembersOnly { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? CoverKey { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbUrl { get; init; }
    public Guid? VenueId { get; init; }
    public string? VenueName { get; init; }
    public required AdminFanEventLocaleContent Zh { get; init; }
    public AdminFanEventLocaleContent? En { get; init; }
    public required int RegisteredCount { get; init; }
    public required int WaitlistCount { get; init; }
    public required bool IsRegistrationOpen { get; init; }

    /// <summary>活動回顧的圖集與關聯文章。</summary>
    public required IReadOnlyList<AdminFanEventImageDto> Images { get; init; }
    public required IReadOnlyList<AdminFanEventArticleDto> Articles { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminFanEventRequest
{
    public string? Slug { get; init; }
    public DateTime? StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public DateTime? RegistrationDeadlineAt { get; init; }

    /// <summary>名額上限；省略＝不限。不可低於目前已報名人數。</summary>
    public int? Capacity { get; init; }
    public bool IsPaidMembersOnly { get; init; }
    public Guid? VenueId { get; init; }

    /// <summary><c>draft</c>（不公開）或 <c>published</c>（公開，需填活動開始時間）。</summary>
    public required string Status { get; init; }
    public bool RemoveCover { get; init; }

    /// <summary>活動回顧的關聯文章：省略（null）＝維持不變；空陣列＝清空；有值＝整份取代。</summary>
    public IReadOnlyList<Guid>? ArticleIds { get; init; }
    public required AdminFanEventContentInput Content { get; init; }
}

// ───────────── 報名名單 ─────────────

public sealed record AdminFanEventRegistrationDto
{
    public required Guid Id { get; init; }
    public Guid? MemberId { get; init; }
    public string? MemberNo { get; init; }
    public required bool IsMember { get; init; }

    /// <summary>報名人姓名（會員用會員主檔姓名）。沒有「解除遮罩」權限時為遮罩值（王○明）。</summary>
    public string? ApplicantName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? Note { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required bool IsMasked { get; init; }
}

public sealed record CreateAdminFanEventRegistrationRequest
{
    /// <summary>會員報名（會員編號對應的會員 id）；省略＝非會員，須填姓名與至少一種聯絡方式。</summary>
    public Guid? MemberId { get; init; }
    public string? ApplicantName { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Note { get; init; }
}

public sealed record UpdateAdminFanEventRegistrationRequest
{
    /// <summary><c>registered</c>／<c>waitlist</c>／<c>cancelled</c>／<c>attended</c>。</summary>
    public required string Status { get; init; }

    /// <summary>省略＝維持不變；空字串＝清除。</summary>
    public string? Note { get; init; }
}
