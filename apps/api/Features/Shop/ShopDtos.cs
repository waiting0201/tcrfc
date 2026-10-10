using System.Text.Json;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.Shop;

// ═══════════════════════════ 目錄 ═══════════════════════════

/// <summary>商品系列（Collection）。<see cref="Narrative"/> 是品牌敘事區塊（S1「系列介紹文」）。</summary>
public sealed record ShopCollectionDto
{
    public required string Slug { get; init; }
    public string? Name { get; init; }
    public string? Narrative { get; init; }
    public required int ProductCount { get; init; }
}

public sealed record ShopDeliveryMethodDto
{
    public required string Code { get; init; }
    public required string Label { get; init; }

    /// <summary>以「目前購物車為空」計算的基本運費；實際運費依小計是否達免運門檻，結帳與購物車回應會算好。</summary>
    public required int BaseFee { get; init; }
}

public sealed record ShopDonationCodeDto
{
    public required string Code { get; init; }
    public required string OrgName { get; init; }
}

/// <summary>商店入口與政策（S6）＋運費規則＋發票選項，前台 8.3 入口頁、購物須知、退換貨政策頁、結帳頁共用。</summary>
public sealed record ShopInfoDto
{
    public string? EntryTitle { get; init; }
    public string? EntryIntro { get; init; }
    public string? PolicyNotice { get; init; }
    public string? PolicyShipping { get; init; }
    public string? PolicyReturns { get; init; }
    public string? PolicyTerms { get; init; }
    public required int ShippingFee { get; init; }
    public int? FreeShippingThreshold { get; init; }
    public required IReadOnlyList<string> ExcludedRegions { get; init; }
    public required IReadOnlyList<ShopDeliveryMethodDto> DeliveryMethods { get; init; }
    public required IReadOnlyList<ShopDonationCodeDto> DonationCodes { get; init; }

    /// <summary>收款主體（俱樂部）名稱，結帳頁須標明（規劃書 §4.13：本商店收款主體是俱樂部）。</summary>
    public string? CollectingSubjectName { get; init; }

    /// <summary>付款是否已可用（LINE Pay 已串接）。false 時前台只能展示、不能結帳付款。</summary>
    public required bool PaymentAvailable { get; init; }

    public required IReadOnlyList<ShopCollectionDto> Collections { get; init; }
}

public sealed record ShopProductListItemDto
{
    public required string Slug { get; init; }
    public string? Name { get; init; }
    public string? CollectionSlug { get; init; }
    public string? CollectionName { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required bool IsNewArrival { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }

    /// <summary>封面（第一張圖）的寬高（像素）與替代文字（當前語系，英文空白回退中文）；沒有圖片時為 <c>null</c>。</summary>
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public string? ImageAlt { get; init; }

    /// <summary>目前售價（促銷價優先）的最低與最高。沒有任何販售中規格時為 null。</summary>
    public int? PriceMin { get; init; }
    public int? PriceMax { get; init; }

    /// <summary>原價（未打折）的最低；只有在有促銷時才有值，供前台畫刪除線。</summary>
    public int? ListPriceMin { get; init; }
    public required bool OnSale { get; init; }

    /// <summary><c>in_stock</c>／<c>low_stock</c>／<c>sold_out</c>。缺貨由庫存自動判定（S1）。</summary>
    public required string StockStatus { get; init; }
    public required string StockStatusLabel { get; init; }
    public required IReadOnlyList<string> Sizes { get; init; }
    public required IReadOnlyList<string> Colours { get; init; }
}

public sealed record ShopImageDto
{
    public required string Url { get; init; }
    public required string ThumbUrl { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }

    /// <summary>圖片替代文字（當前語系，英文空白回退中文）；沒填為 <c>null</c>。</summary>
    public string? Alt { get; init; }
}

public sealed record ShopVariantDto
{
    public required Guid Id { get; init; }
    public required string Sku { get; init; }
    public string? Size { get; init; }
    public string? Colour { get; init; }
    public required string Label { get; init; }

    /// <summary>原價。</summary>
    public required int ListPrice { get; init; }

    /// <summary>目前售價（促銷價優先）。</summary>
    public required int Price { get; init; }
    public required bool OnSale { get; init; }

    /// <summary>可售量（庫存量－已保留量），最高回報 99（超過一律當 99，避免洩漏精確庫存又足夠決定購買上限）。</summary>
    public required int AvailableQty { get; init; }
    public required bool Purchasable { get; init; }
}

public sealed record ShopProductDetailDto
{
    public required string Slug { get; init; }
    public string? Name { get; init; }
    public string? Narrative { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public string? CollectionSlug { get; init; }
    public string? CollectionName { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
    public required bool IsNewArrival { get; init; }
    public JsonElement? SizeChart { get; init; }
    public required IReadOnlyList<ShopImageDto> Images { get; init; }
    public required IReadOnlyList<ShopVariantDto> Variants { get; init; }
    public int? PriceMin { get; init; }
    public int? PriceMax { get; init; }
    public int? ListPriceMin { get; init; }
    public required bool OnSale { get; init; }
    public required string StockStatus { get; init; }
    public required string StockStatusLabel { get; init; }
}

// ═══════════════════════════ 購物車 ═══════════════════════════

public sealed record AddCartItemRequest
{
    public Guid VariantId { get; init; }
    public int Quantity { get; init; } = 1;
}

public sealed record SetCartItemRequest
{
    /// <summary>0 ＝ 從購物車移除。</summary>
    public int Quantity { get; init; }
}

public sealed record ShopCartItemDto
{
    public required Guid VariantId { get; init; }
    public required string ProductSlug { get; init; }
    public string? ProductName { get; init; }
    public required string VariantLabel { get; init; }
    public required string Sku { get; init; }
    public string? ImageThumbUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public string? ImageAlt { get; init; }
    public required int ListPrice { get; init; }
    public required int UnitPrice { get; init; }
    public required bool OnSale { get; init; }
    public required int Quantity { get; init; }
    public required int LineTotal { get; init; }
    public required int AvailableQty { get; init; }

    /// <summary>此列現在能不能結帳：商品下架、規格停售、庫存不足都會是 false，並帶 <see cref="Issue"/>。</summary>
    public required bool Purchasable { get; init; }

    /// <summary><c>unavailable</c>（已下架或停售）／<c>insufficient_stock</c>（庫存不足）；正常為 null。</summary>
    public string? Issue { get; init; }
    public string? IssueMessage { get; init; }
}

public sealed record ShopCartShippingDto
{
    /// <summary>宅配與超商取貨的運費（已套用免運門檻）。現場自取永遠 0。</summary>
    public required int Fee { get; init; }
    public int? FreeThreshold { get; init; }

    /// <summary>還差多少元免運；已免運或沒有設定門檻為 null。</summary>
    public int? AmountToFree { get; init; }
}

public sealed record ShopCartDto
{
    /// <summary>🔴 只在<b>新發出</b>訪客購物車的那一次回應才有值，前台必須存起來並在之後每次請求帶 <c>X-Cart-Token</c>。伺服器端只存雜湊，遺失無法補發。</summary>
    public string? CartToken { get; init; }
    public required string ClubCode { get; init; }
    public required IReadOnlyList<ShopCartItemDto> Items { get; init; }
    public required int ItemCount { get; init; }
    public required int Subtotal { get; init; }
    public required ShopCartShippingDto Shipping { get; init; }
    public required bool CanCheckout { get; init; }
}

// ═══════════════════════════ 結帳與訂單 ═══════════════════════════

public sealed record CheckoutInvoiceRequest
{
    /// <summary><c>mobile_barcode</c> 手機條碼載具／<c>citizen_cert</c> 自然人憑證載具／<c>tax_id</c> 統一編號／<c>donation</c> 捐贈碼（三選一）。</summary>
    public string? Type { get; init; }
    public string? CarrierId { get; init; }
    public string? TaxId { get; init; }
    public string? DonationCode { get; init; }
}

/// <summary>結帳請求。🔴 沒有任何金額欄位——金額一律由伺服器依購物車重算。</summary>
public sealed record CheckoutRequest
{
    /// <summary>買家 Email。非會員必填；會員省略時用帳號 Email。</summary>
    public string? Email { get; init; }
    public string? RecipientName { get; init; }
    public string? RecipientPhone { get; init; }

    /// <summary><c>home_delivery</c>／<c>cvs_pickup</c>／<c>onsite_pickup</c>。</summary>
    public string? DeliveryMethod { get; init; }

    /// <summary>宅配地址（宅配必填）。</summary>
    public string? RecipientAddress { get; init; }

    /// <summary>超商取貨的門市名稱或代碼（超商取貨必填）。</summary>
    public string? PickupStore { get; init; }
    public string? CustomerNote { get; init; }
    public CheckoutInvoiceRequest? Invoice { get; init; }

    /// <summary>通知信語言 <c>zh</c>／<c>en</c>（預設 zh）。</summary>
    public string? Lang { get; init; }
}

public sealed record ConfirmShopOrderRequest
{
    public string? TransactionId { get; init; }
}

public sealed record LookupShopOrderRequest
{
    public string? OrderNo { get; init; }
    public string? Email { get; init; }

    /// <summary>訂單成立信中的連結權杖（與 <c>orderNo</c>＋<c>email</c> 二擇一）。</summary>
    public string? Token { get; init; }
}

public sealed record ShopOrderItemDto
{
    public required string ProductName { get; init; }
    public string? VariantLabel { get; init; }
    public required string Sku { get; init; }
    public required int UnitPrice { get; init; }
    public required int Quantity { get; init; }
    public required int LineTotal { get; init; }
}

public sealed record ShopOrderInvoiceDto
{
    /// <summary><c>mobile_barcode</c>／<c>citizen_cert</c>／<c>tax_id</c>／<c>donation</c>。</summary>
    public required string Type { get; init; }
    public required string TypeLabel { get; init; }

    /// <summary><c>pending</c>／<c>issued</c>／<c>failed</c>；<c>failed</c> 對顧客一律顯示為「處理中」（系統會重試）。</summary>
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? InvoiceNo { get; init; }
    public DateTime? IssuedAt { get; init; }
    public string? TaxId { get; init; }
    public string? DonationCode { get; init; }
}

public sealed record ShopOrderShipmentDto
{
    public string? Carrier { get; init; }
    public string? TrackingNo { get; init; }
    public DateTime? ShippedAt { get; init; }
    public DateTime? DeliveredAt { get; init; }

    /// <summary><c>waiting</c>／<c>picked_up</c>／<c>overdue</c>（超商取貨與現場自取）。</summary>
    public string? PickupStatus { get; init; }
    public string? PickupStatusLabel { get; init; }
    public DateOnly? PickupDeadlineOn { get; init; }
}

public sealed record ShopOrderDto
{
    public required string OrderNo { get; init; }
    public required string ClubCode { get; init; }

    /// <summary>🔴 只在<b>非會員結帳成立的那一次回應</b>有值：之後用 <c>X-Order-Token</c> 標頭操作這張訂單（付款、確認、取消、查詢）。信件裡的連結也帶它。</summary>
    public string? AccessToken { get; init; }

    /// <summary>訂單狀態中文標籤（待付款／已付款／備貨中／已出貨／已完成／已取消／退貨處理中／已退款），直接顯示。</summary>
    public required string Status { get; init; }
    public required string PaymentStatus { get; init; }
    public required string PaymentStatusLabel { get; init; }
    public required string PaymentMethod { get; init; }
    public required string PaymentMethodLabel { get; init; }
    public required string DeliveryMethod { get; init; }
    public required string DeliveryMethodLabel { get; init; }
    public required int Subtotal { get; init; }
    public required int ShippingFee { get; init; }
    public required int Total { get; init; }
    public required IReadOnlyList<ShopOrderItemDto> Items { get; init; }

    /// <summary>收件資料。以「訂單編號＋Email」查詢的訪客只拿到遮罩值（持有 <c>X-Order-Token</c> 或會員本人才是完整值）。</summary>
    public string? RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public string? RecipientAddress { get; init; }
    public string? BuyerEmail { get; init; }
    public string? CustomerNote { get; init; }
    public required bool IsMasked { get; init; }
    public ShopOrderInvoiceDto? Invoice { get; init; }
    public ShopOrderShipmentDto? Shipment { get; init; }

    /// <summary>待付款訂單的付款網址（已請款才有）。</summary>
    public string? PaymentUrl { get; init; }

    /// <summary>待付款訂單的保留期限（UTC）；過了系統自動取消並釋回庫存。</summary>
    public DateTime? ExpiresAt { get; init; }
    public required bool CanPay { get; init; }
    public required bool CanCancel { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? PaidAt { get; init; }
}

/// <summary>「我的訂單」列表的一列（精簡）。</summary>
public sealed record ShopOrderListItemDto
{
    public required string OrderNo { get; init; }
    public required string Status { get; init; }
    public required string PaymentStatusLabel { get; init; }
    public required int Total { get; init; }
    public required int ItemCount { get; init; }
    public string? FirstItemName { get; init; }
    public string? InvoiceNo { get; init; }
    public string? TrackingNo { get; init; }
    public required DateTime CreatedAt { get; init; }
}
