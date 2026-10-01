namespace Tcrfc.Api.Features.FanEvents;

public sealed record FanEventMyRegistrationDto
{
    /// <summary><c>registered</c>（已報名）／<c>waitlist</c>（候補）／<c>cancelled</c>／<c>attended</c>。</summary>
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
}

public sealed record FanEventListItemDto
{
    public required string Slug { get; init; }
    public string? Name { get; init; }
    public string? Location { get; init; }
    public DateTime? StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public DateTime? RegistrationDeadlineAt { get; init; }
    /// <summary>名額上限；null＝不限。</summary>
    public int? Capacity { get; init; }
    /// <summary>剩餘名額；null＝不限。已報名（含已到場）佔名額，候補不佔。</summary>
    public int? SpotsLeft { get; init; }
    /// <summary>true＝限付費球迷會員報名。</summary>
    public required bool IsPaidMembersOnly { get; init; }
    /// <summary>目前是否開放報名（已發布、截止與開始時間未過）。額滿時仍為 true——額滿後報名會進候補，見 <c>IsFull</c>。</summary>
    public required bool IsRegistrationOpen { get; init; }
    public required bool IsFull { get; init; }
    /// <summary><c>upcoming</c>（尚未結束）／<c>past</c>（已結束，8.2 活動回顧）。</summary>
    public required string Phase { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbUrl { get; init; }
}

public sealed record FanEventImagePublicDto
{
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
}

public sealed record FanEventArticlePublicDto
{
    public required string Slug { get; init; }
    public string? Title { get; init; }
}

public sealed record FanEventDetailDto
{
    public required FanEventListItemDto Event { get; init; }
    public string? Description { get; init; }
    public string? VenueName { get; init; }
    /// <summary>活動回顧圖集。</summary>
    public required IReadOnlyList<FanEventImagePublicDto> Images { get; init; }
    /// <summary>活動回顧的關聯文章（只列已發布的）。</summary>
    public required IReadOnlyList<FanEventArticlePublicDto> Articles { get; init; }
    /// <summary>有帶會員權杖時，這位會員在這場活動的報名狀態；沒登入或沒報名為 null。</summary>
    public FanEventMyRegistrationDto? MyRegistration { get; init; }
}

/// <summary>報名本文。已登入會員（帶會員權杖）只需要 <c>Note</c>；非會員必須有 <c>ApplicantName</c> 與（<c>Phone</c> 或 <c>Email</c>）。</summary>
public sealed record FanEventRegisterRequest(string? ApplicantName, string? Phone, string? Email, string? Note);

public sealed record FanEventRegistrationResultDto
{
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    /// <summary>true＝名額已滿，這次報名進入候補（不保證遞補；遞補由客服人工處理）。</summary>
    public required bool IsWaitlisted { get; init; }
}
