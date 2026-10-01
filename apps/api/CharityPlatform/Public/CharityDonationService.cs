using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Invoices;
using Tcrfc.Api.CharityPlatform.Mail;
using Tcrfc.Api.CharityPlatform.Payments;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Public;

/// <summary>
/// 捐款主幹：建單（冪等）→ 發起付款 → 確認／取消 → 結果查詢（規劃書 §3.3–§3.5、§4）。收款主體是協會。
///
/// 🔴 <b>冪等與金流狀態不得讀快取</b>（docs/14「五類不得讀快取」、docs/16 §7）：本類別所有讀取都直接查資料庫，
/// 狀態轉移一律是「<c>UPDATE ... WHERE status IN (預期狀態)</c>」的<b>條件式更新</b>，由資料庫保證只有一個請求贏得轉移——
/// 重複 Confirm 不會重複入帳、重複開票、重複寄信（§4.2 硬性要求）：贏的才做後續動作，輸的只回傳目前結果。
///
/// 🔴 <b>「已扣款但 Confirm 結果未知」絕不可靜默丟棄或當成失敗</b>（§4.3 最嚴重的例外）：金流的 Confirm 發生技術性失敗
/// 時，捐款單維持 <c>pending</c>、最近一次付款紀錄標記為 <c>failed</c>——<b>「捐款單 pending ＋ 最近一次付款 failed」就是
/// 待人工處理的唯一定義</b>（後台異常佇列的第一類）。這個狀態下禁止重新發起付款（避免重複扣款），背景逾時工作也不會把它轉成
/// 逾時，直到人員確認。
/// </summary>
public sealed class CharityDonationService(
    CharityDbContext db,
    CharityPublicCatalog catalog,
    IPaymentGateway gateway,
    CharityDataProtector protector,
    CharityEmailService mail,
    CharityInvoiceService invoices,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<CharityDonationService> logger)
{
    private const int MaxDonorNameLength = 128;
    private const int MaxEmailLength = 255;
    private const int MaxTitleLength = 128;
    private const int MaxAddressLength = 500;

    // ═══════════════════════════════════════════════════════════════════════
    // 建單
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<CreateDonationResponse> CreateAsync(
        CreateDonationRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (!CharityDonationRules.IsValidIdempotencyKey(idempotencyKey))
        {
            throw new AdminValidationException("缺少或不合法的冪等鍵（Idempotency-Key 標頭需為 16–64 個英數、底線或連字號）。");
        }

        var orderNo = CharityDonationRules.DeriveOrderNo(CharityOptions.ResolveOrderNoSecret(configuration), idempotencyKey!);

        // 同一個冪等鍵重複送出：先看這張單是不是已經存在。放在所有「依項目現況」的驗證之前——
        // 重送時項目可能已被下架或金額範圍已改，但原本成功建立的那張單仍然是正確答案。
        var existing = await LoadForIdempotencyAsync(orderNo, cancellationToken);
        if (existing is not null)
        {
            return ReplayOrConflict(existing, request);
        }

        var input = ValidateBasics(request);
        var project = await db.DonationProjects.AsNoTracking()
            .Where(p => p.ProjectSlug == input.ProjectSlug && p.Status == "published")
            .Select(p => new { p.Id, p.MinAmount, p.MaxAmount, p.InvoiceMode, p.ProjectSharePct })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("找不到這個捐款項目，可能已經下架。");

        var (defaultMin, defaultMax) = await catalog.GetDefaultAmountRangeAsync(cancellationToken);
        var min = project.MinAmount ?? defaultMin;
        var max = project.MaxAmount ?? defaultMax;
        if (request.Amount < min || request.Amount > max)
        {
            throw new CharityUnprocessableException($"單筆捐款金額須介於 NT$ {min:N0} 到 NT$ {max:N0} 之間。");
        }

        var invoiceInput = ValidateInvoice(request.Invoice, project.InvoiceMode, input.DonorName);

        // 店家歸屬：對不到有效店家視同無店家歸屬，不報錯（§2.2 第 5 點）。
        var store = await catalog.ResolveActiveStoreRefAsync(request.StoreSlug, cancellationToken);
        var storePct = store?.SharePct ?? 0m;
        if (storePct + project.ProjectSharePct > 100m)
        {
            // N1／N2 儲存時都會擋，走到這裡代表設定被繞過或資料不一致：不能收這筆款（資料庫 CHECK 也會拒絕）。
            logger.LogError("店家與項目分潤率合計超過 100%，拒絕建單（項目 {ProjectId}）", project.Id);
            throw new CharityUnprocessableException("這個捐款項目目前暫時無法接受捐款，請稍後再試或聯繫協會。");
        }

        var (storeAmount, projectAmount, associationAmount) =
            CharityDonationRules.ComputeSplit(request.Amount, storePct, project.ProjectSharePct);

        var now = DateTime.UtcNow;
        var donation = new Donation
        {
            Id = Guid.NewGuid(),
            OrderNo = orderNo,
            DonationProjectId = project.Id,
            DonationStoreId = store?.Id,
            Amount = request.Amount,
            Status = DonationStatus.Created,
            CreatedAt = now,
            DonorName = input.DonorName,
            DonorEmail = input.DonorEmail,
            IsAnonymous = request.IsAnonymous,
            // 此刻的分潤是「暫算」；付款成立（paid）時才是帳務依據的快照，會依當時的設定重算一次（§8.4）。
            StoreSharePctSnapshot = storePct,
            ProjectSharePctSnapshot = project.ProjectSharePct,
            StoreAmount = storeAmount,
            ProjectAmount = projectAmount,
            AssociationAmount = associationAmount,
            InvoiceMode = project.InvoiceMode,
            UpdatedAt = now,
        };
        var invoice = BuildInvoice(donation.Id, project.InvoiceMode, invoiceInput, now);

        try
        {
            db.Donations.Add(donation);
            db.DonationInvoices.Add(invoice);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // 同一個冪等鍵的並發請求：另一個請求搶先建單成功了。丟掉自己這份、讀對方的那張。
            db.ChangeTracker.Clear();
            var winner = await LoadForIdempotencyAsync(orderNo, cancellationToken) ?? throw new InvalidOperationException("唯一鍵衝突後找不到既有捐款單。", ex);
            return ReplayOrConflict(winner, request);
        }

        return new CreateDonationResponse { OrderNo = orderNo, Status = DonationStatus.Created, Amount = donation.Amount, Created = true };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 發起付款
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<StartPaymentResponse> StartPaymentAsync(string orderNo, string? lang, CancellationToken cancellationToken)
    {
        var baseUrl = CharityOptions.ResolvePublicBaseUrl(configuration, environment)
            ?? throw new CharityServiceUnavailableException("付款服務尚未完成設定，請稍後再試或聯繫協會。");

        var donation = await db.Donations.AsNoTracking()
            .Where(d => d.OrderNo == orderNo)
            .Select(d => new
            {
                d.Id,
                d.Status,
                d.Amount,
                ProjectNameZh = d.DonationProject.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                ProjectNameEn = d.DonationProject.DonationProjectsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("找不到這筆捐款單。");

        if (donation.Status is DonationStatus.Paid or DonationStatus.Refunded)
        {
            throw new CharityConflictException("捐款已完成", "這筆捐款已經完成，不需要再付款。");
        }

        var payments = await db.DonationPayments.AsNoTracking()
            .Where(p => p.DonationId == donation.Id)
            .OrderByDescending(p => p.Seq)
            .Select(p => new { p.Id, p.Status, p.RawResponse, p.RequestedAt })
            .ToListAsync(cancellationToken);
        var latest = payments.FirstOrDefault();
        var timeout = TimeSpan.FromMinutes(CharityOptions.ResolvePaymentTimeoutMinutes(configuration));

        if (donation.Status == DonationStatus.Pending)
        {
            if (latest is { Status: PaymentStatus.Failed })
            {
                // 待人工處理：結果未知，可能已經扣款。絕不可再發起一次付款（會重複扣款）。
                throw new CharityConflictException("付款結果確認中", "這筆捐款的付款結果正在確認中，請勿重複付款；確認完成後我們會寄信通知您。");
            }

            if (latest is { Status: PaymentStatus.Requested } && latest.RequestedAt is { } requestedAt && DateTime.UtcNow - requestedAt < timeout
                && TryReadPaymentUrl(latest.RawResponse) is { } existingUrl)
            {
                // 同一張單已經有一筆進行中的付款：沿用它（重複點擊、重新整理不會產生第二筆付款）。
                return new StartPaymentResponse { OrderNo = orderNo, Status = DonationStatus.Pending, PaymentUrl = existingUrl };
            }
        }

        var normalizedLang = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
        var productName = (normalizedLang == "en" ? donation.ProjectNameEn : null) ?? donation.ProjectNameZh ?? "捐款";
        if (productName.Length > 100)
        {
            productName = productName[..100];
        }

        PaymentRequestResult requested;
        try
        {
            requested = await gateway.RequestPaymentAsync(
                new PaymentRequest(
                    orderNo, payments.Count + 1, donation.Amount, productName, normalizedLang,
                    ConfirmUrl: $"{baseUrl}/{normalizedLang}/result/{Uri.EscapeDataString(orderNo)}",
                    CancelUrl: $"{baseUrl}/{normalizedLang}/result/{Uri.EscapeDataString(orderNo)}?cancel=1"),
                cancellationToken);
        }
        catch (Exception ex) when (ex is PaymentGatewayUnavailableException or PaymentGatewayNotConfiguredException)
        {
            // 金流服務中斷：不留下語意不明的半完成狀態（§4.3）——捐款單狀態不動、不寫付款紀錄，前台顯示明確錯誤。
            logger.LogWarning(ex, "發起付款失敗，單號 {OrderNo}", orderNo);
            throw new CharityServiceUnavailableException("付款服務暫時無法使用，請稍後再試；若持續發生請聯繫協會。");
        }

        // 寫入：狀態轉移與付款紀錄同一個交易。條件式更新保證只有一個並發請求贏得 created/failed/expired → pending。
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (donation.Status != DonationStatus.Pending)
        {
            var moved = await db.Donations
                .Where(d => d.Id == donation.Id && (d.Status == DonationStatus.Created || d.Status == DonationStatus.Failed || d.Status == DonationStatus.Expired))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DonationStatus.Pending).SetProperty(d => d.UpdatedAt, DateTime.UtcNow), cancellationToken);
            if (moved == 0)
            {
                // 別的請求搶先處理了（可能剛付完、剛取消、或剛發起付款）。放棄自己這筆金流端的交易（未付款會自行逾時），回報目前狀態。
                await transaction.RollbackAsync(cancellationToken);
                throw new CharityConflictException("捐款單狀態已變更", "這筆捐款的狀態剛剛已經變更，請重新整理頁面。");
            }
        }

        // 同一張單上一筆還在「requested」的（例如已逾時沒付）標記為取消，避免兩筆都活著。
        await db.DonationPayments
            .Where(p => p.DonationId == donation.Id && p.Status == PaymentStatus.Requested)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Cancelled).SetProperty(p => p.UpdatedAt, DateTime.UtcNow), cancellationToken);

        db.DonationPayments.Add(new DonationPayment
        {
            Id = Guid.NewGuid(),
            DonationId = donation.Id,
            TransactionId = requested.TransactionId,
            RequestedAt = DateTime.UtcNow,
            Amount = donation.Amount,
            Status = PaymentStatus.Requested,
            // raw_response 只存不查（docs/16 §8）：額外附上付款網址，讓「同一張單重複發起付款」能沿用而不必再打一次金流。
            RawResponse = JsonSerializer.Serialize(new { paymentUrl = requested.PaymentUrl, response = requested.RawResponse }),
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new StartPaymentResponse { OrderNo = orderNo, Status = DonationStatus.Pending, PaymentUrl = requested.PaymentUrl };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 確認（使用者從 LINE Pay 返回後，confirmUrlType = CLIENT，由伺服器端主動呼叫 Confirm）
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<PublicDonationResultDto> ConfirmAsync(string orderNo, string? transactionId, string? lang, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            throw new AdminValidationException("缺少交易識別碼。");
        }

        var donation = await db.Donations.AsNoTracking()
            .Where(d => d.OrderNo == orderNo)
            .Select(d => new { d.Id, d.Status, d.Amount })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("找不到這筆捐款單。");

        // 冪等：已經有定論的單，重複 Confirm 只回傳目前結果，不打金流、不入帳、不開票、不寄信。
        if (donation.Status is DonationStatus.Paid or DonationStatus.Refunded)
        {
            return await GetResultAsync(orderNo, lang, cancellationToken);
        }

        if (donation.Status is not (DonationStatus.Pending or DonationStatus.Expired))
        {
            throw new CharityConflictException("尚未付款", "這筆捐款尚未發起付款或付款已失敗，請重新發起付款。");
        }

        var payment = await db.DonationPayments.AsNoTracking()
            .Where(p => p.DonationId == donation.Id)
            .OrderByDescending(p => p.Seq)
            .Select(p => new { p.Id, p.TransactionId, p.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null || !string.Equals(payment.TransactionId, transactionId, StringComparison.Ordinal))
        {
            throw new AdminValidationException("交易識別碼與這張捐款單不符。");
        }

        // requested＝正常等待確認；failed＝上一次 Confirm 結果未知（待人工處理的單，允許再確認一次）。cancelled 不可確認。
        if (payment.Status is not (PaymentStatus.Requested or PaymentStatus.Failed))
        {
            throw new CharityConflictException("付款已取消", "這筆付款已經取消，請重新發起付款。");
        }

        PaymentConfirmResult confirmed;
        try
        {
            confirmed = await gateway.ConfirmPaymentAsync(new PaymentConfirmRequest(orderNo, transactionId, donation.Amount), cancellationToken);
        }
        catch (PaymentGatewayUnavailableException ex)
        {
            // 🔴 結果未知：可能已經扣款。絕不當成失敗，也絕不靜默丟棄（見類別說明）。以下寫入用 None——請求被取消也要記下來。
            logger.LogError(ex, "金流 Confirm 發生技術性失敗，結果未知，單號 {OrderNo}，轉入待人工處理", orderNo);
            await MarkConfirmUnknownAsync(donation.Id, payment.Id, ex.Message);
            return await GetResultAsync(orderNo, lang, CancellationToken.None);
        }
        catch (PaymentGatewayNotConfiguredException ex)
        {
            logger.LogWarning(ex, "金流尚未設定，無法確認，單號 {OrderNo}", orderNo);
            throw new CharityServiceUnavailableException("付款服務暫時無法使用，請稍後再試；若持續發生請聯繫協會。");
        }

        // 金流端已有明確結果。從這裡開始所有寫入都用 CancellationToken.None：錢已經動了，不能因為使用者關掉頁面而少記一半。
        if (confirmed.Outcome == PaymentConfirmOutcome.Confirmed)
        {
            if (await MarkPaidAsync(donation.Id, payment.Id, confirmed.RawResponse))
            {
                await RunAfterPaidAsync(donation.Id); // 只有贏得轉移的請求才會進來：感謝信與開票各只做一次
            }
        }
        else
        {
            await MarkDeclinedAsync(donation.Id, payment.Id, PaymentStatus.Failed, confirmed.RawResponse);
        }

        return await GetResultAsync(orderNo, lang, CancellationToken.None);
    }

    /// <summary>使用者在 LINE Pay 取消、返回本站。pending → failed（可重試）；已有定論的單不動。冪等。</summary>
    public async Task<PublicDonationResultDto> CancelAsync(string orderNo, string? lang, CancellationToken cancellationToken)
    {
        var donation = await db.Donations.AsNoTracking()
            .Where(d => d.OrderNo == orderNo)
            .Select(d => new { d.Id, d.Status })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("找不到這筆捐款單。");

        if (donation.Status == DonationStatus.Pending)
        {
            var latest = await db.DonationPayments.AsNoTracking()
                .Where(p => p.DonationId == donation.Id)
                .OrderByDescending(p => p.Seq)
                .Select(p => new { p.Id, p.Status })
                .FirstOrDefaultAsync(cancellationToken);

            // 只有「等待付款中」的才能取消；上一次 Confirm 結果未知的（payment failed）不能因為使用者按取消就當作沒扣款。
            if (latest is { Status: PaymentStatus.Requested })
            {
                await MarkDeclinedAsync(donation.Id, latest.Id, PaymentStatus.Cancelled, rawResponse: null);
            }
        }

        return await GetResultAsync(orderNo, lang, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 結果頁
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<PublicDonationResultDto> GetResultAsync(string orderNo, string? lang, CancellationToken cancellationToken)
    {
        var dbLocale = RequestLocale.ToDbLocale(lang);
        var row = await db.Donations.AsNoTracking()
            .Where(d => d.OrderNo == orderNo)
            .Select(d => new
            {
                d.OrderNo,
                d.Status,
                d.Amount,
                d.CreatedAt,
                d.PaidAt,
                d.DonorName,
                d.DonorEmail,
                d.InvoiceMode,
                ProjectSlug = d.DonationProject.ProjectSlug,
                ProjectRequested = d.DonationProject.DonationProjectsI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Name).FirstOrDefault(),
                ProjectDefault = d.DonationProject.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                StoreRequested = d.DonationStore == null ? null : d.DonationStore.DonationStoresI18ns.Where(i => i.Locale == dbLocale).Select(i => i.Name).FirstOrDefault(),
                StoreDefault = d.DonationStore == null ? null : d.DonationStore.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                Invoice = d.DonationInvoices.OrderByDescending(i => i.Seq).Select(i => new { i.IssueStatus, i.InvoiceNo }).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("找不到這筆捐款單。");

        // 憑證失敗對外一律顯示「處理中」：失敗細節是協會內部的人工補開佇列（§5.3），不嚇捐款人。
        var invoiceStatus = row.Invoice?.IssueStatus switch
        {
            "issued" => "issued",
            _ => "pending",
        };

        return new PublicDonationResultDto
        {
            OrderNo = row.OrderNo,
            Status = row.Status,
            Amount = row.Amount,
            CreatedAt = row.CreatedAt,
            PaidAt = row.PaidAt,
            ProjectSlug = row.ProjectSlug,
            ProjectName = RequestLocale.Pick(row.ProjectRequested, row.ProjectDefault) ?? string.Empty,
            StoreName = RequestLocale.Pick(row.StoreRequested, row.StoreDefault),
            DonorNameMasked = PiiMasking.MaskName(row.DonorName) ?? "○",
            DonorEmailMasked = PiiMasking.MaskEmail(row.DonorEmail) ?? "***",
            InvoiceMode = row.InvoiceMode,
            InvoiceStatus = invoiceStatus,
            InvoiceNo = row.Invoice?.IssueStatus == "issued" ? row.Invoice.InvoiceNo : null,
            Processing = row.Status == DonationStatus.Pending,
            CanRetry = row.Status is DonationStatus.Created or DonationStatus.Failed or DonationStatus.Expired,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 狀態轉移（條件式更新，只有一個請求贏）
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 付款成立：<c>pending／expired → paid</c>，並在同一個交易內寫下<b>成立當下</b>的分潤快照（規劃書 §8.4：之後調整店家或項目的分潤率
    /// 不追溯）與付款紀錄的確認時間。回傳 <c>true</c> 代表這個呼叫贏得轉移（呼叫端才可以做感謝信與開票）。
    /// 逾時的單（<c>expired</c>）在金流端仍可能晚到成功（使用者在最後一刻付款），也要收下，不能因為本站先標了逾時就吞掉一筆真實的捐款。
    /// </summary>
    internal async Task<bool> MarkPaidAsync(Guid donationId, Guid paymentId, string rawResponse)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(CancellationToken.None);

        var basis = await db.Donations.AsNoTracking()
            .Where(d => d.Id == donationId)
            .Select(d => new
            {
                d.Amount,
                StorePct = d.DonationStore == null ? 0m : d.DonationStore.StoreSharePct,
                ProjectPct = d.DonationProject.ProjectSharePct,
            })
            .SingleAsync(CancellationToken.None);

        var storePct = basis.StorePct;
        if (storePct + basis.ProjectPct > 100m)
        {
            // 錢已經收了，不能因為設定不一致就讓 CHECK 約束把這次確認打掉。壓低店家分潤使合計回到 100%，留下錯誤日誌請人檢查。
            logger.LogError("確認付款時店家與項目分潤率合計超過 100%，已壓低店家分潤以維持帳務約束（捐款 {DonationId}）", donationId);
            storePct = 100m - basis.ProjectPct;
        }

        var (storeAmount, projectAmount, associationAmount) = CharityDonationRules.ComputeSplit(basis.Amount, storePct, basis.ProjectPct);
        var now = DateTime.UtcNow;

        var moved = await db.Donations
            .Where(d => d.Id == donationId && (d.Status == DonationStatus.Pending || d.Status == DonationStatus.Expired))
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DonationStatus.Paid)
                .SetProperty(d => d.PaidAt, now)
                .SetProperty(d => d.StoreSharePctSnapshot, storePct)
                .SetProperty(d => d.ProjectSharePctSnapshot, basis.ProjectPct)
                .SetProperty(d => d.StoreAmount, storeAmount)
                .SetProperty(d => d.ProjectAmount, projectAmount)
                .SetProperty(d => d.AssociationAmount, associationAmount)
                .SetProperty(d => d.UpdatedAt, now),
                CancellationToken.None);

        if (moved == 0)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return false;
        }

        var safeRaw = JsonColumn.CoerceToObject(rawResponse); // json 欄位只收物件或陣列（docs/18 E-111）
        await db.DonationPayments
            .Where(p => p.Id == paymentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PaymentStatus.Confirmed)
                .SetProperty(p => p.ConfirmedAt, now)
                .SetProperty(p => p.RawResponse, safeRaw)
                .SetProperty(p => p.UpdatedAt, now),
                CancellationToken.None);

        await transaction.CommitAsync(CancellationToken.None);
        return true;
    }

    /// <summary>金流明確回覆沒有扣款：<c>pending → failed</c>（可重試）。付款紀錄記為 <paramref name="paymentStatus"/>（failed／cancelled）。</summary>
    private async Task MarkDeclinedAsync(Guid donationId, Guid paymentId, string paymentStatus, string? rawResponse)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(CancellationToken.None);
        var now = DateTime.UtcNow;

        var moved = await db.Donations
            .Where(d => d.Id == donationId && d.Status == DonationStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DonationStatus.Failed).SetProperty(d => d.UpdatedAt, now), CancellationToken.None);

        if (moved == 1)
        {
            var safeRaw = JsonColumn.CoerceToObject(rawResponse);
            await db.DonationPayments
                .Where(p => p.Id == paymentId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, paymentStatus)
                    .SetProperty(p => p.RawResponse, p => safeRaw ?? p.RawResponse)
                    .SetProperty(p => p.UpdatedAt, now),
                    CancellationToken.None);
        }

        await transaction.CommitAsync(CancellationToken.None);
    }

    /// <summary>Confirm 結果未知：捐款單維持（或從 <c>expired</c> 轉回）<c>pending</c>，付款紀錄標 <c>failed</c> 並在 raw_response 記下原因。
    /// 這就是「待人工處理」的定義（見類別說明）。</summary>
    private async Task MarkConfirmUnknownAsync(Guid donationId, Guid paymentId, string reason)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(CancellationToken.None);
        var now = DateTime.UtcNow;

        await db.Donations
            .Where(d => d.Id == donationId && d.Status == DonationStatus.Expired)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, DonationStatus.Pending).SetProperty(d => d.UpdatedAt, now), CancellationToken.None);

        var currentRaw = await db.DonationPayments.AsNoTracking().Where(p => p.Id == paymentId).Select(p => p.RawResponse).SingleAsync(CancellationToken.None);
        var merged = MergeConfirmError(currentRaw, reason, now);

        await db.DonationPayments
            .Where(p => p.Id == paymentId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, PaymentStatus.Failed)
                .SetProperty(p => p.RawResponse, merged)
                .SetProperty(p => p.UpdatedAt, now),
                CancellationToken.None);

        await transaction.CommitAsync(CancellationToken.None);
    }

    /// <summary>付款成立後的收尾：感謝信、第一次開立憑證。任何一步失敗都只記錄，<b>不能讓確認付款的回應變成失敗</b>。</summary>
    internal async Task RunAfterPaidAsync(Guid donationId)
    {
        try
        {
            await SendThanksAsync(donationId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "付款成立後寄送感謝信發生未預期錯誤，捐款 {DonationId}", donationId);
        }

        try
        {
            await invoices.TryIssueAsync(donationId, forceFinalFailure: false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // 留在 pending，背景工作會接手重試（不影響已確認的收款）。
            logger.LogError(ex, "付款成立後開立憑證發生未預期錯誤，捐款 {DonationId}", donationId);
        }
    }

    /// <summary>寄（或補寄）捐款感謝信（規劃書 §3.5 第 1 封）。後台「重寄感謝信」也走這裡。回傳是否寄出成功。</summary>
    internal async Task<bool> SendThanksAsync(Guid donationId, CancellationToken cancellationToken)
    {
        var donation = await db.Donations.AsNoTracking()
            .Where(d => d.Id == donationId)
            .Select(d => new
            {
                d.OrderNo,
                d.Amount,
                d.DonorName,
                d.DonorEmail,
                d.PaidAt,
                Project = d.DonationProject.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => new { i.Name, i.FundUsage }).FirstOrDefault(),
            })
            .SingleAsync(cancellationToken);

        var projectName = donation.Project?.Name ?? string.Empty;
        return await mail.SendAsync(
            EmailTemplateCodes.DonationThanks, donation.DonorEmail,
            new Dictionary<string, string>
            {
                ["donor_name"] = donation.DonorName,
                ["order_no"] = donation.OrderNo,
                ["amount"] = donation.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["project_name"] = projectName,
                ["fund_usage"] = donation.Project?.FundUsage ?? string.Empty,
                ["paid_at"] = donation.PaidAt?.AddHours(8).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ["order_details"] = CharityEmailService.BuildOrderDetails(donation.OrderNo, donation.Amount, projectName, donation.Project?.FundUsage),
            },
            cancellationToken);
    }

    /// <summary>
    /// 後台「重新確認付款結果」：針對待人工處理的單（<c>pending</c> ＋最近一次付款 <c>failed</c>＝上次 Confirm 結果未知），
    /// 用同一個交易識別碼再向金流確認一次——金流端的 Confirm 對已完成的交易是冪等的，成功就走和正常付款完全相同的收尾（入帳、感謝信、開票）。
    /// 金流仍然無法回應時，單維持待人工處理。
    /// </summary>
    public async Task<PublicDonationResultDto> RecheckPaymentAsync(string orderNo, CancellationToken cancellationToken)
    {
        var latest = await db.DonationPayments.AsNoTracking()
            .Where(p => p.Donation.OrderNo == orderNo)
            .OrderByDescending(p => p.Seq)
            .Select(p => new { p.TransactionId, p.Status })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new CharityNotFoundException("這筆捐款沒有付款紀錄。");

        if (latest.TransactionId is null)
        {
            throw new CharityConflictException("無法重新確認", "這筆付款沒有金流交易識別碼，無法重新確認。");
        }

        return await ConfirmAsync(orderNo, latest.TransactionId, lang: null, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 輸入驗證與資料建構
    // ═══════════════════════════════════════════════════════════════════════

    private sealed record BasicInput(string ProjectSlug, string DonorName, string DonorEmail);

    private static BasicInput ValidateBasics(CreateDonationRequest request)
    {
        var slug = request.ProjectSlug?.Trim();
        if (string.IsNullOrEmpty(slug) || slug.Length > 320)
        {
            throw new AdminValidationException("請選擇捐款項目。");
        }

        if (request.Amount <= 0)
        {
            throw new AdminValidationException("請輸入大於 0 的捐款金額。");
        }

        var name = request.DonorName?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new AdminValidationException("請輸入姓名。");
        }

        if (name.Length > MaxDonorNameLength)
        {
            throw new AdminValidationException($"姓名不可超過 {MaxDonorNameLength} 個字。");
        }

        var email = request.DonorEmail?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            throw new AdminValidationException("請輸入 Email。");
        }

        if (email.Length > MaxEmailLength || !IsValidEmail(email))
        {
            throw new AdminValidationException("Email 的格式不正確。");
        }

        if (!request.ConsentPrivacy)
        {
            throw new AdminValidationException("請先勾選同意個資蒐集與利用。");
        }

        return new BasicInput(slug, name, email);
    }

    private static bool IsValidEmail(string email)
        => MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('.', StringComparison.Ordinal)
           && !email.Any(char.IsWhiteSpace);

    /// <summary>驗證後的憑證輸入（已正規化，尚未加密）。</summary>
    private sealed record InvoiceValidated(
        string? CarrierType, string? CarrierPlain, string? TaxId, string? InvoiceTitle,
        string? ReceiptTitle, string? NationalIdPlain, string? Address, bool IsAnnualSummary);

    private static InvoiceValidated ValidateInvoice(DonationInvoiceInput? input, string invoiceMode, string donorName)
    {
        if (invoiceMode == InvoiceModes.B2cInvoice)
        {
            var type = input?.Type?.Trim();
            if (type is null || !CarrierTypes.All.Contains(type))
            {
                throw new AdminValidationException("請選擇發票類型（手機條碼載具、捐贈發票或統一編號）。");
            }

            switch (type)
            {
                case CarrierTypes.MobileCarrier:
                    var carrier = input!.MobileCarrier?.Trim().ToUpperInvariant();
                    if (!CharityDonationRules.IsValidMobileCarrier(carrier))
                    {
                        throw new AdminValidationException("手機條碼格式不正確（斜線開頭，共 8 碼）。");
                    }

                    return new InvoiceValidated(type, carrier, null, null, null, null, null, false);

                case CarrierTypes.LoveCode:
                    var love = input!.LoveCode?.Trim();
                    if (!CharityDonationRules.IsValidLoveCode(love))
                    {
                        throw new AdminValidationException("捐贈碼格式不正確（3 到 7 碼數字）。");
                    }

                    return new InvoiceValidated(type, love, null, null, null, null, null, false);

                default: // tax_id
                    var taxId = input!.TaxId?.Trim();
                    if (!CharityDonationRules.IsValidTaxId(taxId))
                    {
                        throw new AdminValidationException("統一編號不正確，請確認 8 碼數字。");
                    }

                    var title = input.InvoiceTitle?.Trim();
                    if (string.IsNullOrEmpty(title))
                    {
                        throw new AdminValidationException("選擇統一編號時，請輸入發票抬頭。");
                    }

                    if (title.Length > MaxTitleLength)
                    {
                        throw new AdminValidationException($"發票抬頭不可超過 {MaxTitleLength} 個字。");
                    }

                    return new InvoiceValidated(type, null, taxId, title, null, null, null, false);
            }
        }

        // donation_receipt：收據抬頭預設帶入捐款人姓名（可修改）；身分證字號與地址選填，身分證字號須通過檢核。
        var receiptTitle = input?.ReceiptTitle?.Trim();
        if (string.IsNullOrEmpty(receiptTitle))
        {
            receiptTitle = donorName;
        }

        if (receiptTitle.Length > MaxTitleLength)
        {
            throw new AdminValidationException($"收據抬頭不可超過 {MaxTitleLength} 個字。");
        }

        var nationalId = input?.NationalId?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(nationalId))
        {
            nationalId = null;
        }
        else if (!CharityDonationRules.IsValidNationalId(nationalId))
        {
            throw new AdminValidationException("身分證字號格式不正確；此欄位為選填，不需要列舉扣除時可以留空。");
        }

        var address = input?.Address?.Trim();
        if (string.IsNullOrEmpty(address))
        {
            address = null;
        }
        else if (address.Length > MaxAddressLength)
        {
            throw new AdminValidationException($"通訊地址不可超過 {MaxAddressLength} 個字。");
        }

        return new InvoiceValidated(null, null, null, null, receiptTitle, nationalId, address, input?.IsAnnualSummary ?? false);
    }

    private DonationInvoice BuildInvoice(Guid donationId, string invoiceMode, InvoiceValidated v, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        DonationId = donationId,
        InvoiceType = invoiceMode,
        CarrierType = v.CarrierType,
        // 手機條碼與捐贈碼、身分證字號一律加密儲存（docs/16 §4.4）；統編與抬頭、地址是開立憑證本來就會印出的資料。
        CarrierIdEncrypted = v.CarrierPlain is null ? null : protector.EncryptCarrierId(v.CarrierPlain),
        TaxId = v.TaxId,
        InvoiceTitle = v.InvoiceTitle,
        ReceiptTitle = v.ReceiptTitle,
        NationalIdEncrypted = v.NationalIdPlain is null ? null : protector.EncryptNationalId(v.NationalIdPlain),
        ReceiptAddress = v.Address,
        IsAnnualSummary = v.IsAnnualSummary,
        IssueStatus = "pending",
        VoidStatus = "none",
        UpdatedAt = now,
    };

    private async Task<ExistingDonation?> LoadForIdempotencyAsync(string orderNo, CancellationToken cancellationToken)
        => await db.Donations.AsNoTracking()
            .Where(d => d.OrderNo == orderNo)
            .Select(d => new ExistingDonation(d.OrderNo, d.Status, d.Amount, d.DonationProject.ProjectSlug, d.DonorEmail))
            .SingleOrDefaultAsync(cancellationToken);

    private sealed record ExistingDonation(string OrderNo, string Status, int Amount, string ProjectSlug, string DonorEmail);

    private static CreateDonationResponse ReplayOrConflict(ExistingDonation existing, CreateDonationRequest request)
    {
        var same = existing.Amount == request.Amount
                   && string.Equals(existing.ProjectSlug, request.ProjectSlug?.Trim(), StringComparison.Ordinal)
                   && string.Equals(existing.DonorEmail, request.DonorEmail?.Trim(), StringComparison.OrdinalIgnoreCase);
        if (!same)
        {
            throw new CharityConflictException("冪等鍵已被使用", "這個冪等鍵已經用於另一筆內容不同的捐款，請重新整理頁面後再送出。");
        }

        return new CreateDonationResponse { OrderNo = existing.OrderNo, Status = existing.Status, Amount = existing.Amount, Created = false };
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static string? TryReadPaymentUrl(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            return doc.RootElement.TryGetProperty("paymentUrl", out var url) && url.ValueKind == JsonValueKind.String ? url.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string MergeConfirmError(string? currentRaw, string reason, DateTime nowUtc)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(currentRaw ?? "{}") as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            root = new JsonObject();
        }

        root["confirmError"] = new JsonObject { ["at"] = nowUtc.ToString("O"), ["message"] = reason.Length > 300 ? reason[..300] : reason };
        return root.ToJsonString();
    }
}
