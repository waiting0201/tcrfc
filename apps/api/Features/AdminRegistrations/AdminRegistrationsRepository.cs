using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminRegistrations;

/// <summary>P3「報名管理」——俱樂部範圍 CRUD 與匯出（主站規劃書 §4.4 P3）。本檔只服務
/// <c>session_id</c>（課程／營隊報名）——<c>P4</c> 試訓管理不在本次範圍，<c>trial_id</c> 那一半
/// 留給日後 <c>S2-4</c> 開發，見任務指示與 <c>CreateAdminRegistrationRequest</c> 上的說明。
///
/// **名額連動**（規劃書行 1097「額滿自動關閉」）：<c>sessions.enrolled_count</c> 是否隨報名異動
/// 增減，取決於這筆報名的狀態是否落在 <see cref="OccupyingStatuses"/>（實際佔用名額的狀態：
/// 待確認／已確認／已繳費／完成）。「取消」與「候補」不佔名額——候補本來就是「排在外面等」，
/// 取消則是把名額還回去。轉梯次（<see cref="UpdateAdminRegistrationRequest.SessionId"/> 與現有值
/// 不同）視為「舊梯次退一位、新梯次佔一位」，兩次調整都用 <see cref="AdjustEnrolledCountAsync"/>
/// 的原子 SQL（<c>UPDATE ... SET enrolled_count = enrolled_count + @delta</c>），避免公開報名
/// 端點（<see cref="Features.Programs.ProgramsRepository"/>）與後台同時寫入同一個梯次時的
/// 競態條件（lost update）。
///
/// 🔴 **後台這一側不做「額滿即拒絕」的關卡**——那是公開報名端點的行為（保護一般訪客不會在人不知
/// 情的情況下報進一個已經額滿的梯次）。後台操作者是人，有判斷力也有責任，允許人工超額或人工把
/// 候補直接轉正，見 <see cref="AdjustEnrolledCountAsync"/> 上「只單向收斂到額滿，不做上限攔阻」
/// 的說明。
///
/// 🔴 「候補遞補提醒」（規劃書行 1097）與確認／取消信（規劃書行 1088「寄送通知信（模板化）」）
/// 本輪未實作——<c>EmailLog.type</c> 值域是封閉的 9 個值（docs/12 §12 第 23 點：會員 5 ＋商店 4），
/// 課程報名的通知信不在其中，前台報名流程要求的「Email／簡訊通知」（docs/02-frontend-spec.md
/// 行 102）同樣沒有對應的樣板與寄送機制，簡訊更是全系統從未建置過的通路。這屬於「綱要與規劃書
/// 在這件事上沒有明確答案」，依任務指示停在這裡、寫進報告，不自行新增 <c>EmailLog.type</c> 值域
/// 或串接簡訊服務。
/// </summary>
public sealed class AdminRegistrationsRepository(ClubDbContext dbContext)
{
    internal static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.Ordinal) { "待確認", "已確認", "已繳費", "完成", "取消", "候補" };

    /// <summary>實際佔用梯次名額的狀態。</summary>
    private static readonly HashSet<string> OccupyingStatuses =
        new(StringComparer.Ordinal) { "待確認", "已確認", "已繳費", "完成" };

    public async Task<IReadOnlyList<AdminRegistrationListItemDto>> ListAsync(
        AdminClubScope scope, Guid? sessionId, string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Registrations.AsNoTracking()
            .Where(r => r.ClubId == scope.ClubId && r.SessionId != null); // P4 試訓不在本次範圍。

        if (sessionId is Guid s)
        {
            query = query.Where(r => r.SessionId == s);
        }
        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }

        return await query
            .OrderByDescending(r => r.RowSeq)
            .Select(r => new AdminRegistrationListItemDto
            {
                Id = r.Id,
                RegistrationNo = r.RegistrationNo,
                SessionId = r.SessionId,
                ProgramNameZh = r.Session!.TrainingProgram.ProgramsI18ns
                    .Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                TrialId = r.TrialId,
                MemberId = r.MemberId,
                IsMember = r.MemberId != null,
                ApplicantName = r.ApplicantName,
                Phone = r.Phone,
                Email = r.Email,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminRegistrationDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var registration = await dbContext.Registrations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.ClubId == scope.ClubId, cancellationToken);

        return registration is null ? null : ToDetailDto(registration);
    }

    public async Task<AdminRegistrationDetailDto> CreateAsync(
        AdminClubScope scope, CreateAdminRegistrationRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var status = request.Status ?? "待確認";
        Validate(request.ApplicantName, status);

        var session = await dbContext.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.ClubId == scope.ClubId, cancellationToken)
            ?? throw new SessionNotFoundForRegistrationException();

        await ValidateMemberAsync(request.MemberId, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var registrationId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var registration = new Registration
        {
            Id = registrationId,
            RegistrationNo = await GenerateUniqueRegistrationNoAsync(scope.ClubCode, now, cancellationToken),
            ClubId = scope.ClubId,
            SessionId = session.Id,
            MemberId = request.MemberId,
            ApplicantName = request.ApplicantName,
            Phone = request.Phone,
            Email = request.Email,
            BirthOn = request.BirthOn,
            GuardianName = request.GuardianName,
            GuardianPhone = request.GuardianPhone,
            HealthDeclaration = request.HealthDeclaration,
            Note = request.Note,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Registrations.Add(registration);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (OccupyingStatuses.Contains(status))
        {
            await AdjustEnrolledCountAsync(session.Id, +1, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return (await GetByIdAsync(scope, registrationId, cancellationToken))!;
    }

    public async Task<AdminRegistrationDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminRegistrationRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request.ApplicantName, request.Status);

        var registration = await dbContext.Registrations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (registration is null)
        {
            return null;
        }

        if (registration.ClubId != scope.ClubId)
        {
            return null; // 跨俱樂部：回 404，不洩漏存在與否。
        }

        var newSession = await dbContext.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.ClubId == scope.ClubId, cancellationToken)
            ?? throw new SessionNotFoundForRegistrationException();

        await ValidateMemberAsync(request.MemberId, cancellationToken);

        var oldSessionId = registration.SessionId;
        var oldOccupies = registration.SessionId is not null && OccupyingStatuses.Contains(registration.Status);
        var newOccupies = OccupyingStatuses.Contains(request.Status);
        var isTransfer = oldSessionId != newSession.Id;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        registration.SessionId = newSession.Id;
        registration.MemberId = request.MemberId;
        registration.ApplicantName = request.ApplicantName;
        registration.Phone = request.Phone;
        registration.Email = request.Email;
        registration.BirthOn = request.BirthOn;
        registration.GuardianName = request.GuardianName;
        registration.GuardianPhone = request.GuardianPhone;
        registration.HealthDeclaration = request.HealthDeclaration;
        registration.Note = request.Note;
        registration.Status = request.Status;
        registration.UpdatedAt = DateTime.UtcNow;
        registration.UpdatedBy = operatorId;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (isTransfer)
        {
            if (oldOccupies && oldSessionId is Guid previousSessionId)
            {
                await AdjustEnrolledCountAsync(previousSessionId, -1, cancellationToken);
            }
            if (newOccupies)
            {
                await AdjustEnrolledCountAsync(newSession.Id, +1, cancellationToken);
            }
        }
        else if (oldOccupies != newOccupies)
        {
            await AdjustEnrolledCountAsync(newSession.Id, newOccupies ? +1 : -1, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>名單匯出（規劃書行 1090「匯出 Excel：名單匯出（含分組欄位）」）。本輪比照既有
    /// FAQ／賽程／積分榜三個模組的既有慣例（<c>Common/CsvUtils.cs</c> 檔頭），以 CSV 實作——
    /// 本專案沒有任何 Excel（<c>.xlsx</c>）產生套件，「Excel 匯出」在既有程式碼裡一律是「CSV，
    /// Excel 可以直接開啟」的既有做法，非本輪新發明，見任務回報「規劃書沒寫清楚、自行判斷」。
    /// **刻意不包含 <c>HealthDeclaration</c>（健康聲明）欄**——名單匯出的用途是人數控管與簽到，
    /// 不需要醫療類個資，這是資料最小化的判斷，不是遺漏。「簽到表列印」由前台／後台畫面直接把這份
    /// 清單資料印出即可，後端不需要另外產生 PDF。</summary>
    public async Task<string> ExportCsvAsync(AdminClubScope scope, Guid? sessionId, string? status, CancellationToken cancellationToken)
    {
        var query = dbContext.Registrations.AsNoTracking()
            .Where(r => r.ClubId == scope.ClubId && r.SessionId != null);

        if (sessionId is Guid s)
        {
            query = query.Where(r => r.SessionId == s);
        }
        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }

        var rows = await query
            .OrderBy(r => r.Session!.TrainingProgram.Slug).ThenBy(r => r.RowSeq)
            .Select(r => new
            {
                r.RegistrationNo,
                ProgramName = r.Session!.TrainingProgram.ProgramsI18ns
                    .Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                r.Session!.StartOn,
                r.ApplicantName,
                r.Phone,
                r.Email,
                r.BirthOn,
                r.GuardianName,
                r.GuardianPhone,
                r.Status,
                IsMember = r.MemberId != null,
                r.Note,
                r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var lines = new List<IEnumerable<string?>>
        {
            new[] { "報名編號", "課程名稱", "梯次開始日期", "學員姓名", "電話", "Email", "出生日期",
                    "家長姓名", "家長電話", "狀態", "是否為會員", "備註", "建立時間" },
        };

        lines.AddRange(rows.Select(r => new[]
        {
            r.RegistrationNo,
            r.ProgramName,
            r.StartOn?.ToString("yyyy-MM-dd"),
            r.ApplicantName,
            r.Phone,
            r.Email,
            r.BirthOn?.ToString("yyyy-MM-dd"),
            r.GuardianName,
            r.GuardianPhone,
            r.Status,
            r.IsMember ? "是" : "否",
            r.Note,
            r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        }));

        return CsvUtils.BuildCsv(lines);
    }

    /// <summary>原子調整梯次名額，比照 <c>UPDATE ... SET x = x + @delta</c> 避免競態條件。
    /// 🔴 只單向收斂到「額滿」（<c>status = N'開放'</c> 時才會被推成 <c>N'額滿'</c>），不會反向
    /// 把「額滿」自動打回「開放」，也不碰「候補」「已結束」——理由與
    /// <c>Features/AdminSessions/AdminSessionsRepository.DeriveDefaultStatus</c> 一致（規劃書
    /// 只講「額滿自動關閉」單向語意，見該檔案上的說明）。名額扣減有下限保護
    /// （<c>enrolled_count</c> 不會被減到負數），避免資料異常時連鎖出現負數名額。</summary>
    private Task AdjustEnrolledCountAsync(Guid sessionId, int delta, CancellationToken cancellationToken)
        => dbContext.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE sessions
            SET enrolled_count = CASE WHEN enrolled_count + {delta} < 0 THEN 0 ELSE enrolled_count + {delta} END,
                status = CASE
                    WHEN status = N'開放' AND capacity IS NOT NULL AND (enrolled_count + {delta}) >= capacity
                    THEN N'額滿'
                    ELSE status
                END,
                updated_at = SYSUTCDATETIME()
            WHERE id = {sessionId}", cancellationToken);

    private async Task<string> GenerateUniqueRegistrationNoAsync(string clubCode, DateTime now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = RegistrationNumberGenerator.Generate(clubCode, now);
            var exists = await dbContext.Registrations.AsNoTracking()
                .AnyAsync(r => r.RegistrationNo == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new AdminRegistrationValidationException("報名編號產生失敗，請重新送出一次。");
    }

    private async Task ValidateMemberAsync(Guid? memberId, CancellationToken cancellationToken)
    {
        if (memberId is null)
        {
            return;
        }

        var exists = await dbContext.Members.AsNoTracking().AnyAsync(m => m.Id == memberId, cancellationToken);
        if (!exists)
        {
            throw new AdminRegistrationValidationException("找不到指定的會員，請確認會員資料是否存在。");
        }
    }

    private static void Validate(string applicantName, string status)
    {
        if (string.IsNullOrWhiteSpace(applicantName))
        {
            throw new AdminRegistrationValidationException("學員姓名為必填欄位。");
        }
        if (!AllowedStatuses.Contains(status))
        {
            throw new AdminRegistrationValidationException(
                "狀態只能是「待確認」「已確認」「已繳費」「完成」「取消」或「候補」其中一種。");
        }
    }

    private static AdminRegistrationDetailDto ToDetailDto(Registration registration) => new()
    {
        Id = registration.Id,
        RegistrationNo = registration.RegistrationNo,
        SessionId = registration.SessionId,
        TrialId = registration.TrialId,
        MemberId = registration.MemberId,
        ApplicantName = registration.ApplicantName,
        Phone = registration.Phone,
        Email = registration.Email,
        BirthOn = registration.BirthOn,
        GuardianName = registration.GuardianName,
        GuardianPhone = registration.GuardianPhone,
        HealthDeclaration = registration.HealthDeclaration,
        Note = registration.Note,
        Status = registration.Status,
        CreatedAt = registration.CreatedAt,
        UpdatedAt = registration.UpdatedAt,
    };
}
