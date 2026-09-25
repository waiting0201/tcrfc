namespace Tcrfc.Api.Features.AdminAuth;

public sealed record LoginRequest(string Username, string Password, string? TotpCode);

public enum LoginOutcome
{
    Success,
    TotpRequired,
    InvalidCredentials,
    Locked,
    Disabled,
}

/// <summary>登入服務內部結果——不是 HTTP 回應形狀，端點負責把它映射成狀態碼與 JSON／Cookie。</summary>
public sealed record LoginResult(
    LoginOutcome Outcome,
    Guid? AdminUserId = null,
    string? Username = null,
    bool IsSuperAdmin = false,
    bool MustChangePassword = false,
    bool TwoFactorEnabled = false,
    string? AccessToken = null,
    DateTime? AccessTokenExpiresAtUtc = null,
    string? RefreshTokenRaw = null,
    DateTime? RefreshTokenExpiresAtUtc = null,
    string? Message = null,
    DateTime? LockedUntilUtc = null);

public sealed record LoginResponse(
    string AccessToken, DateTime AccessTokenExpiresAtUtc, bool MustChangePassword,
    bool TwoFactorEnabled, string Username, bool IsSuperAdmin);

public sealed record RefreshResponse(string AccessToken, DateTime AccessTokenExpiresAtUtc);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record TwoFactorSetupResponse(string Secret, string OtpAuthUrl);

public sealed record TwoFactorConfirmRequest(string Code);

public sealed record TwoFactorDisableRequest(string Password);

/// <summary>前端 agent 回報缺口①：站台切換器需要知道「這個登入的人可以切到哪些俱樂部」。
/// ⛔ 不含密碼雜湊、2FA 密文或任何其他機密欄位——這是給前端顯示用的個人檔案，不是帳號管理端點。</summary>
public sealed record MeClubGrantDto
{
    public required string ClubCode { get; init; }
    public string? ClubNameZh { get; init; }
    public string? ClubNameEn { get; init; }

    /// <summary>是否為 <c>AdminUser.primary_club_id</c>——站台切換器的預設選取值
    /// （規劃書 §4.0「站台切換器」：預設選取 primary_club_id）。</summary>
    public required bool IsPrimary { get; init; }

    /// <summary>系統管理員的授權不受 <c>AdminUserClub</c> 限制（規劃書 §6「系統管理員跳過整個
    /// 資料範圍查詢」），這裡固定回傳 <c>null</c>（無到期日）；非系統管理員回傳實際的
    /// <c>AdminUserClub.expires_on</c>。</summary>
    public DateOnly? ExpiresOn { get; init; }
}

public sealed record MeRoleDto
{
    public required string Code { get; init; }
    public required string NameZh { get; init; }
    public string? NameEn { get; init; }
}

/// <summary>S1-10 修正（2026-09-25）新增：這個帳號目前實際持有的一個權限碼——取代前端各模組
/// 手寫「角色→操作」對照表、跟種子腳本手動同步的既有做法（E-39 同類風險，已在
/// <c>useProgramPermissions</c>／<c>useFormsPermissions</c> 發生過兩次，見
/// <c>Security.IPermissionChecker.GetAllHeldPermissionsAsync</c> 檔頭的完整說明）。
/// 🔴 **只給程式判斷用，前端不得顯示**（主站規劃書 §4.0「介面一律日常中文……不顯示……權限碼」）。</summary>
public sealed record MePermissionDto
{
    public required string Code { get; init; }

    /// <summary>這個人透過（可能不只一個）角色，對這個權限碼持有的 <c>role_permissions.scope_type</c>
    /// 原始集合——不是單一合併值。前端若要做「是否受列級限制」的判斷，含 <c>"all"</c> 或
    /// <c>"own_clubs"</c> 即代表這個人對這個權限碼**至少有一個角色**是不受列級限制的（見
    /// <c>GetAllHeldPermissionsAsync</c> 檔頭「不做合併判斷」的說明）；系統管理員一律是
    /// <c>["all"]</c>。</summary>
    public required IReadOnlyList<string> ScopeTypes { get; init; }
}

public sealed record MeResponse
{
    public required Guid AdminUserId { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsSuperAdmin { get; init; }

    /// <summary><c>AdminUser.primary_club_id</c> 解析出來的俱樂部代碼；帳號從未設定預設俱樂部
    /// 時為 <c>null</c>。</summary>
    public string? PrimaryClubCode { get; init; }

    /// <summary>已過濾到期與停用（系統管理員例外——見 <see cref="MeClubGrantDto.ExpiresOn"/> 說明，
    /// 系統管理員一律回傳「全部啟用中的俱樂部」，不是「被明確授權的俱樂部」，兩者對系統管理員
    /// 而言結果相同，但資料來源不同，見 <c>AdminAuthService.GetMeAsync</c>）。</summary>
    public required IReadOnlyList<MeClubGrantDto> ClubGrants { get; init; }

    public required IReadOnlyList<MeRoleDto> Roles { get; init; }

    /// <summary>🔴 **這份清單跟「目前俱樂部」無關**——本系統的角色指派（<c>admin_user_roles</c>）
    /// 與角色的權限指派（<c>role_permissions</c>）都沒有 <c>club_id</c> 維度，一個人對某個權限碼
    /// 持有哪些 <c>scope_type</c> 不會因為切換到哪個俱樂部而改變；真正決定「這個人能不能碰這個
    /// 俱樂部」的是 <see cref="ClubGrants"/>（<c>AdminUserClub</c>）。前端要判斷「在目前這個俱樂部
    /// 能不能做某件事」，同時看這兩份清單：先確認目前俱樂部在 <see cref="ClubGrants"/> 裡，
    /// 再查 <see cref="Permissions"/> 有沒有對應權限碼——**這是本輪判斷**，回報供下一輪前端改接時
    /// 參考，見 apps/api/README.md「S1-10」段。</summary>
    public required IReadOnlyList<MePermissionDto> Permissions { get; init; }
}
