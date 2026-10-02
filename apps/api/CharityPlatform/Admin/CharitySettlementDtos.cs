namespace Tcrfc.Api.CharityPlatform.Admin;

// N4 回饋金結算（規劃書 §6.4、§8）。店家與項目撥付對象「分開結算、各自一份對帳單」（§8.1），所以一份結算單只有一個對象。
// 狀態（settlements.status）：pending（待結算，草稿）→ settled（已結算，鎖定）→ paid（已付款，鎖定）。
// 每次異動的經辦人與時間由稽核紀錄承載（settledAt／paidAt 即取自稽核，docs/16a 的「狀態異動歷程」存疑項據此結案）。

/// <summary>產生結算單的請求。<c>periodEnd</c> 必須早於今天（台灣日期）：期間還沒結束就結算，當天稍後才付款的捐款會永遠落在期間之外。
/// <c>payeeType</c> 省略＝店家與項目都做；<c>payeeId</c> 省略＝該類型所有有應付金額的對象。</summary>
public sealed record RunSettlementRequest
{
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }

    /// <summary><c>store</c>／<c>project</c>；省略＝兩者。</summary>
    public string? PayeeType { get; init; }

    public Guid? PayeeId { get; init; }
}

/// <summary>登記匯款（實際匯款在系統外執行，系統只登記日期、方式與備註，規劃書 §6.4、§8.6）。</summary>
public sealed record MarkSettlementPaidRequest
{
    public DateOnly RemittedOn { get; init; }

    /// <summary>匯款方式（例如「銀行轉帳」），必填、32 字內。</summary>
    public string? RemitMethod { get; init; }

    public string? RemitNote { get; init; }
}

public sealed record AdminSettlementListItemDto
{
    public required Guid Id { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }

    /// <summary><c>store</c>（店家回饋金）／<c>project</c>（項目撥付金）。</summary>
    public required string PayeeType { get; init; }

    public required Guid PayeeId { get; init; }
    public required string? PayeeName { get; init; }

    /// <summary><c>pending</c>（待結算）／<c>settled</c>（已結算）／<c>paid</c>（已付款）。</summary>
    public required string Status { get; init; }

    /// <summary>一般（正項）捐款的筆數與捐款總額（毛額）。</summary>
    public required int DonationCount { get; init; }

    public required int DonationTotal { get; init; }

    /// <summary>退款沖回（負項）的筆數與金額（金額為負數或 0）。</summary>
    public required int ClawbackCount { get; init; }

    public required int ClawbackAmount { get; init; }

    /// <summary>應付金額＝正項分潤合計＋沖回負項（可能為負：代表上期已付款的退款沖回大於本期應付）。</summary>
    public required int PayableAmount { get; init; }

    public required DateOnly? RemittedOn { get; init; }
    public required string? RemitMethod { get; init; }
    public required string? RemitNote { get; init; }

    /// <summary>確認結算與登記付款的時間與經辦人（取自稽核紀錄）。</summary>
    public required DateTime? SettledAt { get; init; }

    public required string? SettledByName { get; init; }
    public required DateTime? PaidRegisteredAt { get; init; }
    public required string? PaidRegisteredByName { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record AdminSettlementLineDto(
    Guid LineId,
    Guid DonationId,
    string OrderNo,
    DateTime? PaidAt,
    int DonationAmount,
    decimal SharePct,
    int ShareAmount,
    bool IsClawback,
    string? ClawbackReason);

public sealed record AdminSettlementDetailDto
{
    public required AdminSettlementListItemDto Settlement { get; init; }

    /// <summary>逐筆明細：正項在前（依付款時間），沖回負項在後並明列原因與原捐款單號。</summary>
    public required IReadOnlyList<AdminSettlementLineDto> Lines { get; init; }
}

public sealed record AdminSettlementSkipDto(string PayeeType, Guid PayeeId, string? PayeeName, string Reason);

public sealed record AdminSettlementRunResultDto
{
    public required IReadOnlyList<AdminSettlementListItemDto> Created { get; init; }

    /// <summary>沒有產生的對象與原因（例如與既有結算單期間重疊）。</summary>
    public required IReadOnlyList<AdminSettlementSkipDto> Skipped { get; init; }
}

public sealed record AdminSettlementRecalculationDto
{
    public required AdminSettlementDetailDto Detail { get; init; }

    /// <summary>因為已退款而從這份對帳單扣除的捐款單號。</summary>
    public required IReadOnlyList<string> RemovedOrderNos { get; init; }

    /// <summary>（僅草稿）新納入的捐款筆數與沖回筆數。</summary>
    public required int AddedCount { get; init; }

    public required int AddedClawbackCount { get; init; }
}
