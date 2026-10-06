using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminMemberships;

/// <summary>
/// K2 會籍：清單、開通與續會、調整、會員卡張數、批次到期、付款紀錄、續會名單匯出、會員編號規則
/// （規劃書 §4.11 K2）。<c>memberships.club_id</c> 必填，所有查詢一律限定目前操作的俱樂部；跨俱樂部 id 一律 404。
///
/// <b>會籍狀態以到期日為準</b>（見 <see cref="MemberLabels.EffectiveMembershipStatus"/>）：狀態欄是人工覆寫。
/// <b>金流不做</b>（LINE Pay 商店號未到位，B-10）：這裡只有「客服核對款項後手動開通」，付款紀錄是人工登錄。
/// <b>兩隊球季不同步</b>：批次到期與續會名單一律依俱樂部各自執行。
///
/// 🔴 「會籍開通確認」等系統信本輪未寄送——全系統尚無寄信通路（EmailLog 只有紀錄表，沒有寄送機制），見 README「B1」節。
/// </summary>
public sealed class AdminMembershipsRepository(
    ClubDbContext db,
    IPermissionChecker permissions,
    SensitiveActionLogger audit,
    ClubSettingsStore settings,
    MemberNumberGenerator numbers)
{
    private static readonly HashSet<string> PaymentMethods = new(StringComparer.Ordinal) { "linepay", "onsite" };
    private static readonly HashSet<string> AdjustableStatuses = new(StringComparer.Ordinal) { "pending", "active", "expired", "cancelled" };

    public sealed record ListFilter
    {
        public Guid? MemberId { get; init; }
        public string? Keyword { get; init; }
        public string? Tier { get; init; }
        public string? Status { get; init; }
        public Guid? SeasonId { get; init; }
        public Guid? PlanId { get; init; }
        public int? ExpiringWithinDays { get; init; }
    }

    private Task<bool> CanRevealAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => permissions.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, AdminMembersRepository.RevealPermission, cancellationToken);

    // ═══════════════════════════ 清單與詳情 ═══════════════════════════

    private IQueryable<Membership> FilterQuery(AdminClubScope scope, ListFilter filter, bool canReveal)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = db.Memberships.AsNoTracking().Where(m => m.ClubId == scope.ClubId);
        if (filter.MemberId is Guid memberId)
        {
            query = query.Where(m => m.MemberId == memberId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var k = filter.Keyword.Trim();
            query = canReveal
                ? query.Where(m => m.Member.MemberNo.Contains(k) || m.Member.Name.Contains(k) || m.Member.Email.Contains(k)
                                   || (m.Member.Phone != null && m.Member.Phone.Contains(k)))
                : query.Where(m => m.Member.MemberNo.Contains(k));
        }

        if (!string.IsNullOrWhiteSpace(filter.Tier))
        {
            AdminInput.OneOf(filter.Tier, MemberLabels.Tier.Keys.ToHashSet(), "會員層級", "「一般會員」或「球迷會員」");
            query = query.Where(m => m.Tier == filter.Tier);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            AdminInput.OneOf(filter.Status, MemberLabels.MembershipStatus.Keys.ToHashSet(), "會籍狀態", "「待確認」「有效」「已到期」或「已取消」");
            query = filter.Status switch
            {
                "active" => query.Where(x => x.Status == "active" && (x.MembershipEndOn == null || x.MembershipEndOn >= today)),
                "expired" => query.Where(x => x.Status == "expired" || (x.Status == "active" && x.MembershipEndOn != null && x.MembershipEndOn < today)),
                var other => query.Where(x => x.Status == other),
            };
        }

        if (filter.SeasonId is Guid seasonId)
        {
            query = query.Where(m => m.SeasonId == seasonId);
        }

        if (filter.PlanId is Guid planId)
        {
            query = query.Where(m => m.MembershipPlanId == planId);
        }

        if (filter.ExpiringWithinDays is int days)
        {
            if (days is < 1 or > 366)
            {
                throw new AdminValidationException("即將到期的天數請填 1 到 366。");
            }

            var limit = today.AddDays(days);
            query = query.Where(x => x.Status == "active" && x.MembershipEndOn != null && x.MembershipEndOn >= today && x.MembershipEndOn <= limit);
        }

        return query;
    }

    public async Task<PagedResult<AdminMembershipListItemDto>> ListAsync(
        AdminClubScope scope, ListFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var query = FilterQuery(scope, filter, canReveal);
        var total = await query.CountAsync(cancellationToken);
        var ordered = filter.ExpiringWithinDays is null
            ? query.OrderByDescending(m => m.RowSeq)
            : query.OrderBy(m => m.MembershipEndOn).ThenBy(m => m.RowSeq);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(ProjectionExpression()).ToListAsync(cancellationToken);
        return new PagedResult<AdminMembershipListItemDto> { Items = rows.Select(r => ToListItem(r)).ToList(), Page = page, PageSize = pageSize, TotalCount = total };
    }

    // ═══════════════════════════ 待確認申請（網頁升級申請） ═══════════════════════════

    private static readonly HashSet<string> ApplicationStatuses = new(StringComparer.Ordinal)
    {
        "created", "pending_payment", "paid", "activated", "expired", "activation_failed", "cancelled", "refunded",
    };

    /// <summary>
    /// K2「待確認申請」：會員在網頁送出的升級申請（<c>membership_orders</c>）。預設只列 <c>created</c>（待客服核對款項）；
    /// 可用 <paramref name="status"/> 看其他狀態（例如 <c>activation_failed</c>＝已付款但開通失敗、需客服處理）。限定目前俱樂部（<c>club_id</c> ＝受益俱樂部）。
    /// 🔴 <b>讀取當下才換算逾時</b>：待付款超過期限的訂單以 <c>expired</c> 顯示，不改庫（改庫是會員端讀訂單時的事）。
    /// </summary>
    public async Task<PagedResult<AdminMembershipApplicationDto>> ListApplicationsAsync(
        AdminClubScope scope, string? status, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var wanted = string.IsNullOrWhiteSpace(status) ? "created" : status.Trim();
        AdminInput.OneOf(wanted, ApplicationStatuses, "申請狀態", "「待確認」「待付款」「已付款」「已開通」「已逾時」「開通失敗」「已取消」或「已退款」");
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var query = db.MembershipOrders.AsNoTracking().Where(o => o.ClubId == scope.ClubId);
        var now = DateTime.UtcNow;
        query = wanted switch
        {
            "expired" => query.Where(o => o.Status == "expired" || (o.Status == "pending_payment" && o.ExpiresAt != null && o.ExpiresAt <= now)),
            "pending_payment" => query.Where(o => o.Status == "pending_payment" && (o.ExpiresAt == null || o.ExpiresAt > now)),
            var other => query.Where(o => o.Status == other),
        };
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = canReveal
                ? query.Where(o => o.OrderNo.Contains(k) || o.Member.MemberNo.Contains(k) || o.Member.Name.Contains(k) || o.Member.Email.Contains(k)
                                   || (o.Member.Phone != null && o.Member.Phone.Contains(k)))
                : query.Where(o => o.OrderNo.Contains(k) || o.Member.MemberNo.Contains(k));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(o => o.RowSeq).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.OrderNo, o.MemberId, o.Member.MemberNo, o.Member.Name, o.Member.Email, o.Member.Phone, o.MembershipPlanId, PlanCode = o.MembershipPlan.Code,
                PlanName = o.MembershipPlan.MembershipPlansI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                o.MembershipPlan.SeasonId, SeasonCode = o.MembershipPlan.Season.Code, o.Amount, o.Status, o.ExpiresAt, o.ActivatedAt, o.CreatedAt, o.UpdatedAt,
            }).ToListAsync(cancellationToken);
        var items = rows.Select(r =>
        {
            var effective = r.Status == "pending_payment" && r.ExpiresAt is DateTime e && e <= now ? "expired" : r.Status;
            return new AdminMembershipApplicationDto
            {
                OrderNo = r.OrderNo, MemberId = r.MemberId, MemberNo = r.MemberNo,
                MemberName = canReveal ? r.Name : PiiMasking.MaskName(r.Name), MemberEmail = canReveal ? r.Email : PiiMasking.MaskEmail(r.Email),
                MemberPhone = canReveal ? r.Phone : PiiMasking.MaskPhone(r.Phone), PlanId = r.MembershipPlanId, PlanCode = r.PlanCode, PlanName = r.PlanName,
                SeasonId = r.SeasonId, SeasonCode = r.SeasonCode, Amount = r.Amount, Status = effective, StatusLabel = MemberCenter.MembershipOrderLabels.Of(effective, false),
                ExpiresAt = r.ExpiresAt, ActivatedAt = r.ActivatedAt, CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt,
            };
        }).ToList();
        return new PagedResult<AdminMembershipApplicationDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    private sealed record Row(
        Guid Id, Guid MemberId, string MemberNo, string MemberName, string Tier, string Status, Guid SeasonId, string SeasonCode,
        DateOnly? StartOn, DateOnly? EndOn, Guid? PlanId, string? PlanName, int CardCount, int PaidTotal, DateTime UpdatedAt);

    private static System.Linq.Expressions.Expression<Func<Membership, Row>> ProjectionExpression()
        => m => new Row(
            m.Id, m.MemberId, m.Member.MemberNo, m.Member.Name, m.Tier, m.Status, m.SeasonId, m.Season.Code,
            m.MembershipStartOn, m.MembershipEndOn, m.MembershipPlanId,
            m.MembershipPlan == null ? null
                : m.MembershipPlan.MembershipPlansI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            m.MemberCards.Count(c => c.Status == "active"),
            m.MembershipPayments.Sum(p => (int?)p.Amount) ?? 0,
            m.UpdatedAt);

    private static AdminMembershipListItemDto ToListItem(Row r)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effective = MemberLabels.EffectiveMembershipStatus(r.Status, r.EndOn, today);
        return new AdminMembershipListItemDto
        {
            MembershipId = r.Id, MemberId = r.MemberId, MemberNo = r.MemberNo, MemberName = PiiMasking.MaskName(r.MemberName),
            Tier = r.Tier, TierLabel = MemberLabels.Of(MemberLabels.Tier, r.Tier)!, Status = r.Status, EffectiveStatus = effective,
            EffectiveStatusLabel = MemberLabels.Of(MemberLabels.MembershipStatus, effective)!, SeasonId = r.SeasonId, SeasonCode = r.SeasonCode,
            StartOn = r.StartOn, EndOn = r.EndOn, DaysToExpire = r.EndOn is DateOnly end ? end.DayNumber - today.DayNumber : null,
            PlanId = r.PlanId, PlanName = r.PlanName, CardCount = r.CardCount, PaidTotal = r.PaidTotal, UpdatedAt = r.UpdatedAt,
        };
    }

    public async Task<AdminMembershipDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var row = await db.Memberships.AsNoTracking().Where(m => m.Id == id && m.ClubId == scope.ClubId)
            .Select(ProjectionExpression()).FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var meta = await db.Memberships.AsNoTracking().Where(m => m.Id == id)
            .Select(m => new
            {
                m.LastAdjustReason, m.LastAdjustedAt,
                CardQuota = m.MembershipPlan == null ? 1 : m.MembershipPlan.CardQuota,
                JerseyQuota = m.MembershipPlan == null ? 0 : m.MembershipPlan.JerseyQuota,
            })
            .FirstAsync(cancellationToken);
        var payments = await LoadPaymentsAsync(db.MembershipPayments.AsNoTracking().Where(p => p.MembershipId == id), cancellationToken);
        var cards = await db.MemberCards.AsNoTracking().Where(c => c.MembershipId == id).OrderBy(c => c.RowSeq).ToListAsync(cancellationToken);

        return new AdminMembershipDetailDto
        {
            Membership = ToListItem(row),
            LastAdjustReason = meta.LastAdjustReason,
            LastAdjustedAt = meta.LastAdjustedAt,
            Payments = payments.Select(p => p.Payment).ToList(),
            Cards = cards.Select(c => ToCardDto(c, canReveal)).ToList(),
            CardQuota = meta.CardQuota,
            JerseyQuota = meta.JerseyQuota,
        };
    }

    private static AdminMemberCardDto ToCardDto(MemberCard c, bool reveal) => new()
    {
        Id = c.Id, MembershipId = c.MembershipId, HolderName = reveal ? c.HolderName : PiiMasking.MaskName(c.HolderName),
        Status = c.Status, StatusLabel = c.Status == "active" ? "使用中" : "已停用", IssuedAt = c.IssuedAt, RevokedAt = c.RevokedAt,
        ReissueCount = c.ReissueCount,
    };

    private static async Task<List<AdminPaymentListItemDto>> LoadPaymentsAsync(IQueryable<MembershipPayment> query, CancellationToken cancellationToken)
    {
        var rows = await query.OrderByDescending(p => p.RowSeq)
            .Select(p => new
            {
                p.Id, p.MembershipId, p.MembershipPlanId, p.Method, p.Amount, p.PaidOn, p.Note, p.ActivatedStartOn, p.ActivatedEndOn, p.CreatedAt,
                CollectingClubCode = p.CollectingClub.Code, BeneficiaryClubCode = p.Club.Code,
                HandledByName = p.HandledByNavigation == null ? null : p.HandledByNavigation.DisplayName,
                PlanName = p.MembershipPlan.MembershipPlansI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                MemberId = p.Membership.MemberId, MemberNo = p.Membership.Member.MemberNo, SeasonCode = p.Membership.Season.Code,
            })
            .ToListAsync(cancellationToken);
        return rows.Select(p => new AdminPaymentListItemDto
        {
            MembershipId = p.MembershipId, MemberId = p.MemberId, MemberNo = p.MemberNo, SeasonCode = p.SeasonCode,
            Payment = new AdminMemberPaymentDto
            {
                Id = p.Id, PlanId = p.MembershipPlanId, PlanName = p.PlanName, Method = p.Method,
                MethodLabel = p.Method is null ? null : MemberLabels.Of(MemberLabels.PaymentMethod, p.Method), Amount = p.Amount,
                PaidOn = p.PaidOn, CollectingClubCode = p.CollectingClubCode, BeneficiaryClubCode = p.BeneficiaryClubCode, Note = p.Note,
                HandledByName = p.HandledByName, ActivatedStartOn = p.ActivatedStartOn, ActivatedEndOn = p.ActivatedEndOn, CreatedAt = p.CreatedAt,
            },
        }).ToList();
    }

    public async Task<PagedResult<AdminPaymentListItemDto>> ListPaymentsAsync(
        AdminClubScope scope, Guid? membershipId, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.MembershipPayments.AsNoTracking().Where(p => p.ClubId == scope.ClubId);
        if (membershipId is Guid mid)
        {
            query = query.Where(p => p.MembershipId == mid);
        }

        if (from is DateOnly f)
        {
            query = query.Where(p => p.PaidOn != null && p.PaidOn >= f);
        }

        if (to is DateOnly t)
        {
            query = query.Where(p => p.PaidOn != null && p.PaidOn <= t);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await LoadPaymentsAsync(query.OrderByDescending(p => p.RowSeq).Skip((page - 1) * pageSize).Take(pageSize), cancellationToken);
        return new PagedResult<AdminPaymentListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    // ═══════════════════════════ 開通與續會 ═══════════════════════════

    /// <summary>
    /// 手動開通／續會：找到或建立「這位會員在這個俱樂部這個球季」的會籍，升為球迷會員並設定起訖，寫一筆付款紀錄
    /// （同時記受益俱樂部與收款法人），確保有一張使用中的會員卡。全程一個交易。續會就是用下一球季的方案再開通一次。
    /// 同一份會籍、同一方案、同一付款日與金額重複送出視為重複開通（409）。
    /// </summary>
    public async Task<AdminMembershipDetailDto> ActivateAsync(
        AdminClubScope scope, ActivateMembershipRequest request, CancellationToken cancellationToken)
    {
        if (request.BeneficiaryClubId is Guid beneficiary && beneficiary != scope.ClubId)
        {
            throw new AdminValidationException("受益俱樂部必須是目前操作的俱樂部；要替另一個俱樂部開通，請切換到那個俱樂部再操作。", "beneficiaryClubId");
        }

        AdminInput.OneOf(request.PaymentMethod, PaymentMethods, "付款方式", "「LINE Pay」或「現場收款」", "paymentMethod");
        if (request.Amount < 0)
        {
            throw new AdminValidationException("金額不可為負數。", "amount");
        }

        var note = AdminInput.OptionalText(request.Note, "交易備註", 255, "note");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.PaidOn > today.AddDays(1))
        {
            throw new AdminValidationException("付款日期不可晚於今天。", "paidOn");
        }

        var plan = await db.MembershipPlans.Include(p => p.Season)
            .FirstOrDefaultAsync(p => p.Id == request.PlanId && p.ClubId == scope.ClubId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的方案，請確認方案屬於目前的俱樂部。", "planId");
        if (plan.Status != "published")
        {
            throw new AdminValidationException("這個方案目前是下架狀態，請先上架，或改選其他方案。", "planId");
        }

        var member = await db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的會員，請確認會員資料是否存在。", "memberId");
        if (member.Status == "deleted")
        {
            throw new AdminValidationException("這個帳號已刪除或已合併，無法開通會籍。", "memberId");
        }

        var startOn = request.StartOn ?? (plan.StartsOn is DateOnly ps && ps > today ? ps : today);
        var endOn = request.EndOn ?? plan.EndsOn ?? plan.Season.EndOn;
        if (endOn < startOn)
        {
            throw new AdminValidationException(request.StartOn is null && request.EndOn is null
                ? $"這個方案的期間已經在 {endOn:yyyy-MM-dd} 結束了，不能用今天當開始日。請改選其他方案，或自行指定開始日與到期日（補登過去的會籍）。"
                : "到期日不可早於開始日。", "endOn");
        }

        var collectingClubId = await db.Clubs.AsNoTracking().Where(c => c.IsCollectingSubject).OrderBy(c => c.SortOrder)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken) ?? scope.ClubId;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var membership = await db.Memberships
            .FirstOrDefaultAsync(m => m.MemberId == member.Id && m.ClubId == scope.ClubId && m.SeasonId == plan.SeasonId, cancellationToken);

        if (membership is not null && await db.MembershipPayments.AsNoTracking().AnyAsync(
                p => p.MembershipId == membership.Id && p.MembershipPlanId == plan.Id && p.PaidOn == request.PaidOn && p.Amount == request.Amount,
                cancellationToken))
        {
            throw new AdminConflictException("重複開通", "這位會員已經有同一方案、同一天、同一金額的開通紀錄了，請確認是否重複送出。", "planId");
        }

        if (membership is null)
        {
            membership = new Membership
            {
                Id = Guid.NewGuid(), MemberId = member.Id, ClubId = scope.ClubId, SeasonId = plan.SeasonId, CreatedAt = now,
                CreatedBy = scope.Identity.AdminUserId, MembershipStartOn = startOn,
            };
            db.Memberships.Add(membership);
        }
        else if (membership.Tier != "fan_club" || membership.Status != "active" || membership.MembershipStartOn is null)
        {
            membership.MembershipStartOn = startOn;
        }

        membership.Tier = "fan_club";
        membership.Status = "active";
        membership.MembershipPlanId = plan.Id;
        membership.MembershipEndOn = endOn;
        membership.UpdatedAt = now;
        membership.UpdatedBy = scope.Identity.AdminUserId;

        db.MembershipPayments.Add(new MembershipPayment
        {
            Id = Guid.NewGuid(), MembershipId = membership.Id, ClubId = scope.ClubId, CollectingClubId = collectingClubId,
            MembershipPlanId = plan.Id, Method = request.PaymentMethod, Amount = request.Amount, PaidOn = request.PaidOn, Note = note,
            HandledBy = scope.Identity.AdminUserId, ActivatedStartOn = startOn, ActivatedEndOn = endOn, CreatedAt = now, UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        });

        var hasActiveCard = await db.MemberCards.AsNoTracking().AnyAsync(c => c.MembershipId == membership.Id && c.Status == "active", cancellationToken);
        if (!hasActiveCard)
        {
            db.MemberCards.Add(NewCard(membership.Id, scope.ClubId, member.Name, scope.Identity.AdminUserId, now));
        }

        // E 批（2026-10-01）：會員在網頁送出的「升級申請」（membership_orders.created，同一份方案）由客服核對款項開通後一併結案，
        // 會員中心的狀態才會從「待確認」變成「已開通」。已向金流方請款中的訂單（pending_payment）不動，避免客服開通後會員又完成付款而重複扣款。
        var applications = await db.MembershipOrders
            .Where(o => o.MemberId == member.Id && o.ClubId == scope.ClubId && o.MembershipPlanId == plan.Id && o.Status == "created")
            .ToListAsync(cancellationToken);
        foreach (var application in applications)
        {
            application.Status = "activated";
            application.ActivatedAt = now;
            application.ActivationSource = "admin";
            application.MembershipId = membership.Id;
            application.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetByIdAsync(scope, membership.Id, cancellationToken))!;
    }

    /// <summary>建立免費（一般會員）會籍與第一張會員卡。已經有這一季的會籍 → 409。</summary>
    public async Task<AdminMembershipDetailDto> RegisterAsync(
        AdminClubScope scope, RegisterMembershipRequest request, CancellationToken cancellationToken)
    {
        var season = await db.Seasons.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SeasonId && s.ClubId == scope.ClubId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的球季，請確認球季屬於目前的俱樂部。", "seasonId");
        var member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MemberId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的會員，請確認會員資料是否存在。", "memberId");
        if (member.Status == "deleted")
        {
            throw new AdminValidationException("這個帳號已刪除或已合併，無法建立會籍。", "memberId");
        }

        if (await db.Memberships.AsNoTracking().AnyAsync(m => m.MemberId == member.Id && m.ClubId == scope.ClubId && m.SeasonId == season.Id, cancellationToken))
        {
            throw new AdminConflictException("會籍已存在", "這位會員在這個球季已經有會籍了。", "memberId");
        }

        var now = DateTime.UtcNow;
        var membership = new Membership
        {
            Id = Guid.NewGuid(), MemberId = member.Id, ClubId = scope.ClubId, SeasonId = season.Id, Tier = "registered", Status = "active",
            MembershipStartOn = season.StartOn, MembershipEndOn = season.EndOn, CreatedAt = now, UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        db.Memberships.Add(membership);
        db.MemberCards.Add(NewCard(membership.Id, scope.ClubId, member.Name, scope.Identity.AdminUserId, now));
        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(scope, membership.Id, cancellationToken))!;
    }

    private static MemberCard NewCard(Guid membershipId, Guid clubId, string holderName, Guid? operatorId, DateTime now) => new()
    {
        Id = Guid.NewGuid(), MembershipId = membershipId, ClubId = clubId, HolderName = holderName, Token = SecureToken.Generate(),
        Status = "active", IssuedAt = now, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
    };

    // ═══════════════════════════ 調整 ═══════════════════════════

    /// <summary>手動調整層級／狀態／起訖日，必須寫異動原因（只保留最近一次，本庫不建異動歷程表，見 README「待裁決」）。</summary>
    public async Task<AdminMembershipDetailDto?> AdjustAsync(
        AdminClubScope scope, Guid id, AdjustMembershipRequest request, CancellationToken cancellationToken)
    {
        var reason = AdminInput.RequireText(request.Reason, "異動原因", 255, "reason");
        if (request.Tier is null && request.Status is null && request.StartOn is null && request.EndOn is null)
        {
            throw new AdminValidationException("請至少調整一項（層級、狀態或起訖日）。", "reason");
        }

        if (request.Tier is not null)
        {
            AdminInput.OneOf(request.Tier, MemberLabels.Tier.Keys.ToHashSet(), "會員層級", "「一般會員」或「球迷會員」", "tier");
        }

        if (request.Status is not null)
        {
            AdminInput.OneOf(request.Status, AdjustableStatuses, "會籍狀態", "「待確認」「有效」「已到期」或「已取消」", "status");
        }

        var membership = await db.Memberships.Include(m => m.MemberCards)
            .FirstOrDefaultAsync(m => m.Id == id && m.ClubId == scope.ClubId, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        var newStart = request.StartOn ?? membership.MembershipStartOn;
        var newEnd = request.EndOn ?? membership.MembershipEndOn;
        AdminInput.DateRange(newStart, newEnd, "會籍期間", "endOn");

        var before = $"{membership.Tier}/{membership.Status}";
        membership.Tier = request.Tier ?? membership.Tier;
        membership.Status = request.Status ?? membership.Status;
        membership.MembershipStartOn = newStart;
        membership.MembershipEndOn = newEnd;
        membership.LastAdjustReason = reason;
        membership.LastAdjustedAt = DateTime.UtcNow;
        membership.UpdatedAt = DateTime.UtcNow;
        membership.UpdatedBy = scope.Identity.AdminUserId;

        // 取消的會籍，其會員卡一併停用（驗證頁不得再顯示為有效）。
        if (membership.Status == "cancelled")
        {
            foreach (var card in membership.MemberCards.Where(c => c.Status == "active"))
            {
                card.Status = "revoked";
                card.RevokedAt = DateTime.UtcNow;
                card.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "調整會籍", $"會籍 {id}（{before} → {membership.Tier}/{membership.Status}）", purpose: reason);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    // ═══════════════════════════ 會員卡 ═══════════════════════════

    /// <summary>新增副卡（家庭方案）：張數不得超過方案的 <c>card_quota</c>（沒有方案的免費會籍為 1 張）。</summary>
    public async Task<AdminMembershipDetailDto?> AddCardAsync(
        AdminClubScope scope, Guid membershipId, AddMemberCardRequest request, CancellationToken cancellationToken)
    {
        var holder = AdminInput.RequireText(request.HolderName, "持卡人姓名", 64, "holderName");
        var membership = await db.Memberships.Include(m => m.MembershipPlan)
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.ClubId == scope.ClubId, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        var quota = membership.MembershipPlan?.CardQuota ?? 1;
        var active = await db.MemberCards.AsNoTracking().CountAsync(c => c.MembershipId == membershipId && c.Status == "active", cancellationToken);
        if (active >= quota)
        {
            throw new AdminConflictException("已達發卡上限", $"這份會籍的方案最多發 {quota} 張會員卡，已經發滿了。");
        }

        db.MemberCards.Add(NewCard(membershipId, scope.ClubId, holder, scope.Identity.AdminUserId, DateTime.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(scope, membershipId, cancellationToken);
    }

    public async Task<AdminMembershipDetailDto?> RevokeCardAsync(
        AdminClubScope scope, Guid membershipId, Guid cardId, CancellationToken cancellationToken)
    {
        var card = await db.MemberCards.FirstOrDefaultAsync(
            c => c.Id == cardId && c.MembershipId == membershipId && c.ClubId == scope.ClubId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        if (card.Status == "active")
        {
            card.Status = "revoked";
            card.RevokedAt = DateTime.UtcNow;
            card.UpdatedAt = DateTime.UtcNow;
            card.UpdatedBy = scope.Identity.AdminUserId;
            await db.SaveChangesAsync(cancellationToken);
            audit.Record(scope, "停用會員卡", $"會員卡 {cardId}");
        }

        return await GetByIdAsync(scope, membershipId, cancellationToken);
    }

    // ═══════════════════════════ 批次到期 ═══════════════════════════

    /// <summary>球季末批次到期處理：把「到期日早於基準日」的有效會籍標為已到期（僅限目前操作的俱樂部——兩隊球季不同步）。
    /// 可先用 <c>DryRun</c> 試算件數。</summary>
    public async Task<ExpireBatchResultDto> ExpireBatchAsync(
        AdminClubScope scope, ExpireBatchRequest request, CancellationToken cancellationToken)
    {
        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var query = db.Memberships.Where(m => m.ClubId == scope.ClubId && m.Status == "active" && m.MembershipEndOn != null && m.MembershipEndOn < asOf);
        if (request.SeasonId is Guid seasonId)
        {
            query = query.Where(m => m.SeasonId == seasonId);
        }

        var count = await query.CountAsync(cancellationToken);
        if (!request.DryRun && count > 0)
        {
            var now = DateTime.UtcNow;
            var adminId = scope.Identity.AdminUserId;
            await query.ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, "expired")
                .SetProperty(m => m.UpdatedAt, now)
                .SetProperty(m => m.UpdatedBy, adminId), cancellationToken);
            audit.Record(scope, "批次到期處理", $"基準日 {asOf:yyyy-MM-dd}", count);
        }

        return new ExpireBatchResultDto { AsOf = asOf, Count = count, DryRun = request.DryRun };
    }

    // ═══════════════════════════ 續會名單匯出 ═══════════════════════════

    /// <summary>續會名單 CSV（<c>member.export</c>，受限）：<c>scope</c>＝<c>expiring</c>（即將到期，預設 30 天內）或
    /// <c>expired</c>（已到期尚未續會）。含個資，須填用途並寫敏感操作日誌。只匯出目前操作的俱樂部。</summary>
    public async Task<string> RenewalExportCsvAsync(
        AdminClubScope scope, string? kind, int? days, Guid? seasonId, string? purpose, CancellationToken cancellationToken)
    {
        var purposeText = AdminInput.RequireText(purpose, "匯出用途", 200);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = db.Memberships.AsNoTracking().Where(m => m.ClubId == scope.ClubId);
        if (seasonId is Guid s)
        {
            query = query.Where(m => m.SeasonId == s);
        }

        var isExpired = string.Equals(kind, "expired", StringComparison.Ordinal);
        if (isExpired)
        {
            query = query.Where(m => m.Status != "cancelled" && (m.Status == "expired" || (m.Status == "active" && m.MembershipEndOn != null && m.MembershipEndOn < today)));
        }
        else
        {
            var window = days ?? 30;
            if (window is < 1 or > 366)
            {
                throw new AdminValidationException("即將到期的天數請填 1 到 366。");
            }

            var limit = today.AddDays(window);
            query = query.Where(m => m.Status == "active" && m.MembershipEndOn != null && m.MembershipEndOn >= today && m.MembershipEndOn <= limit);
        }

        var rows = await query.OrderBy(m => m.MembershipEndOn).ThenBy(m => m.Member.MemberNo)
            .Select(m => new
            {
                m.Member.MemberNo, m.Member.Name, m.Member.Email, m.Member.Phone, m.Tier, SeasonCode = m.Season.Code, m.MembershipEndOn,
                PlanName = m.MembershipPlan == null ? null
                    : m.MembershipPlan.MembershipPlansI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                LastPaid = m.MembershipPayments.OrderByDescending(p => p.RowSeq).Select(p => (int?)p.Amount).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var lines = new List<IEnumerable<string?>> { new[] { "會員編號", "姓名", "Email", "電話", "會員層級", "方案", "球季", "到期日", "剩餘天數", "最近一次付款金額" } };
        lines.AddRange(rows.Select(r => new[]
        {
            r.MemberNo, r.Name, r.Email, r.Phone, MemberLabels.Of(MemberLabels.Tier, r.Tier), r.PlanName, r.SeasonCode,
            r.MembershipEndOn?.ToString("yyyy-MM-dd"),
            r.MembershipEndOn is DateOnly e ? (e.DayNumber - today.DayNumber).ToString(CultureInfo.InvariantCulture) : null,
            r.LastPaid?.ToString(CultureInfo.InvariantCulture),
        }));

        audit.Record(scope, isExpired ? "匯出續會名單（已到期）" : "匯出續會名單（即將到期）", $"共 {rows.Count} 份會籍", rows.Count, purposeText);
        return CsvUtils.BuildCsv(lines);
    }

    // ═══════════════════════════ 會員編號規則 ═══════════════════════════

    public async Task<AdminMemberSettingsDto> GetSettingsAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var (prefix, digits) = await numbers.GetRuleAsync(scope.ClubId, cancellationToken);
        return new AdminMemberSettingsDto
        {
            MemberNoPrefix = prefix, MemberNoDigits = digits, NextMemberNoPreview = await numbers.NextAsync(scope.ClubId, cancellationToken),
        };
    }

    public async Task<AdminMemberSettingsDto> UpdateSettingsAsync(
        AdminClubScope scope, UpdateAdminMemberSettingsRequest request, CancellationToken cancellationToken)
    {
        var prefix = (request.MemberNoPrefix ?? "").Trim();
        if (prefix.Length > 8 || !prefix.All(char.IsAsciiLetterOrDigit))
        {
            throw new AdminValidationException("會員編號前綴只能使用英文字母與數字，最多 8 個字。", "prefix");
        }

        if (request.MemberNoDigits is < 4 or > 10)
        {
            throw new AdminValidationException("會員編號的流水號位數請填 4 到 10。", "digits");
        }

        await settings.UpsertAsync(scope.ClubId, MemberNumberGenerator.PrefixKey, prefix, "member", scope.Identity.AdminUserId, cancellationToken);
        await settings.UpsertAsync(scope.ClubId, MemberNumberGenerator.DigitsKey,
            request.MemberNoDigits.ToString(CultureInfo.InvariantCulture), "member", scope.Identity.AdminUserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await GetSettingsAsync(scope, cancellationToken);
    }
}
