using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminAccounts;

/// <summary>
/// J1 帳號管理 ＋ J4 的「後台帳號的俱樂部授權」（<c>admin_user_clubs</c>）與「球隊授權」
/// （<c>admin_user_teams</c>），兩者都掛在帳號底下維護，理由見
/// docs/12b-database-tables.md §5.3「為什麼授權掛在人不是角色」。
///
/// ⛔ 全程不回傳 <c>password_hash</c>／<c>two_factor_secret_encrypted</c>——DTO 本身就沒有這兩個
/// 屬性，不是「回傳時特意過濾」，從型別上就不給任何呼叫端有機會外洩（見 AdminAccountDtos.cs）。
/// </summary>
public sealed class AdminAccountsRepository(ClubDbContext dbContext)
{
    // ───────────────────────────── 讀取 ─────────────────────────────

    public async Task<PagedResult<AdminAccountListItemDto>> ListAsync(
        string? status, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.AdminUsers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(u => u.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(u => u.Username.Contains(keyword) || u.DisplayName.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(u => u.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.DisplayName,
                u.Email,
                u.Status,
                u.IsSuperAdmin,
                u.MustChangePassword,
                u.TwoFactorEnabled,
                u.PrimaryClubId,
                u.LastLoginAt,
                u.UpdatedAt,
                RoleCodes = u.AdminRoles.Select(r => r.Code).ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new AdminAccountListItemDto
        {
            Id = r.Id,
            Username = r.Username,
            DisplayName = r.DisplayName,
            Email = r.Email,
            Status = r.Status,
            IsSuperAdmin = r.IsSuperAdmin,
            MustChangePassword = r.MustChangePassword,
            TwoFactorEnabled = r.TwoFactorEnabled,
            PrimaryClubId = r.PrimaryClubId,
            LastLoginAt = r.LastLoginAt,
            RoleCodes = r.RoleCodes,
            UpdatedAt = r.UpdatedAt,
        }).ToList();

        return new PagedResult<AdminAccountListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<AdminAccountDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await dbContext.AdminUsers.AsNoTracking()
            .Include(u => u.AdminRoles)
            .Include(u => u.AdminUserClubAdminUsers).ThenInclude(g => g.Club)
            .Include(u => u.AdminUserTeams).ThenInclude(g => g.Team).ThenInclude(t => t.Club)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return user is null ? null : ToDetailDto(user);
    }

    // ───────────────────────────── 寫入：帳號本身 ─────────────────────────────

    public async Task<AdminAccountDetailDto> CreateAsync(
        CreateAdminAccountRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateUsername(request.Username);
        AdminAuthService.ValidatePasswordPolicy(request.InitialPassword, request.Username);

        if (await dbContext.AdminUsers.AsNoTracking().AnyAsync(u => u.Username == request.Username, cancellationToken))
        {
            throw new AdminAccountUsernameConflictException(request.Username);
        }

        if (request.PrimaryClubId is Guid primaryClubId)
        {
            await EnsureClubExistsAsync(primaryClubId, cancellationToken);
        }

        var roles = await ResolveRolesAsync(request.RoleCodes, cancellationToken);

        var now = DateTime.UtcNow;
        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            DisplayName = request.DisplayName,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email,
            PrimaryClubId = request.PrimaryClubId,
            PasswordHash = PasswordHasher.Hash(request.InitialPassword),
            // 🔴 一律強制首次登入改密（見 CreateAdminAccountRequest 上的說明），不接受呼叫端指定 false。
            MustChangePassword = true,
            Status = "active",
            IsSuperAdmin = request.IsSuperAdmin,
            TwoFactorEnabled = false,
            Locale = request.Locale,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        foreach (var role in roles)
        {
            user.AdminRoles.Add(role);
        }

        dbContext.AdminUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(user.Id, cancellationToken))!;
    }

    public async Task<AdminAccountDetailDto?> UpdateAsync(
        Guid id, UpdateAdminAccountRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var user = await dbContext.AdminUsers
            .Include(u => u.AdminRoles)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (request.PrimaryClubId is Guid primaryClubId)
        {
            await EnsureClubExistsAsync(primaryClubId, cancellationToken);
        }

        // 🔴 防呆（task 5，執行層安全措施，規劃書未明文）：把最後一個啟用中的超管帳號降級，
        // 會讓系統歸零到沒有任何最高管理權限的帳號可以做系統管理操作，包含把這個誤操作本身復原。
        if (user.IsSuperAdmin && !request.IsSuperAdmin && user.Status == "active")
        {
            await EnsureNotLastActiveSuperAdminAsync(excludeAdminUserId: user.Id, cancellationToken);
        }

        var roles = await ResolveRolesAsync(request.RoleCodes, cancellationToken);

        user.DisplayName = request.DisplayName;
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email;
        user.PrimaryClubId = request.PrimaryClubId;
        user.IsSuperAdmin = request.IsSuperAdmin;
        user.Locale = request.Locale;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = operatorId;

        user.AdminRoles.Clear();
        foreach (var role in roles)
        {
            user.AdminRoles.Add(role);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>啟用／停用帳號。停用時**立即撤銷這個帳號名下所有仍有效的更新權杖**
    /// （task 1：「停用帳號要讓既有更新權杖失效」）——存取權杖本身最長 15 分鐘就會自然過期
    /// （AdminTokenService.AccessTokenLifetime），但更新權杖有效期是 14 天，若不主動撤銷，
    /// 被停用的帳號仍能在 15 分鐘內用舊存取權杖操作、並在它過期後用更新權杖換到新的存取權杖，
    /// 停用等於沒有立即生效。</summary>
    public async Task<AdminAccountDetailDto?> SetStatusAsync(
        Guid id, string status, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (status is not ("active" or "disabled"))
        {
            throw new AdminAccountValidationException("帳號狀態只能是「active」或「disabled」。");
        }

        var user = await dbContext.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (status == "disabled" && user.IsSuperAdmin && user.Status == "active")
        {
            await EnsureNotLastActiveSuperAdminAsync(excludeAdminUserId: user.Id, cancellationToken);
        }

        user.Status = status;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = operatorId;

        if (status == "disabled")
        {
            await RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    /// <summary>系統管理員代為重設密碼。比照停用帳號，撤銷既有工作階段——密碼可能是因為
    /// 懷疑外洩才被重設，讓舊的更新權杖繼續有效會讓重設密碼這個動作失去意義。</summary>
    public async Task<bool?> ResetPasswordAsync(
        Guid id, string newPassword, Guid? operatorId, CancellationToken cancellationToken)
    {
        var user = await dbContext.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        AdminAuthService.ValidatePasswordPolicy(newPassword, user.Username);

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.MustChangePassword = true;
        user.PasswordChangedAt = DateTime.UtcNow;
        user.FailedAttemptCount = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = operatorId;

        await RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>系統管理員代為重設 2FA（例如驗證器裝置遺失）。清空既有密鑰與確認時間，
    /// 帳號回到「尚未完成 2FA 設定」狀態——下次登入會被 <see cref="Security.AdminClubAuthorizer"/>／
    /// <see cref="Security.AdminSystemAuthorizer"/> 擋在俱樂部範圍與系統管理端點之外，
    /// 直到重新走一次 <c>/auth/2fa/setup</c>＋<c>/auth/2fa/confirm</c>。同樣撤銷既有工作階段。</summary>
    public async Task<bool?> ResetTwoFactorAsync(Guid id, Guid? operatorId, CancellationToken cancellationToken)
    {
        var user = await dbContext.AdminUsers.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecretEncrypted = null;
        user.TwoFactorConfirmedAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = operatorId;

        await RevokeAllRefreshTokensAsync(user.Id, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ───────────────────────────── 寫入：俱樂部授權（J4） ─────────────────────────────

    public async Task<IReadOnlyList<AdminAccountClubGrantDto>?> ListClubGrantsAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        if (!await dbContext.AdminUsers.AsNoTracking().AnyAsync(u => u.Id == adminUserId, cancellationToken))
        {
            return null;
        }

        var grants = await dbContext.AdminUserClubs.AsNoTracking()
            .Include(g => g.Club)
            .Where(g => g.AdminUserId == adminUserId)
            .OrderBy(g => g.Club.Code)
            .ToListAsync(cancellationToken);

        return grants.Select(ToGrantDto).ToList();
    }

    /// <summary>新增或重新啟用一筆俱樂部授權（PK 是 <c>(admin_user_id, club_id)</c>，已存在就更新
    /// 起訖日並重新設回啟用——例如先前撤銷過的授權要重新開通，不需要先手動刪除舊列）。
    /// 立即生效：<see cref="Security.AdminClubAuthorizer"/> 每個請求都即時查
    /// <c>admin_user_clubs</c>，不經過任何快取或 JWT claims（見該檔案的說明）。</summary>
    public async Task<AdminAccountClubGrantDto?> UpsertClubGrantAsync(
        Guid adminUserId, CreateAdminAccountClubGrantRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (!await dbContext.AdminUsers.AsNoTracking().AnyAsync(u => u.Id == adminUserId, cancellationToken))
        {
            return null;
        }

        await EnsureClubExistsAsync(request.ClubId, cancellationToken);

        var grantedOn = request.GrantedOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.ExpiresOn is DateOnly expiresOn && expiresOn < grantedOn)
        {
            throw new AdminAccountValidationException("到期日不能早於授權起日。");
        }

        var grant = await dbContext.AdminUserClubs
            .Include(g => g.Club)
            .FirstOrDefaultAsync(g => g.AdminUserId == adminUserId && g.ClubId == request.ClubId, cancellationToken);

        if (grant is null)
        {
            grant = new AdminUserClub { AdminUserId = adminUserId, ClubId = request.ClubId };
            dbContext.AdminUserClubs.Add(grant);
        }

        grant.GrantedOn = grantedOn;
        grant.ExpiresOn = request.ExpiresOn;
        grant.GrantedBy = operatorId;
        grant.IsActive = true;

        await dbContext.SaveChangesAsync(cancellationToken);

        // Club 導覽在新增列時尚未載入（Add 進去的是新物件，沒有 Include 過），重新查一次確保
        // ToGrantDto 拿得到 Club.Code——追蹤中的實體重查會直接用 change tracker 裡的資料，不多打一次 SQL 往返资料本体。
        await dbContext.Entry(grant).Reference(g => g.Club).LoadAsync(cancellationToken);
        return ToGrantDto(grant);
    }

    /// <summary>撤銷：<c>is_active = false</c>（軟撤銷，保留 <c>granted_on</c>／<c>granted_by</c>
    /// 的歷史紀錄），不是硬刪除整列——見規劃書 5.3「一個人可授權多個俱樂部」的關聯表設計，
    /// 硬刪除會讓「這個人這個俱樂部什麼時候曾經被授權過」這件事完全不可考。立即生效理由同上。</summary>
    public async Task<bool?> RevokeClubGrantAsync(Guid adminUserId, Guid clubId, CancellationToken cancellationToken)
    {
        var grant = await dbContext.AdminUserClubs
            .FirstOrDefaultAsync(g => g.AdminUserId == adminUserId && g.ClubId == clubId, cancellationToken);

        if (grant is null)
        {
            return null;
        }

        grant.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ───────────────────────────── 寫入：球隊授權（J4，主站規劃書第 1223–1231 行） ─────────────────────────────

    public async Task<IReadOnlyList<AdminAccountTeamGrantDto>?> ListTeamGrantsAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        if (!await dbContext.AdminUsers.AsNoTracking().AnyAsync(u => u.Id == adminUserId, cancellationToken))
        {
            return null;
        }

        var grants = await dbContext.AdminUserTeams.AsNoTracking()
            .Include(g => g.Team).ThenInclude(t => t.Club)
            .Where(g => g.AdminUserId == adminUserId)
            .OrderBy(g => g.Team.Club.Code).ThenBy(g => g.Team.Code)
            .ToListAsync(cancellationToken);

        return grants.Select(ToTeamGrantDto).ToList();
    }

    /// <summary>
    /// 新增或重新啟用一筆球隊授權（PK 是 <c>(admin_user_id, team_id)</c>，比照
    /// <see cref="UpsertClubGrantAsync"/> 的 upsert 語意）。
    ///
    /// 🔴 **只能授權這個帳號目前有效俱樂部授權範圍內的球隊**（coordinator 指示）：球隊授權是
    /// 俱樂部授權底下更細的列級限制，不該讓一個帳號拿到「連俱樂部本身都沒被授權」的球隊——
    /// 那種狀態沒有任何實際意義（<see cref="Security.AdminClubAuthorizer"/> 一開始就會在俱樂部
    /// 範圍那一關擋下這個帳號，球隊授權完全用不到）。「目前有效」＝
    /// <c>admin_user_clubs.is_active</c> 且未到期，跟 docs/12b §7.1b「有效範圍」的定義一致。
    /// ⚠️ **本方法只維護授權資料本身，不做任何列級強制**（例如 C4 賽程寫入檢查
    /// <c>own_teams</c>）——C4 的寫入端點本輪尚未存在，強制點留給那一輪一併實作，見
    /// apps/api/README.md 的說明。
    ///
    /// 立即生效：跟俱樂部授權一樣，任何讀取本表的檢查點都應該即時查表，不經過快取或 JWT claims。
    /// </summary>
    public async Task<AdminAccountTeamGrantDto?> UpsertTeamGrantAsync(
        Guid adminUserId, CreateAdminAccountTeamGrantRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.AdminUsers.AsNoTracking().AnyAsync(u => u.Id == adminUserId, cancellationToken))
        {
            return null;
        }

        var team = await dbContext.Teams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TeamId, cancellationToken);
        if (team is null)
        {
            throw new AdminAccountValidationException($"找不到球隊（id={request.TeamId}）。");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hasEffectiveClubGrant = await dbContext.AdminUserClubs.AsNoTracking()
            .AnyAsync(g => g.AdminUserId == adminUserId && g.ClubId == team.ClubId && g.IsActive
                        && (g.ExpiresOn == null || g.ExpiresOn >= today), cancellationToken);
        if (!hasEffectiveClubGrant)
        {
            throw new AdminAccountValidationException(
                "只能授權這個帳號目前有效俱樂部授權範圍內的球隊，請先確認該帳號已被授權這支球隊所屬的俱樂部。");
        }

        var grant = await dbContext.AdminUserTeams
            .Include(g => g.Team).ThenInclude(t => t.Club)
            .FirstOrDefaultAsync(g => g.AdminUserId == adminUserId && g.TeamId == request.TeamId, cancellationToken);

        if (grant is null)
        {
            grant = new AdminUserTeam { AdminUserId = adminUserId, TeamId = request.TeamId };
            dbContext.AdminUserTeams.Add(grant);
        }

        grant.ExpiresOn = request.ExpiresOn;
        grant.IsActive = true;

        await dbContext.SaveChangesAsync(cancellationToken);

        // 同 UpsertClubGrantAsync：新增列時導覽尚未載入，重新查一次確保 ToTeamGrantDto 拿得到
        // Team.Code／Team.Club.Code。
        await dbContext.Entry(grant).Reference(g => g.Team).LoadAsync(cancellationToken);
        await dbContext.Entry(grant.Team).Reference(t => t.Club).LoadAsync(cancellationToken);
        return ToTeamGrantDto(grant);
    }

    /// <summary>撤銷：<c>is_active = false</c>（軟撤銷），立即生效，理由同 <see cref="RevokeClubGrantAsync"/>。</summary>
    public async Task<bool?> RevokeTeamGrantAsync(Guid adminUserId, Guid teamId, CancellationToken cancellationToken)
    {
        var grant = await dbContext.AdminUserTeams
            .FirstOrDefaultAsync(g => g.AdminUserId == adminUserId && g.TeamId == teamId, cancellationToken);

        if (grant is null)
        {
            return null;
        }

        grant.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ───────────────────────────── 內部工具 ─────────────────────────────

    private async Task RevokeAllRefreshTokensAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.AdminRefreshTokens
            .Where(t => t.AdminUserId == adminUserId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }
    }

    /// <summary>丟例外如果「排除掉 <paramref name="excludeAdminUserId"/> 之後」沒有其他啟用中的
    /// 超管帳號——用於「這個操作會不會讓超管歸零」的判斷，呼叫端在真正套用變更之前先呼叫。</summary>
    private async Task EnsureNotLastActiveSuperAdminAsync(Guid excludeAdminUserId, CancellationToken cancellationToken)
    {
        var otherActiveSuperAdmins = await dbContext.AdminUsers.AsNoTracking()
            .CountAsync(u => u.Id != excludeAdminUserId && u.IsSuperAdmin && u.Status == "active", cancellationToken);

        if (otherActiveSuperAdmins == 0)
        {
            throw new AdminAccountLastSuperAdminException();
        }
    }

    private async Task EnsureClubExistsAsync(Guid clubId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Clubs.AsNoTracking().AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            throw new AdminAccountValidationException($"找不到俱樂部（id={clubId}）。");
        }
    }

    private async Task<List<AdminRole>> ResolveRolesAsync(IReadOnlyList<string> roleCodes, CancellationToken cancellationToken)
    {
        if (roleCodes.Count == 0)
        {
            return [];
        }

        var distinctCodes = roleCodes.Distinct().ToList();
        var roles = await dbContext.AdminRoles.Where(r => distinctCodes.Contains(r.Code)).ToListAsync(cancellationToken);

        if (roles.Count != distinctCodes.Count)
        {
            var missing = distinctCodes.Except(roles.Select(r => r.Code));
            throw new AdminAccountValidationException($"找不到角色代碼：{string.Join("、", missing)}。");
        }

        return roles;
    }

    private static void ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new AdminAccountValidationException("帳號為必填欄位。");
        }
        if (username.Length > 64)
        {
            throw new AdminAccountValidationException("帳號長度不能超過 64 個字元。");
        }
        if (username.Any(char.IsWhiteSpace))
        {
            throw new AdminAccountValidationException("帳號不能包含空白字元。");
        }
    }

    private static AdminAccountClubGrantDto ToGrantDto(AdminUserClub grant)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new AdminAccountClubGrantDto
        {
            ClubId = grant.ClubId,
            ClubCode = grant.Club.Code,
            GrantedOn = grant.GrantedOn,
            ExpiresOn = grant.ExpiresOn,
            IsActive = grant.IsActive,
            IsCurrentlyEffective = grant.IsActive && (grant.ExpiresOn is null || grant.ExpiresOn >= today),
        };
    }

    private static AdminAccountTeamGrantDto ToTeamGrantDto(AdminUserTeam grant)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new AdminAccountTeamGrantDto
        {
            TeamId = grant.TeamId,
            TeamCode = grant.Team.Code,
            ClubCode = grant.Team.Club.Code,
            ExpiresOn = grant.ExpiresOn,
            IsActive = grant.IsActive,
            IsCurrentlyEffective = grant.IsActive && (grant.ExpiresOn is null || grant.ExpiresOn >= today),
        };
    }

    private static AdminAccountDetailDto ToDetailDto(AdminUser user)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new AdminAccountDetailDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Status = user.Status,
            IsSuperAdmin = user.IsSuperAdmin,
            MustChangePassword = user.MustChangePassword,
            TwoFactorEnabled = user.TwoFactorEnabled,
            PrimaryClubId = user.PrimaryClubId,
            Locale = user.Locale,
            LastLoginAt = user.LastLoginAt,
            RoleCodes = user.AdminRoles.Select(r => r.Code).OrderBy(c => c).ToList(),
            ClubGrants = user.AdminUserClubAdminUsers.Select(g => new AdminAccountClubGrantDto
            {
                ClubId = g.ClubId,
                ClubCode = g.Club.Code,
                GrantedOn = g.GrantedOn,
                ExpiresOn = g.ExpiresOn,
                IsActive = g.IsActive,
                IsCurrentlyEffective = g.IsActive && (g.ExpiresOn is null || g.ExpiresOn >= today),
            }).OrderBy(g => g.ClubCode).ToList(),
            TeamGrants = user.AdminUserTeams.Select(g => new AdminAccountTeamGrantDto
            {
                TeamId = g.TeamId,
                TeamCode = g.Team.Code,
                ClubCode = g.Team.Club.Code,
                ExpiresOn = g.ExpiresOn,
                IsActive = g.IsActive,
                IsCurrentlyEffective = g.IsActive && (g.ExpiresOn is null || g.ExpiresOn >= today),
            }).OrderBy(g => g.ClubCode).ThenBy(g => g.TeamCode).ToList(),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
        };
    }
}
