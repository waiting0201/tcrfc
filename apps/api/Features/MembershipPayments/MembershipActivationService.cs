using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Email;
using Tcrfc.Api.Features.MemberCenter;

namespace Tcrfc.Api.Features.MembershipPayments;

/// <summary>
/// 會籍開通（App 規劃書 §9.7 的 <c>POST /api/membership/activate</c> 核心）：把「已付款」的訂單變成球迷會員會籍。
/// 觸發來源只有兩種——付款確認後（<c>payment</c>）與內部端點（<c>internal</c>）；後台客服手動開通走 K2（<c>AdminMembershipsRepository.ActivateAsync</c>，
/// 兩者共用同一套「會籍＋付款紀錄＋會員卡」的寫法，<b>會員模組的開通邏輯一行不改，只是觸發者不同</b>）。
///
/// 🔴 <b>冪等</b>（三道防線）：① 以「<c>paid → activated</c>」的條件式 UPDATE 搶佔，同一張訂單同時只有一個呼叫者能繼續；
/// ② 已開通的訂單再呼叫直接回傳原結果（<c>alreadyActivated</c>），不重複延長會籍、不重複寫付款紀錄；
/// ③ <c>membership_payments.membership_order_id</c> 的篩選唯一索引：一張訂單最多一筆付款紀錄，資料庫層擋最後一關。
/// 🔴 <b>不得靜默失敗</b>（App §5.3「開通失敗須立即告警並由客服人工處理」）：已付款但開通出錯時，訂單標為 <c>activation_failed</c>
/// （附原因）、寫 <c>Critical</c> 等級日誌（正式環境會觸發 Application Insights 告警），回應 409 <c>activation_failed</c>。
/// 稽核（App §9.7）：日誌記錄觸發來源、訂單編號、異動前後的會籍層級與狀態（不含個資；本庫依委託方指示沒有稽核表，見 README 待裁決 1）。
/// </summary>
public sealed class MembershipActivationService(ClubDbContext db, IEmailSender email, ILogger<MembershipActivationService> logger)
{
    public const string SourcePayment = "payment";
    public const string SourceInternal = "internal";

    public async Task<MembershipActivationResultDto> ActivatePaidOrderAsync(string orderNo, string source, CancellationToken cancellationToken)
    {
        var order = await db.MembershipOrders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderNo == orderNo, cancellationToken)
                    ?? throw new MemberNotFoundException("找不到這張訂單。", "order_not_found");
        if (order.Status == "activated")
        {
            return new MembershipActivationResultDto { OrderNo = orderNo, Status = "activated", MembershipId = order.MembershipId, AlreadyActivated = true };
        }

        if (order.Status != "paid")
        {
            throw new MemberConflictException("訂單尚未付款", "這張訂單還沒有完成付款，無法開通。", "order_not_paid");
        }

        try
        {
            return await ActivateCoreAsync(order.Id, orderNo, source, cancellationToken);
        }
        catch (MemberConflictException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            var reason = ex.Message.Length > 255 ? ex.Message[..255] : ex.Message;
            var now = DateTime.UtcNow;
            await db.MembershipOrders.Where(o => o.Id == order.Id && o.Status == "paid")
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "activation_failed").SetProperty(o => o.FailureReason, reason).SetProperty(o => o.UpdatedAt, now), CancellationToken.None);
            logger.LogCritical(ex, "【需人工處理】會籍訂單 {OrderNo} 已付款但開通失敗（來源 {Source}），請客服儘速處理。", orderNo, source);
            throw new MemberConflictException("開通失敗", "付款已完成，但會籍開通時發生問題，客服會盡快為你處理，不需要重複付款。", "activation_failed");
        }
    }

    private async Task<MembershipActivationResultDto> ActivateCoreAsync(Guid orderId, string orderNo, string source, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // 同一張訂單的開通一次只讓一個交易進行（應用程式鎖，交易結束自動釋放）。⚠️ 不能只靠下面的條件式 UPDATE：
        // 並行測試實測會死結——輸家的 UPDATE 先鎖住 PK 索引項目、再等聚集索引的列鎖，而贏家寫付款紀錄時外鍵檢查要 S 鎖同一個 PK 索引項目，
        // 兩邊互等（SQL Server 的索引取鎖順序死結）。先用應用程式鎖排隊，輸家要等贏家提交之後才開始動資料，就不會互相卡住。
        var lockResult = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;",
            [lockResult, new SqlParameter("@resource", $"membership-activate:{orderNo}")], cancellationToken);
        if (lockResult.Value is int code && code < 0)
        {
            throw new MemberConflictException("處理中", "這張訂單正在開通，請稍後重新整理。", "activation_in_progress");
        }

        // 搶佔：只有把 paid 改成 activated 成功的那一個呼叫者能繼續；輸的一方重新讀取，已開通就回傳原結果（冪等）。
        var claimed = await db.MembershipOrders.Where(o => o.Id == orderId && o.Status == "paid")
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "activated").SetProperty(o => o.ActivatedAt, now)
                .SetProperty(o => o.ActivationSource, source).SetProperty(o => o.UpdatedAt, now), cancellationToken);
        if (claimed == 0)
        {
            var current = await db.MembershipOrders.AsNoTracking().FirstAsync(o => o.Id == orderId, cancellationToken);
            if (current.Status == "activated")
            {
                return new MembershipActivationResultDto { OrderNo = orderNo, Status = "activated", MembershipId = current.MembershipId, AlreadyActivated = true };
            }

            throw new MemberConflictException("訂單尚未付款", "這張訂單目前的狀態無法開通。", "order_not_paid");
        }

        var order = await db.MembershipOrders.Include(o => o.Member).Include(o => o.Club).ThenInclude(c => c.ClubsI18ns)
            .Include(o => o.MembershipPlan).ThenInclude(p => p.Season).Include(o => o.MembershipPlan).ThenInclude(p => p.MembershipPlansI18ns)
            .FirstAsync(o => o.Id == orderId, cancellationToken);
        var plan = order.MembershipPlan;
        var member = order.Member;
        if (member.Status == "deleted")
        {
            throw new InvalidOperationException("會員帳號已刪除或已合併。");
        }

        var today = TaiwanClock.Today;
        var startOn = plan.StartsOn is DateOnly ps && ps > today ? ps : today;
        var endOn = plan.EndsOn ?? plan.Season.EndOn;
        if (endOn < startOn)
        {
            throw new InvalidOperationException($"方案期間已於 {endOn:yyyy-MM-dd} 結束。");
        }

        var membership = await db.Memberships.FirstOrDefaultAsync(
            m => m.MemberId == member.Id && m.ClubId == order.ClubId && m.SeasonId == plan.SeasonId, cancellationToken);
        var before = membership is null ? "（無）" : $"{membership.Tier}/{membership.Status}";
        if (membership is null)
        {
            membership = new Membership
            {
                Id = Guid.NewGuid(), MemberId = member.Id, ClubId = order.ClubId, SeasonId = plan.SeasonId, CreatedAt = now, MembershipStartOn = startOn,
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

        db.MembershipPayments.Add(new MembershipPayment
        {
            Id = Guid.NewGuid(), MembershipId = membership.Id, ClubId = order.ClubId, CollectingClubId = order.CollectingClubId,
            MembershipPlanId = plan.Id, Method = "linepay", Amount = order.Amount, PaidOn = today, Note = $"訂單 {orderNo}",
            ActivatedStartOn = startOn, ActivatedEndOn = endOn, MembershipOrderId = order.Id, CreatedAt = now, UpdatedAt = now,
        });

        if (!await db.MemberCards.AsNoTracking().AnyAsync(c => c.MembershipId == membership.Id && c.Status == "active", cancellationToken))
        {
            db.MemberCards.Add(new MemberCard
            {
                Id = Guid.NewGuid(), MembershipId = membership.Id, ClubId = order.ClubId, HolderName = member.Name, Token = SecureToken.Generate(),
                Status = "active", IssuedAt = now, CreatedAt = now, UpdatedAt = now,
            });
        }

        order.MembershipId = membership.Id;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("會籍開通：訂單 {OrderNo}、來源 {Source}、俱樂部 {Club}、異動前 {Before} → 異動後 fan_club/active、到期 {EndOn}",
            orderNo, source, order.Club.Code, before, endOn);

        await SendConfirmationAsync(member, order, plan, endOn, cancellationToken);
        return new MembershipActivationResultDto { OrderNo = orderNo, Status = "activated", MembershipId = membership.Id, AlreadyActivated = false };
    }

    /// <summary>「會籍開通確認」信（規劃書 §3.14 五封系統信之一）。寄信失敗不影響開通（會籍已經生效），只記日誌。</summary>
    private async Task SendConfirmationAsync(Member member, MembershipOrder order, MembershipPlan plan, DateOnly endOn, CancellationToken cancellationToken)
    {
        try
        {
            var lang = member.Locale == "en" ? "en" : "zh";
            var en = lang == "en";
            await email.SendAsync(MemberEmailTemplates.MembershipActivated(
                member.Email, member.Name, MemberCenterService.ClubName(order.Club, en), MemberCenterService.PlanName(plan, en), endOn, lang), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "會籍開通確認信寄送失敗（訂單 {OrderNo}），會籍已生效。", order.OrderNo);
        }
    }
}
