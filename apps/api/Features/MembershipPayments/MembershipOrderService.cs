using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Features.MemberCenter;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MembershipPayments;

/// <summary>
/// 會籍付款訂單（App 規劃書 §5.3 狀態機、§5.4 兩段式流程與冪等性、§9.6）：建立訂單（帶冪等鍵）→ 請款 → 確認 → 開通。
/// 🔴 三條硬規則：① <b>金額由伺服器依方案重算</b>（請求本文根本沒有金額欄位）；② <b>開通以伺服器端向金流方確認的結果為準</b>（<see cref="IPaymentGateway.ConfirmAsync"/>），
/// 不接受用戶端回報付款結果；③ <b>冪等</b>：同一冪等鍵重送只成立一張訂單、同一張訂單重複確認／重複開通不重複延長會籍。
/// 網頁的「升級申請」就是 <c>created</c> 狀態的訂單（官網不接金流：維持收款連結與現場收款，客服核對後在 K2 開通，K2 開通時會一併結案同一份申請）；
/// 金流串接後，同一張訂單可以繼續走 <c>pay</c>／<c>confirm</c>。
/// 狀態一律<b>直接查庫、不讀快取</b>（docs/14：訂單與付款狀態不得讀快取）；逾時採「讀到時換算」（待付款超過 15 分鐘 → <c>expired</c>），沒有背景排程。
/// </summary>
public sealed partial class MembershipOrderService(
    ClubDbContext db, IPaymentGateway gateway, MembershipActivationService activation, ILogger<MembershipOrderService> logger)
{
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);

    [GeneratedRegex(@"^[A-Za-z0-9_\-:.]{8,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdempotencyKeyShape();

    private const string OrderNoAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 去掉容易看錯的 I／O／0／1

    private IQueryable<MembershipOrder> OrdersWithDetail()
        => db.MembershipOrders.Include(o => o.Club).Include(o => o.MembershipPlan).ThenInclude(p => p.Season)
            .Include(o => o.MembershipPlan).ThenInclude(p => p.MembershipPlansI18ns);

    // ═══════════════════════════ 建立（冪等） ═══════════════════════════

    public async Task<(MembershipOrderDto Order, bool Created)> CreateAsync(
        MemberIdentity me, ClubScope club, string? idempotencyKey, CreateMembershipOrderRequest request, string lang, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || !IdempotencyKeyShape().IsMatch(idempotencyKey))
        {
            throw new MemberValidationException("缺少或格式不正確的冪等鍵（Idempotency-Key 標頭，8–64 字元，英數與 _-:.）。", "idempotency_key_required");
        }

        var plan = await db.MembershipPlans.AsNoTracking().Include(p => p.Season)
            .FirstOrDefaultAsync(p => p.Code == request.PlanCode && p.ClubId == club.ClubId && p.Status == "published", cancellationToken)
            ?? throw new MemberNotFoundException("找不到這個會籍方案。", "plan_not_found");

        // 先查冪等鍵：同一鍵重送，內容相同回原訂單，內容不同是用戶端的錯。
        var existing = await OrdersWithDetail().FirstOrDefaultAsync(o => o.MemberId == me.MemberId && o.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.MembershipPlanId != plan.Id || existing.ClubId != club.ClubId)
            {
                throw new MemberConflictException("冪等鍵已使用", "這個冪等鍵已經用在另一張不同內容的訂單上，請產生新的冪等鍵。", "idempotency_key_reused");
            }

            return (ToDto(await ExpireIfNeededAsync(existing, cancellationToken), lang), false);
        }

        var today = TaiwanClock.Today;
        if (plan.Fee <= 0)
        {
            throw new MemberValidationException("這個方案不需要付款。", "plan_free");
        }

        if (plan.Season.EndOn < today || (plan.EndsOn is DateOnly planEnd && planEnd < today))
        {
            throw new MemberConflictException("方案已結束", "這個方案的期間已經結束，無法購買。", "plan_closed");
        }

        var current = await db.Memberships.AsNoTracking().FirstOrDefaultAsync(
            m => m.MemberId == me.MemberId && m.ClubId == club.ClubId && m.SeasonId == plan.SeasonId, cancellationToken);
        if (current is { Tier: "fan_club" } && MemberLabels.EffectiveMembershipStatus(current.Status, current.MembershipEndOn, today) == "active")
        {
            throw new MemberConflictException("已是球迷會員", "你在這個球季已經是球迷會員了。", "already_fan_club");
        }

        // 同一方案同時只能有一張未完成的訂單（待付款逾時的先換算掉）。
        var open = await db.MembershipOrders.Where(o => o.MemberId == me.MemberId && o.MembershipPlanId == plan.Id
                                                        && (o.Status == "created" || o.Status == "pending_payment")).ToListAsync(cancellationToken);
        foreach (var o in open.ToList())
        {
            if (o.Status == "pending_payment" && o.ExpiresAt <= DateTime.UtcNow)
            {
                await ExpireIfNeededAsync(o, cancellationToken);
                open.Remove(o);
            }
        }

        if (open.Count > 0)
        {
            throw new MemberConflictException("已有未完成的訂單", $"你已經有一張這個方案的未完成訂單（{open[0].OrderNo}），請先完成或取消它。", "open_order_exists");
        }

        var collectingClubId = await db.Clubs.AsNoTracking().Where(c => c.IsCollectingSubject).OrderBy(c => c.SortOrder)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken) ?? club.ClubId;

        for (var attempt = 1; ; attempt++)
        {
            var now = DateTime.UtcNow;
            var order = new MembershipOrder
            {
                Id = Guid.NewGuid(), OrderNo = NewOrderNo(), MemberId = me.MemberId, ClubId = club.ClubId, CollectingClubId = collectingClubId,
                MembershipPlanId = plan.Id, IdempotencyKey = idempotencyKey, Amount = plan.Fee, Status = "created", CreatedAt = now, UpdatedAt = now,
            };
            db.MembershipOrders.Add(order);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("建立會籍訂單 {OrderNo}（俱樂部 {Club}、方案 {Plan}）", order.OrderNo, club.ClubCode, plan.Code);
                return (ToDto(await OrdersWithDetail().FirstAsync(o => o.Id == order.Id, cancellationToken), lang), true);
            }
            catch (DbUpdateException)
            {
                db.Entry(order).State = EntityState.Detached;
                // 並行的同一冪等鍵：回先成立的那張。否則是訂單編號撞號，換一個重試。
                var raced = await OrdersWithDetail().FirstOrDefaultAsync(o => o.MemberId == me.MemberId && o.IdempotencyKey == idempotencyKey, cancellationToken);
                if (raced is not null)
                {
                    return (ToDto(raced, lang), false);
                }

                if (attempt >= 5)
                {
                    throw;
                }
            }
        }
    }

    private static string NewOrderNo()
    {
        var suffix = new char[6];
        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = OrderNoAlphabet[RandomNumberGenerator.GetInt32(OrderNoAlphabet.Length)];
        }

        return $"MO{TaiwanClock.Today:yyMMdd}-{new string(suffix)}";
    }

    // ═══════════════════════════ 查詢 ═══════════════════════════

    public async Task<IReadOnlyList<MembershipOrderDto>> ListAsync(Guid memberId, string lang, CancellationToken cancellationToken)
    {
        var rows = await OrdersWithDetail().Where(o => o.MemberId == memberId).OrderByDescending(o => o.RowSeq).Take(50)
            .AsSplitQuery().ToListAsync(cancellationToken);
        var result = new List<MembershipOrderDto>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(ToDto(await ExpireIfNeededAsync(row, cancellationToken), lang));
        }

        return result;
    }

    public async Task<MembershipOrderDto> GetAsync(Guid memberId, string orderNo, string lang, CancellationToken cancellationToken)
        => ToDto(await ExpireIfNeededAsync(await LoadAsync(memberId, orderNo, cancellationToken), cancellationToken), lang);

    private async Task<MembershipOrder> LoadAsync(Guid memberId, string orderNo, CancellationToken cancellationToken)
        => await OrdersWithDetail().FirstOrDefaultAsync(o => o.MemberId == memberId && o.OrderNo == orderNo, cancellationToken)
           ?? throw new MemberNotFoundException("找不到這張訂單。", "order_not_found");

    private async Task<MembershipOrder> ExpireIfNeededAsync(MembershipOrder order, CancellationToken cancellationToken)
    {
        if (order.Status == "pending_payment" && order.ExpiresAt is DateTime expires && expires <= DateTime.UtcNow)
        {
            var now = DateTime.UtcNow;
            await db.MembershipOrders.Where(o => o.Id == order.Id && o.Status == "pending_payment")
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "expired").SetProperty(o => o.UpdatedAt, now), cancellationToken);
            order.Status = "expired";
            order.PaymentUrl = null;
        }

        return order;
    }

    // ═══════════════════════════ 請款 → 確認 → 開通 ═══════════════════════════

    public async Task<MembershipOrderDto> PayAsync(Guid memberId, string orderNo, string lang, CancellationToken cancellationToken)
    {
        var order = await ExpireIfNeededAsync(await LoadAsync(memberId, orderNo, cancellationToken), cancellationToken);
        if (order.Status == "pending_payment" && !string.IsNullOrEmpty(order.PaymentUrl))
        {
            return ToDto(order, lang); // 冪等：已經請過款且尚未逾時，回同一個付款網址
        }

        if (order.Status != "created")
        {
            throw new MemberConflictException("訂單無法付款", $"這張訂單目前是「{MembershipOrderLabels.Of(order.Status, false)}」，無法付款。", "order_not_payable");
        }

        if (!gateway.IsConfigured)
        {
            throw new FeatureNotConfiguredException("線上付款尚未啟用，請先依頁面指引完成付款，客服核對後會為你開通。", "payment_not_configured");
        }

        var now = DateTime.UtcNow;
        var expiresAt = now.Add(PaymentWindow);
        // 先搶佔（created → pending_payment），避免並行的兩次請款各自向金流方開一筆交易。
        var claimed = await db.MembershipOrders.Where(o => o.Id == order.Id && o.Status == "created")
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "pending_payment").SetProperty(o => o.ExpiresAt, expiresAt)
                .SetProperty(o => o.PaymentMethod, gateway.Method).SetProperty(o => o.UpdatedAt, now), cancellationToken);
        if (claimed == 0)
        {
            throw new MemberConflictException("付款處理中", "這張訂單正在建立付款，請稍後重新整理。", "payment_in_progress");
        }

        try
        {
            var plan = order.MembershipPlan;
            var reservation = await gateway.ReserveAsync(
                new PaymentReserveRequest(order.OrderNo, order.Amount, $"{order.Club.Code} {plan.Code}"), cancellationToken);
            await db.MembershipOrders.Where(o => o.Id == order.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentTransactionId, reservation.TransactionId)
                    .SetProperty(o => o.PaymentUrl, reservation.PaymentUrl), cancellationToken);
        }
        catch
        {
            // 金流方請款失敗：退回「已建立」，使用者可以重試，不卡死在待付款。
            await db.MembershipOrders.Where(o => o.Id == order.Id && o.Status == "pending_payment")
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "created").SetProperty(o => o.ExpiresAt, (DateTime?)null)
                    .SetProperty(o => o.PaymentMethod, (string?)null), CancellationToken.None);
            throw;
        }

        db.ChangeTracker.Clear();
        return ToDto(await LoadAsync(memberId, orderNo, cancellationToken), lang);
    }

    public async Task<MembershipOrderDto> ConfirmAsync(Guid memberId, string orderNo, string? transactionId, string lang, CancellationToken cancellationToken)
    {
        var order = await ExpireIfNeededAsync(await LoadAsync(memberId, orderNo, cancellationToken), cancellationToken);
        if (order.Status is "paid" or "activated")
        {
            return ToDto(order, lang); // 冪等：使用者重新整理回呼頁
        }

        if (order.Status == "expired")
        {
            throw new MemberConflictException("訂單已逾時", "這張訂單已經逾時，請重新建立訂單。", "order_expired");
        }

        if (order.Status != "pending_payment")
        {
            throw new MemberConflictException("訂單無法確認", $"這張訂單目前是「{MembershipOrderLabels.Of(order.Status, false)}」，沒有進行中的付款。", "order_not_pending");
        }

        if (string.IsNullOrWhiteSpace(transactionId) || !string.Equals(order.PaymentTransactionId, transactionId, StringComparison.Ordinal))
        {
            throw new MemberValidationException("付款交易識別不符。", "transaction_mismatch");
        }

        // 金額一律用訂單上伺服器算好的值，不是用戶端傳來的。
        var confirmation = await gateway.ConfirmAsync(transactionId, order.OrderNo, order.Amount, cancellationToken);
        if (!confirmation.Success)
        {
            logger.LogWarning("會籍訂單 {OrderNo} 付款確認失敗：{Reason}", order.OrderNo, confirmation.FailureReason);
            throw new MemberConflictException("付款未完成", "付款尚未完成，請回到付款頁重新操作；若已扣款請聯繫客服，不需要重複付款。", "payment_failed");
        }

        var now = DateTime.UtcNow;
        await db.MembershipOrders.Where(o => o.Id == order.Id && o.Status == "pending_payment")
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "paid").SetProperty(o => o.PaidAt, now).SetProperty(o => o.UpdatedAt, now), cancellationToken);
        db.ChangeTracker.Clear();

        await activation.ActivatePaidOrderAsync(order.OrderNo, MembershipActivationService.SourcePayment, cancellationToken);
        db.ChangeTracker.Clear();
        return ToDto(await LoadAsync(memberId, orderNo, cancellationToken), lang);
    }

    public async Task<MembershipOrderDto> CancelAsync(Guid memberId, string orderNo, string lang, CancellationToken cancellationToken)
    {
        var order = await ExpireIfNeededAsync(await LoadAsync(memberId, orderNo, cancellationToken), cancellationToken);
        if (order.Status == "cancelled")
        {
            return ToDto(order, lang);
        }

        if (order.Status is not ("created" or "pending_payment"))
        {
            throw new MemberConflictException("訂單無法取消", $"這張訂單目前是「{MembershipOrderLabels.Of(order.Status, false)}」，無法取消。", "order_not_cancellable");
        }

        var now = DateTime.UtcNow;
        await db.MembershipOrders.Where(o => o.Id == order.Id && (o.Status == "created" || o.Status == "pending_payment"))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "cancelled").SetProperty(o => o.UpdatedAt, now), cancellationToken);
        db.ChangeTracker.Clear();
        return ToDto(await LoadAsync(memberId, orderNo, cancellationToken), lang);
    }

    // ═══════════════════════════ 輸出 ═══════════════════════════

    private MembershipOrderDto ToDto(MembershipOrder o, string lang)
    {
        var en = lang == "en";
        var payable = o.Status is "created" or "pending_payment";
        return new MembershipOrderDto
        {
            OrderNo = o.OrderNo, ClubCode = o.Club.Code, PlanCode = o.MembershipPlan.Code, PlanName = MemberCenterService.PlanName(o.MembershipPlan, en),
            SeasonCode = o.MembershipPlan.Season.Code, Amount = o.Amount, Status = o.Status, StatusLabel = MembershipOrderLabels.Of(o.Status, en),
            PaymentMethod = o.PaymentMethod, PaymentUrl = o.Status == "pending_payment" ? o.PaymentUrl : null, ExpiresAt = o.ExpiresAt,
            PaidAt = o.PaidAt, ActivatedAt = o.ActivatedAt, MembershipId = o.MembershipId, CreatedAt = o.CreatedAt,
            CanPayOnline = payable && gateway.IsConfigured, CanCancel = payable,
        };
    }
}
