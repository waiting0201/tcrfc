using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminPrograms;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSessions;

/// <summary>P2「梯次與場次」——俱樂部範圍 CRUD（主站規劃書 §4.4 P2）。
///
/// **名額控管**（規劃書行 1097「額滿自動關閉、候補遞補提醒」）：<c>sessions.enrolled_count</c> 只由
/// 報名寫入路徑（<see cref="Features.AdminRegistrations.AdminRegistrationsRepository"/>／
/// <see cref="Features.Programs.ProgramsRepository"/> 公開報名）遞增或遞減，本檔的建立／更新
/// 端點完全不接受呼叫端指定這個欄位，理由見 <see cref="CreateAdminSessionRequest"/> 上的說明。
/// 「候補遞補提醒」（有人取消後自動通知候補名單遞補）本輪未實作——沒有對應的 Email 通知基礎設施
/// （<c>EmailLog.type</c> 值域是封閉的 9 個值，docs/12 §12 第 23 點，課程報名不在其中），
/// 見任務回報「規劃書沒寫清楚、自行判斷」。
///
/// 🔴 沒有列級授權：理由同 <c>Features/AdminPrograms/AdminProgramsRepository.cs</c> 檔頭
/// ——<c>sessions</c> 沒有 <c>team_id</c> 欄位。
/// </summary>
public sealed class AdminSessionsRepository(ClubDbContext dbContext, IQueryCache cache)
{
    /// <summary>對應 P2 規劃書行 1098「狀態（開放／額滿／候補／已結束）」，比照
    /// <c>CK_registrations_status</c> 已建立的先例——狀態值直接沿用規劃書的中文字面，
    /// 不翻成英文代碼（見 db/club-schema.sql 該張表的既有寫法）。</summary>
    internal static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "開放", "額滿", "候補", "已結束",
    };

    public async Task<IReadOnlyList<AdminSessionListItemDto>> ListAsync(
        AdminClubScope scope, Guid? programId, CancellationToken cancellationToken)
    {
        var query = dbContext.Sessions.AsNoTracking().Where(s => s.ClubId == scope.ClubId);
        if (programId is Guid p)
        {
            query = query.Where(s => s.ProgramId == p);
        }

        var rows = await query
            .OrderBy(s => s.RowSeq)
            .Select(s => new AdminSessionListItemDto
            {
                Id = s.Id,
                ProgramId = s.ProgramId,
                ProgramNameZh = s.TrainingProgram.ProgramsI18ns
                    .Where(i => i.Locale == Localization.RequestLocale.DefaultDbLocale)
                    .Select(i => i.Name).FirstOrDefault(),
                VenueId = s.VenueId,
                StartOn = s.StartOn,
                EndOn = s.EndOn,
                Capacity = s.Capacity,
                EnrolledCount = s.EnrolledCount,
                Price = s.Price,
                EarlyBirdPrice = s.EarlyBirdPrice,
                EarlyBirdUntil = s.EarlyBirdUntil,
                SignupOpensAt = s.SignupOpensAt,
                SignupClosesAt = s.SignupClosesAt,
                Status = s.Status ?? "開放",
                UpdatedAt = s.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<AdminSessionDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.ClubId == scope.ClubId, cancellationToken);

        return session is null ? null : ToDetailDto(session);
    }

    public async Task<AdminSessionDetailDto> CreateAsync(
        AdminClubScope scope, Guid sessionId, CreateAdminSessionRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request.WeeklySchedule, request.Capacity, request.Price, request.EarlyBirdPrice,
            request.StartOn, request.EndOn, request.SignupOpensAt, request.SignupClosesAt, request.Status);

        var program = await dbContext.Programs
            .FirstOrDefaultAsync(p => p.Id == request.ProgramId && p.ClubId == scope.ClubId, cancellationToken)
            ?? throw new ProgramNotFoundForSessionException();

        var venueId = await ResolveVenueAsync(request.VenueId, cancellationToken);

        var now = DateTime.UtcNow;
        var session = new Session
        {
            Id = sessionId,
            ClubId = scope.ClubId,
            ProgramId = program.Id,
            VenueId = venueId,
            StartOn = request.StartOn,
            EndOn = request.EndOn,
            WeeklySchedule = request.WeeklySchedule,
            Capacity = request.Capacity,
            EnrolledCount = 0,
            Price = request.Price,
            EarlyBirdPrice = request.EarlyBirdPrice,
            EarlyBirdUntil = request.EarlyBirdUntil,
            SignupOpensAt = request.SignupOpensAt,
            SignupClosesAt = request.SignupClosesAt,
            Status = request.Status ?? DeriveDefaultStatus(request.Capacity, enrolledCount: 0),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicCachesAsync(scope, cancellationToken);
        return ToDetailDto(session);
    }

    public async Task<AdminSessionDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminSessionRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
        {
            return null;
        }

        if (session.ClubId != scope.ClubId)
        {
            return null; // 跨俱樂部：回 404，不洩漏存在與否。
        }

        Validate(request.WeeklySchedule, request.Capacity, request.Price, request.EarlyBirdPrice,
            request.StartOn, request.EndOn, request.SignupOpensAt, request.SignupClosesAt, request.Status);

        var venueId = await ResolveVenueAsync(request.VenueId, cancellationToken);

        session.VenueId = venueId;
        session.StartOn = request.StartOn;
        session.EndOn = request.EndOn;
        session.WeeklySchedule = request.WeeklySchedule;
        session.Capacity = request.Capacity;
        session.Price = request.Price;
        session.EarlyBirdPrice = request.EarlyBirdPrice;
        session.EarlyBirdUntil = request.EarlyBirdUntil;
        session.SignupOpensAt = request.SignupOpensAt;
        session.SignupClosesAt = request.SignupClosesAt;
        session.Status = request.Status ?? DeriveDefaultStatus(request.Capacity, session.EnrolledCount);
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = operatorId;

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePublicCachesAsync(scope, cancellationToken);
        return ToDetailDto(session);
    }

    /// <summary>寫入端沒有指定 <c>Status</c> 時的自動推定：額滿即關閉、否則開放。「候補」與
    /// 「已結束」只能由人工設定（額滿之後是否接受候補、活動何時真正結束都不是單看名額能推定的
    /// 資訊）。</summary>
    internal static string DeriveDefaultStatus(int? capacity, int enrolledCount)
        => capacity is int c && enrolledCount >= c ? "額滿" : "開放";

    private async Task<Guid?> ResolveVenueAsync(Guid? venueId, CancellationToken cancellationToken)
    {
        if (venueId is null)
        {
            return null;
        }

        var exists = await dbContext.Venues.AsNoTracking().AnyAsync(v => v.Id == venueId, cancellationToken);
        if (!exists)
        {
            throw new AdminSessionValidationException("找不到指定的場地，請確認場地是否存在。");
        }

        return venueId;
    }

    private static void Validate(
        string? weeklySchedule, int? capacity, int? price, int? earlyBirdPrice,
        DateOnly? startOn, DateOnly? endOn, DateTime? signupOpensAt, DateTime? signupClosesAt, string? status)
    {
        AdminProgramsRepository.ValidateContentJson(weeklySchedule);

        if (capacity is int cap && cap < 0)
        {
            throw new AdminSessionValidationException("名額上限不能是負數。");
        }
        if (price is int p && p < 0)
        {
            throw new AdminSessionValidationException("費用不能是負數。");
        }
        if (earlyBirdPrice is int eb && eb < 0)
        {
            throw new AdminSessionValidationException("早鳥價不能是負數。");
        }
        if (startOn is DateOnly s && endOn is DateOnly e && s > e)
        {
            throw new AdminSessionValidationException("開始日期不能晚於結束日期。");
        }
        if (signupOpensAt is DateTime so && signupClosesAt is DateTime sc && so > sc)
        {
            throw new AdminSessionValidationException("報名開始時間不能晚於報名截止時間。");
        }
        if (status is not null && !AllowedStatuses.Contains(status))
        {
            throw new AdminSessionValidationException("狀態只能是「開放」「額滿」「候補」或「已結束」。");
        }
    }

    /// <summary>梯次名額異動會影響 05 課程前台的可報名狀態顯示（<c>Features/Programs</c>），
    /// 這裡跟 <c>Features/AdminPrograms/AdminProgramsRepository</c> 一樣做寫入後失效——梯次本身
    /// 沒有自己的 <c>entity</c> 快取鍵，公開端點是隨課程項目一併查出梯次清單，故失效
    /// <c>"programs"</c> 這個既有 entity 即可，不需要另開一個。</summary>
    private async Task InvalidatePublicCachesAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => await cache.InvalidateAsync("programs", scope.ClubCode, cancellationToken);

    private static AdminSessionDetailDto ToDetailDto(Session session) => new()
    {
        Id = session.Id,
        ProgramId = session.ProgramId,
        VenueId = session.VenueId,
        StartOn = session.StartOn,
        EndOn = session.EndOn,
        WeeklySchedule = session.WeeklySchedule,
        Capacity = session.Capacity,
        EnrolledCount = session.EnrolledCount,
        Price = session.Price,
        EarlyBirdPrice = session.EarlyBirdPrice,
        EarlyBirdUntil = session.EarlyBirdUntil,
        SignupOpensAt = session.SignupOpensAt,
        SignupClosesAt = session.SignupClosesAt,
        Status = session.Status ?? "開放",
        CreatedAt = session.CreatedAt,
        UpdatedAt = session.UpdatedAt,
    };
}
