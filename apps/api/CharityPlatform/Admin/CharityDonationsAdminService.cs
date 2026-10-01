using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Invoices;
using Tcrfc.Api.CharityPlatform.Mail;
using Tcrfc.Api.CharityPlatform.Payments;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.CharityPlatform.Admin;

public sealed record DonationFilter(
    DateOnly? From, DateOnly? To, string? Status, Guid? ProjectId, Guid? StoreId, bool NoStore,
    string? InvoiceStatus, int? AmountMin, int? AmountMax, string? Keyword);

/// <summary>
/// N3 捐款紀錄的資料操作。🔴 個資只在 API 層遮罩／解除遮罩：預設回傳遮罩值；明文需要 <see cref="CharityPermissions.DonationReveal"/>，
/// 每次明文檢視寫稽核。🔴 退款（<see cref="CharityPermissions.DonationRefund"/>，系統管理員專屬）與含個資的明細匯出
/// （<see cref="CharityPermissions.DonationExport"/>，須填用途備註）每次都寫稽核——稽核與變更在同一次 <c>SaveChanges</c> 提交。
/// </summary>
public sealed class CharityDonationsAdminService(
    CharityDbContext db, ICharityAdminAuthorizer authorizer, CharityAuditLogger audit, CharityDataProtector protector,
    IPaymentGateway gateway, CharityInvoiceService invoices, CharityEmailService mail, CharityDonationService donationService,
    ILogger<CharityDonationsAdminService> logger)
{
    private const int MaxExportRows = 50_000;

    // ═══════════════════════════════════════════════════════════════════════
    // 列表與詳情
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<PagedResult<AdminDonationListItemDto>> ListAsync(
        CharityAdminScope scope, DonationFilter filter, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
        var query = ApplyFilter(db.Donations.AsNoTracking(), filter);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Seq)
            .Skip((p - 1) * size).Take(size)
            .Select(d => new
            {
                d.Id,
                d.OrderNo,
                d.CreatedAt,
                d.PaidAt,
                d.Amount,
                d.Status,
                d.DonationProjectId,
                d.DonationStoreId,
                d.IsAnonymous,
                ProjectName = d.DonationProject.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                StoreName = d.DonationStore == null ? null : d.DonationStore.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                Invoice = d.DonationInvoices.OrderByDescending(i => i.Seq).Select(i => new { i.IssueStatus, i.VoidStatus }).FirstOrDefault(),
                LatestPaymentStatus = d.DonationPayments.OrderByDescending(x => x.Seq).Select(x => x.Status).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminDonationListItemDto>
        {
            Items = rows.Select(r => new AdminDonationListItemDto
            {
                Id = r.Id,
                OrderNo = r.OrderNo,
                CreatedAt = r.CreatedAt,
                PaidAt = r.PaidAt,
                Amount = r.Amount,
                Status = r.Status,
                ProjectId = r.DonationProjectId,
                ProjectName = r.ProjectName,
                StoreId = r.DonationStoreId,
                StoreName = r.StoreName,
                InvoiceStatus = r.Invoice?.IssueStatus,
                InvoiceVoidStatus = r.Invoice?.VoidStatus,
                IsAnonymous = r.IsAnonymous,
                NeedsManualReview = r.Status == DonationStatus.Pending && r.LatestPaymentStatus == PaymentStatus.Failed,
            }).ToList(),
            Page = p,
            PageSize = size,
            TotalCount = total,
        };
    }

    /// <summary>
    /// 單筆詳情。<paramref name="reveal"/> 為 <c>true</c> 時回傳個資明文，且<b>必須</b>持有 reveal 權限（否則 403），並寫稽核。
    /// </summary>
    public async Task<AdminDonationDetailDto?> GetAsync(
        CharityAdminScope scope, Guid id, bool reveal, string sourceIp, CancellationToken cancellationToken)
    {
        if (reveal && !await authorizer.HasAdditionalPermissionAsync(scope, CharityPermissions.DonationReveal, cancellationToken))
        {
            throw new AdminForbiddenException("檢視捐款人完整個資需要額外的授權，請洽系統管理員。");
        }

        var d = await db.Donations.AsNoTracking()
            .Include(x => x.DonationProject).ThenInclude(p => p.DonationProjectsI18ns)
            .Include(x => x.DonationStore).ThenInclude(s => s!.DonationStoresI18ns)
            .Include(x => x.DonationPayments)
            .Include(x => x.DonationInvoices).ThenInclude(i => i.VoidedByNavigation)
            .Include(x => x.RefundedByNavigation)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (d is null)
        {
            return null;
        }

        if (reveal)
        {
            audit.Stage(scope, CharityAuditActions.DonationRevealPii, CharityAuditTargets.Donation, d.Id, "檢視捐款人個資明文", null, sourceIp);
            await db.SaveChangesAsync(cancellationToken);
        }

        var logs = await db.AuditLogs.AsNoTracking()
            .Where(a => a.TargetType == CharityAuditTargets.Donation && a.TargetId == id && a.Action != CharityAuditActions.DonationRevealPii)
            .OrderBy(a => a.OccurredAt)
            .Select(a => new { a.OccurredAt, a.Action, a.ChangeSummary, Name = a.AdminUser.DisplayName })
            .ToListAsync(cancellationToken);

        var invoice = d.DonationInvoices.OrderByDescending(i => i.Seq).FirstOrDefault();
        var payments = d.DonationPayments.OrderBy(p => p.Seq).ToList();
        var latestPayment = payments.LastOrDefault();

        var timeline = new List<AdminTimelineEntryDto> { new(d.CreatedAt, "created", "建立捐款單", null) };
        foreach (var pay in payments)
        {
            if (pay.RequestedAt is { } rq)
            {
                timeline.Add(new(rq, "payment_requested", "發起付款", null));
            }

            if (pay.ConfirmedAt is { } cf)
            {
                timeline.Add(new(cf, "payment_confirmed", "金流確認扣款", null));
            }
        }

        if (d.PaidAt is { } paid)
        {
            timeline.Add(new(paid, "paid", "付款成立（寫入分潤快照）", null));
        }

        foreach (var log in logs)
        {
            timeline.Add(new(log.OccurredAt, log.Action, log.ChangeSummary ?? log.Action, log.Name));
        }

        return new AdminDonationDetailDto
        {
            Id = d.Id,
            OrderNo = d.OrderNo,
            Status = d.Status,
            Amount = d.Amount,
            CreatedAt = d.CreatedAt,
            PaidAt = d.PaidAt,
            UpdatedAt = d.UpdatedAt,
            Project = new AdminDonationProjectRef(
                d.DonationProject.Id, d.DonationProject.ProjectSlug,
                d.DonationProject.DonationProjectsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name),
            Store = d.DonationStore is null
                ? null
                : new AdminDonationStoreRef(d.DonationStore.Id, d.DonationStore.DonationStoresI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name),
            Donor = new AdminDonorDto(
                reveal ? d.DonorName : PiiMasking.MaskName(d.DonorName) ?? "○",
                reveal ? d.DonorEmail : PiiMasking.MaskEmail(d.DonorEmail) ?? "***",
                d.IsAnonymous, reveal),
            Split = new AdminDonationSplitDto(d.StoreSharePctSnapshot, d.ProjectSharePctSnapshot, d.StoreAmount, d.ProjectAmount, d.AssociationAmount),
            InvoiceMode = d.InvoiceMode,
            Payments = payments.Select(x => new AdminPaymentDto(x.Id, x.TransactionId, x.Status, x.RequestedAt, x.ConfirmedAt, x.Amount)).ToList(),
            Invoice = invoice is null ? null : ToInvoiceDto(invoice, reveal),
            Refund = d.Status == DonationStatus.Refunded ? new AdminRefundDto(d.RefundReason, d.RefundedByNavigation?.DisplayName) : null,
            NeedsManualReview = d.Status == DonationStatus.Pending && latestPayment?.Status == PaymentStatus.Failed,
            Timeline = timeline.OrderBy(t => t.At).ToList(),
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 操作：退款、重寄感謝信、重開憑證、重新確認付款
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 人工退款（規劃書 §4.4：前台不提供，後台保留；僅全額）。連動：金流退款 → 憑證作廢或折讓（§5.4）→ 退款通知信。
    /// 🔴 先對金流退款，成功之後才改本站狀態，且之後的寫入都用 <see cref="CancellationToken.None"/>——錢已經退出去了，
    /// 不能因為操作人員關掉頁面而讓本站還顯示「已付款」。🔴 憑證作廢失敗不會讓退款失敗，而是記下來進異常佇列（<c>invoice_void_pending</c>）。
    /// 回饋金沖回（規劃書 §8.5）屬 N4 結算（CH-4）：本站只把狀態改成 <c>refunded</c>，結算引擎依狀態排除或以負項沖回。
    /// </summary>
    public async Task<AdminDonationDetailDto> RefundAsync(
        CharityAdminScope scope, Guid id, string? reason, string sourceIp, CancellationToken cancellationToken)
    {
        var cleanReason = reason?.Trim();
        if (string.IsNullOrEmpty(cleanReason) || cleanReason.Length < 2)
        {
            throw new AdminValidationException("請填寫退款原因（至少 2 個字），稽核與客服追蹤會用到。");
        }

        if (cleanReason.Length > 255)
        {
            throw new AdminValidationException("退款原因不可超過 255 個字。");
        }

        // 🔴 同一筆捐款的退款以 SQL Server 應用程式鎖（sp_getapplock，交易層級）串行化：沒有「退款中」這個捐款單狀態（CHECK 約束沒有），
        // 若只靠最後的條件式更新，兩個並發請求會各打一次金流退款（真實 LINE Pay 會拒絕第二次，但不能把「不重複退款」押在對方的行為上）。
        // 鎖只在這個交易內有效（交易結束自動釋放，連線被回收也不會留下卡死的鎖）；拿不到鎖＝另一個請求正在退這一筆，直接回 409。
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await TryAcquireAppLockAsync(transaction, $"charity-refund:{id:N}", cancellationToken))
        {
            throw new CharityConflictException("退款處理中", "這筆捐款正在處理退款，請稍候重新整理頁面確認結果。");
        }

        var donation = await db.Donations.AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new
            {
                d.Id,
                d.OrderNo,
                d.Amount,
                d.Status,
                d.DonorEmail,
                d.DonorName,
                TransactionId = d.DonationPayments.Where(p => p.Status == PaymentStatus.Confirmed).OrderByDescending(p => p.Seq).Select(p => p.TransactionId).FirstOrDefault(),
                Project = d.DonationProject.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("找不到這筆捐款。");

        if (donation.Status != DonationStatus.Paid)
        {
            throw new CharityConflictException("無法退款", "只有已付款的捐款可以退款；這筆捐款目前的狀態不允許。");
        }

        if (string.IsNullOrEmpty(donation.TransactionId))
        {
            throw new CharityConflictException("無法退款", "這筆捐款沒有已確認的金流交易，無法對金流退款，請洽系統管理員。");
        }

        PaymentRefundResult refund;
        try
        {
            refund = await gateway.RefundPaymentAsync(new PaymentRefundRequest(donation.OrderNo, donation.TransactionId, donation.Amount), cancellationToken);
        }
        catch (Exception ex) when (ex is PaymentGatewayUnavailableException or PaymentGatewayNotConfiguredException)
        {
            logger.LogWarning(ex, "金流退款失敗，單號 {OrderNo}", donation.OrderNo);
            throw new CharityServiceUnavailableException("金流服務暫時無法使用，退款沒有執行，請稍後再試。");
        }

        if (!refund.Succeeded)
        {
            throw new CharityConflictException("金流端拒絕退款", "金流端沒有接受這筆退款（可能已經退過或超過可退款期限），請到金流後台確認。");
        }

        // 錢已退。以下全部不可中斷（用 None 寫入；沿用上面開的交易與鎖，一起提交）。
        var now = DateTime.UtcNow;
        try
        {
            var moved = await db.Donations
                .Where(d => d.Id == id && d.Status == DonationStatus.Paid)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.Status, DonationStatus.Refunded)
                    .SetProperty(d => d.RefundReason, cleanReason)
                    .SetProperty(d => d.RefundedBy, scope.Identity.AdminUserId)
                    .SetProperty(d => d.UpdatedAt, now)
                    .SetProperty(d => d.UpdatedBy, scope.Identity.AdminUserId),
                    CancellationToken.None);

            if (moved == 0)
            {
                // 鎖內讀到 paid、更新時卻不是了：極不可能（只有別的退款路徑會改，而它拿不到鎖）。留錯誤日誌，不重複寫稽核。
                await transaction.RollbackAsync(CancellationToken.None);
                logger.LogError("退款成功但捐款單狀態已不是已付款，單號 {OrderNo}", donation.OrderNo);
                throw new CharityConflictException("狀態已變更", "這筆捐款的狀態剛剛已經變更，請重新整理頁面確認。");
            }

            audit.Stage(scope, CharityAuditActions.DonationRefund, CharityAuditTargets.Donation, id,
                $"全額退款 NT$ {donation.Amount.ToString("N0", CultureInfo.InvariantCulture)}，原因：{cleanReason}", null, sourceIp);
            await db.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not CharityApiException)
        {
            // 金流端已退款、本站卻寫入失敗：必須有人知道（日誌是唯一線索，單號在訊息裡），否則帳上還是已付款。
            logger.LogCritical(ex, "🔴 金流退款已成功，但本站寫入失敗，需人工把捐款單 {OrderNo} 改成已退款", donation.OrderNo);
            throw;
        }

        // 憑證作廢或折讓（失敗不影響退款，記錄後進異常佇列）。
        try
        {
            var tracked = await db.Donations.AsNoTracking().SingleAsync(d => d.Id == id, CancellationToken.None);
            await invoices.VoidOrAllowForRefundAsync(tracked, cleanReason, scope.Identity.AdminUserId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "退款後憑證作廢或折讓失敗，已進異常佇列，單號 {OrderNo}", donation.OrderNo);
        }

        // 退款通知信（失敗只記 email_logs，不影響退款）。
        await mail.SendAsync(
            EmailTemplateCodes.RefundNotice, donation.DonorEmail,
            new Dictionary<string, string>
            {
                ["donor_name"] = donation.DonorName,
                ["order_no"] = donation.OrderNo,
                ["amount"] = donation.Amount.ToString(CultureInfo.InvariantCulture),
                ["project_name"] = donation.Project ?? string.Empty,
                ["refund_reason"] = cleanReason,
            },
            CancellationToken.None);

        return (await GetAsync(scope, id, reveal: false, sourceIp, CancellationToken.None))!;
    }

    /// <summary>以交易層級的應用程式鎖串行化某個資源；<c>LockTimeout = 0</c>：拿不到立刻回 <c>false</c>，不排隊等待。</summary>
    private async Task<bool> TryAcquireAppLockAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, string resource, CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.CommandText = "sp_getapplock";

        void Add(string name, System.Data.DbType type, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        Add("@Resource", System.Data.DbType.String, resource);
        Add("@LockMode", System.Data.DbType.String, "Exclusive");
        Add("@LockOwner", System.Data.DbType.String, "Transaction");
        Add("@LockTimeout", System.Data.DbType.Int32, 0);

        var result = command.CreateParameter();
        result.ParameterName = "@result";
        result.DbType = System.Data.DbType.Int32;
        result.Direction = System.Data.ParameterDirection.ReturnValue;
        command.Parameters.Add(result);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return (int)result.Value! >= 0; // 0＝取得、1＝等待後取得；負數＝逾時、被取消、死結、錯誤
    }

    public async Task<bool> ResendThanksAsync(CharityAdminScope scope, Guid id, string sourceIp, CancellationToken cancellationToken)
    {
        var status = await db.Donations.AsNoTracking().Where(d => d.Id == id).Select(d => d.Status).SingleOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("找不到這筆捐款。");
        if (status != DonationStatus.Paid)
        {
            throw new CharityConflictException("無法寄送", "只有已付款的捐款才能寄送感謝信。");
        }

        var sent = await donationService.SendThanksAsync(id, cancellationToken);
        audit.Stage(scope, CharityAuditActions.DonationResendThanks, CharityAuditTargets.Donation, id, sent ? "重寄感謝信（已寄出）" : "重寄感謝信（寄送失敗）", null, sourceIp);
        await db.SaveChangesAsync(CancellationToken.None);
        return sent;
    }

    public async Task<AdminDonationDetailDto> ReissueInvoiceAsync(CharityAdminScope scope, Guid id, string sourceIp, CancellationToken cancellationToken)
    {
        var outcome = await invoices.ReissueAsync(id, cancellationToken);
        audit.Stage(scope, CharityAuditActions.DonationReissueInvoice, CharityAuditTargets.Donation, id, $"重新開立憑證（結果：{outcome}）", null, sourceIp);
        await db.SaveChangesAsync(CancellationToken.None);
        return (await GetAsync(scope, id, reveal: false, sourceIp, cancellationToken))!;
    }

    public async Task<AdminDonationDetailDto> RecheckPaymentAsync(CharityAdminScope scope, Guid id, string sourceIp, CancellationToken cancellationToken)
    {
        var orderNo = await db.Donations.AsNoTracking().Where(d => d.Id == id).Select(d => d.OrderNo).SingleOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("找不到這筆捐款。");

        var result = await donationService.RecheckPaymentAsync(orderNo, cancellationToken);
        audit.Stage(scope, CharityAuditActions.DonationRecheckPayment, CharityAuditTargets.Donation, id, $"重新確認付款結果（結果：{result.Status}）", null, sourceIp);
        await db.SaveChangesAsync(CancellationToken.None);
        return (await GetAsync(scope, id, reveal: false, sourceIp, cancellationToken))!;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 異常佇列
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<AdminAnomalyCountsDto> CountAnomaliesAsync(CharityAdminScope scope, CancellationToken cancellationToken)
        => new(
            await ConfirmFailedQuery().CountAsync(cancellationToken),
            await InvoiceFailedQuery().CountAsync(cancellationToken),
            await InvoiceVoidPendingQuery().CountAsync(cancellationToken),
            await db.ReconciliationDiscrepancies.AsNoTracking().CountAsync(r => r.ResolutionStatus == "pending", cancellationToken));

    public async Task<IReadOnlyList<AdminAnomalyDto>> ListAnomaliesAsync(CharityAdminScope scope, string? kind, CancellationToken cancellationToken)
    {
        var result = new List<AdminAnomalyDto>();

        if (kind is null or "confirm_failed")
        {
            result.AddRange((await ConfirmFailedQuery().OrderBy(d => d.UpdatedAt).Take(200)
                .Select(d => new { d.Id, d.OrderNo, d.Amount, d.UpdatedAt }).ToListAsync(cancellationToken))
                .Select(d => new AdminAnomalyDto
                {
                    Kind = "confirm_failed", DonationId = d.Id, OrderNo = d.OrderNo, Amount = d.Amount, OccurredAt = d.UpdatedAt,
                    DiscrepancyType = null, ResolutionStatus = null, DiscrepancyId = null,
                }));
        }

        if (kind is null or "invoice_failed")
        {
            result.AddRange((await InvoiceFailedQuery().OrderBy(i => i.UpdatedAt).Take(200)
                .Select(i => new { i.DonationId, i.Donation.OrderNo, i.Donation.Amount, i.UpdatedAt }).ToListAsync(cancellationToken))
                .Select(i => new AdminAnomalyDto
                {
                    Kind = "invoice_failed", DonationId = i.DonationId, OrderNo = i.OrderNo, Amount = i.Amount, OccurredAt = i.UpdatedAt,
                    DiscrepancyType = null, ResolutionStatus = null, DiscrepancyId = null,
                }));
        }

        if (kind is null or "invoice_void_pending")
        {
            result.AddRange((await InvoiceVoidPendingQuery().OrderBy(i => i.UpdatedAt).Take(200)
                .Select(i => new { i.DonationId, i.Donation.OrderNo, i.Donation.Amount, i.Donation.UpdatedAt }).ToListAsync(cancellationToken))
                .Select(i => new AdminAnomalyDto
                {
                    Kind = "invoice_void_pending", DonationId = i.DonationId, OrderNo = i.OrderNo, Amount = i.Amount, OccurredAt = i.UpdatedAt,
                    DiscrepancyType = null, ResolutionStatus = null, DiscrepancyId = null,
                }));
        }

        if (kind is null or "reconciliation")
        {
            result.AddRange((await db.ReconciliationDiscrepancies.AsNoTracking().Where(r => r.ResolutionStatus == "pending")
                .OrderBy(r => r.CreatedAt).Take(200)
                .Select(r => new
                {
                    r.Id, r.DiscrepancyType, r.ResolutionStatus, r.CreatedAt, r.DonationId,
                    OrderNo = r.Donation == null ? null : r.Donation.OrderNo, Amount = r.SiteAmount ?? r.GatewayAmount,
                }).ToListAsync(cancellationToken))
                .Select(r => new AdminAnomalyDto
                {
                    Kind = "reconciliation", DonationId = r.DonationId, OrderNo = r.OrderNo, Amount = r.Amount, OccurredAt = r.CreatedAt,
                    DiscrepancyType = r.DiscrepancyType, ResolutionStatus = r.ResolutionStatus, DiscrepancyId = r.Id,
                }));
        }

        return result.OrderBy(a => a.OccurredAt).ToList();
    }

    private IQueryable<Donation> ConfirmFailedQuery()
        => db.Donations.AsNoTracking().Where(d => d.Status == DonationStatus.Pending
            && d.DonationPayments.OrderByDescending(p => p.Seq).Take(1).Any(p => p.Status == PaymentStatus.Failed));

    private IQueryable<DonationInvoice> InvoiceFailedQuery()
        => db.DonationInvoices.AsNoTracking().Where(i => i.IssueStatus == "failed" && i.VoidStatus == "none" && i.Donation.Status == DonationStatus.Paid);

    private IQueryable<DonationInvoice> InvoiceVoidPendingQuery()
        => db.DonationInvoices.AsNoTracking().Where(i => i.VoidStatus == "none" && i.Donation.Status == DonationStatus.Refunded);

    // ═══════════════════════════════════════════════════════════════════════
    // 含個資的明細匯出
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 匯出含個資的明細 CSV（規劃書 §6.3：匯出須額外授權並寫稽核日誌；§10：記錄用途備註）。
    /// 🔴 用途備註必填（至少 4 個字）；稽核寫入「篩選條件＋筆數＋用途備註」（不含任何個資明文）。🔴 文字欄位做 CSV 公式注入防護。
    /// 上限 <see cref="MaxExportRows"/> 筆，超過要求縮小篩選範圍（不靜默截斷——匯出被截斷卻看起來完整，對勸募帳務比失敗更糟）。
    /// </summary>
    public async Task<byte[]> ExportAsync(
        CharityAdminScope scope, DonationFilter filter, string? purposeNote, string sourceIp, CancellationToken cancellationToken)
    {
        var purpose = purposeNote?.Trim();
        if (string.IsNullOrEmpty(purpose) || purpose.Length < 4)
        {
            throw new AdminValidationException("匯出含個資的明細需要填寫用途備註（至少 4 個字），會記錄在稽核紀錄。");
        }

        var query = ApplyFilter(db.Donations.AsNoTracking(), filter);
        var count = await query.CountAsync(cancellationToken);
        if (count > MaxExportRows)
        {
            throw new CharityUnprocessableException($"符合條件的捐款有 {count:N0} 筆，超過單次匯出上限 {MaxExportRows:N0} 筆，請縮小期間或篩選條件後再匯出。");
        }

        var rows = await query
            .OrderBy(d => d.CreatedAt).ThenBy(d => d.Seq)
            .Select(d => new
            {
                d.OrderNo, d.CreatedAt, d.PaidAt, d.Status, d.Amount, d.DonorName, d.DonorEmail, d.IsAnonymous, d.InvoiceMode,
                d.StoreAmount, d.ProjectAmount, d.AssociationAmount,
                Project = d.DonationProject.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                Store = d.DonationStore == null ? null : d.DonationStore.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                Invoice = d.DonationInvoices.OrderByDescending(i => i.Seq).Select(i => new { i.IssueStatus, i.VoidStatus, i.InvoiceNo }).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var csv = new List<IEnumerable<string?>>
        {
            new string?[]
            {
                "單號", "建立時間", "付款時間", "狀態", "金額", "捐款項目", "來源店家", "捐款人姓名", "Email", "具名／匿名",
                "憑證模式", "憑證狀態", "憑證作廢狀態", "憑證號碼", "店家回饋金", "項目撥付金", "協會留存",
            },
        };
        foreach (var r in rows)
        {
            csv.Add(new string?[]
            {
                CsvUtils.SafeCell(r.OrderNo), Taiwan(r.CreatedAt), r.PaidAt is { } p ? Taiwan(p) : null, r.Status,
                r.Amount.ToString(CultureInfo.InvariantCulture), CsvUtils.SafeCell(r.Project), CsvUtils.SafeCell(r.Store),
                CsvUtils.SafeCell(r.DonorName), CsvUtils.SafeCell(r.DonorEmail), r.IsAnonymous ? "匿名" : "具名",
                r.InvoiceMode, r.Invoice?.IssueStatus, r.Invoice?.VoidStatus, CsvUtils.SafeCell(r.Invoice?.InvoiceNo),
                r.StoreAmount.ToString(CultureInfo.InvariantCulture), r.ProjectAmount.ToString(CultureInfo.InvariantCulture),
                r.AssociationAmount.ToString(CultureInfo.InvariantCulture),
            });
        }

        audit.Stage(scope, CharityAuditActions.DonationExport, CharityAuditTargets.Donation, null,
            $"匯出含個資明細 {rows.Count} 筆；條件：{DescribeFilter(filter)}", purpose, sourceIp);
        await db.SaveChangesAsync(CancellationToken.None);

        return CsvUtils.ToUtf8BytesWithBom(CsvUtils.BuildCsv(csv));
    }

    // ───────────────────────────────────────────────────────────────────────

    private static string Taiwan(DateTime utc) => utc.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string DescribeFilter(DonationFilter f)
    {
        var parts = new List<string>();
        if (f.From is { } from) parts.Add($"自 {from:yyyy-MM-dd}");
        if (f.To is { } to) parts.Add($"至 {to:yyyy-MM-dd}");
        if (!string.IsNullOrEmpty(f.Status)) parts.Add($"狀態 {f.Status}");
        if (f.ProjectId is { } pid) parts.Add($"項目 {pid}");
        if (f.StoreId is { } sid) parts.Add($"店家 {sid}");
        if (f.NoStore) parts.Add("無店家歸屬");
        if (!string.IsNullOrEmpty(f.InvoiceStatus)) parts.Add($"憑證 {f.InvoiceStatus}");
        if (f.AmountMin is { } min) parts.Add($"金額≥{min}");
        if (f.AmountMax is { } max) parts.Add($"金額≤{max}");
        return parts.Count == 0 ? "（無，全部）" : string.Join("、", parts);
    }

    private static IQueryable<Donation> ApplyFilter(IQueryable<Donation> query, DonationFilter f)
    {
        if (f.From is { } from)
        {
            var fromUtc = TaiwanClock.StartOfDayUtc(from);
            query = query.Where(d => d.CreatedAt >= fromUtc);
        }

        if (f.To is { } to)
        {
            var toUtcExclusive = TaiwanClock.StartOfDayUtc(to.AddDays(1));
            query = query.Where(d => d.CreatedAt < toUtcExclusive);
        }

        if (!string.IsNullOrEmpty(f.Status))
        {
            query = query.Where(d => d.Status == f.Status);
        }

        if (f.ProjectId is { } projectId)
        {
            query = query.Where(d => d.DonationProjectId == projectId);
        }

        if (f.NoStore)
        {
            query = query.Where(d => d.DonationStoreId == null);
        }
        else if (f.StoreId is { } storeId)
        {
            query = query.Where(d => d.DonationStoreId == storeId);
        }

        if (!string.IsNullOrEmpty(f.InvoiceStatus))
        {
            query = query.Where(d => d.DonationInvoices.OrderByDescending(i => i.Seq).Take(1).Any(i => i.IssueStatus == f.InvoiceStatus));
        }

        if (f.AmountMin is { } amin)
        {
            query = query.Where(d => d.Amount >= amin);
        }

        if (f.AmountMax is { } amax)
        {
            query = query.Where(d => d.Amount <= amax);
        }

        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var k = f.Keyword.Trim();
            query = query.Where(d => d.OrderNo.StartsWith(k));
        }

        return query;
    }

    private AdminInvoiceDto ToInvoiceDto(DonationInvoice i, bool reveal)
    {
        var carrierPlain = protector.TryDecryptCarrierId(i.CarrierIdEncrypted);
        var nationalPlain = protector.TryDecryptNationalId(i.NationalIdEncrypted);
        return new AdminInvoiceDto
        {
            InvoiceType = i.InvoiceType,
            CarrierType = CarrierTypes.Normalize(i.CarrierType),
            CarrierId = i.CarrierIdEncrypted is null ? null : reveal ? carrierPlain : MaskCarrier(carrierPlain),
            TaxId = i.TaxId,
            InvoiceTitle = i.InvoiceTitle,
            // 收據抬頭預設就是捐款人姓名（可改），屬個資：未解除遮罩時一律遮罩。發票抬頭（統編公司名稱）是商業登記公開資訊，不遮。
            ReceiptTitle = reveal ? i.ReceiptTitle : PiiMasking.MaskName(i.ReceiptTitle),
            NationalId = i.NationalIdEncrypted is null ? null : reveal ? nationalPlain : CharityDataProtector.MaskNationalId(nationalPlain),
            ReceiptAddress = reveal ? i.ReceiptAddress : PiiMasking.MaskAddress(i.ReceiptAddress),
            IsAnnualSummary = i.IsAnnualSummary,
            IssueStatus = i.IssueStatus,
            VoidStatus = i.VoidStatus,
            InvoiceNo = i.InvoiceNo,
            IssuedAt = i.IssuedAt,
            VoidReason = i.VoidReason,
            VoidedByName = i.VoidedByNavigation?.DisplayName,
        };
    }

    private static string MaskCarrier(string? plain)
        => string.IsNullOrEmpty(plain) || plain.Length < 4 ? "***" : plain[..2] + new string('*', plain.Length - 2);
}
