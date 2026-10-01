namespace Tcrfc.Api.Features.MemberCenter;

/// <summary>俱樂部品牌（會員卡卡面與會籍列表用：「每份會籍一張卡，各帶該俱樂部的標誌與品牌色」）。</summary>
public sealed record MemberClubBrandDto
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? LogoLightUrl { get; init; }
    public string? LogoDarkUrl { get; init; }
    public string? BrandColor { get; init; }
    public string? BrandSecondaryColor { get; init; }
}

/// <summary>會員自己的一張電子會員卡。<c>Token</c> 是這張卡的 QR 憑證（QR 內容＝<c>{官網網址}/m/{token}</c>，在裝置端組出），只有會員本人看得到。
/// <c>IsValid</c>＝卡片使用中且會籍有效（以到期日為準）。</summary>
public sealed record MemberCardDto
{
    public required Guid Id { get; init; }
    public required Guid MembershipId { get; init; }
    public required MemberClubBrandDto Club { get; init; }
    public required string MemberNo { get; init; }
    public required string HolderName { get; init; }
    public required string Tier { get; init; }
    public required string TierLabel { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required bool IsValid { get; init; }
    public required string Token { get; init; }
    public required int ReissueCount { get; init; }
    public DateTime? IssuedAt { get; init; }
}

public sealed record MemberPendingOrderDto
{
    public required string OrderNo { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
}

/// <summary>一份會籍（一人每俱樂部每球季一份）。<c>Status</c> 是「有效與否以到期日為準」的有效狀態（<c>pending</c>／<c>active</c>／<c>expired</c>／<c>cancelled</c>）。
/// 升級申請待確認時 <c>PendingOrder</c> 有值（網頁的「待確認」就是它）。</summary>
public sealed record MyMembershipDto
{
    public required Guid Id { get; init; }
    public required MemberClubBrandDto Club { get; init; }
    public required string SeasonCode { get; init; }
    public required string Tier { get; init; }
    public required string TierLabel { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public string? PlanCode { get; init; }
    public string? PlanName { get; init; }
    public required int CardQuota { get; init; }
    public required int JerseyQuota { get; init; }
    public required bool IsCurrentSeason { get; init; }
    /// <summary>到期前 30 天內（含）且仍有效：畫面顯示續會提示（規劃書 §3.14「到期前顯示續會提示與付款指引」）。</summary>
    public required bool RenewalDue { get; init; }
    public MemberPendingOrderDto? PendingOrder { get; init; }
    public required IReadOnlyList<MemberCardDto> Cards { get; init; }
}

public sealed record MemberJoinableClubDto
{
    public required string Code { get; init; }
    public required string Name { get; init; }
}

/// <summary>「我的會籍」：逐俱樂部列出；沒有會籍的俱樂部放在 <c>JoinableClubs</c>（畫面顯示加入入口，不隱藏，規劃書 §3.14／App §3.5）。</summary>
public sealed record MyMembershipsDto
{
    public required IReadOnlyList<MyMembershipDto> Memberships { get; init; }
    public required IReadOnlyList<MemberJoinableClubDto> JoinableClubs { get; init; }
}

public sealed record MemberJerseyDto
{
    public required Guid Id { get; init; }
    public required string ClubCode { get; init; }
    public required Guid MembershipId { get; init; }
    public required string RecipientName { get; init; }
    public string? Phone { get; init; }
    public string? Size { get; init; }
    public string? DeliveryMethod { get; init; }
    public string? DeliveryMethodLabel { get; init; }
    public string? Address { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public DateOnly? ShippedOn { get; init; }
    public DateOnly? ReceivedOn { get; init; }
    /// <summary>只有狀態還是「待處理」時會員可以改（已寄出後改不了）。</summary>
    public required bool Editable { get; init; }
}

/// <summary>某份付費會籍的球衣登記概況：方案含幾件（<c>Quota</c>）、已登記幾件（<c>Used</c>）。</summary>
public sealed record MemberJerseyGroupDto
{
    public required Guid MembershipId { get; init; }
    public required string ClubCode { get; init; }
    public required string ClubName { get; init; }
    public required string SeasonCode { get; init; }
    public required int Quota { get; init; }
    public required int Used { get; init; }
    public required bool CanRegister { get; init; }
    public required IReadOnlyList<MemberJerseyDto> Items { get; init; }
}

public sealed record MemberJerseyRequest(Guid MembershipId, string RecipientName, string Size, string DeliveryMethod, string? Phone, string? Address);

public sealed record MemberJerseyUpdateRequest(string RecipientName, string Size, string DeliveryMethod, string? Phone, string? Address);

/// <summary>電子會員卡公開驗證頁 <c>/m/&lt;token&gt;</c> 的回應。🔴 欄位<b>只有</b>這五個：姓名首字、會員編號、層級、有效／已過期。
/// <b>不得新增「適用球隊」或任何其他欄位</b>（主站規劃書 §3.14、App §3.6：token 已隱含俱樂部）。</summary>
public sealed record CardVerificationDto
{
    public required string NameInitial { get; init; }
    public required string MemberNo { get; init; }
    public required string Tier { get; init; }
    public required string TierLabel { get; init; }
    /// <summary><c>valid</c>（有效）／<c>expired</c>（已過期）。</summary>
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
}

public sealed record MemberRegistrationDto
{
    public required Guid Id { get; init; }
    public required string RegistrationNo { get; init; }
    public required string ClubCode { get; init; }
    public required string Status { get; init; }
    public required string ApplicantName { get; init; }
    public Guid? SessionId { get; init; }
    public Guid? TrialId { get; init; }
    public required DateTime CreatedAt { get; init; }
}
