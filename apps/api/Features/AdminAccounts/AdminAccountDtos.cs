namespace Tcrfc.Api.Features.AdminAccounts;

/// <summary>後台帳號清單一列。⛔ 一律不含 <c>password_hash</c>／<c>two_factor_secret_encrypted</c>
/// 這兩個欄位——任何雜湊或秘密都不得離開伺服器，見 apps/api/README.md 的說明。</summary>
public sealed record AdminAccountListItemDto
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

public sealed record AdminAccountClubGrantDto
{
    public required Guid ClubId { get; init; }
    public required string ClubCode { get; init; }
    public required DateOnly GrantedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public required bool IsActive { get; init; }

    /// <summary>依「有效範圍 ＝ is_active 且未到期」（docs/12b §7.1b）算出來的當下是否仍生效，
    /// 省去前端自己比對日期。</summary>
    public required bool IsCurrentlyEffective { get; init; }
}

/// <summary>
/// J4「球隊授權」（<c>admin_user_teams</c>，主站規劃書第 1223–1231 行 J4 表格）——供「學院管理者
/// 不得改動一線隊賽程」這類**列級**限制使用。⚠️ 本輪只維護授權資料本身，**不做強制**——強制點
/// （例如 C4 賽程寫入時檢查 <c>own_teams</c>）留給 C4 寫入端點實作時一併處理，見 apps/api/README.md。
/// </summary>
public sealed record AdminAccountTeamGrantDto
{
    public required Guid TeamId { get; init; }
    public required string TeamCode { get; init; }
    public required string ClubCode { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsCurrentlyEffective { get; init; }
}

/// <summary><c>admin_user_teams</c> 沒有 <c>granted_on</c>／<c>granted_by</c> 欄位（比 <c>admin_user_clubs</c>
/// 精簡），所以這裡沒有對應的輸入欄位——這是綱要本身的形狀，不是本輪省略。</summary>
public sealed record CreateAdminAccountTeamGrantRequest
{
    public required Guid TeamId { get; init; }

    /// <summary>省略＝無期限。</summary>
    public DateOnly? ExpiresOn { get; init; }
}

/// <summary>後台帳號詳情（編輯頁用）。</summary>
public sealed record AdminAccountDetailDto
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
    public required IReadOnlyList<AdminAccountClubGrantDto> ClubGrants { get; init; }
    public required IReadOnlyList<AdminAccountTeamGrantDto> TeamGrants { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// 建立帳號。✅ **已裁決（2026-09-24，coordinator）**：規劃書 §4.10 J1 只寫「新增／停用帳號、
/// 密碼政策、兩階段驗證」，**不做邀請信**——「建立者指定初始密碼＋強制首次改密」即是定案寫法，
/// 不是暫時的最小可行方案。系統信目前只有 9 封（會員 5＋商店 4，docs/14-invariants.md），本來就
/// 沒有「後台帳號邀請信」樣板。<c>must_change_password</c>
/// 一律強制為 <c>true</c>（比照種子超管 <c>sa@system.local</c> 的既有慣例），初始密碼由建立者
/// 透過站外管道（口頭、既有的內部溝通管道）轉交，不透過系統寄送——見 apps/api/README.md。
/// </summary>
public sealed record CreateAdminAccountRequest
{
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public Guid? PrimaryClubId { get; init; }
    public required string InitialPassword { get; init; }

    /// <summary>是否為系統管理員（<c>is_super_admin</c>，跳過整個資料範圍與權限查詢）。
    /// 只有本身已是系統管理員的呼叫端才能打到這支端點（權限碼 <c>system.account.create</c>
    /// 為 <c>sysadmin_only</c>），所以這裡不需要額外的「只有超管能設超管」檢查。</summary>
    public bool IsSuperAdmin { get; init; }

    /// <summary>角色代碼清單（<c>admin_roles.code</c>），對應 J2 的角色。可為空陣列
    /// （例如純超管帳號可以不掛任何角色，靠 <c>is_super_admin</c> 本身就跳過權限檢查）。</summary>
    public IReadOnlyList<string> RoleCodes { get; init; } = [];

    public string? Locale { get; init; }
}

/// <summary>更新帳號基本資料與角色指派。⛔ 不含密碼／狀態／2FA——那三項各自是獨立端點
/// （重設密碼、停用／啟用、重設 2FA），理由見 apps/api/README.md「為什麼分開」。</summary>
public sealed record UpdateAdminAccountRequest
{
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public Guid? PrimaryClubId { get; init; }
    public bool IsSuperAdmin { get; init; }
    public IReadOnlyList<string> RoleCodes { get; init; } = [];
    public string? Locale { get; init; }
}

public sealed record SetAdminAccountStatusRequest
{
    /// <summary>值域 <c>active</c>／<c>disabled</c>（<c>admin_users.status</c>）。</summary>
    public required string Status { get; init; }
}

public sealed record ResetAdminAccountPasswordRequest
{
    public required string NewPassword { get; init; }
}

public sealed record CreateAdminAccountClubGrantRequest
{
    public required Guid ClubId { get; init; }

    /// <summary>省略則用今天（UTC 日期）。</summary>
    public DateOnly? GrantedOn { get; init; }

    /// <summary>省略＝無期限。</summary>
    public DateOnly? ExpiresOn { get; init; }
}
