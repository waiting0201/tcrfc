using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminPrograms;

/// <summary>P1「課程／營隊項目」——俱樂部範圍 CRUD（主站規劃書 §4.4 P1）。
///
/// 🔴 <c>programs.club_id</c> 是 50 張必填表之一（docs/12 §5.4），跟 <c>staff</c>／<c>articles</c>
/// 那 9 張可為空表不同，本檔不需要處理「共同資料唯讀」的情境。
///
/// 🔴 沒有列級授權（<see cref="TeamRowScope"/>）：<c>programs</c>／<c>sessions</c>／
/// <c>registrations</c> 三張表**都沒有 <c>team_id</c> 欄位**——課程項目是俱樂部層級的獨立資料，
/// 不像 <c>C</c> 模組的球隊／球員／教練／賽事那樣掛在特定梯隊底下。規劃書 §6 矩陣把「學院／課程
/// 管理」的資料範圍寫成「授權的俱樂部**與球隊**」，但「與球隊」這個限定只用在該角色能同時操作
/// <c>team.*</c>（C 模組，<c>scope_type=academy_only</c>）與 <c>calendar.*</c>（梯隊賽事）這兩組
/// 權限碼上；「課程／報名」欄本身矩陣直接寫「✔全」，沒有再拆分學院梯隊的子範圍，也沒有機制可拆
/// （沒有 <c>team_id</c> 可過濾）。因此 <c>program.*</c> 權限碼只套 <see cref="AdminClubScope"/>
/// 這一層，見 <c>db/seed/generate-club-seed-sql.py</c> 對應段落的說明。
///
/// 🔴 <c>IQueryCache</c> 只為了寫入後失效——公開唯讀端點 <c>Features/Programs/ProgramsRepository.cs</c>
/// （entity="programs"）已接快取，寫入這裡不失效會讓公開頁面在 TTL 到期前顯示舊資料。
/// </summary>
public sealed class AdminProgramsRepository(ClubDbContext dbContext, IQueryCache cache)
{
    /// <summary>對應前台 05 課程與活動的 5.1–5.5 五個課程頁（主站規劃書 §4.4 P1）。</summary>
    internal static readonly HashSet<string> AllowedProgramTypes = new(StringComparer.Ordinal)
    {
        "children_training", "summer_camp", "winter_camp", "specialist_training", "school_community",
    };

    internal static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal) { "draft", "published" };

    public async Task<IReadOnlyList<AdminProgramListItemDto>> ListAsync(
        AdminClubScope scope, string? programType, CancellationToken cancellationToken)
    {
        var query = dbContext.Programs.AsNoTracking().Where(p => p.ClubId == scope.ClubId);
        if (programType is not null)
        {
            query = query.Where(p => p.ProgramType == programType);
        }

        var rows = await query
            .OrderBy(p => p.RowSeq)
            .Select(p => new
            {
                p.Id,
                p.Slug,
                p.ProgramType,
                p.Audience,
                p.AgeMin,
                p.AgeMax,
                p.Status,
                p.CoverKey,
                p.UpdatedAt,
                SessionCount = p.Sessions.Count,
                NameZh = p.ProgramsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = p.ProgramsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminProgramListItemDto
        {
            Id = r.Id,
            Slug = r.Slug,
            ProgramType = r.ProgramType,
            Audience = r.Audience,
            AgeMin = r.AgeMin,
            AgeMax = r.AgeMax,
            Status = r.Status ?? "draft",
            CoverKey = r.CoverKey,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            SessionCount = r.SessionCount,
            UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminProgramDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var program = await dbContext.Programs.AsNoTracking()
            .Include(p => p.ProgramsI18ns)
            .Include(p => p.Staff).ThenInclude(s => s.StaffI18ns)
            .Include(p => p.Partners)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);

        return program is null ? null : ToDetailDto(program);
    }

    public async Task<AdminProgramDetailDto> CreateAsync(
        AdminClubScope scope, Guid programId, CreateAdminProgramRequest request, string? coverKey, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateProgramType(request.ProgramType);
        ValidateStatus(request.Status);
        ValidateAgeRange(request.AgeMin, request.AgeMax);
        ValidateContent(request.Content);

        if (await dbContext.Programs.AsNoTracking()
                .AnyAsync(p => p.ClubId == scope.ClubId && p.Slug == request.Slug, cancellationToken))
        {
            throw new ProgramSlugConflictException(request.Slug);
        }

        var staff = await ResolveStaffAsync(scope, request.StaffIds ?? [], cancellationToken);
        var partners = await ResolvePartnersAsync(scope, request.PartnerIds ?? [], cancellationToken);

        var now = DateTime.UtcNow;
        var program = new TrainingProgram
        {
            Id = programId,
            ClubId = scope.ClubId,
            Slug = request.Slug,
            ProgramType = request.ProgramType,
            Audience = request.Audience,
            AgeMin = request.AgeMin,
            AgeMax = request.AgeMax,
            Status = request.Status ?? "draft",
            CoverKey = coverKey,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Programs.Add(program);
        AddOrReplaceI18n(program, RequestLocale.DefaultDbLocale, request.Content.Zh);
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(program, "en", request.Content.En);
        }

        foreach (var s in staff)
        {
            program.Staff.Add(s);
        }
        foreach (var p in partners)
        {
            program.Partners.Add(p);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("programs", scope.ClubCode, cancellationToken);
        return (await GetByIdAsync(scope, program.Id, cancellationToken))!;
    }

    public async Task<AdminProgramDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminProgramRequest request, ProgramCoverKeyUpdate coverUpdate, Guid? operatorId, CancellationToken cancellationToken)
    {
        ValidateProgramType(request.ProgramType);
        ValidateStatus(request.Status);
        ValidateAgeRange(request.AgeMin, request.AgeMax);
        ValidateContent(request.Content);

        var program = await dbContext.Programs
            .Include(p => p.ProgramsI18ns)
            .Include(p => p.Staff)
            .Include(p => p.Partners)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (program is null)
        {
            return null;
        }

        if (program.ClubId != scope.ClubId)
        {
            return null; // 跨俱樂部：回 404，不洩漏存在與否（比照 AdminArticlesRepository 慣例）。
        }

        if (!string.Equals(program.Slug, request.Slug, StringComparison.Ordinal)
            && await dbContext.Programs.AsNoTracking()
                .AnyAsync(p => p.ClubId == scope.ClubId && p.Slug == request.Slug && p.Id != id, cancellationToken))
        {
            throw new ProgramSlugConflictException(request.Slug);
        }

        program.Slug = request.Slug;
        program.ProgramType = request.ProgramType;
        program.Audience = request.Audience;
        program.AgeMin = request.AgeMin;
        program.AgeMax = request.AgeMax;
        program.Status = request.Status ?? "draft";
        program.UpdatedAt = DateTime.UtcNow;
        program.UpdatedBy = operatorId;

        if (coverUpdate.Change)
        {
            program.CoverKey = coverUpdate.NewKey;
        }

        AddOrReplaceI18n(program, RequestLocale.DefaultDbLocale, request.Content.Zh);
        var existingEn = program.ProgramsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (request.Content.En is not null)
        {
            AddOrReplaceI18n(program, "en", request.Content.En);
        }
        else if (existingEn is not null)
        {
            dbContext.Remove(existingEn);
        }

        // 省略＝維持不變、空陣列＝清空——比照 AdminArticlesRepository 對 Tags 的既有語意。
        if (request.StaffIds is not null)
        {
            var staff = await ResolveStaffAsync(scope, request.StaffIds, cancellationToken);
            program.Staff.Clear();
            foreach (var s in staff)
            {
                program.Staff.Add(s);
            }
        }

        if (request.PartnerIds is not null)
        {
            var partners = await ResolvePartnersAsync(scope, request.PartnerIds, cancellationToken);
            program.Partners.Clear();
            foreach (var p in partners)
            {
                program.Partners.Add(p);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("programs", scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>教練團可以指派本俱樂部的教練，或兩隊共同（<c>staff.club_id IS NULL</c>）的教練——
    /// 比照 <see cref="Features.AdminStaff.AdminStaffRepository"/> 讀取端「俱樂部專屬優先、回退共同」
    /// 的既有原則，只是這裡是「哪些人可以被指派」而不是「哪些人會被列出」。</summary>
    // 🔴 CS0118：`Staff` 在這個檔案裡是型別／命名空間雙重意義的識別字（本檔命名空間
    // `Tcrfc.Api.Features.AdminPrograms` 的父層底下還有 `Tcrfc.Api.Features.Staff`——公開唯讀
    // 端點），裸寫 `Staff` 會被編譯器解成那個命名空間，理由與寫法逐字比照
    // `Features/AdminStaff/AdminStaffRepository.cs` 同一處說明。
    private async Task<List<Data.EfEntities.Staff>> ResolveStaffAsync(AdminClubScope scope, IReadOnlyList<Guid> staffIds, CancellationToken cancellationToken)
    {
        if (staffIds.Count == 0)
        {
            return [];
        }

        var staff = await dbContext.Staff
            .Where(s => staffIds.Contains(s.Id) && (s.ClubId == scope.ClubId || s.ClubId == null))
            .ToListAsync(cancellationToken);

        if (staff.Count != staffIds.Distinct().Count())
        {
            throw new AdminProgramValidationException("教練團裡有找不到的教練／團隊成員，請確認名單。");
        }

        return staff;
    }

    private async Task<List<Partner>> ResolvePartnersAsync(AdminClubScope scope, IReadOnlyList<Guid> partnerIds, CancellationToken cancellationToken)
    {
        if (partnerIds.Count == 0)
        {
            return [];
        }

        var partners = await dbContext.Partners
            .Where(p => partnerIds.Contains(p.Id) && p.ClubId == scope.ClubId)
            .ToListAsync(cancellationToken);

        if (partners.Count != partnerIds.Distinct().Count())
        {
            throw new AdminProgramValidationException("合作夥伴裡有找不到的夥伴，請確認名單。");
        }

        return partners;
    }

    private void AddOrReplaceI18n(TrainingProgram program, string locale, AdminProgramLocaleContent content)
    {
        var existing = program.ProgramsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            existing = new ProgramsI18n { ProgramId = program.Id, Locale = locale };
            program.ProgramsI18ns.Add(existing);
            dbContext.ProgramsI18ns.Add(existing);
        }

        existing.Name = content.Name;
        existing.Intro = content.Intro;
        existing.Content = content.Content;
    }

    private static void ValidateProgramType(string? programType)
    {
        if (programType is not null && !AllowedProgramTypes.Contains(programType))
        {
            throw new AdminProgramValidationException(
                "課程類型只能是「兒童訓練」「夏令營」「冬令營」「專項訓練」或「校園社區」其中一種。");
        }
    }

    private static void ValidateStatus(string? status)
    {
        if (status is not null && !AllowedStatuses.Contains(status))
        {
            throw new AdminProgramValidationException("狀態只能是「草稿」或「已發布」。");
        }
    }

    private static void ValidateAgeRange(int? ageMin, int? ageMax)
    {
        if (ageMin is int min && min < 0)
        {
            throw new AdminProgramValidationException("最小年齡不能是負數。");
        }
        if (ageMax is int max && max < 0)
        {
            throw new AdminProgramValidationException("最大年齡不能是負數。");
        }
        if (ageMin is int lo && ageMax is int hi && lo > hi)
        {
            throw new AdminProgramValidationException("最小年齡不能大於最大年齡。");
        }
    }

    private static void ValidateContent(AdminProgramContentInput content)
    {
        if (string.IsNullOrWhiteSpace(content.Zh.Name))
        {
            throw new AdminProgramValidationException("中文名稱為必填欄位。");
        }

        ValidateContentJson(content.Zh.Content);
        if (content.En is not null)
        {
            ValidateContentJson(content.En.Content);
        }
    }

    /// <summary><c>programs_i18n.content</c> 是 SQL Server <c>json</c> 型別欄位，寫入非合法 JSON
    /// 會在資料庫層被拒絕——但那條路徑會讓呼叫端看到未經處理的資料庫例外訊息，違反
    /// docs/14-invariants.md「後端任何會回給使用者的訊息都視同介面文字」，故在寫入前先在應用層
    /// 驗證一次語法合法性，提前給出看得懂的中文錯誤訊息。不驗證區塊結構本身，理由見
    /// <see cref="AdminProgramLocaleContent"/> 上的說明。</summary>
    internal static void ValidateContentJson(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        try
        {
            using var _ = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            throw new AdminProgramValidationException("課程內容不是合法的 JSON 格式，請確認區塊編輯器的輸出內容。");
        }
    }

    private static AdminProgramDetailDto ToDetailDto(TrainingProgram program)
    {
        var zh = program.ProgramsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = program.ProgramsI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminProgramDetailDto
        {
            Id = program.Id,
            Slug = program.Slug,
            ProgramType = program.ProgramType,
            Audience = program.Audience,
            AgeMin = program.AgeMin,
            AgeMax = program.AgeMax,
            Status = program.Status ?? "draft",
            CoverKey = program.CoverKey,
            Zh = new AdminProgramLocaleContent { Name = zh?.Name ?? "", Intro = zh?.Intro, Content = zh?.Content },
            En = en is null ? null : new AdminProgramLocaleContent { Name = en.Name ?? "", Intro = en.Intro, Content = en.Content },
            Staff = program.Staff
                .Select(s => new AdminProgramStaffDto
                {
                    StaffId = s.Id,
                    NameZh = s.StaffI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name,
                })
                .ToList(),
            Partners = program.Partners.Select(p => new AdminProgramPartnerDto { PartnerId = p.Id, Slug = p.Slug }).ToList(),
            CreatedAt = program.CreatedAt,
            UpdatedAt = program.UpdatedAt,
        };
    }
}
