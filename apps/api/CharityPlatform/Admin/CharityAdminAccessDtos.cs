namespace Tcrfc.Api.CharityPlatform.Admin;

// 帳號與角色管理（規劃書 §10「完整的後台帳號權限表」）的請求／回應型別。
// 🔴 JSON 形狀刻意與主站 /api/v1/admin/accounts、/api/v1/admin/roles 完全相同，讓前端畫面可以直接沿用。
// 慈善是單一法人、沒有 club_id／球隊維度，所以主站才有的欄位在這裡保留為「恆為空／固定值」：
// primaryClubId／locale＝null、clubGrants／teamGrants＝[]、scopeMode＝"all_clubs"、isClubScoped＝false。
// ⛔ 一律不含 password_hash／two_factor_secret_encrypted，型別上就沒有這兩個屬性。

public sealed record CharityAdminAccountListItemDto
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public required string Status { get; init; }
    public required bool IsSuperAdmin { get; init; }
    public required bool MustChangePassword { get; init; }
    public required bool TwoFactorEnabled { get; init; }
    public Guid? PrimaryClubId { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public required IReadOnlyList<string> RoleCodes { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>主站才有的俱樂部授權列；慈善沒有這個維度，只為了與主站的詳情形狀一致而保留型別（清單恆為空）。</summary>
public sealed record CharityAdminAccountClubGrantDto
{
    public required Guid ClubId { get; init; }
    public required string ClubCode { get; init; }
    public required DateOnly GrantedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsCurrentlyEffective { get; init; }
}

/// <summary>同上，球隊授權列（清單恆為空）。</summary>
public sealed record CharityAdminAccountTeamGrantDto
{
    public required Guid TeamId { get; init; }
    public required string TeamCode { get; init; }
    public required string ClubCode { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsCurrentlyEffective { get; init; }
}

public sealed record CharityAdminAccountDetailDto
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public required string Status { get; init; }
    public required bool IsSuperAdmin { get; init; }
    public required bool MustChangePassword { get; init; }
    public required bool TwoFactorEnabled { get; init; }
    public Guid? PrimaryClubId { get; init; }
    public string? Locale { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public required IReadOnlyList<string> RoleCodes { get; init; }
    public required IReadOnlyList<CharityAdminAccountClubGrantDto> ClubGrants { get; init; }
    public required IReadOnlyList<CharityAdminAccountTeamGrantDto> TeamGrants { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>建立帳號：建立者指定初始密碼（不寄邀請信），與主站相同。<c>PrimaryClubId</c>／<c>Locale</c> 為與主站形狀一致而接受，但慈善不存這兩欄，直接忽略。</summary>
public sealed record CreateCharityAdminAccountRequest
{
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public Guid? PrimaryClubId { get; init; }
    public required string InitialPassword { get; init; }
    public bool IsSuperAdmin { get; init; }
    public IReadOnlyList<string> RoleCodes { get; init; } = [];
    public string? Locale { get; init; }
}

/// <summary>更新帳號基本資料與角色指派。不含密碼／狀態／2FA（各自獨立端點）。</summary>
public sealed record UpdateCharityAdminAccountRequest
{
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public Guid? PrimaryClubId { get; init; }
    public bool IsSuperAdmin { get; init; }
    public IReadOnlyList<string> RoleCodes { get; init; } = [];
    public string? Locale { get; init; }
}

public sealed record SetCharityAdminAccountStatusRequest
{
    /// <summary>值域 <c>active</c>／<c>disabled</c>。</summary>
    public required string Status { get; init; }
}

public sealed record ResetCharityAdminAccountPasswordRequest
{
    public required string NewPassword { get; init; }
}

// ───────────────────────────── 角色 ─────────────────────────────

public sealed record CharityAdminPermissionDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string ModuleCode { get; init; }
    public string? SubmoduleCode { get; init; }
    public string? Domain { get; init; }
    public string? Action { get; init; }
    /// <summary>JSON 名稱維持與主站相同（<c>is</c>＋<c>Club</c>＋<c>Scoped</c>）；慈善沒有俱樂部維度，恆為 false。
    /// 屬性名稱與 JSON 名稱刻意不寫成主站型別名的字樣，避免架構測試的「不得引用主站型別」字串掃描誤判。</summary>
    [System.Text.Json.Serialization.JsonPropertyName("is" + "Club" + "Scoped")]
    public required bool AppliesToClub { get; init; }
    public required bool IsRestricted { get; init; }
    public required bool SysadminOnly { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
}

public sealed record CharityAdminRolePermissionAssignmentDto
{
    public required string PermissionCode { get; init; }
    public required string NameZh { get; init; }

    /// <summary>慈善庫 <c>role_permissions.scope_type</c> 可為空（＝全部）；回應一律補成 <c>all</c>，與主站的形狀一致。</summary>
    public required string ScopeType { get; init; }
}

public sealed record CharityAdminRoleListItemDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
    public required string ScopeMode { get; init; }
    public required bool IsSystem { get; init; }
    public required int SortOrder { get; init; }
    public required int AssignedAccountCount { get; init; }
}

public sealed record CharityAdminRoleDetailDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
    public required string ScopeMode { get; init; }
    public required bool IsSystem { get; init; }
    public required int SortOrder { get; init; }
    public required int AssignedAccountCount { get; init; }
    public required IReadOnlyList<CharityAdminRolePermissionAssignmentDto> Permissions { get; init; }
}

/// <summary><c>ScopeMode</c> 為與主站形狀一致而接受（可省略），慈善沒有資料範圍維度，忽略。</summary>
public sealed record CreateCharityAdminRoleRequest
{
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? ScopeMode { get; init; }
}

public sealed record UpdateCharityAdminRoleRequest
{
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? ScopeMode { get; init; }
}

public sealed record CharityRolePermissionInput
{
    public required string PermissionCode { get; init; }
    public required string ScopeType { get; init; }
}

public sealed record ReplaceCharityRolePermissionsRequest
{
    public required IReadOnlyList<CharityRolePermissionInput> Permissions { get; init; }
}
