namespace Tcrfc.Api.Features.AdminPlayers;

/// <summary>C2 球員單一語系內容（<c>players_i18n</c>）。</summary>
public sealed record AdminPlayerLocaleContent
{
    public required string Name { get; init; }
    public string? Bio { get; init; }
}

public sealed record AdminPlayerContentInput
{
    public required AdminPlayerLocaleContent Zh { get; init; }
    public AdminPlayerLocaleContent? En { get; init; }
}

/// <summary>
/// 後台列表／詳情不遮罩任何欄位——<c>players</c> 不在 docs/12b-database-tables.md §8 受限欄位
/// 清單內（球員名冊含生日是球隊官網例行公開的競技資訊，見既有公開端點
/// <c>Features/Players/PlayerDto.cs</c> 上的同一段說明），後台檢視者本來就看得到完整值，
/// 這裡不用再另外遮罩一次。
/// </summary>
public sealed record AdminPlayerListItemDto
{
    public required Guid Id { get; init; }
    public required Guid TeamId { get; init; }
    public required string TeamCode { get; init; }
    public int? ShirtNo { get; init; }
    public string? Position { get; init; }
    public DateOnly? BirthOn { get; init; }
    public string? Status { get; init; }
    public string? PhotoKey { get; init; }

    /// <summary>肖像同意狀態（S1-7a）：<c>not_consented</c>／<c>consented</c>／
    /// <c>consented_by_guardian</c>。後台一律看得到真實值與 <see cref="PhotoKey"/>——
    /// 只有公開端點會依此擋照片輸出，見 <c>Features/Players/PlayersRepository.cs</c>。</summary>
    public required string PortraitConsentStatus { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminPlayerDetailDto
{
    public required Guid Id { get; init; }
    public required Guid TeamId { get; init; }
    public required string TeamCode { get; init; }
    public int? ShirtNo { get; init; }
    public string? Position { get; init; }
    public DateOnly? BirthOn { get; init; }
    public int? HeightCm { get; init; }
    public int? WeightKg { get; init; }
    public string? Nationality { get; init; }
    public string? PreferredFoot { get; init; }
    public DateOnly? JoinedOn { get; init; }
    public string? Status { get; init; }
    public string? PhotoKey { get; init; }
    public required string PortraitConsentStatus { get; init; }
    public required AdminPlayerLocaleContent Zh { get; init; }
    public AdminPlayerLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 🔴 <c>TeamId</c> 必須是**這個俱樂部自己的球隊**（repository 檢查，跨俱樂部指派會擋下）——
/// <c>players.club_id</c> 由後端一律填成 <paramref name="TeamId"/> 所屬球隊的俱樂部（即路由的
/// <c>{club}</c>），不接受呼叫端另外指定，兩者天生一致，不會有欄位互相矛盾的空間。
/// 🔴 <c>Status</c>：主站規劃書 §4.3 C2「現役／離隊／外借／海外發展」，值域見
/// <c>AdminPlayersRepository.AllowedStatuses</c>；省略時預設 <c>active</c>（新增球員預設現役，
/// 屬執行層判斷，規劃書沒有明定預設值）。
/// </summary>
public sealed record CreateAdminPlayerRequest
{
    public required Guid TeamId { get; init; }
    public int? ShirtNo { get; init; }
    public string? Position { get; init; }
    public DateOnly? BirthOn { get; init; }
    public int? HeightCm { get; init; }
    public int? WeightKg { get; init; }
    public string? Nationality { get; init; }
    public string? PreferredFoot { get; init; }
    public DateOnly? JoinedOn { get; init; }
    public string? Status { get; init; }

    /// <summary>肖像同意狀態（S1-7a）。省略時預設 <c>not_consented</c>——fail-closed，
    /// **新建球員預設不公開輸出照片**，直到後台明確填寫已取得同意，見
    /// <c>AdminPlayersRepository.AllowedPortraitConsentStatuses</c>。</summary>
    public string? PortraitConsentStatus { get; init; }
    public required AdminPlayerContentInput Content { get; init; }
}

public sealed record UpdateAdminPlayerRequest
{
    public required Guid TeamId { get; init; }
    public int? ShirtNo { get; init; }
    public string? Position { get; init; }
    public DateOnly? BirthOn { get; init; }
    public int? HeightCm { get; init; }
    public int? WeightKg { get; init; }
    public string? Nationality { get; init; }
    public string? PreferredFoot { get; init; }
    public DateOnly? JoinedOn { get; init; }
    public string? Status { get; init; }

    /// <summary>省略時同建立請求，回退為 <c>not_consented</c>（fail-closed，比照既有 <c>Status</c>
    /// 欄位「省略即回退預設值」的既有語意，逐字比照 <c>AdminPlayersRepository.UpdateAsync</c>
    /// 對 <c>Status</c> 的既有寫法——這裡回退到最安全的值，不是回退到「維持不變」）。</summary>
    public string? PortraitConsentStatus { get; init; }
    public required AdminPlayerContentInput Content { get; init; }

    /// <summary>true＝移除目前的照片，不接受同時夾帶新檔案（比照 <c>UpdateArticleRequest.RemoveCover</c>）。</summary>
    public bool RemovePhoto { get; init; }
}
