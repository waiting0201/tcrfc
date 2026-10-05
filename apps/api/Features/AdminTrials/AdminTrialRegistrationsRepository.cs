using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminTrials;

/// <summary>
/// P4「報名名單管理」：試訓場次底下的報名（<c>registrations.trial_id</c>，與課程報名共用同一張表，兩個外鍵恰有一個非空）。
/// 狀態流程、名額連動與 P3 相同（待確認 → 已確認 → 已繳費 → 完成／取消／候補）；繳費為線下作業。
/// 🔴 通知信本輪未寄送（全系統尚無寄信通路），見 README「B1」節。健康聲明維持現行決定（明文欄位，見 P3 的說明），
/// 名單匯出與簽到表刻意不含健康聲明。
/// </summary>
public sealed class AdminTrialRegistrationsRepository(ClubDbContext db, AdminTrialsRepository trials, SensitiveActionLogger audit)
{
    private static readonly HashSet<string> Statuses =
        new(StringComparer.Ordinal) { "待確認", "已確認", "已繳費", "完成", "取消", "候補" };

    private async Task<Trial> RequireTrialAsync(AdminClubScope scope, Guid trialId, CancellationToken cancellationToken)
        => await db.Trials.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trialId && t.ClubId == scope.ClubId, cancellationToken)
           ?? throw new TrialNotFoundException();

    public async Task<IReadOnlyList<AdminTrialRegistrationListItemDto>?> ListAsync(
        AdminClubScope scope, Guid trialId, string? status, string? keyword, bool? isMember, CancellationToken cancellationToken)
    {
        if (!await db.Trials.AsNoTracking().AnyAsync(t => t.Id == trialId && t.ClubId == scope.ClubId, cancellationToken))
        {
            return null;
        }

        var query = Filter(scope, trialId, status, keyword, isMember);
        var rows = await query.OrderBy(r => r.RowSeq).ToListAsync(cancellationToken);
        return rows.Select(ToListItem).ToList();
    }

    private IQueryable<Registration> Filter(AdminClubScope scope, Guid trialId, string? status, string? keyword, bool? isMember)
    {
        var query = db.Registrations.AsNoTracking().Where(r => r.ClubId == scope.ClubId && r.TrialId == trialId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, Statuses, "狀態", "「待確認」「已確認」「已繳費」「完成」「取消」或「候補」");
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(r => r.ApplicantName.Contains(k) || r.RegistrationNo.Contains(k)
                || (r.Phone != null && r.Phone.Contains(k)) || (r.Email != null && r.Email.Contains(k)));
        }

        if (isMember is bool m)
        {
            query = m ? query.Where(r => r.MemberId != null) : query.Where(r => r.MemberId == null);
        }

        return query;
    }

    public async Task<AdminTrialRegistrationDetailDto?> GetAsync(AdminClubScope scope, Guid trialId, Guid id, CancellationToken cancellationToken)
    {
        var r = await db.Registrations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TrialId == trialId && x.ClubId == scope.ClubId, cancellationToken);
        if (r is null)
        {
            return null;
        }

        var over = await db.Trials.AsNoTracking().AnyAsync(t => t.Id == trialId && t.Capacity != null && t.EnrolledCount > t.Capacity, cancellationToken);
        return ToDetail(r) with { IsOverCapacity = over };
    }

    public async Task<AdminTrialRegistrationDetailDto> CreateAsync(
        AdminClubScope scope, Guid trialId, CreateAdminTrialRegistrationRequest request, CancellationToken cancellationToken)
    {
        var status = request.Status ?? "待確認";
        var input = Validate(request.ApplicantName, status, request.Phone, request.Email, request.BirthOn, request.GuardianName, request.GuardianPhone);
        var trial = await RequireTrialAsync(scope, trialId, cancellationToken);
        await ValidateMemberAsync(request.MemberId, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var registration = new Registration
        {
            Id = Guid.NewGuid(), RegistrationNo = await GenerateNoAsync(scope.ClubCode, now, cancellationToken), ClubId = scope.ClubId, TrialId = trial.Id,
            MemberId = request.MemberId, ApplicantName = input.Name, Phone = input.Phone, Email = input.Email, BirthOn = request.BirthOn,
            GuardianName = input.GuardianName, GuardianPhone = input.GuardianPhone, HealthDeclaration = request.HealthDeclaration, Note = request.Note,
            Status = status, CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        db.Registrations.Add(registration);
        await db.SaveChangesAsync(cancellationToken);
        if (AdminTrialsRepository.Occupies(status))
        {
            await trials.AdjustEnrolledCountAsync(trial.Id, +1, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return (await GetAsync(scope, trialId, registration.Id, cancellationToken))!;
    }

    public async Task<AdminTrialRegistrationDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid trialId, Guid id, UpdateAdminTrialRegistrationRequest request, CancellationToken cancellationToken)
    {
        var registration = await db.Registrations.FirstOrDefaultAsync(
            r => r.Id == id && r.TrialId == trialId && r.ClubId == scope.ClubId, cancellationToken);
        if (registration is null)
        {
            return null;
        }

        // 舊資料（本驗證上線前代填、原本就沒有任何聯絡方式）仍要能改狀態／取消，所以「至少一項聯絡方式」只對原本有聯絡方式的報名強制；
        // 格式與未成年家長欄位的檢查一律照做。
        var hadContact = registration.Phone is not null || registration.Email is not null;
        var input = Validate(request.ApplicantName, request.Status, request.Phone, request.Email, request.BirthOn, request.GuardianName, request.GuardianPhone,
            requireContact: hadContact);

        await ValidateMemberAsync(request.MemberId, cancellationToken);
        var oldOccupies = AdminTrialsRepository.Occupies(registration.Status);
        var newOccupies = AdminTrialsRepository.Occupies(request.Status);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        registration.MemberId = request.MemberId;
        registration.ApplicantName = input.Name;
        registration.Phone = input.Phone;
        registration.Email = input.Email;
        registration.BirthOn = request.BirthOn;
        registration.GuardianName = input.GuardianName;
        registration.GuardianPhone = input.GuardianPhone;
        registration.HealthDeclaration = request.HealthDeclaration;
        registration.Note = request.Note;
        registration.Status = request.Status;
        registration.UpdatedAt = DateTime.UtcNow;
        registration.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        if (oldOccupies != newOccupies)
        {
            await trials.AdjustEnrolledCountAsync(trialId, newOccupies ? +1 : -1, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(scope, trialId, id, cancellationToken);
    }

    /// <summary>候補遞補：候補 → 已確認（佔用名額）。後台是人為判斷，不擋下超額；沒有名額時仍可遞補，由承辦自行負責。</summary>
    public async Task<AdminTrialRegistrationDetailDto?> PromoteAsync(AdminClubScope scope, Guid trialId, Guid id, CancellationToken cancellationToken)
    {
        var registration = await db.Registrations.FirstOrDefaultAsync(
            r => r.Id == id && r.TrialId == trialId && r.ClubId == scope.ClubId, cancellationToken);
        if (registration is null)
        {
            return null;
        }

        if (registration.Status != "候補")
        {
            throw new AdminValidationException("只有候補中的報名才能遞補。");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        registration.Status = "已確認";
        registration.UpdatedAt = DateTime.UtcNow;
        registration.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        await trials.AdjustEnrolledCountAsync(trialId, +1, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(scope, trialId, id, cancellationToken);
    }

    /// <summary>名單 CSV（<c>program.trial_registration.export</c>，受限）。不含健康聲明。</summary>
    public async Task<(string Csv, DateOnly TrialOn)?> ExportCsvAsync(AdminClubScope scope, Guid trialId, string? status, string? purpose, CancellationToken cancellationToken)
    {
        var purposeText = AdminInput.RequireText(purpose, "匯出用途", 200);
        var trial = await db.Trials.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trialId && t.ClubId == scope.ClubId, cancellationToken);
        if (trial is null)
        {
            return null;
        }

        var rows = await Filter(scope, trialId, status, null, null).OrderBy(r => r.RowSeq).ToListAsync(cancellationToken);
        var lines = new List<IEnumerable<string?>>
        {
            new[] { "報名編號", "試訓日期", "姓名", "電話", "Email", "出生日期", "家長姓名", "家長電話", "狀態", "是否為會員", "備註", "報名時間（台灣時間）" },
        };
        lines.AddRange(rows.Select(r => new[]
        {
            r.RegistrationNo, trial.TrialOn.ToString("yyyy-MM-dd"), r.ApplicantName, r.Phone, r.Email, r.BirthOn?.ToString("yyyy-MM-dd"),
            r.GuardianName, r.GuardianPhone, r.Status, r.MemberId != null ? "是" : "否", r.Note, TaiwanClock.ToText(r.CreatedAt),
        }));
        audit.Record(scope, "匯出試訓報名名單", $"試訓 {trialId}", rows.Count, purposeText);
        return (CsvUtils.BuildCsv(lines), trial.TrialOn);
    }

    public async Task<AdminTrialSignInSheetDto?> SignInSheetAsync(AdminClubScope scope, Guid trialId, CancellationToken cancellationToken)
    {
        var info = await db.Trials.AsNoTracking().Where(t => t.Id == trialId && t.ClubId == scope.ClubId)
            .Select(t => new
            {
                t.TrialOn,
                TeamName = t.Team == null ? null : t.Team.TeamsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                VenueName = t.Venue == null ? null : t.Venue.VenuesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                Audience = t.TrialsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Audience).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (info is null)
        {
            return null;
        }

        var attending = new[] { "待確認", "已確認", "已繳費", "完成" };
        var rows = await db.Registrations.AsNoTracking()
            .Where(r => r.TrialId == trialId && r.ClubId == scope.ClubId && attending.Contains(r.Status))
            .OrderBy(r => r.ApplicantName).ThenBy(r => r.RowSeq).ToListAsync(cancellationToken);
        return new AdminTrialSignInSheetDto
        {
            TrialId = trialId, TrialOn = info.TrialOn, TeamName = info.TeamName, VenueName = info.VenueName, AudienceZh = info.Audience,
            GeneratedAt = DateTime.UtcNow,
            Rows = rows.Select((r, i) => new AdminSignInRowDto
            {
                No = i + 1, RegistrationNo = r.RegistrationNo, ApplicantName = r.ApplicantName, Phone = r.Phone, GuardianName = r.GuardianName,
                GuardianPhone = r.GuardianPhone, Status = r.Status,
            }).ToList(),
        };
    }

    // ── 內部 ───────────────────────────────────────────

    private sealed record ValidatedInput(string Name, string? Phone, string? Email, string? GuardianName, string? GuardianPhone);

    private const int AdultAge = 18;

    /// <summary>比照前台 <c>TrialsRepository.Validate</c>：電話與 Email 至少一項、格式、出生日期合理、未滿 18 歲須填家長姓名與電話。
    /// 後台代填與前台報名走同一套聯絡與監護人規則，只有「名額不擋」是後台才有的人為判斷。</summary>
    private static ValidatedInput Validate(
        string applicantName, string status, string? phone, string? email, DateOnly? birthOn, string? guardianName, string? guardianPhone,
        bool requireContact = true)
    {
        var name = AdminInput.RequireText(applicantName, "姓名", 64);
        AdminInput.OneOf(status, Statuses, "狀態", "「待確認」「已確認」「已繳費」「完成」「取消」或「候補」");
        var p = AdminInput.OptionalPhone(phone, "電話");
        var e = AdminInput.OptionalEmail(email, "Email")?.ToLowerInvariant();
        if (requireContact && p is null && e is null)
        {
            throw new AdminValidationException("電話與 Email 至少需要填寫一項，以便後續聯繫。");
        }

        var today = TaiwanClock.Today;
        if (birthOn is { } birth && (birth > today || birth < today.AddYears(-100)))
        {
            throw new AdminValidationException("出生日期不正確，請重新確認。");
        }

        var gName = AdminInput.OptionalText(guardianName, "家長姓名", 64);
        var gPhone = AdminInput.OptionalPhone(guardianPhone, "家長電話");
        if (birthOn is { } b && b > today.AddYears(-AdultAge) && (gName is null || gPhone is null))
        {
            throw new AdminValidationException("報名者未滿 18 歲，請填寫家長（監護人）的姓名與電話。");
        }

        return new ValidatedInput(name, p, e, gName, gPhone);
    }

    private async Task ValidateMemberAsync(Guid? memberId, CancellationToken cancellationToken)
    {
        if (memberId is not null && !await db.Members.AsNoTracking().AnyAsync(m => m.Id == memberId, cancellationToken))
        {
            throw new AdminValidationException("找不到指定的會員，請確認會員資料是否存在。");
        }
    }

    private async Task<string> GenerateNoAsync(string clubCode, DateTime now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = RegistrationNumberGenerator.Generate(clubCode, now);
            if (!await db.Registrations.AsNoTracking().AnyAsync(r => r.RegistrationNo == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new AdminValidationException("報名編號產生失敗，請重新送出一次。");
    }

    private static AdminTrialRegistrationListItemDto ToListItem(Registration r) => new()
    {
        Id = r.Id, RegistrationNo = r.RegistrationNo, MemberId = r.MemberId, IsMember = r.MemberId != null, ApplicantName = r.ApplicantName,
        Phone = r.Phone, Email = r.Email, BirthOn = r.BirthOn, GuardianName = r.GuardianName, GuardianPhone = r.GuardianPhone, Note = r.Note,
        Status = r.Status, CreatedAt = r.CreatedAt,
    };

    private static AdminTrialRegistrationDetailDto ToDetail(Registration r) => new()
    {
        Id = r.Id, RegistrationNo = r.RegistrationNo, TrialId = r.TrialId!.Value, MemberId = r.MemberId, ApplicantName = r.ApplicantName, Phone = r.Phone,
        Email = r.Email, BirthOn = r.BirthOn, GuardianName = r.GuardianName, GuardianPhone = r.GuardianPhone, HealthDeclaration = r.HealthDeclaration,
        Note = r.Note, Status = r.Status, CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt,
    };
}

/// <summary>試訓場次不存在或不屬於這個俱樂部（對應 404）。</summary>
public sealed class TrialNotFoundException() : Exception("找不到這個俱樂部的試訓場次，請確認場次是否存在。");
