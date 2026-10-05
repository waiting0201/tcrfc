using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Trials;

/// <summary>
/// 3.3／4.7 試訓場次的公開讀取與線上報名（主站規劃書 §3.3「試訓場次列表 + 線上報名」、後台 P4）。
/// <c>trials.club_id</c> 必填，所有查詢硬過濾目前俱樂部。報名寫入 <c>registrations</c>（與課程報名同一張表，<c>trial_id</c> 非空、<c>session_id</c> 為空）。
///
/// ### 名額與候補（比照 <c>ProgramsRepository.SubmitRegistrationAsync</c>）
/// 佔名額用<b>單一條件式 UPDATE</b>（<c>status = 開放 AND (capacity IS NULL OR enrolled_count &lt; capacity)</c>），影響列數 1＝佔到名額（報名狀態「待確認」，
/// 佔滿時場次自動轉「額滿」）；0＝名額已滿或場次不是「開放」，這筆報名改為「候補」。並行搶最後一個名額時只有一個請求會贏。
///
/// ### 未成年個資（規劃書沒寫細節，執行層決定；B-9 待決不阻擋開發）
/// 與既有課程報名同一套做法：健康聲明、家長聯絡等欄位存在 <c>registrations</c>（明文欄位，後台 P4 名單匯出與簽到表不含健康聲明，見 AdminTrialRegistrationsRepository）。
/// 本端點額外要求：<b>報名者未滿 18 歲時，家長姓名與電話必填</b>——試訓對象多為青少年，沒有家長聯絡方式的報名後台無法處理。
/// 回應只含報名編號與狀態，不回傳任何個資；錯誤訊息不內插使用者輸入。
///
/// ### 重複報名
/// 同一場試訓、同姓名且（電話或 Email 任一相同）、狀態不是「取消」的報名視為重複，回 409——擋雙擊與重送。
/// 這會讓知道別人姓名＋電話的人得知「他報過名」，但他本來就握有這兩項資料，洩漏的只有「報過名」這一個事實，風險低於讓名單充滿重複報名。
/// </summary>
public sealed partial class TrialsRepository(ClubDbContext db)
{
    private const string StatusOpen = "開放";
    private const string StatusFull = "額滿";
    private const string StatusWaitlist = "候補";
    private const string StatusEnded = "已結束";
    private const string RegistrationPending = "待確認";
    private const string RegistrationWaitlisted = "候補";
    private const int AdultAge = 18;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailFormat();

    public async Task<IReadOnlyList<PublicTrialDto>> ListAsync(
        ClubScope scope, string? teamCode, string dbLocale, CancellationToken cancellationToken)
    {
        var today = TaiwanClock.Today;
        var def = RequestLocale.DefaultDbLocale;
        var query = db.Trials.AsNoTracking()
            .Where(t => t.ClubId == scope.ClubId && t.Status != StatusEnded && t.TrialOn >= today);
        if (!string.IsNullOrWhiteSpace(teamCode))
        {
            var code = teamCode.Trim();
            query = query.Where(t => t.Team != null && t.Team.Code == code);
        }

        var rows = await query
            .OrderBy(t => t.TrialOn).ThenBy(t => t.RowSeq)
            .Take(100)
            .Select(t => new
            {
                t.Id, t.TrialOn, t.Capacity, t.EnrolledCount, t.DeadlineOn, t.Status, t.VenueId,
                TeamCode = t.Team == null ? null : t.Team.Code,
                TeamNameReq = t.Team == null ? null : t.Team.TeamsI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Name).FirstOrDefault(),
                TeamNameDef = t.Team == null ? null : t.Team.TeamsI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                AudienceReq = t.TrialsI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Audience).FirstOrDefault(),
                AudienceDef = t.TrialsI18ns.Where(i => i.Locale == def).Select(i => i.Audience).FirstOrDefault(),
                VenueNameReq = t.Venue == null ? null : t.Venue.VenuesI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Name).FirstOrDefault(),
                VenueNameDef = t.Venue == null ? null : t.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                VenueAddressReq = t.Venue == null ? null : t.Venue.VenuesI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Address).FirstOrDefault(),
                VenueAddressDef = t.Venue == null ? null : t.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Address).FirstOrDefault(),
                VenueLat = t.Venue == null ? null : t.Venue.Lat,
                VenueLng = t.Venue == null ? null : t.Venue.Lng,
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r =>
        {
            var deadlineOk = r.DeadlineOn is null || r.DeadlineOn >= today;
            return new PublicTrialDto
            {
                Id = r.Id,
                TrialOn = r.TrialOn,
                TeamCode = r.TeamCode,
                TeamName = RequestLocale.Pick(r.TeamNameReq, r.TeamNameDef),
                Audience = RequestLocale.Pick(r.AudienceReq, r.AudienceDef),
                VenueId = r.VenueId,
                VenueName = RequestLocale.Pick(r.VenueNameReq, r.VenueNameDef),
                VenueAddress = RequestLocale.Pick(r.VenueAddressReq, r.VenueAddressDef),
                VenueLat = r.VenueLat,
                VenueLng = r.VenueLng,
                Capacity = r.Capacity,
                EnrolledCount = r.EnrolledCount,
                DeadlineOn = r.DeadlineOn,
                Status = r.Status,
                StatusCode = EnrollmentStatus.OfSlot(r.Status).Code,
                StatusLabelZh = EnrollmentStatus.OfSlot(r.Status).Zh,
                StatusLabelEn = EnrollmentStatus.OfSlot(r.Status).En,
                IsSignupOpen = r.Status == StatusOpen && deadlineOk,
                AcceptsWaitlist = (r.Status == StatusFull || r.Status == StatusWaitlist) && deadlineOk,
            };
        }).ToList();
    }

    public async Task<TrialRegistrationSubmittedDto> SubmitRegistrationAsync(
        ClubScope scope, Guid trialId, SubmitTrialRegistrationRequest request, Guid? memberId, CancellationToken cancellationToken)
    {
        var input = Validate(request);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // 先鎖場次列，讓同一場試訓的報名串行化：否則同一人並行重複送出時，下面「查重複 → 插入」的檢查會同時通過，
        // 產生兩筆報名並重複扣名額（E-172 同類：檢查與插入之間的空窗）。比照 FanEventsRepository 的做法。
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM trials WITH (UPDLOCK, ROWLOCK) WHERE id = {trialId} AND club_id = {scope.ClubId}", cancellationToken);

        var trial = await db.Trials.AsNoTracking()
            .Where(t => t.Id == trialId && t.ClubId == scope.ClubId)
            .Select(t => new { t.Status, t.TrialOn, t.DeadlineOn })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new PublicNotFoundException("找不到試訓場次", "找不到這場試訓，請重新整理頁面後再試一次。");

        var today = TaiwanClock.Today;
        if (trial.Status == StatusEnded || trial.TrialOn < today)
        {
            throw new PublicConflictException("無法報名", "這場試訓已經結束，無法再報名。");
        }

        if (trial.DeadlineOn is { } deadline && deadline < today)
        {
            throw new PublicConflictException("無法報名", "這場試訓的報名已經截止。");
        }

        var duplicate = await db.Registrations.AsNoTracking().AnyAsync(r =>
            r.TrialId == trialId && r.Status != "取消" && r.ApplicantName == input.Name
            && ((input.Phone != null && r.Phone == input.Phone) || (input.Email != null && r.Email == input.Email)),
            cancellationToken);
        if (duplicate)
        {
            throw new PublicConflictException("已經報名過了", "這場試訓已經有相同姓名與聯絡方式的報名，如需更改請與我們聯絡。");
        }

        var now = DateTime.UtcNow;
        // 單一條件式 UPDATE 搶名額（見類別說明）。同一個 UPDATE 同時判斷「額滿自動關閉」：搶到最後一個名額就轉「額滿」。
        var claimed = await db.Trials
            .Where(t => t.Id == trialId && t.ClubId == scope.ClubId && t.Status == StatusOpen
                        && (t.Capacity == null || t.EnrolledCount < t.Capacity))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.EnrolledCount, t => t.EnrolledCount + 1)
                .SetProperty(t => t.Status, t => t.Capacity != null && t.EnrolledCount + 1 >= t.Capacity ? StatusFull : t.Status)
                .SetProperty(t => t.UpdatedAt, now), cancellationToken);

        var registrationNo = await GenerateUniqueRegistrationNoAsync(scope.ClubCode, now, cancellationToken);
        var status = claimed > 0 ? RegistrationPending : RegistrationWaitlisted;

        db.Registrations.Add(new Registration
        {
            Id = Guid.NewGuid(), RegistrationNo = registrationNo, ClubId = scope.ClubId, TrialId = trialId, MemberId = memberId,
            ApplicantName = input.Name, Phone = input.Phone, Email = input.Email, BirthOn = input.BirthOn,
            GuardianName = input.GuardianName, GuardianPhone = input.GuardianPhone,
            HealthDeclaration = input.HealthDeclaration, Note = input.Note,
            Status = status, CreatedAt = now, UpdatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var submitted = EnrollmentStatus.OfRegistration(status);
        return new TrialRegistrationSubmittedDto { RegistrationNo = registrationNo, Status = status, StatusCode = submitted.Code, StatusLabelZh = submitted.Zh, StatusLabelEn = submitted.En };
    }

    private sealed record ValidatedInput(
        string Name, string? Phone, string? Email, DateOnly? BirthOn, string? GuardianName, string? GuardianPhone,
        string? HealthDeclaration, string? Note);

    private static ValidatedInput Validate(SubmitTrialRegistrationRequest request)
    {
        var name = Trimmed(request.ApplicantName);
        if (name is null)
        {
            throw new PublicValidationException("請填寫報名者姓名。");
        }

        if (name.Length > 64)
        {
            throw new PublicValidationException("報名者姓名不可超過 64 個字。");
        }

        var phone = Trimmed(request.Phone);
        var email = Trimmed(request.Email)?.ToLowerInvariant();
        if (phone is null && email is null)
        {
            throw new PublicValidationException("電話與電子郵件至少需要填寫一項，以便後續聯繫。");
        }

        if (phone is not null && phone.Length > 32)
        {
            throw new PublicValidationException("電話不可超過 32 個字。");
        }

        if (email is not null && (email.Length > 255 || !EmailFormat().IsMatch(email)))
        {
            throw new PublicValidationException("電子郵件地址的格式不正確，請檢查後再送出。");
        }

        var today = TaiwanClock.Today;
        if (request.BirthOn is { } birth && (birth > today || birth < today.AddYears(-100)))
        {
            throw new PublicValidationException("出生日期不正確，請重新確認。");
        }

        var guardianName = Trimmed(request.GuardianName);
        var guardianPhone = Trimmed(request.GuardianPhone);
        if (guardianName is { Length: > 64 })
        {
            throw new PublicValidationException("家長姓名不可超過 64 個字。");
        }

        if (guardianPhone is { Length: > 32 })
        {
            throw new PublicValidationException("家長電話不可超過 32 個字。");
        }

        if (request.BirthOn is { } b && b > today.AddYears(-AdultAge) && (guardianName is null || guardianPhone is null))
        {
            throw new PublicValidationException("報名者未滿 18 歲，請填寫家長（監護人）的姓名與電話。");
        }

        var health = Trimmed(request.HealthDeclaration);
        if (health is { Length: > 2000 })
        {
            throw new PublicValidationException("健康聲明不可超過 2000 個字。");
        }

        var note = Trimmed(request.Note);
        if (note is { Length: > 1000 })
        {
            throw new PublicValidationException("備註不可超過 1000 個字。");
        }

        return new ValidatedInput(name, phone, email, request.BirthOn, guardianName, guardianPhone, health, note);
    }

    private static string? Trimmed(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private async Task<string> GenerateUniqueRegistrationNoAsync(string clubCode, DateTime now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = RegistrationNumberGenerator.Generate(clubCode, now);
            if (!await db.Registrations.AsNoTracking().AnyAsync(r => r.RegistrationNo == candidate, cancellationToken))
            {
                return candidate;
            }
        }

        throw new PublicConflictException("請稍後再試", "報名編號產生失敗，請重新送出一次。");
    }
}
