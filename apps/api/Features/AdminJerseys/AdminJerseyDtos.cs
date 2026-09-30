namespace Tcrfc.Api.Features.AdminJerseys;

public sealed record AdminJerseyDto
{
    public required Guid Id { get; init; }
    public required Guid MemberId { get; init; }
    public required string MemberNo { get; init; }
    public Guid? MembershipId { get; init; }

    /// <summary>領用人姓名。沒有「檢視完整個資」權限的角色（合作球隊管理）看到的是遮罩值。</summary>
    public string? RecipientName { get; init; }
    public string? Phone { get; init; }
    public string? Size { get; init; }
    public string? DeliveryMethod { get; init; }
    public string? DeliveryMethodLabel { get; init; }
    public string? Address { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public DateOnly? ShippedOn { get; init; }
    public DateOnly? ReceivedOn { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public required bool IsMasked { get; init; }
}

/// <summary>後台代填球衣登記（現場入會、電話登記）。件數不得超過該會籍方案的 <c>jersey_quota</c>。
/// <c>DeliveryMethod</c>：<c>ship</c>（寄送，須填電話與地址）／<c>pickup</c>（到場領取）。</summary>
public sealed record CreateAdminJerseyRequest
{
    public required Guid MembershipId { get; init; }
    public required string RecipientName { get; init; }
    public string? Phone { get; init; }
    public required string Size { get; init; }
    public required string DeliveryMethod { get; init; }
    public string? Address { get; init; }
}

/// <summary>只改有帶的欄位（省略＝不變）。修改領用人／電話／地址需要「檢視完整個資」權限。
/// 狀態：<c>pending</c>（待處理）／<c>shipped</c>（已寄出）／<c>received</c>（已領取）。</summary>
public sealed record UpdateAdminJerseyRequest
{
    public string? RecipientName { get; init; }
    public string? Phone { get; init; }
    public string? Size { get; init; }
    public string? DeliveryMethod { get; init; }
    public string? Address { get; init; }
    public string? Status { get; init; }
}

public sealed record BatchJerseyStatusRequest
{
    public required IReadOnlyList<Guid> Ids { get; init; }
    public required string Status { get; init; }
}

public sealed record AdminJerseySizeSummaryDto
{
    public string? Size { get; init; }
    public required string SizeLabel { get; init; }
    public required int Total { get; init; }
    public required int Ship { get; init; }
    public required int Pickup { get; init; }
}
