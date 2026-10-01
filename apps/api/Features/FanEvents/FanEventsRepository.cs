using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Features.MemberAuth;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.FanEvents;

/// <summary>
/// 8.2 球迷會活動的公開讀取與報名（主站規劃書 §3.8：「活動列表＋報名（可限定僅付費會員）＋活動回顧」）。
/// 報名規則與後台 F2（<c>AdminFanEventsRepository</c>）一致：已報名＋已到場佔名額、候補不佔；<b>名額已滿自動進候補</b>；同一會員不得重複報名。
/// 限付費會員的活動需要<b>會員權杖</b>，且該會員在這個俱樂部持有<b>有效的球迷會員會籍</b>（以到期日為準）；非限定的活動非會員也能報名（留姓名與電話或 Email）。
/// 並行安全：報名在交易內鎖住活動列再數名額，不會超收。
/// 🔴 這個端點接收非會員的個資（姓名、電話、Email），<b>公開寫入一律限流</b>（端點層），個資不回傳給任何人（報名者本人只看到自己的狀態）。
/// </summary>
public sealed class FanEventsRepository(ClubDbContext db, IImagePublicUrlResolver imageUrls)
{
    private static readonly string[] OccupyingStatuses = ["registered", "attended"];

    private static readonly IReadOnlyDictionary<string, (string Zh, string En)> StatusLabels = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["registered"] = ("已報名", "Registered"),
        ["waitlist"] = ("候補", "Waitlisted"),
        ["cancelled"] = ("已取消", "Cancelled"),
        ["attended"] = ("已到場", "Attended"),
    };

    private IQueryable<FanEvent> Published(ClubScope club)
        => db.FanEvents.AsNoTracking().Where(e => e.ClubId == club.ClubId && e.Status == "published");

    public async Task<IReadOnlyList<FanEventListItemDto>> ListAsync(ClubScope club, string? phase, string dbLocale, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var query = Published(club);
        // 已結束＝結束時間（沒有就用開始時間）已過；尚未結束＝其餘（含沒有時間的）。
        if (phase == "past")
        {
            query = query.Where(e => (e.EndsAt ?? e.StartsAt) < now);
        }
        else if (phase == "upcoming")
        {
            query = query.Where(e => (e.EndsAt ?? e.StartsAt) == null || (e.EndsAt ?? e.StartsAt) >= now);
        }

        var rows = await query.Include(e => e.FanEventsI18ns).OrderBy(e => e.StartsAt == null ? 1 : 0).ThenBy(e => e.StartsAt).ThenBy(e => e.RowSeq)
            .AsSplitQuery().ToListAsync(cancellationToken);
        if (phase == "past")
        {
            rows = rows.OrderByDescending(e => e.StartsAt).ToList();
        }

        var counts = await OccupiedCountsAsync(rows.Select(r => r.Id).ToList(), cancellationToken);
        return rows.Select(e => ToListItem(e, dbLocale, counts.GetValueOrDefault(e.Id), now)).ToList();
    }

    public async Task<FanEventDetailDto?> GetAsync(ClubScope club, string slug, Guid? memberId, string dbLocale, CancellationToken cancellationToken)
    {
        var e = await Published(club).Include(x => x.FanEventsI18ns).Include(x => x.FanEventImages).Include(x => x.FanEventArticles)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);
        if (e is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var counts = await OccupiedCountsAsync([e.Id], cancellationToken);
        var requested = e.FanEventsI18ns.FirstOrDefault(i => i.Locale == dbLocale);
        var fallback = e.FanEventsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);

        string? venueName = null;
        if (e.VenueId is Guid venueId)
        {
            var venue = await db.VenuesI18ns.AsNoTracking().Where(v => v.VenueId == venueId && (v.Locale == dbLocale || v.Locale == RequestLocale.DefaultDbLocale)).ToListAsync(cancellationToken);
            venueName = RequestLocale.Pick(venue.FirstOrDefault(v => v.Locale == dbLocale)?.Name, venue.FirstOrDefault(v => v.Locale == RequestLocale.DefaultDbLocale)?.Name);
        }

        var articleIds = e.FanEventArticles.OrderBy(a => a.SortOrder).Select(a => a.ArticleId).ToList();
        var articles = articleIds.Count == 0
            ? []
            : await db.Articles.AsNoTracking().Include(a => a.ArticlesI18ns)
                .Where(a => articleIds.Contains(a.Id) && a.Status == "published" && (a.PublishedAt == null || a.PublishedAt <= now))
                .ToListAsync(cancellationToken);

        FanEventMyRegistrationDto? mine = null;
        if (memberId is Guid mid)
        {
            var reg = await db.FanEventRegistrations.AsNoTracking()
                .Where(r => r.FanEventId == e.Id && r.MemberId == mid && r.Status != "cancelled").Select(r => r.Status).FirstOrDefaultAsync(cancellationToken);
            if (reg is not null)
            {
                mine = new FanEventMyRegistrationDto { Status = reg, StatusLabel = StatusLabel(reg, dbLocale == "en") };
            }
        }

        return new FanEventDetailDto
        {
            Event = ToListItem(e, dbLocale, counts.GetValueOrDefault(e.Id), now),
            Description = RequestLocale.Pick(requested?.Description, fallback?.Description), VenueName = venueName,
            Images = e.FanEventImages.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq)
                .Select(i => new FanEventImagePublicDto { ImageUrl = imageUrls.Resolve(i.ImageKey), ImageThumbUrl = imageUrls.Resolve(ImageObjectKey.ForThumbnail(i.ImageKey)), Width = i.ImageWidth, Height = i.ImageHeight }).ToList(),
            Articles = articleIds.Select(id => articles.FirstOrDefault(a => a.Id == id)).OfType<Article>()
                .Select(a => new FanEventArticlePublicDto
                {
                    Slug = a.Slug,
                    Title = RequestLocale.Pick(a.ArticlesI18ns.FirstOrDefault(i => i.Locale == dbLocale)?.Title, a.ArticlesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Title),
                }).ToList(),
            MyRegistration = mine,
        };
    }

    // ═══════════════════════════ 報名 ═══════════════════════════

    public async Task<FanEventRegistrationResultDto> RegisterAsync(
        ClubScope club, string slug, FanEventRegisterRequest request, MemberIdentity? member, string dbLocale, CancellationToken cancellationToken)
    {
        var en = dbLocale == "en";
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note is { Length: > 500 })
        {
            throw new MemberValidationException("備註不可超過 500 字。");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var eventId = await Published(club).Where(e => e.Slug == slug).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(cancellationToken)
                      ?? throw new MemberNotFoundException("找不到這場活動。");
        // 鎖住活動列：同一場活動的報名一次一個，名額計算才不會被並行請求打穿。
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM fan_events WITH (UPDLOCK, ROWLOCK) WHERE id = {eventId}", cancellationToken);
        var ev = await db.FanEvents.AsNoTracking().FirstAsync(e => e.Id == eventId, cancellationToken);

        var now = DateTime.UtcNow;
        if ((ev.RegistrationDeadlineAt is DateTime deadline && deadline < now) || (ev.StartsAt is DateTime starts && starts < now))
        {
            throw new MemberConflictException("報名已截止", "這場活動已經截止報名或已經開始。", "registration_closed");
        }

        string? name = null, phone = null, email = null;
        if (ev.IsPaidMembersOnly)
        {
            if (member is null)
            {
                throw new MemberUnauthenticatedException("這場活動限付費球迷會員報名，請先登入。", "login_required");
            }

            if (!await HasPaidMembershipAsync(club.ClubId, member.MemberId, cancellationToken))
            {
                throw new MemberForbiddenException("這場活動限付費球迷會員報名，你目前沒有有效的球迷會員會籍。", "fan_club_required");
            }
        }

        if (member is not null)
        {
            if (await db.FanEventRegistrations.AsNoTracking().AnyAsync(r => r.FanEventId == eventId && r.MemberId == member.MemberId && r.Status != "cancelled", cancellationToken))
            {
                throw new MemberConflictException("已報名", "你已經報名這場活動了。", "already_registered");
            }
        }
        else
        {
            name = (request.ApplicantName ?? string.Empty).Trim();
            if (name.Length is 0 or > 64)
            {
                throw new MemberValidationException("請填寫姓名（64 字以內）。");
            }

            phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            if (phone is not null && (phone.Length > 32 || !phone.All(c => char.IsDigit(c) || c is '+' or '-' or ' ' or '(' or ')')))
            {
                throw new MemberValidationException("電話格式不正確。");
            }

            email = string.IsNullOrWhiteSpace(request.Email) ? null : MemberAuthService.NormalizeEmail(request.Email);
            if (phone is null && email is null)
            {
                throw new MemberValidationException("請至少填寫電話或 Email，方便聯絡。");
            }

            // 非會員重複報名：同一個 Email 或電話已有未取消的報名就擋下。
            if (await db.FanEventRegistrations.AsNoTracking().AnyAsync(r => r.FanEventId == eventId && r.Status != "cancelled"
                    && ((email != null && r.Email == email) || (phone != null && r.Phone == phone)), cancellationToken))
            {
                throw new MemberConflictException("已報名", "這個 Email 或電話已經報名這場活動了。", "already_registered");
            }
        }

        var occupied = await db.FanEventRegistrations.AsNoTracking().CountAsync(r => r.FanEventId == eventId && OccupyingStatuses.Contains(r.Status), cancellationToken);
        var status = ev.Capacity is int capacity && occupied >= capacity ? "waitlist" : "registered";
        db.FanEventRegistrations.Add(new FanEventRegistration
        {
            Id = Guid.NewGuid(), ClubId = club.ClubId, FanEventId = eventId, MemberId = member?.MemberId, Status = status,
            ApplicantName = name, Phone = phone, Email = email, Note = note, CreatedAt = now, UpdatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new FanEventRegistrationResultDto { Status = status, StatusLabel = StatusLabel(status, en), IsWaitlisted = status == "waitlist" };
    }

    /// <summary>會員取消自己的報名（已報名或候補）。取消已報名者不會自動遞補候補者（由客服人工處理，同後台 F2）。</summary>
    public async Task CancelMyRegistrationAsync(ClubScope club, string slug, Guid memberId, CancellationToken cancellationToken)
    {
        var eventId = await Published(club).Where(e => e.Slug == slug).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(cancellationToken)
                      ?? throw new MemberNotFoundException("找不到這場活動。");
        var now = DateTime.UtcNow;
        var affected = await db.FanEventRegistrations.Where(r => r.FanEventId == eventId && r.MemberId == memberId && (r.Status == "registered" || r.Status == "waitlist"))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "cancelled").SetProperty(r => r.UpdatedAt, now), cancellationToken);
        if (affected == 0)
        {
            throw new MemberNotFoundException("你沒有這場活動的有效報名。");
        }
    }

    private async Task<bool> HasPaidMembershipAsync(Guid clubId, Guid memberId, CancellationToken cancellationToken)
    {
        var today = TaiwanClock.Today;
        return await db.Memberships.AsNoTracking().AnyAsync(m => m.MemberId == memberId && m.ClubId == clubId && m.Tier == "fan_club"
            && m.Status == "active" && (m.MembershipEndOn == null || m.MembershipEndOn >= today), cancellationToken);
    }

    // ═══════════════════════════ 共用 ═══════════════════════════

    private async Task<Dictionary<Guid, int>> OccupiedCountsAsync(IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken)
        => await db.FanEventRegistrations.AsNoTracking().Where(r => eventIds.Contains(r.FanEventId) && OccupyingStatuses.Contains(r.Status))
            .GroupBy(r => r.FanEventId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

    private FanEventListItemDto ToListItem(FanEvent e, string dbLocale, int occupied, DateTime now)
    {
        var requested = e.FanEventsI18ns.FirstOrDefault(i => i.Locale == dbLocale);
        var fallback = e.FanEventsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var full = e.Capacity is int cap && occupied >= cap;
        var end = e.EndsAt ?? e.StartsAt;
        var open = (e.RegistrationDeadlineAt is null || e.RegistrationDeadlineAt >= now) && (e.StartsAt is null || e.StartsAt >= now);
        return new FanEventListItemDto
        {
            Slug = e.Slug, Name = RequestLocale.Pick(requested?.Name, fallback?.Name), Location = RequestLocale.Pick(requested?.Location, fallback?.Location),
            StartsAt = e.StartsAt, EndsAt = e.EndsAt, RegistrationDeadlineAt = e.RegistrationDeadlineAt, Capacity = e.Capacity,
            SpotsLeft = e.Capacity is int c ? Math.Max(0, c - occupied) : null, IsPaidMembersOnly = e.IsPaidMembersOnly,
            IsRegistrationOpen = open, IsFull = full, Phase = end is DateTime endAt && endAt < now ? "past" : "upcoming",
            CoverUrl = e.CoverKey is null ? null : imageUrls.Resolve(e.CoverKey),
            CoverThumbUrl = e.CoverKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(e.CoverKey)),
        };
    }

    private static string StatusLabel(string status, bool en)
        => StatusLabels.TryGetValue(status, out var l) ? (en ? l.En : l.Zh) : status;
}
