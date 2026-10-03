using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.CharityPlatform.Admin;

/// <summary>
/// 慈善後台「帳號管理」（對應主站 J1 <c>AdminAccountsRepository</c>，但讀寫慈善庫獨立帳號體系）。
/// 🔴 每個寫入方法的第一個參數是 <see cref="CharityAdminScope"/>（型別層強制「先通過授權」）；
/// 🔴 稽核與資料變更在同一次 <c>SaveChanges</c> 提交（<see cref="CharityAuditLogger.Stage"/>）；
/// 🔴 稽核摘要不含密碼或任何雜湊。沒有刪除帳號的操作（只能停用，與主站相同），所以「不能刪自己」以此保證。
/// </summary>
public sealed class CharityAdminAccountsService(CharityDbContext db, CharityAuditLogger audit)
{
    // ───────────────────────────── 讀取 ─────────────────────────────

    public async Task<PagedResult<CharityAdminAccountListItemDto>> ListAsync(
        string? status, string? keyword, int page, int pageSize, CancellationToken ct)
    {
        var query = db.AdminUsers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(u => u.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(u => u.Username.Contains(keyword) || u.DisplayName.Contains(keyword));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(u => u.Username).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new
            {
                u.Id, u.Username, u.DisplayName, u.Email, u.Status, u.IsSuperAdmin, u.MustChangePassword,
                u.TwoFactorEnabled, u.LastLoginAt, u.UpdatedAt,
                RoleCodes = u.AdminRoles.Select(r => r.Code).ToList(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new CharityAdminAccountListItemDto
        {
            Id = r.Id, Username = r.Username, DisplayName = r.DisplayName, Email = r.Email, Status = r.Status,
            IsSuperAdmin = r.IsSuperAdmin, MustChangePassword = r.MustChangePassword, TwoFactorEnabled = r.TwoFactorEnabled,
            PrimaryClubId = null, LastLoginAt = r.LastLoginAt, RoleCodes = r.RoleCodes.OrderBy(c => c).ToList(), UpdatedAt = r.UpdatedAt,
        }).ToList();

        return new PagedResult<CharityAdminAccountListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<CharityAdminAccountDetailDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var user = await db.AdminUsers.AsNoTracking().Include(u => u.AdminRoles).FirstOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? null : ToDetail(user);
    }

    // ───────────────────────────── 寫入 ─────────────────────────────

    public async Task<CharityAdminAccountDetailDto> CreateAsync(
        CharityAdminScope scope, CreateCharityAdminAccountRequest request, string? sourceIp, CancellationToken ct)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        ValidateUsername(username);
        ValidateDisplayName(request.DisplayName);
        AdminAuthService.ValidatePasswordPolicy(request.InitialPassword ?? string.Empty, username);

        if (await db.AdminUsers.AsNoTracking().AnyAsync(u => u.Username == username, ct))
        {
            throw new CharityConflictException("帳號重複", $"帳號「{username}」已經被使用，請換一個。");
        }

        var roles = await ResolveRolesAsync(request.RoleCodes, ct);
        var now = DateTime.UtcNow;
        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = request.DisplayName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            PasswordHash = PasswordHasher.Hash(request.InitialPassword!),
            MustChangePassword = false, // 與主站相同：改密碼為選用，只有管理員代為重設時才設為 true。
            Status = "active",
            IsSuperAdmin = request.IsSuperAdmin,
            TwoFactorEnabled = false,
            CreatedAt = now, UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        foreach (var role in roles)
        {
            user.AdminRoles.Add(role);
        }

        db.AdminUsers.Add(user);
        audit.Stage(scope, CharityAuditActions.AdminAccountCreate, CharityAuditTargets.AdminAccount, user.Id,
            $"建立帳號，系統管理員：{(user.IsSuperAdmin ? "是" : "否")}，角色 {roles.Count} 個", null, sourceIp);
        await db.SaveChangesAsync(ct);

        return (await GetByIdAsync(user.Id, ct))!;
    }

    public async Task<CharityAdminAccountDetailDto?> UpdateAsync(
        CharityAdminScope scope, Guid id, UpdateCharityAdminAccountRequest request, string? sourceIp, CancellationToken ct)
    {
        ValidateDisplayName(request.DisplayName);
        var user = await db.AdminUsers.Include(u => u.AdminRoles).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return null;
        }

        // 🔴 把最後一位啟用中的系統管理員降級，會讓系統歸零到沒有人能做系統管理（包含復原這個誤操作）。
        if (user.IsSuperAdmin && !request.IsSuperAdmin && user.Status == "active")
        {
            await EnsureNotLastActiveSuperAdminAsync(user.Id, ct);
        }

        var roles = await ResolveRolesAsync(request.RoleCodes, ct);
        var summary = $"更新帳號資料，系統管理員：{(request.IsSuperAdmin ? "是" : "否")}，角色 {roles.Count} 個";

        user.DisplayName = request.DisplayName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        user.IsSuperAdmin = request.IsSuperAdmin;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = scope.Identity.AdminUserId;
        user.AdminRoles.Clear();
        foreach (var role in roles)
        {
            user.AdminRoles.Add(role);
        }

        audit.Stage(scope, CharityAuditActions.AdminAccountUpdate, CharityAuditTargets.AdminAccount, user.Id, summary, null, sourceIp);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    /// <summary>啟用／停用。停用時立即撤銷該帳號所有有效的更新權杖（存取權杖最長 15 分鐘自然過期）。</summary>
    public async Task<CharityAdminAccountDetailDto?> SetStatusAsync(
        CharityAdminScope scope, Guid id, string status, string? sourceIp, CancellationToken ct)
    {
        if (status is not ("active" or "disabled"))
        {
            throw new AdminValidationException("帳號狀態只能是「active」或「disabled」。");
        }

        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return null;
        }

        if (status == "disabled" && user.IsSuperAdmin && user.Status == "active")
        {
            await EnsureNotLastActiveSuperAdminAsync(user.Id, ct);
        }

        user.Status = status;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = scope.Identity.AdminUserId;
        if (status == "disabled")
        {
            await RevokeAllRefreshTokensAsync(user.Id, ct);
        }

        audit.Stage(scope, CharityAuditActions.AdminAccountStatus, CharityAuditTargets.AdminAccount, user.Id,
            status == "disabled" ? "停用帳號" : "啟用帳號", null, sourceIp);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    /// <summary>管理員代為重設密碼（比照主站：撤銷既有工作階段、清鎖定、<c>must_change_password=true</c> 僅作提示）。</summary>
    public async Task<bool?> ResetPasswordAsync(
        CharityAdminScope scope, Guid id, string newPassword, string? sourceIp, CancellationToken ct)
    {
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return null;
        }

        AdminAuthService.ValidatePasswordPolicy(newPassword ?? string.Empty, user.Username);

        user.PasswordHash = PasswordHasher.Hash(newPassword!);
        user.MustChangePassword = true;
        user.PasswordChangedAt = DateTime.UtcNow;
        user.FailedAttemptCount = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = scope.Identity.AdminUserId;

        await RevokeAllRefreshTokensAsync(user.Id, ct);
        audit.Stage(scope, CharityAuditActions.AdminAccountResetPassword, CharityAuditTargets.AdminAccount, user.Id,
            "管理員代為重設密碼", null, sourceIp);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>管理員代為重設兩階段驗證（例如驗證器裝置遺失）。</summary>
    public async Task<bool?> ResetTwoFactorAsync(CharityAdminScope scope, Guid id, string? sourceIp, CancellationToken ct)
    {
        var user = await db.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
        {
            return null;
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecretEncrypted = null;
        user.TwoFactorConfirmedAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = scope.Identity.AdminUserId;

        await RevokeAllRefreshTokensAsync(user.Id, ct);
        audit.Stage(scope, CharityAuditActions.AdminAccountResetTotp, CharityAuditTargets.AdminAccount, user.Id,
            "管理員代為重設兩階段驗證", null, sourceIp);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private async Task RevokeAllRefreshTokensAsync(Guid adminUserId, CancellationToken ct)
    {
        var tokens = await db.AdminRefreshTokens.Where(t => t.AdminUserId == adminUserId && t.RevokedAt == null).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }
    }

    private async Task EnsureNotLastActiveSuperAdminAsync(Guid excludeAdminUserId, CancellationToken ct)
    {
        var others = await db.AdminUsers.AsNoTracking()
            .CountAsync(u => u.Id != excludeAdminUserId && u.IsSuperAdmin && u.Status == "active", ct);
        if (others == 0)
        {
            throw new CharityConflictException("操作被擋下", "系統至少要保留一個啟用中的最高管理權限帳號，這個操作會讓系統歸零，已被擋下。");
        }
    }

    private async Task<List<AdminRole>> ResolveRolesAsync(IReadOnlyList<string>? roleCodes, CancellationToken ct)
    {
        if (roleCodes is null || roleCodes.Count == 0)
        {
            return [];
        }

        var codes = roleCodes.Distinct().ToList();
        var roles = await db.AdminRoles.Where(r => codes.Contains(r.Code)).ToListAsync(ct);
        if (roles.Count != codes.Count)
        {
            var missing = codes.Except(roles.Select(r => r.Code));
            throw new AdminValidationException($"找不到角色代碼：{string.Join("、", missing)}。");
        }

        return roles;
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new AdminValidationException("帳號為必填欄位。");
        }
        if (username.Length > 64)
        {
            throw new AdminValidationException("帳號長度不能超過 64 個字元。");
        }
        if (username.Any(char.IsWhiteSpace))
        {
            throw new AdminValidationException("帳號不能包含空白字元。");
        }
    }

    private static void ValidateDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new AdminValidationException("顯示名稱為必填欄位。");
        }
        if (displayName.Trim().Length > 100)
        {
            throw new AdminValidationException("顯示名稱長度不能超過 100 個字元。");
        }
    }

    private static CharityAdminAccountDetailDto ToDetail(AdminUser user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        DisplayName = user.DisplayName,
        Email = user.Email,
        Status = user.Status,
        IsSuperAdmin = user.IsSuperAdmin,
        MustChangePassword = user.MustChangePassword,
        TwoFactorEnabled = user.TwoFactorEnabled,
        PrimaryClubId = null,
        Locale = null,
        LastLoginAt = user.LastLoginAt,
        RoleCodes = user.AdminRoles.Select(r => r.Code).OrderBy(c => c).ToList(),
        ClubGrants = [],
        TeamGrants = [],
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
    };
}
