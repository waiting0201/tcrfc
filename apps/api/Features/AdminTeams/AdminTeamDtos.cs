namespace Tcrfc.Api.Features.AdminTeams;

/// <summary>
/// 「我能寫哪些球隊」下拉選單用（<c>/api/v1/admin/{club}/teams/writable</c>）——C1–C4 共用同一份
/// 「參賽球隊／所屬球隊」選單需要把選項收斂成呼叫端依 <see cref="Tcrfc.Api.Security.TeamRowScope"/> 真的能寫
/// 的球隊，見 <c>AdminTeamsEndpoints</c> 檔頭「為什麼是獨立端點不是加旗標」的完整說明。欄位只給
/// 下拉選單需要的最小集合，跟 <see cref="AdminTeamAdminListItemDto"/>（球隊管理列表頁，欄位齊全）
/// 刻意不同——這支端點的呼叫端不一定有 <c>team.team.view</c>，不能假設它們拿得到完整球隊明細。
/// </summary>
public sealed record AdminWritableTeamDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Type { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
}

/// <summary>C1 球隊單一語系內容（<c>teams_i18n</c>）。</summary>
public sealed record AdminTeamLocaleContent
{
    public required string Name { get; init; }
    public string? Intro { get; init; }
}

public sealed record AdminTeamContentInput
{
    public required AdminTeamLocaleContent Zh { get; init; }
    public AdminTeamLocaleContent? En { get; init; }
}

/// <summary>C1 球隊管理——俱樂部範圍列表用（<c>/api/v1/admin/{club}/teams</c>）。
/// 「這個俱樂部自己的球隊清單」，含 SortOrder／HeroKey／UpdatedAt。</summary>
public sealed record AdminTeamAdminListItemDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Type { get; init; }
    public required string Gender { get; init; }
    public string? AgeBand { get; init; }
    public string? TeamColor { get; init; }
    public string? HeroKey { get; init; }
    public string? HeroUrl { get; init; }
    public string? HeroThumbUrl { get; init; }
    public required int SortOrder { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminTeamDetailDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Type { get; init; }
    public required string Gender { get; init; }
    public string? AgeBand { get; init; }
    public string? TeamColor { get; init; }
    public string? HeroKey { get; init; }
    public string? HeroUrl { get; init; }
    public string? HeroThumbUrl { get; init; }
    public required int SortOrder { get; init; }
    public required AdminTeamLocaleContent Zh { get; init; }
    public AdminTeamLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 🔴 <c>Code</c>：規劃書明文「全站唯一，不得改成 (club_id, code) 複合鍵」（主站 §4.3 C1、
/// docs/12b §6.1）——建立時檢查的是全站範圍，不是本俱樂部範圍。
/// 🔴 <c>Type</c>／<c>Gender</c>：值域由 repository 驗證（<c>first_team</c>／<c>academy</c>；
/// <c>men</c>／<c>women</c>／<c>mixed</c>）。<c>type = first_team</c> 每俱樂部至多一筆，同樣由
/// repository 檢查。
/// </summary>
public sealed record CreateAdminTeamRequest
{
    public required string Code { get; init; }
    public required string Type { get; init; }
    public required string Gender { get; init; }
    public string? AgeBand { get; init; }
    public string? TeamColor { get; init; }
    public int SortOrder { get; init; }
    public required AdminTeamContentInput Content { get; init; }
}

public sealed record UpdateAdminTeamRequest
{
    public required string Code { get; init; }
    public required string Type { get; init; }
    public required string Gender { get; init; }
    public string? AgeBand { get; init; }
    public string? TeamColor { get; init; }
    public int SortOrder { get; init; }
    public required AdminTeamContentInput Content { get; init; }

    /// <summary>true＝移除目前的主視覺圖片，不接受同時夾帶新檔案（比照 <c>UpdateArticleRequest.RemoveCover</c>）。</summary>
    public bool RemoveHero { get; init; }
}
