namespace Tcrfc.Api.CharityPlatform.Admin;

// N3 捐款紀錄（規劃書 §6.3）。🔴 捐款人個資（姓名／Email／身分證字號／地址）是受限資料：API 層回傳<b>遮罩後的值</b>，
// 完整檢視需要 reveal 權限並寫稽核；遮罩是在 API 層做的，不是前端隱藏（docs/16 §5）。

public sealed record AdminDonationListItemDto
{
    public required Guid Id { get; init; }
    public required string OrderNo { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime? PaidAt { get; init; }
    public required int Amount { get; init; }

    /// <summary><c>created</c>／<c>pending</c>／<c>paid</c>／<c>failed</c>／<c>expired</c>／<c>refunded</c>。</summary>
    public required string Status { get; init; }

    public required Guid ProjectId { get; init; }
    public required string? ProjectName { get; init; }

    /// <summary>來源店家；沒有店家歸屬（直接捐款）為 <c>null</c>。</summary>
    public required Guid? StoreId { get; init; }

    public required string? StoreName { get; init; }

    /// <summary>憑證開立狀態：<c>pending</c>／<c>issued</c>／<c>failed</c>；已作廢或折讓時另見 <see cref="InvoiceVoidStatus"/>。</summary>
    public required string? InvoiceStatus { get; init; }

    public required string? InvoiceVoidStatus { get; init; }
    public required bool IsAnonymous { get; init; }

    /// <summary>🔴 待人工處理（捐款單 pending 且最近一次付款 failed＝付款確認結果未知，可能已扣款）。</summary>
    public required bool NeedsManualReview { get; init; }
}

public sealed record AdminDonationDetailDto
{
    public required Guid Id { get; init; }
    public required string OrderNo { get; init; }
    public required string Status { get; init; }
    public required int Amount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime? PaidAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required AdminDonationProjectRef Project { get; init; }
    public required AdminDonationStoreRef? Store { get; init; }
    public required AdminDonorDto Donor { get; init; }

    /// <summary>分潤快照（付款成立時寫入，之後調整設定不追溯，規劃書 §8.4）。尚未付款的單是建單時的暫算值。</summary>
    public required AdminDonationSplitDto Split { get; init; }

    public required string InvoiceMode { get; init; }
    public required IReadOnlyList<AdminPaymentDto> Payments { get; init; }
    public required AdminInvoiceDto? Invoice { get; init; }
    public required AdminRefundDto? Refund { get; init; }
    public required bool NeedsManualReview { get; init; }

    /// <summary>狀態與操作的時間軸（建單、發起付款、付款確認、退款、補寄信、重開憑證等）。</summary>
    public required IReadOnlyList<AdminTimelineEntryDto> Timeline { get; init; }
}

public sealed record AdminDonationProjectRef(Guid Id, string Slug, string? Name);

public sealed record AdminDonationStoreRef(Guid Id, string? Name);

/// <summary>捐款人。<see cref="Revealed"/> 為 <c>false</c> 時 <see cref="Name"/>／<see cref="Email"/> 是遮罩後的值（如 <c>王○明</c>、<c>a***@gmail.com</c>）。</summary>
public sealed record AdminDonorDto(string Name, string Email, bool IsAnonymous, bool Revealed);

public sealed record AdminDonationSplitDto(
    decimal StoreSharePct, decimal ProjectSharePct, int StoreAmount, int ProjectAmount, int AssociationAmount);

/// <summary>金流交易（⛔ 不含金流端原始回應 <c>raw_response</c>，那是只存不查的內部資料）。</summary>
public sealed record AdminPaymentDto(
    Guid Id, string? TransactionId, string Status, DateTime? RequestedAt, DateTime? ConfirmedAt, int Amount);

/// <summary>憑證。敏感欄位（載具、身分證字號、地址）預設遮罩；有 reveal 權限並要求明文時才完整回傳。</summary>
public sealed record AdminInvoiceDto
{
    /// <summary><c>b2c_invoice</c>／<c>donation_receipt</c>。</summary>
    public required string InvoiceType { get; init; }

    /// <summary><c>mobile_carrier</c>／<c>love_code</c>／<c>tax_id</c>（電子發票）。</summary>
    public required string? CarrierType { get; init; }

    public required string? CarrierId { get; init; }
    public required string? TaxId { get; init; }
    public required string? InvoiceTitle { get; init; }
    public required string? ReceiptTitle { get; init; }
    public required string? NationalId { get; init; }
    public required string? ReceiptAddress { get; init; }
    public required bool IsAnnualSummary { get; init; }
    public required string IssueStatus { get; init; }
    public required string VoidStatus { get; init; }
    public required string? InvoiceNo { get; init; }
    public required DateTime? IssuedAt { get; init; }
    public required string? VoidReason { get; init; }
    public required string? VoidedByName { get; init; }
}

public sealed record AdminRefundDto(string? Reason, string? RefundedByName);

public sealed record AdminTimelineEntryDto(DateTime At, string Kind, string Text, string? ByName);

public sealed record RefundDonationRequest
{
    /// <summary>退款原因（必填，2–255 字，供稽核與客服追蹤）。</summary>
    public string? Reason { get; init; }
}

public sealed record AdminAnomalyDto
{
    /// <summary><c>confirm_failed</c>（已扣款但確認失敗，結果未知）／<c>invoice_failed</c>（憑證開立失敗）／
    /// <c>invoice_void_pending</c>（已退款但憑證尚未作廢或折讓）／<c>reconciliation</c>（對帳差異）。</summary>
    public required string Kind { get; init; }

    public required Guid? DonationId { get; init; }
    public required string? OrderNo { get; init; }
    public required int? Amount { get; init; }
    public required DateTime OccurredAt { get; init; }

    /// <summary>對帳差異的類型與處理狀態（其他類別為 <c>null</c>）。</summary>
    public required string? DiscrepancyType { get; init; }

    public required string? ResolutionStatus { get; init; }
    public required Guid? DiscrepancyId { get; init; }
}

public sealed record AdminAnomalyCountsDto(int ConfirmFailed, int InvoiceFailed, int InvoiceVoidPending, int Reconciliation);
