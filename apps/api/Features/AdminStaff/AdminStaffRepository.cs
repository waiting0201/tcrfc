using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminStaff;

/// <summary>C3「教練與團隊成員」——俱樂部範圍 CRUD（主站規劃書 §4.3 C3）。
/// <c>staff.club_id</c> 是 9 張可為空表之一（行政與醫療多為兩隊共同，docs/12b §4.2），
/// 讀取沿用「俱樂部專屬優先、回退共同」（列出時兩者都給，見 <see cref="ListAsync"/>），
/// 寫入時共同列一律唯讀（<see cref="SharedStaffReadOnlyException"/>），逐字比照
/// <c>Features/AdminNews/AdminArticlesRepository.cs</c> 對 <c>articles</c> 的既有處理。
/// 🔴 <c>IQueryCache</c> 只為了寫入後失效——公開唯讀端點 <c>Features/Staff/StaffEndpoints.cs</c>
/// （entity="staff"）已接快取，寫入這裡不失效會讓公開頁面在 TTL 到期前顯示舊資料。只失效
/// <paramref name="scope"/> 當下的俱樂部，不需要跨俱樂部失效——寫入路徑本身就不允許碰共同列
/// （<c>club_id IS NULL</c>），不會有「改了 A 俱樂部的資料卻影響到 B 俱樂部快取」的情況。</summary>
public sealed class AdminStaffRepository(ClubDbContext dbContext, IQueryCache cache)
{
    private static readonly HashSet<string> AllowedStaffGroups =
        new(StringComparer.Ordinal) { "管理層", "行政", "醫療", "後勤" };

    /// <summary>肖像同意狀態值域（S1-7a，db/club-schema.sql <c>CK_staff_portrait_consent_status</c>）。</summary>
    private static readonly HashSet<string> AllowedPortraitConsentStatuses =
        new(StringComparer.Ordinal) { "not_consented", "consented", "consented_by_guardian" };

    public async Task<IReadOnlyList<AdminStaffListItemDto>> ListAsync(
        AdminClubScope scope, Guid? teamId, CancellationToken cancellationToken)
    {
        var query = dbContext.Staff.AsNoTracking()
            .Where(s => s.ClubId == scope.ClubId || s.ClubId == null);

        if (teamId is Guid t)
        {
            query = query.Where(s => s.StaffTeams.Any(st => st.TeamId == t));
        }

        var rows = await query
            .OrderBy(s => s.ClubId == null ? 1 : 0).ThenBy(s => s.RowSeq)
            .Select(s => new
            {
                s.Id,
                IsShared = s.ClubId == null,
                s.StaffGroup,
                s.Licence,
                s.PhotoKey,
                s.PortraitConsentStatus,
                s.UpdatedAt,
                NameZh = s.StaffI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = s.StaffI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                TeamCodes = s.StaffTeams.Select(st => st.Team.Code).ToList(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminStaffListItemDto
        {
            Id = r.Id,
            IsShared = r.IsShared,
            StaffGroup = r.StaffGroup,
            Licence = r.Licence,
            PhotoKey = r.PhotoKey,
            PortraitConsentStatus = r.PortraitConsentStatus,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            TeamCodes = r.TeamCodes,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminStaffDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var staff = await dbContext.Staff.AsNoTracking()
            .Include(s => s.StaffI18ns)
            .Include(s => s.StaffTeams).ThenInclude(st => st.Team)
            .FirstOrDefaultAsync(s => s.Id == id && (s.ClubId == scope.ClubId || s.ClubId == null), cancellationToken);

        return staff is null ? null : ToDetailDto(staff);
    }

    /// <summary>🔴 建立一律歸屬 <paramref name="scope"/> 當下的俱樂部，不接受建立共同
    /// （<c>club_id</c> 為空）資料——見本檔 <c>CreateAdminStaffRequest</c> 上的說明。</summary>
    public async Task<AdminStaffDetailDto> CreateAsync(
        AdminClubScope scope, Guid staffId, CreateAdminStaffRequest request, string? photoKey, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateStaffGroup(request.StaffGroup);
        ValidatePortraitConsentStatus(request.PortraitConsentStatus);
        ValidateContent(request.Content);
        var teams = await ResolveTeamsAsync(scope, request.Teams ?? [], cancellationToken);

        var now = DateTime.UtcNow;
        var staff = new Data.EfEntities.Staff
        {
            Id = staffId,
            ClubId = scope.ClubId,
            StaffGroup = request.StaffGroup,
            Licence = request.Licence,
            PhotoKey = photoKey,
            // 🔴 fail-closed（docs/12 §12 第 32 點），同 AdminPlayersRepository.CreateAsync。
            PortraitConsentStatus = request.PortraitConsentStatus ?? "not_consented",
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Staff.Add(staff);
        AddOrReplaceI18n(staff, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(staff, "en", request.Content.En);
        }

        foreach (var (team, roleCode) in teams)
        {
            dbContext.StaffTeams.Add(new StaffTeam { StaffId = staff.Id, TeamId = team.Id, RoleCode = roleCode });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("staff", scope.ClubCode, cancellationToken);
        return (await GetByIdAsync(scope, staff.Id, cancellationToken))!;
    }

    public async Task<AdminStaffDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminStaffRequest request, StaffPhotoKeyUpdate photoUpdate, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateStaffGroup(request.StaffGroup);
        ValidatePortraitConsentStatus(request.PortraitConsentStatus);
        ValidateContent(request.Content);

        var staff = await dbContext.Staff
            .Include(s => s.StaffI18ns)
            .Include(s => s.StaffTeams)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (staff is null)
        {
            return null;
        }

        if (staff.ClubId is null)
        {
            throw new SharedStaffReadOnlyException();
        }

        if (staff.ClubId != scope.ClubId)
        {
            return null; // 跨俱樂部：回 404，不洩漏存在與否（比照 AdminArticlesRepository 慣例）。
        }

        staff.StaffGroup = request.StaffGroup;
        staff.Licence = request.Licence;
        staff.PortraitConsentStatus = request.PortraitConsentStatus ?? "not_consented";
        staff.UpdatedAt = DateTime.UtcNow;
        staff.UpdatedBy = operatorId;

        if (photoUpdate.Change)
        {
            staff.PhotoKey = photoUpdate.NewKey;
        }

        AddOrReplaceI18n(staff, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = staff.StaffI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(staff, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        // 省略＝維持不變、空陣列＝清空——跟既有 AdminArticlesRepository 的 Tags／Relations 語意
        // 一致（見 apps/api/README.md S1-5 段），不是「null 就是清空」。
        if (request.Teams is not null)
        {
            var teams = await ResolveTeamsAsync(scope, request.Teams, cancellationToken);
            dbContext.StaffTeams.RemoveRange(staff.StaffTeams);
            foreach (var (team, roleCode) in teams)
            {
                dbContext.StaffTeams.Add(new StaffTeam { StaffId = staff.Id, TeamId = team.Id, RoleCode = roleCode });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("staff", scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>🔴 每個指派的球隊都必須屬於 <paramref name="scope"/>.ClubId——教練負責的梯隊
    /// 只能是自己俱樂部的球隊，不允許透過這個俱樂部範圍端點把一位教練指派到別的俱樂部的球隊。</summary>
    private async Task<List<(Team Team, string? RoleCode)>> ResolveTeamsAsync(
        AdminClubScope scope, IReadOnlyList<AdminStaffTeamAssignmentInput> assignments, CancellationToken cancellationToken)
    {
        var result = new List<(Team, string?)>();
        foreach (var assignment in assignments)
        {
            var team = await dbContext.Teams.FirstOrDefaultAsync(t => t.Id == assignment.TeamId, cancellationToken);
            if (team is null || team.ClubId != scope.ClubId)
            {
                throw new AdminStaffValidationException($"找不到這個俱樂部的球隊（id={assignment.TeamId}）。");
            }
            result.Add((team, assignment.RoleCode));
        }
        return result;
    }

    // 🔴 CS0118：`Staff` 在這個檔案裡是型別／命名空間雙重意義的識別字——本檔命名空間
    // `Tcrfc.Api.Features.AdminStaff` 的父層 `Tcrfc.Api.Features` 底下還有一個
    // `Tcrfc.Api.Features.Staff`（公開唯讀端點），裸寫 `Staff` 會被編譯器解成那個命名空間而不是
    // `Data.EfEntities.Staff` 這個實體型別，兩處全部改成完整命名空間。
    private void AddOrReplaceI18n(Data.EfEntities.Staff staff, string locale, AdminStaffLocaleContent content)
    {
        var existing = staff.StaffI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new StaffI18n { StaffId = staff.Id, Locale = locale };
            staff.StaffI18ns.Add(existing);
            dbContext.StaffI18ns.Add(existing);
        }

        existing.Name = content.Name;
        existing.Title = content.Title;
        existing.Bio = content.Bio;
    }

    private static void ValidateStaffGroup(string? staffGroup)
    {
        if (staffGroup is not null && !AllowedStaffGroups.Contains(staffGroup))
        {
            throw new AdminStaffValidationException("分組只能是「管理層」「行政」「醫療」或「後勤」。");
        }
    }

    private static void ValidatePortraitConsentStatus(string? portraitConsentStatus)
    {
        if (portraitConsentStatus is not null && !AllowedPortraitConsentStatuses.Contains(portraitConsentStatus))
        {
            throw new AdminStaffValidationException(
                "肖像同意狀態只能是「not_consented」（未同意）、「consented」（本人已同意）或" +
                "「consented_by_guardian」（監護人已同意）。");
        }
    }

    private static void ValidateContent(AdminStaffContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Name))
        {
            throw new AdminStaffValidationException("中文姓名為必填欄位。");
        }
    }

    private static AdminStaffDetailDto ToDetailDto(Data.EfEntities.Staff staff)
    {
        var zh = staff.StaffI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = staff.StaffI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminStaffDetailDto
        {
            Id = staff.Id,
            IsShared = staff.ClubId is null,
            StaffGroup = staff.StaffGroup,
            Licence = staff.Licence,
            PhotoKey = staff.PhotoKey,
            PortraitConsentStatus = staff.PortraitConsentStatus,
            Zh = new AdminStaffLocaleContent { Name = zh?.Name ?? "", Title = zh?.Title, Bio = zh?.Bio },
            En = en is null ? null : new AdminStaffLocaleContent { Name = en.Name ?? "", Title = en.Title, Bio = en.Bio },
            Teams = staff.StaffTeams
                .Select(st => new AdminStaffTeamAssignmentDto { TeamId = st.TeamId, TeamCode = st.Team.Code, RoleCode = st.RoleCode })
                .ToList(),
            CreatedAt = staff.CreatedAt,
            UpdatedAt = staff.UpdatedAt,
        };
    }
}
