using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Programs;

/// <summary>05 課程與活動公開讀取＋報名送出（主站規劃書 §3.5）。<c>programs.club_id</c> 是
/// 50 張必填表之一，不像 <c>staff</c> 需要 <see cref="Data.ClubOrSharedSql"/> 回退共同資料，
/// 直接 <c>WHERE club_id = @ClubId</c> 即可。</summary>
public sealed class ProgramsRepository(IClubSqlConnectionFactory connectionFactory, IQueryCache cache)
{
    private const string CacheEntity = "programs";

    private sealed record ProgramRow(
        Guid Id, string Slug, string? ProgramType, string? Audience, int? AgeMin, int? AgeMax, string? CoverKey, bool HasOpenSession);
    private sealed record ProgramI18nRow(string Locale, string? Name, string? Intro, string? Content);
    private sealed record StaffRow(Guid Id, string? Name);
    private sealed record PartnerRow(Guid Id, string Slug, string? Name, string? LogoDarkKey, string? LogoLightKey, string? WebsiteUrl);
    private sealed record SessionRow(
        Guid Id, DateOnly? StartOn, DateOnly? EndOn, string? WeeklySchedule, int? Capacity, int EnrolledCount,
        int? Price, int? EarlyBirdPrice, DateOnly? EarlyBirdUntil, DateTime? SignupOpensAt, DateTime? SignupClosesAt,
        string Status, Guid? VenueId, string? VenueName, string? VenueAddress, decimal? VenueLat, decimal? VenueLng);
    private sealed record SessionGateRow(Guid Id, string? Status, DateTime? SignupOpensAt, DateTime? SignupClosesAt);

    /// <summary>只服務 <c>session_id</c>——見 <see cref="SubmitProgramRegistrationRequest"/> 上的說明。</summary>
    private static readonly HashSet<string> SessionOccupyingResultStatus = new(StringComparer.Ordinal) { "待確認" };

    public async Task<PagedResult<ProgramListItemDto>> ListAsync(
        ClubScope scope, string? programType, string dbLocale, int page, int pageSize, CancellationToken cancellationToken)
    {
        var qualifier = $"{programType ?? CacheDimensions.NoQualifier}:{page}:{pageSize}";

        return await cache.GetOrCreateAsync(
            CacheEntity, scope.ClubCode, dbLocale, qualifier,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                const string countSql = """
                    SELECT COUNT(*) FROM programs p
                    WHERE p.club_id = @ClubId AND p.status = 'published'
                      AND (@ProgramType IS NULL OR p.program_type = @ProgramType)
                    """;

                const string listSql = """
                    SELECT p.id AS Id, p.slug AS Slug, p.program_type AS ProgramType, p.audience AS Audience,
                           p.age_min AS AgeMin, p.age_max AS AgeMax, p.cover_key AS CoverKey,
                           CAST(CASE WHEN EXISTS (
                               SELECT 1 FROM sessions s WHERE s.program_id = p.id AND s.status IN (N'開放', N'候補')
                           ) THEN 1 ELSE 0 END AS bit) AS HasOpenSession
                    FROM programs p
                    WHERE p.club_id = @ClubId AND p.status = 'published'
                      AND (@ProgramType IS NULL OR p.program_type = @ProgramType)
                    ORDER BY p.row_seq
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
                    """;

                var parameters = new { scope.ClubId, ProgramType = programType, Offset = (page - 1) * pageSize, PageSize = pageSize };

                var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: ct));
                var rows = (await connection.QueryAsync<ProgramRow>(new CommandDefinition(listSql, parameters, cancellationToken: ct))).AsList();

                var programIds = rows.Select(r => r.Id).ToList();
                var i18nById = await LoadI18nAsync(connection, programIds, dbLocale, ct);

                var items = rows.Select(r =>
                {
                    var i18n = i18nById.GetValueOrDefault(r.Id);
                    var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
                    var requested = i18n?.GetValueOrDefault(dbLocale);

                    return new ProgramListItemDto
                    {
                        Id = r.Id,
                        Slug = r.Slug,
                        ProgramType = r.ProgramType,
                        Audience = r.Audience,
                        AgeMin = r.AgeMin,
                        AgeMax = r.AgeMax,
                        CoverKey = r.CoverKey,
                        Name = RequestLocale.Pick(requested?.Name, fallback?.Name),
                        Intro = RequestLocale.Pick(requested?.Intro, fallback?.Intro),
                        HasOpenSession = r.HasOpenSession,
                    };
                }).ToList();

                return new PagedResult<ProgramListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
            },
            cancellationToken);
    }

    public async Task<ProgramDetailDto?> GetBySlugAsync(ClubScope scope, string slug, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            CacheEntity, scope.ClubCode, dbLocale, slug,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                const string programSql = """
                    SELECT id AS Id, slug AS Slug, program_type AS ProgramType, audience AS Audience,
                           age_min AS AgeMin, age_max AS AgeMax, cover_key AS CoverKey,
                           CAST(0 AS bit) AS HasOpenSession
                    FROM programs
                    WHERE club_id = @ClubId AND slug = @Slug AND status = 'published'
                    """;

                var program = await connection.QuerySingleOrDefaultAsync<ProgramRow>(
                    new CommandDefinition(programSql, new { scope.ClubId, Slug = slug }, cancellationToken: ct));
                if (program is null)
                {
                    return null;
                }

                var i18nById = await LoadI18nAsync(connection, [program.Id], dbLocale, ct);
                var i18n = i18nById.GetValueOrDefault(program.Id);
                var fallback = i18n?.GetValueOrDefault(RequestLocale.DefaultDbLocale);
                var requested = i18n?.GetValueOrDefault(dbLocale);

                const string staffSql = """
                    SELECT s.id AS Id,
                           COALESCE(
                               (SELECT name FROM staff_i18n WHERE staff_id = s.id AND locale = @Locale),
                               (SELECT name FROM staff_i18n WHERE staff_id = s.id AND locale = @DefaultLocale)
                           ) AS Name
                    FROM program_staff ps
                    JOIN staff s ON s.id = ps.staff_id
                    WHERE ps.program_id = @ProgramId
                    """;
                var staff = (await connection.QueryAsync<StaffRow>(new CommandDefinition(
                    staffSql, new { ProgramId = program.Id, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: ct))).AsList();

                const string partnerSql = """
                    SELECT pt.id AS Id, pt.slug AS Slug, pt.logo_dark_key AS LogoDarkKey, pt.logo_light_key AS LogoLightKey,
                           pt.website_url AS WebsiteUrl,
                           COALESCE(
                               (SELECT name FROM partners_i18n WHERE partner_id = pt.id AND locale = @Locale),
                               (SELECT name FROM partners_i18n WHERE partner_id = pt.id AND locale = @DefaultLocale)
                           ) AS Name
                    FROM program_partners pp
                    JOIN partners pt ON pt.id = pp.partner_id
                    WHERE pp.program_id = @ProgramId
                    """;
                var partners = (await connection.QueryAsync<PartnerRow>(new CommandDefinition(
                    partnerSql, new { ProgramId = program.Id, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: ct))).AsList();

                const string sessionSql = """
                    SELECT s.id AS Id, s.start_on AS StartOn, s.end_on AS EndOn, s.weekly_schedule AS WeeklySchedule,
                           s.capacity AS Capacity, s.enrolled_count AS EnrolledCount, s.price AS Price,
                           s.early_bird_price AS EarlyBirdPrice, s.early_bird_until AS EarlyBirdUntil,
                           s.signup_opens_at AS SignupOpensAt, s.signup_closes_at AS SignupClosesAt,
                           s.status AS Status, s.venue_id AS VenueId,
                           COALESCE(
                               (SELECT name FROM venues_i18n WHERE venue_id = s.venue_id AND locale = @Locale),
                               (SELECT name FROM venues_i18n WHERE venue_id = s.venue_id AND locale = @DefaultLocale)
                           ) AS VenueName,
                           COALESCE(
                               (SELECT address FROM venues_i18n WHERE venue_id = s.venue_id AND locale = @Locale),
                               (SELECT address FROM venues_i18n WHERE venue_id = s.venue_id AND locale = @DefaultLocale)
                           ) AS VenueAddress,
                           v.lat AS VenueLat, v.lng AS VenueLng
                    FROM sessions s
                    LEFT JOIN venues v ON v.id = s.venue_id
                    WHERE s.program_id = @ProgramId
                    ORDER BY s.start_on
                    """;
                var sessions = (await connection.QueryAsync<SessionRow>(new CommandDefinition(
                    sessionSql, new { ProgramId = program.Id, Locale = dbLocale, DefaultLocale = RequestLocale.DefaultDbLocale }, cancellationToken: ct))).AsList();

                return new ProgramDetailDto
                {
                    Id = program.Id,
                    Slug = program.Slug,
                    ProgramType = program.ProgramType,
                    Audience = program.Audience,
                    AgeMin = program.AgeMin,
                    AgeMax = program.AgeMax,
                    CoverKey = program.CoverKey,
                    Name = RequestLocale.Pick(requested?.Name, fallback?.Name),
                    Intro = RequestLocale.Pick(requested?.Intro, fallback?.Intro),
                    Content = RequestLocale.Pick(requested?.Content, fallback?.Content),
                    Staff = staff.Select(s => new ProgramStaffSummaryDto { Id = s.Id, Name = s.Name }).ToList(),
                    Partners = partners.Select(p => new ProgramPartnerSummaryDto
                    {
                        Id = p.Id, Slug = p.Slug, Name = p.Name, LogoDarkKey = p.LogoDarkKey, LogoLightKey = p.LogoLightKey, WebsiteUrl = p.WebsiteUrl,
                    }).ToList(),
                    Sessions = sessions.Select(s => new ProgramSessionDto
                    {
                        Id = s.Id,
                        StartOn = s.StartOn,
                        EndOn = s.EndOn,
                        WeeklySchedule = s.WeeklySchedule,
                        Capacity = s.Capacity,
                        EnrolledCount = s.EnrolledCount,
                        Price = s.Price,
                        EarlyBirdPrice = s.EarlyBirdPrice,
                        EarlyBirdUntil = s.EarlyBirdUntil,
                        SignupOpensAt = s.SignupOpensAt,
                        SignupClosesAt = s.SignupClosesAt,
                        Status = s.Status,
                        VenueId = s.VenueId,
                        VenueName = s.VenueName,
                        VenueAddress = s.VenueAddress,
                        VenueLat = s.VenueLat,
                        VenueLng = s.VenueLng,
                    }).ToList(),
                };
            },
            cancellationToken);
    }

    /// <summary>
    /// 公開報名送出（主站規劃書 3.5 行 389）。<c>sessionId</c> 必須屬於 <paramref name="scope"/>
    /// 當下的俱樂部，且梯次狀態不是「已結束」。
    ///
    /// 🔴 **名額判定用單一原子 SQL 陳述式完成，不是「先查再寫」**——<c>UPDATE ... WHERE status =
    /// N'開放' AND (capacity IS NULL OR enrolled_count &lt; capacity)</c> 只有在梯次「目前正是
    /// 開放中且還有名額」時才會真的更新到那一列（影響列數＝1）；梯次目前是「額滿」「候補」或
    /// 「已結束」（已結束在更早一步被擋下）、或者兩個訪客同時搶最後一位名額時，落敗的那一次呼叫
    /// 影響列數會是 0——這一次呼叫直接判定为「候補」，不需要额外的重試或鎖定語法，SQL Server 對
    /// 單一 <c>UPDATE</c> 陳述式本身就有隱含的列鎖定保護，兩個併發請求不會同時判定「還有名額」。
    /// 這是規劃書「額滿自動關閉、候補遞補」在報名寫入路徑上的落點。
    /// </summary>
    public async Task<ProgramRegistrationSubmittedDto> SubmitRegistrationAsync(
        ClubScope scope, Guid sessionId, SubmitProgramRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApplicantName))
        {
            throw new ProgramRegistrationValidationException("學員姓名為必填欄位。");
        }
        if (string.IsNullOrWhiteSpace(request.Phone) && string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ProgramRegistrationValidationException("電話與 Email 至少需要填寫一項，以便後續聯繫。");
        }

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        const string sessionSql = """
            SELECT id AS Id, status AS Status, signup_opens_at AS SignupOpensAt, signup_closes_at AS SignupClosesAt
            FROM sessions
            WHERE id = @SessionId AND club_id = @ClubId
            """;
        var session = await connection.QuerySingleOrDefaultAsync<SessionGateRow>(
            new CommandDefinition(sessionSql, new { SessionId = sessionId, scope.ClubId }, transaction, cancellationToken: cancellationToken));

        if (session is null)
        {
            throw new ProgramSessionNotFoundException();
        }
        if (session.Status == "已結束")
        {
            throw new ProgramRegistrationValidationException("這個梯次已經結束報名。");
        }

        var now = DateTime.UtcNow;
        if (session.SignupOpensAt is DateTime opensAt && now < opensAt)
        {
            throw new ProgramRegistrationValidationException("這個梯次尚未開放報名。");
        }
        if (session.SignupClosesAt is DateTime closesAt && now > closesAt)
        {
            throw new ProgramRegistrationValidationException("這個梯次的報名已經截止。");
        }

        const string claimSlotSql = """
            UPDATE sessions
            SET enrolled_count = enrolled_count + 1,
                status = CASE WHEN capacity IS NOT NULL AND enrolled_count + 1 >= capacity THEN N'額滿' ELSE status END,
                updated_at = SYSUTCDATETIME()
            OUTPUT INSERTED.id
            WHERE id = @SessionId AND status = N'開放' AND (capacity IS NULL OR enrolled_count < capacity)
            """;
        var claimedId = await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(claimSlotSql, new { SessionId = sessionId }, transaction, cancellationToken: cancellationToken));

        var status = claimedId is not null ? "待確認" : "候補";

        var registrationNo = await GenerateUniqueRegistrationNoAsync(connection, transaction, scope.ClubCode, now, cancellationToken);

        const string insertSql = """
            INSERT INTO registrations
                (id, registration_no, club_id, session_id, applicant_name, phone, email, birth_on,
                 guardian_name, guardian_phone, health_declaration, note, status, created_at, updated_at)
            VALUES
                (@Id, @RegistrationNo, @ClubId, @SessionId, @ApplicantName, @Phone, @Email, @BirthOn,
                 @GuardianName, @GuardianPhone, @HealthDeclaration, @Note, @Status, @Now, @Now)
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertSql, new
        {
            Id = Guid.NewGuid(),
            RegistrationNo = registrationNo,
            scope.ClubId,
            SessionId = sessionId,
            request.ApplicantName,
            request.Phone,
            request.Email,
            request.BirthOn,
            request.GuardianName,
            request.GuardianPhone,
            request.HealthDeclaration,
            request.Note,
            Status = status,
            Now = now,
        }, transaction, cancellationToken: cancellationToken));

        transaction.Commit();

        await cache.InvalidateAsync(CacheEntity, scope.ClubCode, cancellationToken);
        return new ProgramRegistrationSubmittedDto { RegistrationNo = registrationNo, Status = status };
    }

    private static async Task<string> GenerateUniqueRegistrationNoAsync(
        System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, string clubCode, DateTime now, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = RegistrationNumberGenerator.Generate(clubCode, now);
            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM registrations WHERE registration_no = @No", new { No = candidate }, transaction, cancellationToken: cancellationToken));
            if (exists == 0)
            {
                return candidate;
            }
        }

        throw new ProgramRegistrationValidationException("報名編號產生失敗，請重新送出一次。");
    }

    private static async Task<Dictionary<Guid, Dictionary<string, ProgramI18nRow>>> LoadI18nAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> programIds, string dbLocale, CancellationToken cancellationToken)
    {
        if (programIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT program_id AS ProgramId, locale AS Locale, name AS Name, intro AS Intro, content AS Content
            FROM programs_i18n
            WHERE program_id IN @ProgramIds AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = await connection.QueryAsync<(Guid ProgramId, string Locale, string? Name, string? Intro, string? Content)>(
            new CommandDefinition(sql, new { ProgramIds = programIds, Locales = locales }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.ProgramId).ToDictionary(
            g => g.Key,
            g => g.ToDictionary(r => r.Locale, r => new ProgramI18nRow(r.Locale, r.Name, r.Intro, r.Content)));
    }
}
