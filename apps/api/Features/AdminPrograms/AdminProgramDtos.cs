namespace Tcrfc.Api.Features.AdminPrograms;

/// <summary>P1 課程／營隊項目單一語系內容（<c>programs_i18n</c>）。<c>Content</c> 對應
/// <c>programs_i18n.content</c>（SQL Server <c>json</c> 型別，區塊編輯器的整段 JSON），
/// 本檔只驗證語法合法性（<see cref="AdminProgramsRepository.ValidateContentJson"/>），
/// 不逐區塊驗證結構——規劃書只要求「課程內容（區塊編輯）」，沒有像 B1 頁面
/// （<c>PageBlockContentProcessor</c>）那樣明訂區塊型別清單，本輪不超出範圍另外發明一套。</summary>
public sealed record AdminProgramLocaleContent
{
    public required string Name { get; init; }
    public string? Intro { get; init; }
    public string? Content { get; init; }
}

public sealed record AdminProgramContentInput
{
    public required AdminProgramLocaleContent Zh { get; init; }
    public AdminProgramLocaleContent? En { get; init; }
}

public sealed record AdminProgramListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? ProgramType { get; init; }
    public string? Audience { get; init; }
    public int? AgeMin { get; init; }
    public int? AgeMax { get; init; }
    public required string Status { get; init; }
    public string? CoverKey { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required int SessionCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminProgramStaffDto
{
    public required Guid StaffId { get; init; }
    public string? NameZh { get; init; }
}

public sealed record AdminProgramPartnerDto
{
    public required Guid PartnerId { get; init; }
    public required string Slug { get; init; }
}

public sealed record AdminProgramDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public string? ProgramType { get; init; }
    public string? Audience { get; init; }
    public int? AgeMin { get; init; }
    public int? AgeMax { get; init; }
    public required string Status { get; init; }
    public string? CoverKey { get; init; }
    public required AdminProgramLocaleContent Zh { get; init; }
    public AdminProgramLocaleContent? En { get; init; }
    public required IReadOnlyList<AdminProgramStaffDto> Staff { get; init; }
    public required IReadOnlyList<AdminProgramPartnerDto> Partners { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 🔴 建立一律歸屬呼叫端當下的俱樂部（<c>programs.club_id = scope.ClubId</c>）——
/// <c>programs</c> 是 50 張 <c>club_id</c> 必填表之一（docs/12 §5.4、主站規劃書 §5.4），
/// 不像 <c>staff</c>／<c>articles</c> 有「兩隊共同」的可為空情境，這裡不需要
/// <c>SharedXxxReadOnlyException</c> 那一類處理。<see cref="ProgramType"/> 值域見
/// <see cref="AdminProgramsRepository.AllowedProgramTypes"/>（對應前台 5.1–5.5 五個課程頁）；
/// <see cref="Status"/> 值域見 <see cref="AdminProgramsRepository.AllowedStatuses"/>
/// （<c>draft</c>／<c>published</c>，比照一般內容型別的兩態慣例——docs/14「S0-7g」已裁決
/// 沒有排程發布需求的型別不補 <c>published_at</c>，規劃書 P1 沒有要求排程，見任務回報）。
/// </summary>
public sealed record CreateAdminProgramRequest
{
    public required string Slug { get; init; }
    public string? ProgramType { get; init; }
    public string? Audience { get; init; }
    public int? AgeMin { get; init; }
    public int? AgeMax { get; init; }
    public string? Status { get; init; }
    public required AdminProgramContentInput Content { get; init; }
    public IReadOnlyList<Guid>? StaffIds { get; init; }
    public IReadOnlyList<Guid>? PartnerIds { get; init; }
}

public sealed record UpdateAdminProgramRequest
{
    public required string Slug { get; init; }
    public string? ProgramType { get; init; }
    public string? Audience { get; init; }
    public int? AgeMin { get; init; }
    public int? AgeMax { get; init; }
    public string? Status { get; init; }
    public required AdminProgramContentInput Content { get; init; }

    /// <summary>省略＝維持不變、空陣列＝清空——比照 <c>UpdateAdminStaffRequest.Teams</c> 的既有語意。</summary>
    public IReadOnlyList<Guid>? StaffIds { get; init; }
    public IReadOnlyList<Guid>? PartnerIds { get; init; }

    /// <summary>true＝移除目前的封面圖，不接受同時夾帶新檔案（比照 <c>UpdateArticleRequest.RemoveCover</c>）。</summary>
    public bool RemoveCover { get; init; }
}
