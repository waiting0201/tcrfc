using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminShop;
using Tcrfc.Api.Features.Email;
using Tcrfc.Api.Features.MembershipPayments;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Shop;

/// <summary>誰在操作這張訂單：會員本人（權杖）或持有訂單權杖的訪客（<c>X-Order-Token</c>）。</summary>
public sealed record OrderAccess(Guid? MemberId, string? OrderToken);

/// <summary>
/// 站內商店結帳與訂單（主站規劃書 §3.8 8.3「結帳流程／付款方式／發票／訂單查詢」、§4.13）：結帳建立訂單 → 付款（LINE Pay 請款）→ 確認 → 開立發票 → 通知信；訂單查詢。
/// 🔴 硬規則（同會籍付款訂單，App 規劃書 §5.4）：① <b>金額由伺服器依購物車重算</b>（請求本文沒有任何金額欄位）；② <b>付款成立以伺服器端向金流方確認的結果為準</b>，不接受用戶端回報；
/// ③ <b>冪等</b>：同一冪等鍵重送只成立一張訂單、同一張訂單重複確認不重複扣庫存、不重複開發票、不重複寄信（付款成立走 <see cref="ShopOrderLifecycle.ConfirmPaymentAsync"/>，帶前置狀態的單句更新）；
/// ④ <b>防超賣</b>：下單即「保留」庫存（<see cref="InventoryService"/>，交易內對規格列加更新鎖、按規格 id 排序取鎖避免死結）；付款成立才扣減，付款失敗／逾時釋回。
/// 🔴 <b>訂單、庫存、購物車、付款狀態一律直接查庫、不讀快取</b>（docs/14 五類）：本類別不注入快取服務。
/// 🔴 <b>不混買、不拆單</b>：購物車以 <c>club_id</c> 區隔，一張訂單只有一個賣方俱樂部（<c>selling_club_id</c>＝<c>club_id</c>）；<c>collecting_club_id</c> ＝ 收款主體俱樂部。
/// 「訂單是否於結帳時依俱樂部拆單」未定案（STATUS B-8）——現行禁止混買所以不會發生，<b>本類別不實作拆單</b>。
/// 逾時採「讀到時換算＋背景作業清掃」：待付款超過 S6「待付款保留時間」（預設 30 分鐘）→ 自動取消並釋回庫存。
/// </summary>
public sealed partial class ShopOrderService(
    ClubDbContext db, ShopCartService carts, InventoryService inventory, ShopOrderLifecycle lifecycle, ShopSettingsReader settings, ClubTextSettings texts,
    IPaymentGateway gateway, ShopInvoiceService invoices, IEmailSender email, IConfiguration configuration, ILogger<ShopOrderService> logger)
{
    public const string OrderTokenHeader = "X-Order-Token";

    /// <summary>訪客查詢權杖（含信件連結）的有效天數，自訂單成立起算；過期後仍可用「訂單編號＋Email」查詢。</summary>
    public static readonly TimeSpan LookupTokenLifetime = TimeSpan.FromDays(30);

    private const int MaxPlacementAttempts = 5;

    [GeneratedRegex(@"^[A-Za-z0-9_\-:.]{8,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdempotencyKeyShape();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailShape();

    [GeneratedRegex(@"^[0-9+\-() #]{6,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneShape();

    [GeneratedRegex(@"^/[0-9A-Z.+\-]{7}$", RegexOptions.CultureInvariant)]
    private static partial Regex MobileBarcodeShape();

    [GeneratedRegex(@"^[A-Z]{2}[0-9]{14}$", RegexOptions.CultureInvariant)]
    private static partial Regex CitizenCertShape();

    [GeneratedRegex(@"^[0-9]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaxIdShape();

    private static readonly HashSet<string> DeliveryMethods = new(StringComparer.Ordinal) { "home_delivery", "cvs_pickup", "onsite_pickup" };

    private sealed record ValidatedCheckout(
        string Email, string Name, string? Phone, string DeliveryMethod, string? Address, string? Note,
        string InvoiceKind, string? CarrierType, string? CarrierId, string? TaxId, string? DonationCode, string Lang);

    // ═══════════════════════════ 結帳 ═══════════════════════════

    public async Task<(ShopOrderDto Order, bool Created)> CheckoutAsync(
        ClubScope scope, CartOwner? owner, MemberIdentity? me, string? idempotencyKey, CheckoutRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || !IdempotencyKeyShape().IsMatch(idempotencyKey))
        {
            throw new MemberValidationException("請在標頭帶 Idempotency-Key（8 到 64 個英數字元，可含 - _ : .）。", "idempotency_key_required");
        }

        if (owner is null)
        {
            throw new MemberConflictException("購物車是空的", "找不到你的購物車，請先把商品加入購物車。", "cart_empty");
        }

        var lang = string.Equals(request.Lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
        var dbLocale = lang == "en" ? "en" : RequestLocale.DefaultDbLocale;
        var memberRow = me is null ? null : await db.Members.AsNoTracking().Where(m => m.Id == me.MemberId).Select(m => new { m.Email, m.Name, m.Phone }).FirstOrDefaultAsync(cancellationToken);
        var input = await ValidateAsync(scope, request, memberRow?.Email, memberRow?.Name, memberRow?.Phone, lang, cancellationToken);
        var fingerprint = Fingerprint(owner, input);

        // 冪等：同一冪等鍵 → 回原訂單（擁有者與內容必須相同，否則是用戶端的錯）。購物車此時可能已清空，所以先於購物車檢查。
        var replay = await FindByIdempotencyKeyAsync(scope, idempotencyKey, fingerprint, lang, cancellationToken);
        if (replay is not null)
        {
            return (replay, false);
        }

        var (cart, lines) = await carts.LoadLinesAsync(scope, owner, dbLocale, cancellationToken);
        if (cart is null || lines.Count == 0)
        {
            // 並行的同一冪等鍵：快的請求可能在上面「查冪等鍵」之後、讀購物車之前成立訂單並清空購物車（E-172 同類，加固）。
            var raced = await FindByIdempotencyKeyAsync(scope, idempotencyKey, fingerprint, lang, cancellationToken);
            if (raced is not null)
            {
                return (raced, false);
            }

            throw new MemberConflictException("購物車是空的", "購物車是空的，請先把商品加入購物車。", "cart_empty");
        }

        var bad = lines.FirstOrDefault(l => l.Issue == "unavailable");
        if (bad is not null)
        {
            throw new MemberConflictException("商品已下架", $"「{bad.ProductName}」已下架或停售，請從購物車移除後再結帳。", "item_unavailable");
        }

        var collecting = await db.Clubs.AsNoTracking().Where(c => c.IsCollectingSubject).OrderBy(c => c.SortOrder).Select(c => c.Id).FirstOrDefaultAsync(cancellationToken);
        if (collecting == Guid.Empty)
        {
            throw new FeatureNotConfiguredException("商店尚未開放結帳。", "collecting_subject_missing");
        }

        // 運費由伺服器依小計與配送方式重算；金額一律用「現在的價格」。
        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        var shippingFee = ShopSettingsReader.ComputeShippingFee(await settings.GetShippingAsync(scope.ClubId, cancellationToken), input.DeliveryMethod, subtotal);
        var encryptedCarrier = input.CarrierId is null ? null : invoices.ProtectCarrier(input.CarrierId);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var orderId = await PlaceOrderAsync(
                    scope, owner, me?.MemberId, cart.Id, lines, input, encryptedCarrier, subtotal, shippingFee, collecting, idempotencyKey, fingerprint, cancellationToken);
                var order = await LoadAsync(orderId, cancellationToken);
                await SendOrderCreatedAsync(scope, order, input, cancellationToken);
                return (await ToDtoAsync(scope, order, mask: false, includeToken: me is null, lang, cancellationToken), true);
            }
            catch (InsufficientStockException ex)
            {
                db.ChangeTracker.Clear();
                // 庫存被「逾時未付款」的訂單占著 → 先掃一輪釋回再試一次；仍不足才回報。
                var released = attempt == 1
                    ? await lifecycle.ExpireStalePendingAsync(scope.ClubId, TimeSpan.FromMinutes(await settings.GetPendingTimeoutMinutesAsync(scope.ClubId, cancellationToken)), null, cancellationToken)
                    : 0;
                if (released > 0)
                {
                    db.ChangeTracker.Clear();
                    continue;
                }

                var line = lines.FirstOrDefault(l => string.Equals(l.Sku, ex.Sku, StringComparison.Ordinal));
                var label = line is null ? ex.Sku : $"{line.ProductName}（{line.VariantLabel}）";
                throw new MemberConflictException("庫存不足", ex.Available <= 0 ? $"「{label}」剛剛已經售完了。" : $"「{label}」目前只剩 {ex.Available} 件，請調整數量。", "insufficient_stock");
            }
            catch (DbUpdateException ex) when (ShopCartService.IsUniqueViolation(ex))
            {
                db.ChangeTracker.Clear();
                // 並行的同一冪等鍵：回先成立的那張；否則是訂單編號撞號，換一個重試。
                var raced = await FindByIdempotencyKeyAsync(scope, idempotencyKey, fingerprint, lang, cancellationToken);
                if (raced is not null)
                {
                    return (raced, false);
                }

                if (attempt >= MaxPlacementAttempts)
                {
                    throw;
                }
            }
        }
    }

    private async Task<Guid> PlaceOrderAsync(
        ClubScope scope, CartOwner owner, Guid? memberId, Guid cartId, IReadOnlyList<ShopCartService.CartLine> lines, ValidatedCheckout input, string? encryptedCarrier,
        int subtotal, int shippingFee, Guid collectingClubId, string idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = orderId, OrderNo = RegistrationNumberGenerator.Generate(ShopLabels.OrderPrefix(scope.ClubCode), now.AddHours(8)), ClubId = scope.ClubId,
            SellingClubId = scope.ClubId, CollectingClubId = collectingClubId, MemberId = memberId, LookupToken = SecureToken.Generate(),
            RecipientName = input.Name, RecipientPhone = input.Phone, RecipientAddress = input.Address, BuyerEmail = input.Email,
            Subtotal = subtotal, ShippingFee = shippingFee, Total = subtotal + shippingFee, PaymentStatus = "pending", PaymentMethod = "linepay",
            OrderStatus = ShopLabels.Pending, DeliveryMethod = input.DeliveryMethod, IsManual = false, CustomerNote = input.Note, SettlementStatus = "pending",
            IdempotencyKey = idempotencyKey, RequestFingerprint = fingerprint, CreatedAt = now, UpdatedAt = now,
        };
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Orders.Add(order);
        foreach (var l in lines)
        {
            db.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(), ClubId = scope.ClubId, OrderId = orderId, ProductVariantId = l.VariantId, ProductNameSnapshot = l.ProductName ?? l.ProductSlug,
                VariantLabelSnapshot = string.IsNullOrEmpty(l.VariantLabel) ? null : l.VariantLabel, SkuSnapshot = l.Sku, UnitPriceSnapshot = l.UnitPrice, Quantity = l.Quantity,
                LineTotal = l.UnitPrice * l.Quantity, CreatedAt = now, UpdatedAt = now,
            });
        }

        db.StoreInvoices.Add(new StoreInvoice
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, OrderId = orderId, CarrierType = input.CarrierType, CarrierIdEncrypted = encryptedCarrier, TaxId = input.TaxId,
            DonationCode = input.DonationCode, IssueStatus = "pending", VoidStatus = "none", CreatedAt = now, UpdatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        // 按規格 id 排序取鎖：兩張同時結帳的訂單以相同順序鎖定規格列，不會互相死結；超賣由「保留量 ≤ 庫存量」的檢查擋下。
        foreach (var l in lines.OrderBy(l => l.VariantId))
        {
            await inventory.ApplyAsync(new InventoryChange(scope.ClubId, l.VariantId, "reserve", 0, l.Quantity, "前台下單保留", orderId, null), cancellationToken);
        }

        // 同一個交易內清掉購物車：訂單成立與購物車清空要嘛一起成立、要嘛都不成立。
        await db.CartItems.Where(i => i.CartId == cartId).ExecuteDeleteAsync(cancellationToken);
        await db.Carts.Where(c => c.Id == cartId).ExecuteDeleteAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return orderId;
    }

    private async Task<ShopOrderDto?> FindByIdempotencyKeyAsync(ClubScope scope, string key, string fingerprint, string lang, CancellationToken cancellationToken)
    {
        var existingId = await db.Orders.AsNoTracking().Where(o => o.ClubId == scope.ClubId && o.IdempotencyKey == key)
            .Select(o => new { o.Id, o.RequestFingerprint, o.MemberId }).FirstOrDefaultAsync(cancellationToken);
        if (existingId is null)
        {
            return null;
        }

        if (!string.Equals(existingId.RequestFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new MemberConflictException("冪等鍵已使用", "這個冪等鍵已經用在另一張不同內容的訂單上，請產生新的冪等鍵。", "idempotency_key_reused");
        }

        var order = await ExpireIfNeededAsync(await LoadAsync(existingId.Id, cancellationToken), cancellationToken);
        return await ToDtoAsync(scope, order, mask: false, includeToken: existingId.MemberId is null, lang, cancellationToken);
    }

    // ═══════════════════════════ 付款 ═══════════════════════════

    public async Task<ShopOrderDto> PayAsync(ClubScope scope, OrderAccess access, string orderNo, string lang, CancellationToken cancellationToken)
    {
        var order = await ExpireIfNeededAsync(await LoadOwnedAsync(scope, access, orderNo, cancellationToken), cancellationToken);
        if (order.PaymentStatus != "pending" || order.OrderStatus != ShopLabels.Pending)
        {
            throw new MemberConflictException("訂單無法付款", $"這張訂單目前是「{StatusLabel(order.OrderStatus, false)}」，無法付款。", "order_not_payable");
        }

        if (!string.IsNullOrEmpty(order.PaymentUrl) && !string.IsNullOrEmpty(order.LinepayTransactionId))
        {
            return await ToDtoAsync(scope, order, false, false, lang, cancellationToken); // 冪等：已請過款且尚未逾時，回同一個付款網址
        }

        if (!gateway.IsConfigured)
        {
            throw new FeatureNotConfiguredException("線上付款尚未啟用，請稍後再試或聯繫客服。", "payment_not_configured");
        }

        // 先搶佔，避免並行的兩次請款各自向金流方開一筆交易。
        var claimed = await db.Orders.Where(o => o.Id == order.Id && o.PaymentStatus == "pending" && o.LinepayTransactionId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.LinepayTransactionId, "RESERVING").SetProperty(o => o.UpdatedAt, DateTime.UtcNow), cancellationToken);
        if (claimed == 0)
        {
            var again = await LoadAsync(order.Id, cancellationToken);
            if (!string.IsNullOrEmpty(again.PaymentUrl))
            {
                return await ToDtoAsync(scope, again, false, false, lang, cancellationToken);
            }

            throw new MemberConflictException("付款處理中", "這張訂單正在建立付款，請稍後重新整理。", "payment_in_progress");
        }

        try
        {
            var first = order.OrderItems.OrderBy(i => i.CreatedAt).ThenBy(i => i.RowSeq).FirstOrDefault();
            var name = first is null ? order.OrderNo : $"{first.ProductNameSnapshot}{(order.OrderItems.Count > 1 ? " 等" : "")}";
            var reservation = await gateway.ReserveAsync(new PaymentReserveRequest(order.OrderNo, order.Total, name), cancellationToken);
            await db.Orders.Where(o => o.Id == order.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.LinepayTransactionId, reservation.TransactionId).SetProperty(o => o.PaymentUrl, reservation.PaymentUrl)
                    .SetProperty(o => o.UpdatedAt, DateTime.UtcNow), cancellationToken);
        }
        catch
        {
            // 金流方請款失敗：放掉搶佔，使用者可以重試，不卡死。
            await db.Orders.Where(o => o.Id == order.Id && o.LinepayTransactionId == "RESERVING")
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.LinepayTransactionId, (string?)null), CancellationToken.None);
            throw;
        }

        return await ToDtoAsync(scope, await LoadAsync(order.Id, cancellationToken), false, false, lang, cancellationToken);
    }

    public async Task<ShopOrderDto> ConfirmAsync(
        ClubScope scope, OrderAccess access, string orderNo, string? transactionId, string lang, CancellationToken cancellationToken)
    {
        var order = await ExpireIfNeededAsync(await LoadOwnedAsync(scope, access, orderNo, cancellationToken), cancellationToken);
        if (order.PaymentStatus == "paid" || order.PaymentStatus == "refunded")
        {
            return await ToDtoAsync(scope, order, false, false, lang, cancellationToken); // 冪等：使用者重新整理回呼頁
        }

        if (order.PaymentStatus is "expired" or "failed" || order.OrderStatus == ShopLabels.Cancelled)
        {
            throw new MemberConflictException("訂單已逾時", "這張訂單已經逾時或取消，請重新下單。", "order_expired");
        }

        if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrEmpty(order.LinepayTransactionId) || order.LinepayTransactionId == "RESERVING"
            || !string.Equals(order.LinepayTransactionId, transactionId, StringComparison.Ordinal))
        {
            throw new MemberValidationException("付款交易識別不符。", "transaction_mismatch");
        }

        // 金額一律用訂單上伺服器算好的值，不是用戶端傳來的。
        var confirmation = await gateway.ConfirmAsync(transactionId, order.OrderNo, order.Total, cancellationToken);
        if (!confirmation.Success)
        {
            logger.LogWarning("商店訂單 {OrderNo} 付款確認失敗：{Reason}", order.OrderNo, confirmation.FailureReason);
            throw new MemberConflictException("付款未完成", "付款尚未完成，請回到付款頁重新操作；若已扣款請聯繫客服，不需要重複付款。", "payment_failed");
        }

        PaymentConfirmResult result;
        try
        {
            result = await lifecycle.ConfirmPaymentAsync(order.Id, transactionId, null, cancellationToken);
        }
        catch (AdminConflictException)
        {
            // 金流方已確認，但訂單剛好被逾時清掃取消：款項已收、訂單已取消，必須人工退款。
            logger.LogError("🔴 商店訂單 {OrderNo} 金流已確認但訂單狀態已改變（逾時取消），需人工退款。", order.OrderNo);
            throw new MemberConflictException("訂單狀態已改變", "付款已完成，但訂單剛好逾時取消，請聯繫客服協助處理，不需要重複付款。", "order_state_changed");
        }

        db.ChangeTracker.Clear();
        if (result == PaymentConfirmResult.Confirmed)
        {
            // 只有「第一個」確認成功的請求會走到這裡（單句條件更新）：開發票與寄信恰好一次。
            await db.Orders.Where(o => o.Id == order.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.PaymentUrl, (string?)null), cancellationToken);
            var invoiceNo = await invoices.TryIssueAsync(order.Id, cancellationToken);
            await SendPaymentCompletedAsync(scope, order, invoiceNo, lang, cancellationToken);
        }

        return await ToDtoAsync(scope, await LoadAsync(order.Id, cancellationToken), false, false, lang, cancellationToken);
    }

    public async Task<ShopOrderDto> CancelAsync(ClubScope scope, OrderAccess access, string orderNo, string lang, CancellationToken cancellationToken)
    {
        var order = await ExpireIfNeededAsync(await LoadOwnedAsync(scope, access, orderNo, cancellationToken), cancellationToken);
        if (order.OrderStatus == ShopLabels.Cancelled)
        {
            return await ToDtoAsync(scope, order, false, false, lang, cancellationToken);
        }

        if (order.PaymentStatus != "pending" || order.OrderStatus != ShopLabels.Pending)
        {
            throw new MemberConflictException("訂單無法取消", "這張訂單已付款或已進入出貨流程，請聯繫客服申請退換貨。", "order_not_cancellable");
        }

        await lifecycle.ExpireAsync(order.Id, "買家取消付款", "failed", null, cancellationToken);
        db.ChangeTracker.Clear();
        return await ToDtoAsync(scope, await LoadAsync(order.Id, cancellationToken), false, false, lang, cancellationToken);
    }

    // ═══════════════════════════ 查詢 ═══════════════════════════

    public async Task<ShopOrderDto> GetAsync(ClubScope scope, OrderAccess access, string orderNo, string lang, CancellationToken cancellationToken)
    {
        var order = await ExpireIfNeededAsync(await LoadOwnedAsync(scope, access, orderNo, cancellationToken), cancellationToken);
        return await ToDtoAsync(scope, order, mask: false, includeToken: false, lang, cancellationToken);
    }

    public async Task<IReadOnlyList<ShopOrderListItemDto>> ListMineAsync(ClubScope scope, Guid memberId, string lang, CancellationToken cancellationToken)
    {
        var en = lang == "en";
        var rows = await db.Orders.AsNoTracking().Include(o => o.OrderItems).Include(o => o.Shipment).Include(o => o.StoreInvoices)
            .Where(o => o.ClubId == scope.ClubId && o.MemberId == memberId).OrderByDescending(o => o.RowSeq).Take(50).AsSplitQuery().ToListAsync(cancellationToken);
        return rows.Select(o => new ShopOrderListItemDto
        {
            OrderNo = o.OrderNo, Status = StatusLabel(o.OrderStatus, en), PaymentStatusLabel = ShopLabelsPublic.PaymentStatusLabel(o.PaymentStatus, en), Total = o.Total,
            ItemCount = o.OrderItems.Sum(i => i.Quantity), FirstItemName = o.OrderItems.OrderBy(i => i.RowSeq).Select(i => i.ProductNameSnapshot).FirstOrDefault(),
            InvoiceNo = o.StoreInvoices.OrderByDescending(i => i.CreatedAt).Select(i => i.InvoiceNo).FirstOrDefault(), TrackingNo = o.Shipment?.TrackingNo, CreatedAt = o.CreatedAt,
        }).ToList();
    }

    /// <summary>訪客查詢（前台 <c>/order/lookup</c>）：「訂單編號＋Email」或信件連結的權杖。<b>找不到、Email 不符、權杖過期一律同一個 404</b>，不洩漏訂單是否存在；以 Email 查到的收件資料是遮罩值。</summary>
    public async Task<ShopOrderDto> LookupAsync(ClubScope scope, LookupShopOrderRequest request, string lang, CancellationToken cancellationToken)
    {
        var notFound = new MemberNotFoundException("找不到符合的訂單，請確認訂單編號與下單時填寫的 Email。", "order_not_found");
        Order? order;
        var mask = true;
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            var token = request.Token.Trim();
            if (token.Length > 128)
            {
                throw notFound;
            }

            var byToken = await db.Orders.AsNoTracking().Where(o => o.ClubId == scope.ClubId && o.LookupToken == token).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(cancellationToken);
            order = byToken is Guid tokenId ? await LoadAsync(tokenId, cancellationToken) : null;
            if (order is null || DateTime.UtcNow > order.CreatedAt + LookupTokenLifetime)
            {
                throw notFound;
            }

            mask = false;
        }
        else
        {
            var orderNo = request.OrderNo?.Trim().ToUpperInvariant();
            var emailIn = request.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(orderNo) || string.IsNullOrEmpty(emailIn) || orderNo.Length > 32 || emailIn.Length > 255)
            {
                throw new MemberValidationException("請輸入訂單編號與下單時填寫的 Email。", "lookup_fields_required");
            }

            var byNo = await db.Orders.AsNoTracking().Where(o => o.ClubId == scope.ClubId && o.OrderNo == orderNo).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(cancellationToken);
            order = byNo is Guid noId ? await LoadAsync(noId, cancellationToken) : null;
            if (order is null || string.IsNullOrEmpty(order.BuyerEmail)
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(order.BuyerEmail.ToLowerInvariant()), Encoding.UTF8.GetBytes(emailIn)))
            {
                throw notFound;
            }
        }

        order = await ExpireIfNeededAsync(order, cancellationToken);
        return await ToDtoAsync(scope, order, mask, includeToken: false, lang, cancellationToken);
    }

    // ═══════════════════════════ 內部 ═══════════════════════════

    private async Task<Order> LoadAsync(Guid id, CancellationToken cancellationToken)
        => await db.Orders.AsNoTracking().Include(o => o.OrderItems).Include(o => o.Shipment).Include(o => o.StoreInvoices).AsSplitQuery()
               .FirstOrDefaultAsync(o => o.Id == id, cancellationToken) ?? throw new MemberNotFoundException("找不到這張訂單。", "order_not_found");

    /// <summary>依擁有者載入訂單：會員本人，或持有（未過期的）訂單權杖的訪客。其餘一律 404（含別俱樂部的訂單）。</summary>
    private async Task<Order> LoadOwnedAsync(ClubScope scope, OrderAccess access, string orderNo, CancellationToken cancellationToken)
    {
        var normalized = orderNo.Trim().ToUpperInvariant();
        var id = await db.Orders.AsNoTracking().Where(o => o.ClubId == scope.ClubId && o.OrderNo == normalized).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(cancellationToken);
        var order = id is Guid g ? await LoadAsync(g, cancellationToken) : null;
        var owned = order is not null && (
            (access.MemberId is Guid m && order.MemberId == m)
            || (!string.IsNullOrEmpty(access.OrderToken) && DateTime.UtcNow <= order.CreatedAt + LookupTokenLifetime
                && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(order.LookupToken), Encoding.UTF8.GetBytes(access.OrderToken))));
        return owned ? order! : throw new MemberNotFoundException("找不到這張訂單。", "order_not_found");
    }

    private async Task<Order> ExpireIfNeededAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.PaymentStatus != "pending" || order.OrderStatus != ShopLabels.Pending)
        {
            return order;
        }

        var timeout = await settings.GetPendingTimeoutMinutesAsync(order.ClubId, cancellationToken);
        if (order.CreatedAt.AddMinutes(timeout) > DateTime.UtcNow)
        {
            return order;
        }

        await lifecycle.ExpireAsync(order.Id, "付款逾時，自動釋回庫存", "expired", null, cancellationToken);
        db.ChangeTracker.Clear();
        return await LoadAsync(order.Id, cancellationToken);
    }

    private async Task<ShopOrderDto> ToDtoAsync(ClubScope scope, Order o, bool mask, bool includeToken, string lang, CancellationToken cancellationToken)
    {
        var en = lang == "en";
        var pending = o.PaymentStatus == "pending" && o.OrderStatus == ShopLabels.Pending;
        DateTime? expiresAt = null;
        if (pending)
        {
            expiresAt = o.CreatedAt.AddMinutes(await settings.GetPendingTimeoutMinutesAsync(o.ClubId, cancellationToken));
        }

        var invoice = o.StoreInvoices.OrderByDescending(i => i.CreatedAt).FirstOrDefault();
        ShopOrderInvoiceDto? invoiceDto = null;
        if (invoice is not null)
        {
            var type = invoice.CarrierType ?? (invoice.TaxId is not null ? "tax_id" : invoice.DonationCode is not null ? "donation" : "mobile_barcode");
            invoiceDto = new ShopOrderInvoiceDto
            {
                Type = type, TypeLabel = ShopLabelsPublic.InvoiceTypeLabel(type, en), Status = invoice.IssueStatus == "issued" ? "issued" : "pending",
                StatusLabel = ShopLabelsPublic.InvoiceStatusLabel(invoice.IssueStatus, en), InvoiceNo = invoice.InvoiceNo, IssuedAt = invoice.IssuedAt,
                TaxId = invoice.TaxId, DonationCode = invoice.DonationCode,
            };
        }

        ShopOrderShipmentDto? shipment = null;
        if (o.Shipment is { } s)
        {
            var pickup = s.PickupStatus;
            if (pickup == "waiting" && s.PickupDeadlineOn is DateOnly deadline && deadline < TaiwanClock.Today)
            {
                pickup = "overdue";
            }

            shipment = new ShopOrderShipmentDto
            {
                Carrier = s.Carrier, TrackingNo = s.TrackingNo, ShippedAt = s.ShippedAt, DeliveredAt = s.DeliveredAt, PickupStatus = pickup,
                PickupStatusLabel = pickup is null ? null : ShopLabelsPublic.PickupLabel(pickup, en), PickupDeadlineOn = s.PickupDeadlineOn,
            };
        }

        return new ShopOrderDto
        {
            OrderNo = o.OrderNo, ClubCode = scope.ClubCode, AccessToken = includeToken ? o.LookupToken : null, Status = StatusLabel(o.OrderStatus, en),
            PaymentStatus = o.PaymentStatus, PaymentStatusLabel = ShopLabelsPublic.PaymentStatusLabel(o.PaymentStatus, en), PaymentMethod = o.PaymentMethod,
            PaymentMethodLabel = ShopLabelsPublic.PaymentMethodLabel(o.PaymentMethod, en), DeliveryMethod = o.DeliveryMethod ?? "home_delivery",
            DeliveryMethodLabel = ShopLabelsPublic.DeliveryLabel(o.DeliveryMethod ?? "home_delivery", en), Subtotal = o.Subtotal, ShippingFee = o.ShippingFee, Total = o.Total,
            Items = o.OrderItems.OrderBy(i => i.RowSeq).Select(i => new ShopOrderItemDto
            {
                ProductName = i.ProductNameSnapshot, VariantLabel = i.VariantLabelSnapshot, Sku = i.SkuSnapshot, UnitPrice = i.UnitPriceSnapshot, Quantity = i.Quantity, LineTotal = i.LineTotal,
            }).ToList(),
            RecipientName = mask ? PiiMasking.MaskName(o.RecipientName) : o.RecipientName, RecipientPhone = mask ? PiiMasking.MaskPhone(o.RecipientPhone) : o.RecipientPhone,
            RecipientAddress = mask ? PiiMasking.MaskAddress(o.RecipientAddress) : o.RecipientAddress, BuyerEmail = mask ? PiiMasking.MaskEmail(o.BuyerEmail) : o.BuyerEmail,
            CustomerNote = mask ? null : o.CustomerNote, IsMasked = mask, Invoice = invoiceDto, Shipment = shipment, PaymentUrl = pending ? o.PaymentUrl : null, ExpiresAt = expiresAt,
            CanPay = pending && gateway.IsConfigured, CanCancel = pending, CreatedAt = o.CreatedAt, PaidAt = o.PaidAt,
        };
    }

    public static string StatusLabel(string zhStatus, bool en)
        => !en ? zhStatus : zhStatus switch
        {
            "待付款" => "Awaiting payment",
            "已付款" => "Paid",
            "備貨中" => "Preparing",
            "已出貨" => "Shipped",
            "已完成" => "Completed",
            "已取消" => "Cancelled",
            "退貨處理中" => "Return in progress",
            "已退款" => "Refunded",
            _ => zhStatus,
        };

    // ═══════════════════════════ 驗證與指紋 ═══════════════════════════

    private async Task<ValidatedCheckout> ValidateAsync(
        ClubScope scope, CheckoutRequest r, string? memberEmail, string? memberName, string? memberPhone, string lang, CancellationToken cancellationToken)
    {
        var email = (string.IsNullOrWhiteSpace(r.Email) ? memberEmail : r.Email)?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email) || email.Length > 255 || !EmailShape().IsMatch(email))
        {
            throw new MemberValidationException("請填寫正確的 Email（訂單成立信會寄到這裡）。", "invalid_email");
        }

        var name = (string.IsNullOrWhiteSpace(r.RecipientName) ? memberName : r.RecipientName)?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 64)
        {
            throw new MemberValidationException("請填寫收件人姓名（最多 64 個字）。", "invalid_recipient_name");
        }

        var delivery = r.DeliveryMethod?.Trim();
        if (delivery is null || !DeliveryMethods.Contains(delivery))
        {
            throw new MemberValidationException("請選擇配送方式：宅配、超商取貨或現場自取。", "invalid_delivery_method");
        }

        var phone = (string.IsNullOrWhiteSpace(r.RecipientPhone) ? memberPhone : r.RecipientPhone)?.Trim();
        if (string.IsNullOrEmpty(phone) || !PhoneShape().IsMatch(phone))
        {
            throw new MemberValidationException("請填寫正確的聯絡電話。", "invalid_phone");
        }

        string? address = null;
        if (delivery == "home_delivery")
        {
            address = r.RecipientAddress?.Trim();
            if (string.IsNullOrEmpty(address) || address.Length > 500)
            {
                throw new MemberValidationException("宅配請填寫完整收件地址（最多 500 個字）。", "invalid_address");
            }

            var map = await texts.LoadAsync(scope.ClubId, [ShopSettingKeys.ExcludedRegions], cancellationToken);
            if (ClubTextSettings.GetValue(map, ShopSettingKeys.ExcludedRegions) is { Length: > 0 } raw)
            {
                var excluded = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw) ?? [];
                var hit = excluded.FirstOrDefault(region => address.Contains(region, StringComparison.Ordinal));
                if (hit is not null)
                {
                    throw new MemberValidationException($"很抱歉，目前不配送到「{hit}」，請改用超商取貨或現場自取。", "region_not_deliverable");
                }
            }
        }
        else if (delivery == "cvs_pickup")
        {
            var store = r.PickupStore?.Trim();
            if (string.IsNullOrEmpty(store) || store.Length > 200)
            {
                throw new MemberValidationException("超商取貨請填寫取貨門市名稱或代碼。", "invalid_pickup_store");
            }

            address = $"超商取貨門市：{store}";
        }

        var note = string.IsNullOrWhiteSpace(r.CustomerNote) ? null : r.CustomerNote.Trim();
        if (note is { Length: > 500 })
        {
            throw new MemberValidationException("備註最多 500 個字。", "invalid_note");
        }

        // 發票：必開電子發票，載具／統編／捐贈碼三選一。
        var inv = r.Invoice ?? throw new MemberValidationException("請選擇電子發票開立方式（載具、統一編號或捐贈碼擇一）。", "invoice_required");
        string kind = inv.Type?.Trim() ?? string.Empty;
        string? carrierType = null, carrierId = null, taxId = null, donation = null;
        switch (kind)
        {
            case "mobile_barcode":
                carrierId = inv.CarrierId?.Trim().ToUpperInvariant();
                if (carrierId is null || !MobileBarcodeShape().IsMatch(carrierId))
                {
                    throw new MemberValidationException("手機條碼載具格式不正確（斜線開頭共 8 碼，例如 /ABC+123）。", "invalid_carrier");
                }

                carrierType = kind;
                break;
            case "citizen_cert":
                carrierId = inv.CarrierId?.Trim().ToUpperInvariant();
                if (carrierId is null || !CitizenCertShape().IsMatch(carrierId))
                {
                    throw new MemberValidationException("自然人憑證載具格式不正確（2 個英文字母加 14 位數字）。", "invalid_carrier");
                }

                carrierType = kind;
                break;
            case "tax_id":
                taxId = inv.TaxId?.Trim();
                if (taxId is null || !TaxIdShape().IsMatch(taxId) || !IsValidTaxId(taxId))
                {
                    throw new MemberValidationException("統一編號不正確，請確認 8 位數字。", "invalid_tax_id");
                }

                break;
            case "donation":
                donation = inv.DonationCode?.Trim();
                if (string.IsNullOrEmpty(donation) || !await db.InvoiceDonationCodes.AsNoTracking().AnyAsync(d => d.Code == donation && d.IsActive, cancellationToken))
                {
                    throw new MemberValidationException("請從清單中選擇要捐贈的團體（捐贈碼不正確）。", "invalid_donation_code");
                }

                break;
            default:
                throw new MemberValidationException("請選擇電子發票開立方式（載具、統一編號或捐贈碼擇一）。", "invoice_required");
        }

        return new ValidatedCheckout(email, name, phone, delivery, address, note, kind, carrierType, carrierId, taxId, donation, lang);
    }

    /// <summary>統一編號檢核碼（財政部公告演算法：權重 1,2,1,2,1,2,4,1，各位乘積的十位與個位相加後總和為 10 的倍數；第 7 碼為 7 時總和或總和＋1 為 10 的倍數皆可）。</summary>
    public static bool IsValidTaxId(string id)
    {
        int[] weights = [1, 2, 1, 2, 1, 2, 4, 1];
        var sum = 0;
        for (var i = 0; i < 8; i++)
        {
            var p = (id[i] - '0') * weights[i];
            sum += p / 10 + p % 10;
        }

        return sum % 10 == 0 || (id[6] == '7' && (sum + 1) % 10 == 0);
    }

    private static string Fingerprint(CartOwner owner, ValidatedCheckout v)
    {
        var canonical = string.Join('\u001f', owner.Fingerprint, v.Email, v.Name, v.Phone, v.DeliveryMethod, v.Address, v.Note, v.InvoiceKind, v.CarrierId, v.TaxId, v.DonationCode);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    // ═══════════════════════════ 通知信（盡力而為，失敗不影響訂單）═══════════════════════════

    private async Task SendOrderCreatedAsync(ClubScope scope, Order order, ValidatedCheckout input, CancellationToken cancellationToken)
    {
        try
        {
            var (clubName, domain) = await ClubDisplayAsync(scope.ClubId, input.Lang, cancellationToken);
            var timeout = await settings.GetPendingTimeoutMinutesAsync(scope.ClubId, cancellationToken);
            var lines = order.OrderItems.OrderBy(i => i.RowSeq).Select(i => new ShopEmailTemplates.Line(
                i.VariantLabelSnapshot is { Length: > 0 } label ? $"{i.ProductNameSnapshot}（{label}）" : i.ProductNameSnapshot, i.Quantity, i.LineTotal)).ToList();
            await email.SendAsync(ShopEmailTemplates.OrderCreated(
                input.Email, input.Name, clubName, order.OrderNo, order.Total, lines, order.CreatedAt.AddMinutes(timeout),
                order.MemberId is null ? LookupLink(domain, input.Lang, order.LookupToken) : null, input.Lang), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "訂單 {OrderNo} 的成立通知信寄送失敗（不影響訂單）。", order.OrderNo);
        }
    }

    private async Task SendPaymentCompletedAsync(ClubScope scope, Order order, string? invoiceNo, string lang, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                return;
            }

            var (clubName, domain) = await ClubDisplayAsync(scope.ClubId, lang, cancellationToken);
            await email.SendAsync(ShopEmailTemplates.PaymentCompleted(
                order.BuyerEmail, order.RecipientName ?? string.Empty, clubName, order.OrderNo, order.Total, invoiceNo,
                order.MemberId is null ? LookupLink(domain, lang, order.LookupToken) : null, lang), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "訂單 {OrderNo} 的付款完成通知信寄送失敗（不影響訂單）。", order.OrderNo);
        }
    }

    private async Task<(string Name, string Domain)> ClubDisplayAsync(Guid clubId, string lang, CancellationToken cancellationToken)
    {
        var club = await db.Clubs.AsNoTracking().Include(c => c.ClubsI18ns).FirstAsync(c => c.Id == clubId, cancellationToken);
        var wanted = lang == "en" ? "en" : RequestLocale.DefaultDbLocale;
        var name = club.ClubsI18ns.FirstOrDefault(i => i.Locale == wanted)?.Name ?? club.ClubsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name ?? club.Code;
        return (name, club.Domain);
    }

    /// <summary>信件連結：<c>{站台網址}/{zh|en}/order/lookup/?token=…</c>（與前台 <c>order/lookup</c> 頁一致）。站台網址同會員信件用 <c>MEMBER_EMAIL_LINK_BASE_URL</c> 覆寫。</summary>
    private string LookupLink(string domain, string lang, string token)
    {
        var baseUrl = configuration["MEMBER_EMAIL_LINK_BASE_URL"] is { Length: > 0 } configured ? configured.TrimEnd('/') : $"https://{domain}";
        return $"{baseUrl}/{lang}/order/lookup/?token={Uri.EscapeDataString(token)}";
    }
}
