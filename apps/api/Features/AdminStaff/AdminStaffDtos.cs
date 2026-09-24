namespace Tcrfc.Api.Features.AdminStaff;

/// <summary>C3 教練與團隊成員單一語系內容（<c>staff_i18n</c>）。</summary>
public sealed record AdminStaffLocaleContent
{
    public required string Name { get; init; }
    public string? Title { get; init; }
    public string? Bio { get; init; }
}

public sealed record AdminStaffContentInput
{
    public required AdminStaffLocaleContent Zh { get; init; }
    public AdminStaffLocaleContent? En { get; init; }
}

/// <summary>一筆球隊指派（<c>staff_teams</c>），供教練「負責梯隊」使用。</summary>
public sealed record AdminStaffTeamAssignmentInput
{
    public required Guid TeamId { get; init; }
    public string? RoleCode { get; init; }
}

public sealed record AdminStaffTeamAssignmentDto
{
    public required Guid TeamId { get; init; }
    public required string TeamCode { get; init; }
    public string? RoleCode { get; init; }
}

public sealed record AdminStaffListItemDto
{
    public required Guid Id { get; init; }

    /// <summary>true＝這筆是兩隊共同資料（<c>club_id IS NULL</c>）——比照公開端點
    /// <c>Features/Staff/StaffDto.cs</c> 同一個欄位的說明。共用列在這個俱樂部範圍的清單裡
    /// **仍會出現**（讓維護人員看得到自己隊上也共用哪些人），但不可編輯，見
    /// <see cref="SharedStaffReadOnlyException"/>。</summary>
    public required bool IsShared { get; init; }
    public string? StaffGroup { get; init; }
    public string? Licence { get; init; }
    public string? PhotoKey { get; init; }

    /// <summary>肖像同意狀態（S1-7a），同 <c>AdminPlayerListItemDto.PortraitConsentStatus</c>。</summary>
    public required string PortraitConsentStatus { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required IReadOnlyList<string> TeamCodes { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminStaffDetailDto
{
    public required Guid Id { get; init; }
    public required bool IsShared { get; init; }
    public string? StaffGroup { get; init; }
    public string? Licence { get; init; }
    public string? PhotoKey { get; init; }
    public required string PortraitConsentStatus { get; init; }
    public required AdminStaffLocaleContent Zh { get; init; }
    public AdminStaffLocaleContent? En { get; init; }
    public required IReadOnlyList<AdminStaffTeamAssignmentDto> Teams { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 🔴 後台建立一律歸屬呼叫端當下的俱樂部（<c>staff.club_id = scope.ClubId</c>），不接受建立
/// 「共同（<c>club_id</c> 為空）」的教練或團隊成員——跟 <c>AdminArticlesRepository.CreateAsync</c>
/// 對 <c>articles</c>（同屬 9 張可為空表）的既有處理方式完全一致，見該檔案上的說明與
/// docs/14-invariants.md「共同內容...只有超管能建立」。<c>StaffGroup</c> 值域見
/// <c>AdminStaffRepository.AllowedStaffGroups</c>（管理層／行政／醫療／後勤，主站規劃書 §4.3 C3；
/// 「顧問」職稱歸入「管理層」是 S0-3c 已拍板的執行層決定，不在這裡的驗證邏輯裡，屬填值選擇）。
/// </summary>
public sealed record CreateAdminStaffRequest
{
    public string? StaffGroup { get; init; }
    public string? Licence { get; init; }

    /// <summary>肖像同意狀態（S1-7a）。省略時預設 <c>not_consented</c>（fail-closed），理由與
    /// 值域比照 <c>Features/AdminPlayers/CreateAdminPlayerRequest.PortraitConsentStatus</c>。</summary>
    public string? PortraitConsentStatus { get; init; }
    public required AdminStaffContentInput Content { get; init; }
    public IReadOnlyList<AdminStaffTeamAssignmentInput>? Teams { get; init; }
}

public sealed record UpdateAdminStaffRequest
{
    public string? StaffGroup { get; init; }
    public string? Licence { get; init; }
    public string? PortraitConsentStatus { get; init; }
    public required AdminStaffContentInput Content { get; init; }
    public IReadOnlyList<AdminStaffTeamAssignmentInput>? Teams { get; init; }

    /// <summary>true＝移除目前的照片，不接受同時夾帶新檔案（比照 <c>UpdateArticleRequest.RemoveCover</c>）。</summary>
    public bool RemovePhoto { get; init; }
}
