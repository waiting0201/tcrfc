using Tcrfc.Api.Features.AdminMembers;

namespace Tcrfc.Api.Features.AdminMemberships;

// ═════════════ 方案 ═════════════

public sealed record AdminPlanLocaleContent
{
    public required string Name { get; init; }

    /// <summary>權益說明。</summary>
    public string? BenefitNote { get; init; }
}

public sealed record AdminPlanContentInput
{
    public required AdminPlanLocaleContent Zh { get; init; }
    public AdminPlanLocaleContent? En { get; init; }
}

/// <summary>新增與更新同形（更新是整份取代；省略英文版＝移除英文版）。<c>Status</c>：<c>published</c>（上架）／<c>draft</c>（下架）。
/// 費用單位「元」。<c>CardQuota</c> 是這份會籍可發幾張會員卡（家庭方案 3）；<c>JerseyQuota</c> 是含幾件球衣。</summary>
public sealed record UpsertAdminPlanRequest
{
    public required Guid SeasonId { get; init; }

    /// <summary>方案代碼（例：single、family）：小寫英文字母、數字與連字號，同一俱樂部同一球季內不可重複。</summary>
    public required string Code { get; init; }
    public required int Fee { get; init; }
    public int CardQuota { get; init; } = 1;
    public int JerseyQuota { get; init; }

    /// <summary>季中入會計價規則（文字說明，例：「照比例」「不折價」）。</summary>
    public string? MidSeasonRule { get; init; }
    public DateOnly? StartsOn { get; init; }
    public DateOnly? EndsOn { get; init; }
    public int SortOrder { get; init; }
    public string Status { get; init; } = "draft";
    public required AdminPlanContentInput Content { get; init; }
}

public sealed record AdminPlanListItemDto
{
    public required Guid Id { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public required string Code { get; init; }
    public required int Fee { get; init; }
    public required int CardQuota { get; init; }
    public required int JerseyQuota { get; init; }
    public DateOnly? StartsOn { get; init; }
    public DateOnly? EndsOn { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }

    /// <summary>目前使用這個方案的會籍份數（有會籍使用時不能刪除、不能換球季）。</summary>
    public required int MembershipCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminPlanDetailDto
{
    public required Guid Id { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public required string Code { get; init; }
    public required int Fee { get; init; }
    public required int CardQuota { get; init; }
    public required int JerseyQuota { get; init; }
    public string? MidSeasonRule { get; init; }
    public DateOnly? StartsOn { get; init; }
    public DateOnly? EndsOn { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required AdminPlanLocaleContent Zh { get; init; }
    public AdminPlanLocaleContent? En { get; init; }
    public required int MembershipCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

// ═════════════ 會籍 ═════════════

public sealed record AdminMembershipListItemDto
{
    public required Guid MembershipId { get; init; }
    public required Guid MemberId { get; init; }
    public required string MemberNo { get; init; }

    /// <summary>遮罩姓名（王○明）。完整個資請到會員詳情解除遮罩。</summary>
    public string? MemberName { get; init; }
    public required string Tier { get; init; }
    public required string TierLabel { get; init; }
    public required string Status { get; init; }
    public required string EffectiveStatus { get; init; }
    public required string EffectiveStatusLabel { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public int? DaysToExpire { get; init; }
    public Guid? PlanId { get; init; }
    public string? PlanName { get; init; }
    public required int CardCount { get; init; }
    public required int PaidTotal { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminMembershipDetailDto
{
    public required AdminMembershipListItemDto Membership { get; init; }
    public string? LastAdjustReason { get; init; }
    public DateTime? LastAdjustedAt { get; init; }
    public required IReadOnlyList<AdminMemberPaymentDto> Payments { get; init; }
    public required IReadOnlyList<AdminMemberCardDto> Cards { get; init; }

    /// <summary>方案允許的會員卡張數；沒有方案（免費會籍）為 1。</summary>
    public required int CardQuota { get; init; }
    public required int JerseyQuota { get; init; }
}

/// <summary>手動開通與續會（規劃書 §4.11 K2「開通與續會」）。受益俱樂部只能是目前操作的俱樂部，
/// 收款法人由系統帶入（收款主體俱樂部，代收代付）。<c>PaymentMethod</c>：<c>linepay</c>／<c>onsite</c>。</summary>
public sealed record ActivateMembershipRequest
{
    public required Guid MemberId { get; init; }
    public required Guid PlanId { get; init; }

    /// <summary>受益俱樂部（省略＝目前操作的俱樂部；填了必須與目前操作的俱樂部相同）。</summary>
    public Guid? BeneficiaryClubId { get; init; }
    public required string PaymentMethod { get; init; }

    /// <summary>實收金額（元），可與方案費用不同（例：季中入會的折算）。</summary>
    public required int Amount { get; init; }
    public required DateOnly PaidOn { get; init; }
    public string? Note { get; init; }

    /// <summary>省略＝方案期間起日（尚未開始則為今天）。</summary>
    public DateOnly? StartOn { get; init; }

    /// <summary>省略＝方案期間迄日（再省略則用球季結束日）。</summary>
    public DateOnly? EndOn { get; init; }
}

/// <summary>免費（一般會員）會籍：現場入會的會員需要一份會籍才有會員卡。</summary>
public sealed record RegisterMembershipRequest
{
    public required Guid MemberId { get; init; }
    public required Guid SeasonId { get; init; }
}

/// <summary>手動調整會籍（規劃書 §4.11 K2「手動調整層級（含異動原因紀錄）」）。至少要改一項，<c>Reason</c> 必填。</summary>
public sealed record AdjustMembershipRequest
{
    public string? Tier { get; init; }
    public string? Status { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public required string Reason { get; init; }
}

public sealed record AddMemberCardRequest
{
    public required string HolderName { get; init; }
}

public sealed record ExpireBatchRequest
{
    /// <summary>以這一天為基準（到期日早於這天的有效會籍會被標為已到期）。省略＝今天。</summary>
    public DateOnly? AsOf { get; init; }
    public Guid? SeasonId { get; init; }

    /// <summary>true＝只試算件數，不寫入。</summary>
    public bool DryRun { get; init; }
}

public sealed record ExpireBatchResultDto
{
    public required DateOnly AsOf { get; init; }
    public required int Count { get; init; }
    public required bool DryRun { get; init; }
}

public sealed record AdminPaymentListItemDto
{
    public required AdminMemberPaymentDto Payment { get; init; }
    public required Guid MembershipId { get; init; }
    public required Guid MemberId { get; init; }
    public required string MemberNo { get; init; }
    public required string SeasonCode { get; init; }
}

public sealed record AdminMemberSettingsDto
{
    public required string MemberNoPrefix { get; init; }
    public required int MemberNoDigits { get; init; }

    /// <summary>依目前規則，下一個新會員的編號預覽。</summary>
    public required string NextMemberNoPreview { get; init; }
}

public sealed record UpdateAdminMemberSettingsRequest
{
    /// <summary>前綴：英文字母與數字，最多 8 字，可為空。</summary>
    public required string MemberNoPrefix { get; init; }

    /// <summary>流水號位數：4 到 10。</summary>
    public required int MemberNoDigits { get; init; }
}

/// <summary>
/// K2「待確認申請」清單的一列（F 批，2026-10-01）：會員在網頁會員中心送出的升級申請（<c>membership_orders</c>，狀態 <c>created</c>＝待客服核對款項）。
/// 客服核對款項後用 <c>POST …/memberships/activate</c> 開通——本列帶了開通所需的 <see cref="MemberId"/>／<see cref="PlanId"/>／<see cref="Amount"/>，
/// 開通成功時會一併把同一份申請結案（<c>status → activated</c>，<c>activation_source = admin</c>），清單上就不會再出現。
/// 姓名、Email、電話依「完整個資」權限遮罩（同會籍清單）。
/// </summary>
public sealed record AdminMembershipApplicationDto
{
    public required string OrderNo { get; init; }
    public required Guid MemberId { get; init; }
    public required string MemberNo { get; init; }
    public string? MemberName { get; init; }
    public string? MemberEmail { get; init; }
    public string? MemberPhone { get; init; }
    public required Guid PlanId { get; init; }
    public required string PlanCode { get; init; }
    public string? PlanName { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }

    /// <summary>伺服器依方案算好的應收金額（元）。</summary>
    public required int Amount { get; init; }

    /// <summary><c>created</c>／<c>pending_payment</c>／<c>paid</c>／<c>activated</c>／<c>expired</c>／<c>activation_failed</c>／<c>cancelled</c>／<c>refunded</c>。</summary>
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? ActivatedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
