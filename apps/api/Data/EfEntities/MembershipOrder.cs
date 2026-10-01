namespace Tcrfc.Api.Data.EfEntities;

/// <summary>會籍付款訂單（E 批；App 規劃書 §5.3／§5.4）。欄位語意見 db/club-schema.sql 與 docs/12b §6.5c。</summary>
public partial class MembershipOrder
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string OrderNo { get; set; } = null!;

    public Guid MemberId { get; set; }

    /// <summary>受益俱樂部。</summary>
    public Guid ClubId { get; set; }

    /// <summary>收款主體（恆為俱樂部，藍鯨會籍代收代付）。</summary>
    public Guid CollectingClubId { get; set; }

    public Guid MembershipPlanId { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    /// <summary>整數元，由伺服器依方案重算。</summary>
    public int Amount { get; set; }

    public string Status { get; set; } = "created";

    public string? PaymentMethod { get; set; }

    public string? PaymentTransactionId { get; set; }

    public string? PaymentUrl { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public string? ActivationSource { get; set; }

    public Guid? MembershipId { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual Club Club { get; set; } = null!;

    public virtual Club CollectingClub { get; set; } = null!;

    public virtual MembershipPlan MembershipPlan { get; set; } = null!;

    public virtual Membership? Membership { get; set; }
}
