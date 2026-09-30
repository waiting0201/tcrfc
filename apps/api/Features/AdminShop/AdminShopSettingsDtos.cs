namespace Tcrfc.Api.Features.AdminShop;

public sealed record AdminShopBilingualText
{
    public string? Zh { get; init; }
    public string? En { get; init; }
}

public sealed record AdminCollectingSubjectDto
{
    public required Guid ClubId { get; init; }
    public string? Name { get; init; }

    /// <summary>S6 介面必須顯示的提醒：本商店的收款主體是俱樂部，不是慈善捐款平台的主辦協會。</summary>
    public required string Notice { get; init; }
}

public sealed record AdminShopSettingsDto
{
    /// <summary>本商店的收款主體（俱樂部）。兩隊的藍鯨商品採代收代付，收款主體只有這一個。</summary>
    public required AdminCollectingSubjectDto CollectingSubject { get; init; }

    /// <summary>單一固定運費（元）。不做重量或級距計費。</summary>
    public required int ShippingFee { get; init; }

    /// <summary>免運門檻（元）；沒有＝不設免運。</summary>
    public int? FreeShippingThreshold { get; init; }

    /// <summary>離島與不配送地區（一行一個地區名稱）。</summary>
    public required IReadOnlyList<string> ExcludedRegions { get; init; }

    /// <summary>低庫存預設門檻（規格可各自覆寫）。</summary>
    public required int LowStockThreshold { get; init; }

    /// <summary>待付款訂單保留庫存的分鐘數，逾時自動釋回。</summary>
    public required int PendingTimeoutMinutes { get; init; }
    public required AdminShopBilingualText EntryTitle { get; init; }
    public required AdminShopBilingualText EntryIntro { get; init; }
    public required AdminShopBilingualText PolicyNotice { get; init; }
    public required AdminShopBilingualText PolicyShipping { get; init; }
    public required AdminShopBilingualText PolicyReturns { get; init; }
    public required AdminShopBilingualText PolicyTerms { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed record UpdateAdminShopSettingsRequest
{
    public required int ShippingFee { get; init; }
    public int? FreeShippingThreshold { get; init; }
    public IReadOnlyList<string>? ExcludedRegions { get; init; }
    public int? LowStockThreshold { get; init; }
    public int? PendingTimeoutMinutes { get; init; }
    public AdminShopBilingualText? EntryTitle { get; init; }
    public AdminShopBilingualText? EntryIntro { get; init; }
    public AdminShopBilingualText? PolicyNotice { get; init; }
    public AdminShopBilingualText? PolicyShipping { get; init; }
    public AdminShopBilingualText? PolicyReturns { get; init; }
    public AdminShopBilingualText? PolicyTerms { get; init; }
}

// ───────────── 金流與發票憑證（只有系統管理員）─────────────

public sealed record AdminCredentialStatusDto
{
    public required bool Configured { get; init; }

    /// <summary>識別碼（Channel ID／店家代號）只顯示遮罩值；密鑰永遠不回傳。</summary>
    public string? IdentifierMasked { get; init; }

    /// <summary>電子發票的字軌。</summary>
    public string? InvoicePrefix { get; init; }
    public DateTime? RotatedAt { get; init; }
}

public sealed record AdminCredentialEnvironmentsDto
{
    public required AdminCredentialStatusDto Sandbox { get; init; }
    public required AdminCredentialStatusDto Production { get; init; }
}

public sealed record AdminInvoiceRetryDto
{
    public required int MaxRetries { get; init; }
    public required int IntervalMinutes { get; init; }
}

public sealed record AdminShopCredentialsDto
{
    public required AdminCollectingSubjectDto CollectingSubject { get; init; }

    /// <summary>目前使用的金流環境：<c>sandbox</c> 測試／<c>production</c> 正式。</summary>
    public required string Environment { get; init; }
    public required AdminCredentialEnvironmentsDto LinePay { get; init; }
    public required AdminCredentialEnvironmentsDto EInvoice { get; init; }
    public required AdminInvoiceRetryDto InvoiceRetry { get; init; }

    /// <summary>金流與電子發票尚未串接（B-10）：憑證可先存放，但系統目前不會用它連線。</summary>
    public required bool IntegrationConnected { get; init; }
}

public sealed record UpdateAdminLinePayCredentialRequest
{
    /// <summary><c>sandbox</c> 或 <c>production</c>。</summary>
    public required string Environment { get; init; }
    public required string ChannelId { get; init; }

    /// <summary>Channel Secret。第一次設定必填；之後省略＝沿用原本的密鑰。</summary>
    public string? ChannelSecret { get; init; }
}

public sealed record UpdateAdminEInvoiceCredentialRequest
{
    public required string Environment { get; init; }
    public string? MerchantId { get; init; }

    /// <summary>發票服務金鑰；省略＝沿用原本的金鑰。</summary>
    public string? ApiKey { get; init; }

    /// <summary>字軌（兩位大寫英文字母，如 <c>AB</c>）。</summary>
    public required string InvoicePrefix { get; init; }
}

public sealed record UpdateAdminPaymentModeRequest
{
    public required string Environment { get; init; }
}

public sealed record UpdateAdminInvoiceRetryRequest
{
    public required int MaxRetries { get; init; }
    public required int IntervalMinutes { get; init; }
}

// ───────────── 發票捐贈碼 ─────────────

public sealed record AdminDonationCodeDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string OrgName { get; init; }
    public required bool IsActive { get; init; }
    public required int SortOrder { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminDonationCodeRequest
{
    /// <summary>捐贈碼（3 到 7 位數字）。</summary>
    public required string Code { get; init; }
    public required string OrgName { get; init; }
    public bool IsActive { get; init; } = true;
    public int? SortOrder { get; init; }
}

// ───────────── 報表 ─────────────

public sealed record AdminShopTopSkuDto
{
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
    public string? VariantLabel { get; init; }
    public required int Quantity { get; init; }
    public required int Revenue { get; init; }
}

public sealed record AdminShopReportSummaryDto
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }

    /// <summary>已付款訂單（不含已取消）的營收合計（含運費）。</summary>
    public required int GrossRevenue { get; init; }

    /// <summary>期間內已執行的退款金額合計。</summary>
    public required int RefundedAmount { get; init; }
    public required int NetRevenue { get; init; }
    public required int OrderCount { get; init; }

    /// <summary>客單價＝營收 ÷ 訂單數（無訂單為 0）。</summary>
    public required int AverageOrderValue { get; init; }
    public required int ReturnOrderCount { get; init; }

    /// <summary>退貨率＝有退款案件的訂單數 ÷ 訂單數（百分比，小數一位）。</summary>
    public required decimal ReturnRatePercent { get; init; }
    public required IReadOnlyList<AdminShopTopSkuDto> TopSkus { get; init; }
    public required int LowStockCount { get; init; }
    public required int OutOfStockCount { get; init; }
    public required int TotalAvailableQuantity { get; init; }
}

public sealed record AdminSellingClubTotalDto
{
    public required Guid SellingClubId { get; init; }
    public required string SellingClubCode { get; init; }
    public string? SellingClubName { get; init; }
    public required int OrderCount { get; init; }
    public required int GrossRevenue { get; init; }
    public required int RefundedAmount { get; init; }
    public required int NetRevenue { get; init; }
    public required int SettledAmount { get; init; }
    public required int PendingSettlementAmount { get; init; }
}
