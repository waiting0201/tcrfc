using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Features.MemberAuth;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MemberCenter;

/// <summary>
/// 會員中心的讀取與會員自助操作（主站規劃書 §3.14、App 規劃書 §3.5／§3.6／§9.3）：我的會籍、電子會員卡、球衣登記、我的報名，以及電子會員卡的公開驗證。
/// 🔴 硬規則：<b>任何回傳會員個資的方法，查詢條件一律是呼叫端權杖裡的 memberId</b>（參數由 <see cref="MemberAuthenticator"/> 提供），
/// 不接受路由或請求本文傳進來的 memberId；別人的會籍／會員卡／球衣一律 404（不洩漏存在與否）。
/// 🔴 <b>會籍有效與否、卡片驗證一律直接查庫，不讀快取</b>（docs/14：會員卡驗證、會籍與訂單狀態不得讀快取——讀到陳舊值＝已撤銷／已過期的卡通過查驗）。
/// </summary>
public sealed class MemberCenterService(ClubDbContext db, MemberMembershipService membershipService)
{
    private static readonly IReadOnlyDictionary<string, string> TierLabelEn = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["registered"] = "Member",
        ["fan_club"] = "Fan Club Member",
    };

    private static readonly IReadOnlyDictionary<string, string> StatusLabelEn = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pending"] = "Pending",
        ["active"] = "Active",
        ["expired"] = "Expired",
        ["cancelled"] = "Cancelled",
    };

    private static readonly IReadOnlyDictionary<string, string> JerseyLabelEn = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pending"] = "Pending",
        ["shipped"] = "Shipped",
        ["received"] = "Received",
    };

    private static readonly IReadOnlyDictionary<string, string> DeliveryLabelEn = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ship"] = "Shipping",
        ["pickup"] = "Pick-up",
    };

    public static string TierLabel(string tier, bool en) => en ? TierLabelEn.GetValueOrDefault(tier, tier) : MemberLabels.Of(MemberLabels.Tier, tier)!;

    public static string StatusLabel(string status, bool en) => en ? StatusLabelEn.GetValueOrDefault(status, status) : MemberLabels.Of(MemberLabels.MembershipStatus, status)!;

    // ═══════════════════════════ 我的會籍與電子會員卡 ═══════════════════════════

    public async Task<MyMembershipsDto> GetMembershipsAsync(Guid memberId, string lang, CancellationToken cancellationToken)
    {
        var en = lang == "en";
        var today = TaiwanClock.Today;
        var member = await db.Members.AsNoTracking().FirstAsync(m => m.Id == memberId, cancellationToken);
        var rows = await db.Memberships.AsNoTracking().Where(m => m.MemberId == memberId)
            .Include(m => m.Club).ThenInclude(c => c.ClubsI18ns)
            .Include(m => m.Season).Include(m => m.MemberCards)
            .Include(m => m.MembershipPlan).ThenInclude(p => p!.MembershipPlansI18ns)
            .OrderBy(m => m.Club.SortOrder).ThenByDescending(m => m.Season.StartOn)
            .AsSplitQuery().ToListAsync(cancellationToken);

        var openOrders = await db.MembershipOrders.AsNoTracking()
            .Where(o => o.MemberId == memberId && (o.Status == "created" || o.Status == "pending_payment"))
            .Select(o => new { o.OrderNo, o.Status, o.ClubId, o.MembershipPlan.SeasonId }).ToListAsync(cancellationToken);

        var items = rows.Select(m =>
        {
            var effective = MemberLabels.EffectiveMembershipStatus(m.Status, m.MembershipEndOn, today);
            var pending = openOrders.FirstOrDefault(o => o.ClubId == m.ClubId && o.SeasonId == m.SeasonId);
            var brand = Brand(m.Club, en);
            return new MyMembershipDto
            {
                Id = m.Id, Club = brand, SeasonCode = m.Season.Code, Tier = m.Tier, TierLabel = TierLabel(m.Tier, en),
                Status = effective, StatusLabel = StatusLabel(effective, en), StartOn = m.MembershipStartOn, EndOn = m.MembershipEndOn,
                PlanCode = m.MembershipPlan?.Code, PlanName = m.MembershipPlan is null ? null : PlanName(m.MembershipPlan, en),
                CardQuota = m.MembershipPlan?.CardQuota ?? 1, JerseyQuota = m.MembershipPlan?.JerseyQuota ?? 0,
                IsCurrentSeason = m.Season.StartOn <= today && today <= m.Season.EndOn,
                RenewalDue = effective == "active" && m.MembershipEndOn is DateOnly end && end <= today.AddDays(30),
                PendingOrder = pending is null ? null : new MemberPendingOrderDto
                {
                    OrderNo = pending.OrderNo, Status = pending.Status, StatusLabel = MembershipOrderLabels.Of(pending.Status, en),
                },
                Cards = m.MemberCards.OrderBy(c => c.RowSeq).Select(c => ToCardDto(c, m, member, brand, effective, en)).ToList(),
            };
        }).ToList();

        // 沒有「目前球季」會籍的俱樂部：畫面顯示加入入口。
        var clubs = await db.Clubs.AsNoTracking().Include(c => c.ClubsI18ns).Where(c => c.Status == "active").OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
        var joinable = new List<MemberJoinableClubDto>();
        foreach (var club in clubs)
        {
            var current = await membershipService.CurrentSeasonAsync(club.Id, cancellationToken);
            if (current is not null && !rows.Any(m => m.ClubId == club.Id && m.SeasonId == current.Id))
            {
                joinable.Add(new MemberJoinableClubDto { Code = club.Code, Name = ClubName(club, en) });
            }
        }

        return new MyMembershipsDto { Memberships = items, JoinableClubs = joinable };
    }

    public async Task<IReadOnlyList<MemberCardDto>> GetCardsAsync(Guid memberId, string lang, CancellationToken cancellationToken)
    {
        var all = await GetMembershipsAsync(memberId, lang, cancellationToken);
        return all.Memberships.SelectMany(m => m.Cards).ToList();
    }

    public async Task<MyMembershipDto> JoinAsync(Guid memberId, ClubScope club, string lang, CancellationToken cancellationToken)
    {
        var member = await db.Members.AsNoTracking().FirstAsync(m => m.Id == memberId, cancellationToken);
        var membership = await membershipService.EnsureRegisteredAsync(memberId, member.Name, club.ClubId, cancellationToken)
                         ?? throw new MemberConflictException("目前無法加入", "這個俱樂部目前沒有開放加入的球季，請稍後再試。", "season_not_available");
        var all = await GetMembershipsAsync(memberId, lang, cancellationToken);
        return all.Memberships.First(m => m.Id == membership.Id);
    }

    /// <summary>重新產生會員卡 QR：同一張卡換 token、補發次數 +1，<b>舊 token 立即失效</b>（卡片外流時自保，規劃書 §3.14）。</summary>
    public async Task<MemberCardDto> RegenerateCardAsync(Guid memberId, Guid cardId, string lang, CancellationToken cancellationToken)
    {
        var card = await db.MemberCards.Include(c => c.Membership)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.Membership.MemberId == memberId, cancellationToken)
            ?? throw new MemberNotFoundException("找不到這張會員卡。");
        if (card.Status != "active")
        {
            throw new MemberConflictException("會員卡已停用", "這張會員卡已停用，無法重新產生 QR。", "card_revoked");
        }

        var now = DateTime.UtcNow;
        card.Token = SecureToken.Generate();
        card.ReissueCount += 1;
        card.IssuedAt = now;
        card.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        var all = await GetMembershipsAsync(memberId, lang, cancellationToken);
        return all.Memberships.SelectMany(m => m.Cards).First(c => c.Id == cardId);
    }

    private static MemberCardDto ToCardDto(MemberCard card, Membership membership, Member member, MemberClubBrandDto brand, string effectiveStatus, bool en)
    {
        var valid = card.Status == "active" && effectiveStatus == "active";
        return new MemberCardDto
        {
            Id = card.Id, MembershipId = membership.Id, Club = brand, MemberNo = member.MemberNo, HolderName = card.HolderName,
            Tier = membership.Tier, TierLabel = TierLabel(membership.Tier, en), ValidUntil = membership.MembershipEndOn,
            Status = valid ? "valid" : card.Status == "revoked" ? "revoked" : "expired",
            StatusLabel = en ? (valid ? "Valid" : card.Status == "revoked" ? "Revoked" : "Expired") : (valid ? "有效" : card.Status == "revoked" ? "已停用" : "已過期"),
            IsValid = valid, Token = card.Token, ReissueCount = card.ReissueCount, IssuedAt = card.IssuedAt, ServerTime = DateTime.UtcNow,
        };
    }

    // ═══════════════════════════ 公開驗證 ═══════════════════════════

    /// <summary>
    /// <c>/m/&lt;token&gt;</c> 驗證頁：掃開<b>僅顯示</b>姓名首字、會員編號、層級與「有效／已過期」，不顯示任何其他個資，<b>也不顯示俱樂部／適用球隊</b>。
    /// 查無、已撤銷、帳號已停用或刪除一律 null（呼叫端回 404，不分原因）。不快取。
    /// </summary>
    public async Task<CardVerificationDto?> VerifyCardAsync(string token, string lang, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64)
        {
            return null;
        }

        var row = await db.MemberCards.AsNoTracking().Where(c => c.Token == token && c.Status == "active")
            .Select(c => new
            {
                c.Membership.Tier, c.Membership.Status, c.Membership.MembershipEndOn,
                c.Membership.Member.Name, c.Membership.Member.MemberNo, MemberStatus = c.Membership.Member.Status,
            }).FirstOrDefaultAsync(cancellationToken);
        if (row is null || row.MemberStatus != "active")
        {
            return null;
        }

        var en = lang == "en";
        var effective = MemberLabels.EffectiveMembershipStatus(row.Status, row.MembershipEndOn, TaiwanClock.Today);
        var valid = effective == "active";
        var initial = System.Globalization.StringInfo.GetNextTextElement(row.Name.Trim());
        return new CardVerificationDto
        {
            NameInitial = initial, MemberNo = row.MemberNo, Tier = row.Tier, TierLabel = TierLabel(row.Tier, en),
            Status = valid ? "valid" : "expired", StatusLabel = en ? (valid ? "Valid" : "Expired") : (valid ? "有效" : "已過期"),
        };
    }

    // ═══════════════════════════ 球衣登記 ═══════════════════════════

    public async Task<IReadOnlyList<MemberJerseyGroupDto>> GetJerseysAsync(Guid memberId, string lang, CancellationToken cancellationToken)
    {
        var en = lang == "en";
        var today = TaiwanClock.Today;
        var memberships = await db.Memberships.AsNoTracking().Where(m => m.MemberId == memberId && m.Tier == "fan_club")
            .Include(m => m.Club).ThenInclude(c => c.ClubsI18ns).Include(m => m.Season).Include(m => m.MembershipPlan)
            .OrderBy(m => m.Club.SortOrder).ThenByDescending(m => m.Season.StartOn).AsSplitQuery().ToListAsync(cancellationToken);
        var issues = await db.JerseyIssues.AsNoTracking().Where(j => j.MemberId == memberId && j.MembershipId != null)
            .OrderBy(j => j.RowSeq).ToListAsync(cancellationToken);

        return memberships.Select(m =>
        {
            var items = issues.Where(j => j.MembershipId == m.Id).Select(j => ToJerseyDto(j, m.Club.Code, en)).ToList();
            var quota = m.MembershipPlan?.JerseyQuota ?? 0;
            var active = MemberLabels.EffectiveMembershipStatus(m.Status, m.MembershipEndOn, today) == "active";
            return new MemberJerseyGroupDto
            {
                MembershipId = m.Id, ClubCode = m.Club.Code, ClubName = ClubName(m.Club, en), SeasonCode = m.Season.Code,
                Quota = quota, Used = items.Count, CanRegister = active && items.Count < quota, Items = items,
            };
        }).Where(g => g.Quota > 0 || g.Items.Count > 0).ToList();
    }

    public async Task<MemberJerseyDto> CreateJerseyAsync(Guid memberId, ClubScope club, MemberJerseyRequest request, string lang, CancellationToken cancellationToken)
    {
        var v = ValidateJersey(request.RecipientName, request.Size, request.DeliveryMethod, request.Phone, request.Address);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // 鎖住這份會籍列，避免兩個並行請求同時通過「件數上限」檢查後都寫入。
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM memberships WITH (UPDLOCK, ROWLOCK) WHERE id = {request.MembershipId}", cancellationToken);
        var membership = await db.Memberships.Include(m => m.MembershipPlan)
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId && m.MemberId == memberId && m.ClubId == club.ClubId, cancellationToken)
            ?? throw new MemberNotFoundException("找不到這份會籍。");
        if (membership.Tier != "fan_club" || MemberLabels.EffectiveMembershipStatus(membership.Status, membership.MembershipEndOn, TaiwanClock.Today) != "active")
        {
            throw new MemberConflictException("無法登記球衣", "只有有效的球迷會員會籍可以登記球衣。", "not_fan_club");
        }

        var quota = membership.MembershipPlan?.JerseyQuota ?? 0;
        var used = await db.JerseyIssues.AsNoTracking().CountAsync(j => j.MembershipId == membership.Id, cancellationToken);
        if (used >= quota)
        {
            throw new MemberConflictException("已達球衣件數上限", quota == 0 ? "這份會籍的方案不含球衣。" : $"這份會籍的方案含 {quota} 件球衣，已經登記滿了。", "jersey_quota_reached");
        }

        var now = DateTime.UtcNow;
        var jersey = new JerseyIssue
        {
            Id = Guid.NewGuid(), ClubId = club.ClubId, MemberId = memberId, MembershipId = membership.Id, RecipientName = v.Recipient,
            Phone = v.Phone, Size = v.Size, DeliveryMethod = request.DeliveryMethod, Address = v.Address, Status = "pending", CreatedAt = now, UpdatedAt = now,
        };
        db.JerseyIssues.Add(jersey);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToJerseyDto(jersey, club.ClubCode, lang == "en");
    }

    public async Task<MemberJerseyDto> UpdateJerseyAsync(Guid memberId, ClubScope club, Guid jerseyId, MemberJerseyUpdateRequest request, string lang, CancellationToken cancellationToken)
    {
        var v = ValidateJersey(request.RecipientName, request.Size, request.DeliveryMethod, request.Phone, request.Address);
        var jersey = await db.JerseyIssues.FirstOrDefaultAsync(j => j.Id == jerseyId && j.MemberId == memberId && j.ClubId == club.ClubId, cancellationToken)
                     ?? throw new MemberNotFoundException("找不到這筆球衣登記。");
        if (jersey.Status != "pending")
        {
            throw new MemberConflictException("已無法修改", "這件球衣已經處理（寄出或領取），無法再修改，請聯繫客服。", "jersey_locked");
        }

        jersey.RecipientName = v.Recipient;
        jersey.Phone = v.Phone;
        jersey.Size = v.Size;
        jersey.DeliveryMethod = request.DeliveryMethod;
        jersey.Address = v.Address;
        jersey.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToJerseyDto(jersey, club.ClubCode, lang == "en");
    }

    private static (string Recipient, string Size, string? Phone, string? Address) ValidateJersey(
        string? recipient, string? size, string? deliveryMethod, string? phone, string? address)
    {
        var name = (recipient ?? string.Empty).Trim();
        if (name.Length is 0 or > 64)
        {
            throw new MemberValidationException("請填寫領用人姓名（64 字以內）。");
        }

        var normalizedSize = (size ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedSize.Length is 0 or > 16)
        {
            throw new MemberValidationException("請填寫尺寸（16 字以內）。");
        }

        if (deliveryMethod is not ("ship" or "pickup"))
        {
            throw new MemberValidationException("領取方式只能是寄送（ship）或到場領取（pickup）。");
        }

        var p = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        var a = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        if (p is { Length: > 32 } || a is { Length: > 500 })
        {
            throw new MemberValidationException("電話或地址過長。");
        }

        if (deliveryMethod == "ship" && (p is null || a is null))
        {
            throw new MemberValidationException("選擇寄送請填寫收件人電話與地址。");
        }

        return (name, normalizedSize, p, a);
    }

    private static MemberJerseyDto ToJerseyDto(JerseyIssue j, string clubCode, bool en) => new()
    {
        Id = j.Id, ClubCode = clubCode, MembershipId = j.MembershipId!.Value, RecipientName = j.RecipientName, Phone = j.Phone, Size = j.Size,
        DeliveryMethod = j.DeliveryMethod,
        DeliveryMethodLabel = j.DeliveryMethod is null ? null : en ? DeliveryLabelEn.GetValueOrDefault(j.DeliveryMethod, j.DeliveryMethod) : MemberLabels.Of(MemberLabels.Delivery, j.DeliveryMethod),
        Address = j.Address, Status = j.Status,
        StatusLabel = en ? JerseyLabelEn.GetValueOrDefault(j.Status, j.Status) : MemberLabels.Of(MemberLabels.Jersey, j.Status)!,
        ShippedOn = j.ShippedOn, ReceivedOn = j.ReceivedOn, Editable = j.Status == "pending",
    };

    // ═══════════════════════════ 我的報名 ═══════════════════════════

    /// <summary>會員自己的課程／試訓報名（報名時帶著會員權杖才會記到 <c>member_id</c>；網頁前台不做報名歸戶，這是行動 App 的「我的報名」用，主站 §3.14／App §3.9）。</summary>
    public async Task<IReadOnlyList<MemberRegistrationDto>> GetRegistrationsAsync(Guid memberId, string lang, CancellationToken cancellationToken)
    {
        var dbLocale = Localization.RequestLocale.ToDbLocale(lang);
        var def = Localization.RequestLocale.DefaultDbLocale;
        var rows = await db.Registrations.AsNoTracking().Where(r => r.MemberId == memberId)
            .OrderByDescending(r => r.CreatedAt).Take(100)
            .Select(r => new
            {
                r.Id, r.RegistrationNo, r.ClubId, r.Status, r.ApplicantName, r.SessionId, r.TrialId, r.CreatedAt,
                Session = r.Session == null ? null : new
                {
                    ProgramSlug = r.Session.TrainingProgram.Slug,
                    NameReq = r.Session.TrainingProgram.ProgramsI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Name).FirstOrDefault(),
                    NameDef = r.Session.TrainingProgram.ProgramsI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                    r.Session.StartOn, r.Session.EndOn, r.Session.WeeklySchedule, r.Session.Price, r.Session.EarlyBirdPrice, r.Session.EarlyBirdUntil,
                    SessionStatus = r.Session.Status,
                    VenueNameReq = r.Session.Venue == null ? null : r.Session.Venue.VenuesI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Name).FirstOrDefault(),
                    VenueNameDef = r.Session.Venue == null ? null : r.Session.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                    VenueAddrReq = r.Session.Venue == null ? null : r.Session.Venue.VenuesI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Address).FirstOrDefault(),
                    VenueAddrDef = r.Session.Venue == null ? null : r.Session.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Address).FirstOrDefault(),
                },
                Trial = r.Trial == null ? null : new
                {
                    r.Trial.TrialOn,
                    TrialStatus = r.Trial.Status,
                    TeamReq = r.Trial.Team == null ? null : r.Trial.Team.TeamsI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Name).FirstOrDefault(),
                    TeamDef = r.Trial.Team == null ? null : r.Trial.Team.TeamsI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                    VenueNameReq = r.Trial.Venue == null ? null : r.Trial.Venue.VenuesI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Name).FirstOrDefault(),
                    VenueNameDef = r.Trial.Venue == null ? null : r.Trial.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Name).FirstOrDefault(),
                    VenueAddrReq = r.Trial.Venue == null ? null : r.Trial.Venue.VenuesI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Address).FirstOrDefault(),
                    VenueAddrDef = r.Trial.Venue == null ? null : r.Trial.Venue.VenuesI18ns.Where(i => i.Locale == def).Select(i => i.Address).FirstOrDefault(),
                },
            })
            .ToListAsync(cancellationToken);
        var clubCodes = await db.Clubs.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
        return rows.Select(r =>
        {
            var st = Common.EnrollmentStatus.OfRegistration(r.Status);
            var courseSlotStatus = r.Session is null ? default : Common.EnrollmentStatus.OfSlot(r.Session.SessionStatus);
            var trialSlotStatus = r.Trial is null ? default : Common.EnrollmentStatus.OfSlot(r.Trial.TrialStatus);
            return new MemberRegistrationDto
            {
                Id = r.Id, RegistrationNo = r.RegistrationNo, ClubCode = clubCodes.GetValueOrDefault(r.ClubId, string.Empty), Status = r.Status,
                StatusCode = st.Code, StatusLabelZh = st.Zh, StatusLabelEn = st.En,
                ApplicantName = r.ApplicantName, SessionId = r.SessionId, TrialId = r.TrialId, CreatedAt = r.CreatedAt,
                Kind = r.TrialId is not null ? "trial" : "session",
                IsFallbackLocale = Localization.RequestLocale.IsFallback(dbLocale, r.Session is not null ? r.Session.NameReq : r.Trial?.TeamReq),
                Course = r.Session is null ? null : new MemberRegistrationCourseDto
                {
                    ProgramSlug = r.Session.ProgramSlug,
                    ProgramName = Localization.RequestLocale.Pick(r.Session.NameReq, r.Session.NameDef),
                    StartOn = r.Session.StartOn, EndOn = r.Session.EndOn, WeeklySchedule = r.Session.WeeklySchedule,
                    VenueName = Localization.RequestLocale.Pick(r.Session.VenueNameReq, r.Session.VenueNameDef),
                    VenueAddress = Localization.RequestLocale.Pick(r.Session.VenueAddrReq, r.Session.VenueAddrDef),
                    Price = r.Session.Price, EarlyBirdPrice = r.Session.EarlyBirdPrice, EarlyBirdUntil = r.Session.EarlyBirdUntil,
                    SessionStatusCode = courseSlotStatus.Code, SessionStatusLabelZh = courseSlotStatus.Zh, SessionStatusLabelEn = courseSlotStatus.En,
                },
                Trial = r.Trial is null ? null : new MemberRegistrationTrialDto
                {
                    TrialOn = r.Trial.TrialOn,
                    TeamName = Localization.RequestLocale.Pick(r.Trial.TeamReq, r.Trial.TeamDef),
                    VenueName = Localization.RequestLocale.Pick(r.Trial.VenueNameReq, r.Trial.VenueNameDef),
                    VenueAddress = Localization.RequestLocale.Pick(r.Trial.VenueAddrReq, r.Trial.VenueAddrDef),
                    TrialStatusCode = trialSlotStatus.Code, TrialStatusLabelZh = trialSlotStatus.Zh, TrialStatusLabelEn = trialSlotStatus.En,
                },
            };
        }).ToList();
    }

    // ═══════════════════════════ 共用 ═══════════════════════════

    private MemberClubBrandDto Brand(Club club, bool en) => new()
    {
        Code = club.Code, Name = ClubName(club, en),
    };

    public static string ClubName(Club club, bool en)
    {
        var wanted = en ? "en" : "zh-Hant";
        return club.ClubsI18ns.FirstOrDefault(i => i.Locale == wanted && !string.IsNullOrWhiteSpace(i.Name))?.Name
               ?? club.ClubsI18ns.FirstOrDefault(i => i.Locale == "zh-Hant")?.Name ?? club.Code;
    }

    public static string PlanName(MembershipPlan plan, bool en)
    {
        var wanted = en ? "en" : "zh-Hant";
        return plan.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == wanted && !string.IsNullOrWhiteSpace(i.Name))?.Name
               ?? plan.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == "zh-Hant")?.Name ?? plan.Code;
    }
}

/// <summary>會籍付款訂單狀態的顯示標籤（資料庫存英文代碼）。</summary>
public static class MembershipOrderLabels
{
    private static readonly IReadOnlyDictionary<string, (string Zh, string En)> Map = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["created"] = ("待確認", "Awaiting confirmation"),
        ["pending_payment"] = ("待付款", "Awaiting payment"),
        ["paid"] = ("已付款", "Paid"),
        ["activated"] = ("已開通", "Activated"),
        ["expired"] = ("已逾時", "Expired"),
        ["activation_failed"] = ("開通失敗，客服處理中", "Activation failed — support is handling it"),
        ["cancelled"] = ("已取消", "Cancelled"),
        ["refunded"] = ("已退款", "Refunded"),
    };

    public static string Of(string status, bool en) => Map.TryGetValue(status, out var l) ? (en ? l.En : l.Zh) : status;
}
