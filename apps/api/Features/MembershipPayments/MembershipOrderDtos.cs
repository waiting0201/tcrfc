namespace Tcrfc.Api.Features.MembershipPayments;

public sealed record CreateMembershipOrderRequest(string PlanCode);

public sealed record ConfirmMembershipOrderRequest(string TransactionId);

public sealed record InternalActivateRequest(string OrderNo);

public sealed record MembershipOrderDto
{
    public required string OrderNo { get; init; }
    public required string ClubCode { get; init; }
    public required string PlanCode { get; init; }
    public string? PlanName { get; init; }
    public required string SeasonCode { get; init; }
    /// <summary>整數元，伺服器依方案計算。</summary>
    public required int Amount { get; init; }
    /// <summary><c>created</c>／<c>pending_payment</c>／<c>paid</c>／<c>activated</c>／<c>expired</c>／<c>activation_failed</c>／<c>cancelled</c>／<c>refunded</c>。</summary>
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? PaymentMethod { get; init; }
    /// <summary>只有「待付款且尚未逾時」才有值。</summary>
    public string? PaymentUrl { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public DateTime? ActivatedAt { get; init; }
    public Guid? MembershipId { get; init; }
    public required DateTime CreatedAt { get; init; }
    /// <summary>true＝線上付款已串接，可以呼叫 <c>…/pay</c>（目前正式環境為 false，見 docs/17 §3）。</summary>
    public required bool CanPayOnline { get; init; }
    public required bool CanCancel { get; init; }
}

public sealed record MembershipActivationResultDto
{
    public required string OrderNo { get; init; }
    public required string Status { get; init; }
    public Guid? MembershipId { get; init; }
    /// <summary>true＝這張訂單先前已經開通過，本次呼叫沒有再做任何事（冪等）。</summary>
    public required bool AlreadyActivated { get; init; }
}
