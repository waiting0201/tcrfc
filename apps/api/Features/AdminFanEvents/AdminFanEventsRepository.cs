using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminFanEvents;

/// <summary>
/// F2 球迷會活動（主站規劃書 §4.6 F2）：活動 CRUD、報名名單（可限定僅付費會員報名）、活動回顧（關聯圖集與文章）。
/// 會員名單、會籍方案與福利對照表都在 K 模組，這裡不另建。<c>fan_events.club_id</c> 必填，跨俱樂部 id 一律 404。
/// 報名名單含聯絡方式，視同個資：有 <c>member.pii.reveal</c> 才看得到完整值，其餘遮罩（F2 的角色不因此取得會員模組權限）。
/// 名額只算「已報名＋已到場」；額滿後後台代填的報名自動進候補。
/// </summary>
public sealed class AdminFanEventsRepository(
    ClubDbContext db, IImagePublicUrlResolver imageUrls, IPermissionChecker permissions, SensitiveActionLogger audit)
{
    public static readonly IReadOnlyDictionary<string, string> StatusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["draft"] = "草稿",
        ["published"] = "已發布",
    };

    public static readonly IReadOnlyDictionary<string, string> RegistrationLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["registered"] = "已報名",
        ["waitlist"] = "候補",
        ["cancelled"] = "已取消",
        ["attended"] = "已到場",
    };

    private static readonly string[] OccupyingStatuses = ["registered", "attended"];

    // ═════════════ 活動 ═════════════

    public async Task<IReadOnlyList<AdminFanEventListItemDto>> ListAsync(
        AdminClubScope scope, string? status, DateOnly? from, DateOnly? to, string? keyword, CancellationToken cancellationToken)
    {
        var query = db.FanEvents.AsNoTracking().Where(e => e.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, StatusLabels.Keys.ToHashSet(), "狀態", "「草稿」或「已發布」");
            query = query.Where(e => e.Status == status);
        }

        if (from is DateOnly f)
        {
            var fromUtc = TaiwanClock.StartOfDayUtc(f);
            query = query.Where(e => e.StartsAt >= fromUtc);
        }

        if (to is DateOnly t)
        {
            var toUtc = TaiwanClock.StartOfDayUtc(t.AddDays(1));
            query = query.Where(e => e.StartsAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(e => e.Slug.Contains(k) || e.FanEventsI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        var rows = await query.OrderByDescending(e => e.StartsAt).ThenByDescending(e => e.RowSeq)
            .Select(e => new
            {
                Event = e,
                Registered = e.FanEventRegistrations.Count(r => r.Status == "registered" || r.Status == "attended"),
                Waitlist = e.FanEventRegistrations.Count(r => r.Status == "waitlist"),
                Zh = e.FanEventsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                En = e.FanEventsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        return rows.Select(r => new AdminFanEventListItemDto
        {
            Id = r.Event.Id, Slug = r.Event.Slug, StartsAt = r.Event.StartsAt, EndsAt = r.Event.EndsAt,
            RegistrationDeadlineAt = r.Event.RegistrationDeadlineAt, Capacity = r.Event.Capacity, IsPaidMembersOnly = r.Event.IsPaidMembersOnly,
            Status = r.Event.Status, StatusLabel = StatusLabels[r.Event.Status], CoverKey = r.Event.CoverKey, CoverThumbUrl = ThumbUrl(r.Event.CoverKey),
            VenueId = r.Event.VenueId, NameZh = r.Zh, NameEn = r.En, RegisteredCount = r.Registered, WaitlistCount = r.Waitlist,
            IsRegistrationOpen = IsOpen(r.Event, r.Registered, now), UpdatedAt = r.Event.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminFanEventDetailDto?> GetAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var row = await LoadAsync(scope, id, tracking: false, cancellationToken);
        return row is null ? null : await ToDetailAsync(row, cancellationToken);
    }

    public async Task<AdminFanEventDetailDto> CreateAsync(
        AdminClubScope scope, Guid id, UpsertAdminFanEventRequest request, ImageFieldUpdate cover, CancellationToken cancellationToken)
    {
        var slug = Validate(request, currentRegistered: 0);
        await EnsureVenueAsync(request.VenueId, cancellationToken);
        var articles = request.ArticleIds is null ? [] : await ResolveArticlesAsync(scope, request.ArticleIds, cancellationToken);
        slug ??= AdminInput.GenerateSlug("event", request.Content.En?.Name);
        await EnsureSlugFreeAsync(scope, slug, null, cancellationToken);
        var now = DateTime.UtcNow;
        var row = new FanEvent
        {
            Id = id, ClubId = scope.ClubId, Slug = slug, CoverKey = cover.Change ? cover.Key : null, CreatedAt = now, UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        Apply(row, request);
        db.FanEvents.Add(row);
        SetI18n(row, request.Content);
        SetArticles(row, articles);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(scope, id, cancellationToken))!;
    }

    public async Task<AdminFanEventDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminFanEventRequest request, ImageFieldUpdate cover, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var row = await LoadAsync(scope, id, tracking: true, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var registered = await db.FanEventRegistrations.AsNoTracking().CountAsync(r => r.FanEventId == id && OccupyingStatuses.Contains(r.Status), cancellationToken);
        var newSlug = Validate(request, registered);
        await EnsureVenueAsync(request.VenueId, cancellationToken);
        if (newSlug is not null && !string.Equals(newSlug, row.Slug, StringComparison.Ordinal))
        {
            await EnsureSlugFreeAsync(scope, newSlug, id, cancellationToken);
            row.Slug = newSlug;
        }

        if (cover.Change)
        {
            orphans.Image(row.CoverKey);
            row.CoverKey = cover.Key;
        }

        Apply(row, request);
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = scope.Identity.AdminUserId;
        SetI18n(row, request.Content);
        if (request.ArticleIds is not null)
        {
            var articles = await ResolveArticlesAsync(scope, request.ArticleIds, cancellationToken);
            db.FanEventArticles.RemoveRange(row.FanEventArticles);
            row.FanEventArticles.Clear();
            SetArticles(row, articles);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var row = await db.FanEvents.Include(e => e.FanEventImages).FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        if (await db.FanEventRegistrations.AsNoTracking().AnyAsync(r => r.FanEventId == id, cancellationToken))
        {
            throw new AdminConflictException("活動已有報名紀錄", "這場活動已經有報名紀錄，不能刪除；請改為草稿（不公開）。");
        }

        orphans.Image(row.CoverKey);
        foreach (var image in row.FanEventImages)
        {
            orphans.Image(image.ImageKey);
        }

        db.FanEvents.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminFanEventDetailDto?> AddImagesAsync(
        AdminClubScope scope, Guid id, IReadOnlyList<UploadedImageInfo> images, CancellationToken cancellationToken)
    {
        var row = await db.FanEvents.FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var maxOrder = await db.FanEventImages.Where(i => i.FanEventId == id).Select(i => (int?)i.SortOrder).MaxAsync(cancellationToken);
        var next = (maxOrder ?? -1) + 1;
        var now = DateTime.UtcNow;
        foreach (var image in images)
        {
            db.FanEventImages.Add(new FanEventImage
            {
                Id = Guid.NewGuid(), FanEventId = id, ImageKey = image.Key, ImageWidth = image.Width, ImageHeight = image.Height, SortOrder = next++,
                CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
            });
        }

        row.UpdatedAt = now;
        row.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteImageAsync(AdminClubScope scope, Guid id, Guid imageId, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var row = await db.FanEvents.Include(e => e.FanEventImages).FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        var image = row?.FanEventImages.FirstOrDefault(i => i.Id == imageId);
        if (row is null || image is null)
        {
            return false;
        }

        orphans.Image(image.ImageKey);
        db.FanEventImages.Remove(image);
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminFanEventDetailDto?> ReorderImagesAsync(AdminClubScope scope, Guid id, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var row = await db.FanEvents.Include(e => e.FanEventImages).FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var ordered = row.FanEventImages.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).ToList();
        var order = AdminReorder.Compute(ordered.Select(i => i.Id).ToList(), ids, "圖片");
        var byId = ordered.ToDictionary(i => i.Id);
        for (var i = 0; i < order.Count; i++)
        {
            byId[order[i]].SortOrder = i;
        }

        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    // ═════════════ 報名名單 ═════════════

    private Task<bool> CanRevealAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => permissions.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, AdminMembersRepository.RevealPermission, cancellationToken);

    public async Task<IReadOnlyList<AdminFanEventRegistrationDto>?> ListRegistrationsAsync(
        AdminClubScope scope, Guid eventId, string? status, string? keyword, CancellationToken cancellationToken)
    {
        if (!await db.FanEvents.AsNoTracking().AnyAsync(e => e.Id == eventId && e.ClubId == scope.ClubId, cancellationToken))
        {
            return null;
        }

        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var query = db.FanEventRegistrations.AsNoTracking().Include(r => r.Member).Where(r => r.FanEventId == eventId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, RegistrationLabels.Keys.ToHashSet(), "狀態", "「已報名」「候補」「已取消」或「已到場」");
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 沒有解除遮罩權限時只比對會員編號，避免搜尋變成探測個資的工具（同 K1）。
            var k = keyword.Trim();
            query = canReveal
                ? query.Where(r => (r.Member != null && (r.Member.MemberNo.Contains(k) || r.Member.Name.Contains(k) || (r.Member.Phone != null && r.Member.Phone.Contains(k))))
                    || (r.ApplicantName != null && r.ApplicantName.Contains(k)) || (r.Phone != null && r.Phone.Contains(k)))
                : query.Where(r => r.Member != null && r.Member.MemberNo.Contains(k));
        }

        var rows = await query.OrderBy(r => r.Status == "waitlist" ? 1 : r.Status == "cancelled" ? 2 : 0).ThenBy(r => r.RowSeq).ToListAsync(cancellationToken);
        if (canReveal && rows.Count > 0)
        {
            audit.Record(scope, "檢視球迷會活動報名名單（完整）", $"活動 {eventId}", rows.Count);
        }

        return rows.Select(r => ToRegistrationDto(r, canReveal)).ToList();
    }

    public async Task<AdminFanEventRegistrationDto?> CreateRegistrationAsync(
        AdminClubScope scope, Guid eventId, CreateAdminFanEventRegistrationRequest request, CancellationToken cancellationToken)
    {
        var ev = await db.FanEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId && e.ClubId == scope.ClubId, cancellationToken);
        if (ev is null)
        {
            return null;
        }

        Member? member = null;
        string? name = null, phone = null, email = null;
        if (request.MemberId is Guid memberId)
        {
            member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
            if (member is null || member.Status == "deleted")
            {
                throw new AdminValidationException("找不到指定的會員，或這個帳號已刪除。");
            }

            if (await db.FanEventRegistrations.AsNoTracking().AnyAsync(r => r.FanEventId == eventId && r.MemberId == memberId && r.Status != "cancelled", cancellationToken))
            {
                throw new AdminConflictException("已報名", "這位會員已經報名這場活動了。");
            }
        }
        else
        {
            if (ev.IsPaidMembersOnly)
            {
                throw new AdminValidationException("這場活動限付費球迷會員報名，請選擇會員。");
            }

            name = AdminInput.RequireText(request.ApplicantName, "報名人姓名", 64);
            phone = AdminInput.OptionalText(request.Phone, "電話", 32);
            email = AdminInput.OptionalEmail(request.Email, "Email");
            if (phone is null && email is null)
            {
                throw new AdminValidationException("非會員報名請至少填寫電話或 Email，方便聯絡。");
            }
        }

        if (ev.IsPaidMembersOnly && member is not null && !await HasPaidMembershipAsync(scope, member.Id, cancellationToken))
        {
            throw new AdminValidationException("這場活動限付費球迷會員報名，這位會員目前沒有有效的球迷會員會籍。");
        }

        var registered = await db.FanEventRegistrations.AsNoTracking().CountAsync(r => r.FanEventId == eventId && OccupyingStatuses.Contains(r.Status), cancellationToken);
        var status = ev.Capacity is int cap && registered >= cap ? "waitlist" : "registered";
        var now = DateTime.UtcNow;
        var row = new FanEventRegistration
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, FanEventId = eventId, MemberId = member?.Id, Status = status,
            ApplicantName = name, Phone = phone, Email = email, Note = AdminInput.OptionalText(request.Note, "備註", 500),
            CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        db.FanEventRegistrations.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        row.Member = member;
        return ToRegistrationDto(row, await CanRevealAsync(scope, cancellationToken));
    }

    public async Task<AdminFanEventRegistrationDto?> UpdateRegistrationAsync(
        AdminClubScope scope, Guid eventId, Guid registrationId, UpdateAdminFanEventRegistrationRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Status, RegistrationLabels.Keys.ToHashSet(), "狀態", "「已報名」「候補」「已取消」或「已到場」");
        var ev = await db.FanEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId && e.ClubId == scope.ClubId, cancellationToken);
        var row = ev is null ? null : await db.FanEventRegistrations.Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.FanEventId == eventId && r.ClubId == scope.ClubId, cancellationToken);
        if (ev is null || row is null)
        {
            return null;
        }

        var wasOccupying = OccupyingStatuses.Contains(row.Status);
        var willOccupy = OccupyingStatuses.Contains(request.Status);
        if (request.Status == "attended" && row.Status != "registered" && row.Status != "attended")
        {
            throw new AdminValidationException("只有已報名的人才能標記為已到場。");
        }

        if (!wasOccupying && willOccupy)
        {
            if (row.MemberId is Guid memberId
                && await db.FanEventRegistrations.AsNoTracking().AnyAsync(r => r.FanEventId == eventId && r.MemberId == memberId && r.Status != "cancelled" && r.Id != row.Id, cancellationToken))
            {
                throw new AdminConflictException("已報名", "這位會員已經有另一筆有效的報名了。");
            }

            var registered = await db.FanEventRegistrations.AsNoTracking().CountAsync(r => r.FanEventId == eventId && OccupyingStatuses.Contains(r.Status), cancellationToken);
            if (ev.Capacity is int cap && registered >= cap)
            {
                throw new AdminConflictException("名額已滿", "這場活動的名額已經滿了，請先調高名額或取消其他人的報名。");
            }
        }

        row.Status = request.Status;
        if (request.Note is not null)
        {
            row.Note = AdminInput.OptionalText(request.Note, "備註", 500);
        }

        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return ToRegistrationDto(row, await CanRevealAsync(scope, cancellationToken));
    }

    private Task<bool> HasPaidMembershipAsync(AdminClubScope scope, Guid memberId, CancellationToken cancellationToken)
    {
        var today = TaiwanClock.Today;
        return db.Memberships.AsNoTracking().AnyAsync(m => m.MemberId == memberId && m.ClubId == scope.ClubId && m.Tier == "fan_club" && m.Status == "active"
            && (m.MembershipStartOn == null || m.MembershipStartOn <= today) && (m.MembershipEndOn == null || m.MembershipEndOn >= today), cancellationToken);
    }

    private static AdminFanEventRegistrationDto ToRegistrationDto(FanEventRegistration r, bool reveal)
    {
        var isMember = r.Member is not null;
        var name = isMember ? r.Member!.Name : r.ApplicantName;
        var phone = isMember ? r.Member!.Phone : r.Phone;
        var email = isMember ? r.Member!.Email : r.Email;
        return new AdminFanEventRegistrationDto
        {
            Id = r.Id, MemberId = r.MemberId, MemberNo = r.Member?.MemberNo, IsMember = isMember,
            ApplicantName = reveal ? name : PiiMasking.MaskName(name), Phone = reveal ? phone : PiiMasking.MaskPhone(phone),
            Email = reveal ? email : PiiMasking.MaskEmail(email), Status = r.Status, StatusLabel = RegistrationLabels[r.Status],
            Note = r.Note, CreatedAt = r.CreatedAt, IsMasked = !reveal,
        };
    }

    // ═════════════ 內部 ═════════════

    private static string? Validate(UpsertAdminFanEventRequest request, int currentRegistered)
    {
        AdminInput.OneOf(request.Status, StatusLabels.Keys.ToHashSet(), "狀態", "「草稿」或「已發布」");
        AdminInput.RequireText(request.Content.Zh.Name, "中文活動名稱", 128);
        AdminInput.OptionalText(request.Content.Zh.Location, "中文活動地點", 200);
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文活動名稱", 128);
            AdminInput.OptionalText(request.Content.En.Location, "英文活動地點", 200);
        }

        if (request.Capacity is int cap)
        {
            if (cap < 1)
            {
                throw new AdminValidationException("名額上限至少要 1 人；不限名額請留空。");
            }

            if (cap < currentRegistered)
            {
                throw new AdminConflictException("名額低於已報名人數", $"目前已有 {currentRegistered} 人報名，名額不能調到比這個少。");
            }
        }

        if (request.StartsAt is DateTime start && request.EndsAt is DateTime end && end < start)
        {
            throw new AdminValidationException("活動結束時間不可早於開始時間。");
        }

        if (request.StartsAt is DateTime s && request.RegistrationDeadlineAt is DateTime deadline && deadline > s)
        {
            throw new AdminValidationException("報名截止時間不可晚於活動開始時間。");
        }

        if (request.Status == "published" && request.StartsAt is null)
        {
            throw new AdminValidationException("發布活動前請先填寫活動開始時間。");
        }

        return string.IsNullOrWhiteSpace(request.Slug) ? null : AdminInput.Slug(request.Slug.Trim());
    }

    private static void Apply(FanEvent row, UpsertAdminFanEventRequest request)
    {
        row.StartsAt = Utc(request.StartsAt);
        row.EndsAt = Utc(request.EndsAt);
        row.RegistrationDeadlineAt = Utc(request.RegistrationDeadlineAt);
        row.Capacity = request.Capacity;
        row.IsPaidMembersOnly = request.IsPaidMembersOnly;
        row.VenueId = request.VenueId;
        row.Status = request.Status;
    }

    /// <summary>API 的時間戳一律是 UTC、JSON 不帶時區記號；無時區的輸入視為 UTC，只有明確標示本地時區的才換算。</summary>
    private static DateTime? Utc(DateTime? value) => value is DateTime v ? (v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc)) : null;

    private async Task EnsureVenueAsync(Guid? venueId, CancellationToken cancellationToken)
    {
        if (venueId is Guid v && !await db.Venues.AsNoTracking().AnyAsync(x => x.Id == v, cancellationToken))
        {
            throw new AdminValidationException("找不到指定的場地。");
        }
    }

    private async Task EnsureSlugFreeAsync(AdminClubScope scope, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await db.FanEvents.AsNoTracking().AnyAsync(e => e.ClubId == scope.ClubId && e.Slug == slug && e.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經被這個俱樂部的另一場活動使用，請換一個。");
        }
    }

    private async Task<IReadOnlyList<Article>> ResolveArticlesAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        var rows = await db.Articles.Where(a => distinct.Contains(a.Id) && (a.ClubId == scope.ClubId || a.ClubId == null)).ToListAsync(cancellationToken);
        if (rows.Count != distinct.Count)
        {
            throw new AdminValidationException("關聯文章含有不存在的文章，請重新選擇。");
        }

        return distinct.Select(id => rows.First(r => r.Id == id)).ToList();
    }

    private void SetArticles(FanEvent row, IReadOnlyList<Article> articles)
    {
        for (var i = 0; i < articles.Count; i++)
        {
            var link = new FanEventArticle { FanEventId = row.Id, ArticleId = articles[i].Id, SortOrder = i };
            row.FanEventArticles.Add(link);
            db.FanEventArticles.Add(link);
        }
    }

    private void SetI18n(FanEvent row, AdminFanEventContentInput content)
    {
        Upsert(row, RequestLocale.DefaultDbLocale, content.Zh);
        var en = row.FanEventsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(row, "en", content.En);
        }
        else if (en is not null)
        {
            row.FanEventsI18ns.Remove(en);
            db.FanEventsI18ns.Remove(en);
        }
    }

    private void Upsert(FanEvent row, string locale, AdminFanEventLocaleContent content)
    {
        var i18n = row.FanEventsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (i18n is null)
        {
            i18n = new FanEventsI18n { FanEventId = row.Id, Locale = locale };
            row.FanEventsI18ns.Add(i18n);
            db.FanEventsI18ns.Add(i18n);
        }

        i18n.Name = content.Name.Trim();
        i18n.Description = string.IsNullOrWhiteSpace(content.Description) ? null : content.Description;
        i18n.Location = string.IsNullOrWhiteSpace(content.Location) ? null : content.Location.Trim();
    }

    private Task<FanEvent?> LoadAsync(AdminClubScope scope, Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = db.FanEvents.Include(e => e.FanEventsI18ns).Include(e => e.FanEventImages).Include(e => e.FanEventArticles).AsSplitQuery();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);
    }

    private static bool IsOpen(FanEvent e, int registered, DateTime now)
        => e.Status == "published" && (e.RegistrationDeadlineAt is null || e.RegistrationDeadlineAt >= now)
           && (e.StartsAt is null || e.StartsAt >= now) && (e.Capacity is null || registered < e.Capacity);

    private async Task<AdminFanEventDetailDto> ToDetailAsync(FanEvent e, CancellationToken cancellationToken)
    {
        var counts = await db.FanEventRegistrations.AsNoTracking().Where(r => r.FanEventId == e.Id)
            .GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var registered = counts.Where(c => OccupyingStatuses.Contains(c.Status)).Sum(c => c.Count);
        var waitlist = counts.FirstOrDefault(c => c.Status == "waitlist")?.Count ?? 0;
        string? venueName = null;
        if (e.VenueId is Guid venueId)
        {
            venueName = await db.VenuesI18ns.AsNoTracking().Where(v => v.VenueId == venueId && v.Locale == RequestLocale.DefaultDbLocale)
                .Select(v => v.Name).FirstOrDefaultAsync(cancellationToken);
        }

        var articleIds = e.FanEventArticles.OrderBy(a => a.SortOrder).Select(a => a.ArticleId).ToList();
        var articleRows = articleIds.Count == 0 ? [] : await db.Articles.AsNoTracking()
            .Where(a => articleIds.Contains(a.Id))
            .Select(a => new
            {
                a.Id, a.Slug, a.Status,
                Title = a.ArticlesI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Title).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        var zh = e.FanEventsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = e.FanEventsI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminFanEventDetailDto
        {
            Id = e.Id, Slug = e.Slug, StartsAt = e.StartsAt, EndsAt = e.EndsAt, RegistrationDeadlineAt = e.RegistrationDeadlineAt, Capacity = e.Capacity,
            IsPaidMembersOnly = e.IsPaidMembersOnly, Status = e.Status, StatusLabel = StatusLabels[e.Status], CoverKey = e.CoverKey,
            CoverUrl = e.CoverKey is null ? null : imageUrls.Resolve(e.CoverKey), CoverThumbUrl = ThumbUrl(e.CoverKey), VenueId = e.VenueId, VenueName = venueName,
            Zh = new AdminFanEventLocaleContent { Name = zh?.Name ?? "", Description = zh?.Description, Location = zh?.Location },
            En = en is null ? null : new AdminFanEventLocaleContent { Name = en.Name ?? "", Description = en.Description, Location = en.Location },
            RegisteredCount = registered, WaitlistCount = waitlist, IsRegistrationOpen = IsOpen(e, registered, DateTime.UtcNow),
            Images = e.FanEventImages.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).Select(i => new AdminFanEventImageDto
            {
                Id = i.Id, ImageKey = i.ImageKey, ImageUrl = imageUrls.Resolve(i.ImageKey), ImageThumbUrl = ThumbUrl(i.ImageKey),
                ImageWidth = i.ImageWidth, ImageHeight = i.ImageHeight, SortOrder = i.SortOrder,
            }).ToList(),
            Articles = articleIds.Select(id => articleRows.FirstOrDefault(a => a.Id == id)).Where(a => a is not null)
                .Select(a => new AdminFanEventArticleDto { Id = a!.Id, Slug = a.Slug, TitleZh = a.Title, Status = a.Status }).ToList(),
            CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt,
        };
    }

    private string? ThumbUrl(string? key) => key is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(key));
}
