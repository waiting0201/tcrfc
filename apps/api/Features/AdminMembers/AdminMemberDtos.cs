namespace Tcrfc.Api.Features.AdminMembers;

/// <summary>一份會籍在會員名單／詳情裡的摘要。<c>Status</c> 是資料庫存的狀態；<c>EffectiveStatus</c> 依到期日換算
/// （到期日已過但尚未批次處理的 active 會顯示為 expired），畫面顯示與篩選以 <c>EffectiveStatus</c> 為準。</summary>
public sealed record AdminMemberMembershipSummaryDto
{
    public required Guid MembershipId { get; init; }
    public required Guid ClubId { get; init; }
    public required string ClubCode { get; init; }
    public string? ClubName { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public required string Tier { get; init; }
    public required string TierLabel { get; init; }
    public required string Status { get; init; }
    public required string EffectiveStatus { get; init; }
    public required string EffectiveStatusLabel { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }

    /// <summary>距離到期還有幾天（已過期為負數；沒有到期日為 <c>null</c>）。</summary>
    public int? DaysToExpire { get; init; }
    public Guid? PlanId { get; init; }
    public string? PlanName { get; init; }
}

public sealed record AdminMemberListItemDto
{
    public required Guid Id { get; init; }
    public required string MemberNo { get; init; }

    /// <summary>列表一律是遮罩值（王○明）。完整值只能在詳情以 <c>reveal=true</c> 取得。</summary>
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public required string SignupSource { get; init; }
    public required string SignupSourceLabel { get; init; }

    /// <summary>只回「是否已綁定 LINE」。LINE 識別碼本身不對外顯示、不匯出。</summary>
    public required bool LineBound { get; init; }

    /// <summary>資料庫狀態：active／suspended／deleted。</summary>
    public required string Status { get; init; }

    /// <summary>畫面用狀態：active／suspended／unverified（Email 尚未驗證）／deleted。</summary>
    public required string DisplayStatus { get; init; }
    public required string DisplayStatusLabel { get; init; }
    public string? Locale { get; init; }
    public string? LocaleLabel { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }

    /// <summary>球衣狀態彙總（有任何待處理→pending；否則有已寄出→shipped；否則有已領取→received；沒有球衣登記為 <c>null</c>）。</summary>
    public string? JerseyStatus { get; init; }
    public string? JerseyStatusLabel { get; init; }

    /// <summary>只含「你有授權的俱樂部」的會籍列。</summary>
    public required IReadOnlyList<AdminMemberMembershipSummaryDto> Memberships { get; init; }
    public required bool IsMasked { get; init; }
}

public sealed record AdminMemberPaymentDto
{
    public required Guid Id { get; init; }
    public Guid? PlanId { get; init; }
    public string? PlanName { get; init; }
    public string? Method { get; init; }
    public string? MethodLabel { get; init; }
    public required int Amount { get; init; }
    public DateOnly? PaidOn { get; init; }

    /// <summary>收款法人（代收代付：恆為本俱樂部）。</summary>
    public required string CollectingClubCode { get; init; }

    /// <summary>受益俱樂部（這筆會費屬於哪個俱樂部的會籍）。</summary>
    public required string BeneficiaryClubCode { get; init; }
    public string? Note { get; init; }
    public string? HandledByName { get; init; }
    public DateOnly? ActivatedStartOn { get; init; }
    public DateOnly? ActivatedEndOn { get; init; }
    public required DateTime CreatedAt { get; init; }
}

/// <summary>會員卡狀態。🔴 不回傳 QR 的憑證字串——那是對外驗證用的秘密，後台沒有需要看到它的情境。</summary>
public sealed record AdminMemberCardDto
{
    public required Guid Id { get; init; }
    public required Guid MembershipId { get; init; }
    public string? HolderName { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public DateTime? IssuedAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    public required int ReissueCount { get; init; }
}

public sealed record AdminMemberMembershipDetailDto
{
    public required AdminMemberMembershipSummaryDto Membership { get; init; }
    public string? LastAdjustReason { get; init; }
    public DateTime? LastAdjustedAt { get; init; }
    public required IReadOnlyList<AdminMemberPaymentDto> Payments { get; init; }
    public required IReadOnlyList<AdminMemberCardDto> Cards { get; init; }
}

public sealed record AdminMemberJerseyDto
{
    public required Guid Id { get; init; }
    public required Guid ClubId { get; init; }
    public required string ClubCode { get; init; }
    public string? RecipientName { get; init; }
    public string? Size { get; init; }
    public string? DeliveryMethod { get; init; }
    public string? DeliveryMethodLabel { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public DateOnly? ShippedOn { get; init; }
    public DateOnly? ReceivedOn { get; init; }
}

/// <summary>會員註冊時的監護人同意紀錄（主站規劃書「會員資料安全要求」、App 規劃書 §4.5）。</summary>
public sealed record AdminMemberGuardianConsentDto
{
    /// <summary>同意時間（伺服器時間，UTC）。</summary>
    public required DateTime ConsentedAt { get; init; }

    /// <summary>監護人姓名：完整值或遮罩值（<c>王○明</c>）；會員刪除帳號後為 <c>null</c>（已清除）。</summary>
    public string? GuardianName { get; init; }

    /// <summary><c>parent</c>／<c>legal_guardian</c>。</summary>
    public string? Relationship { get; init; }

    /// <summary>日常中文：父母／法定監護人。</summary>
    public string? RelationshipLabel { get; init; }

    /// <summary>同意文案版本（文案本身待法務 B-9）；沒記錄為 <c>null</c>。</summary>
    public string? ConsentTextVersion { get; init; }
}

public sealed record AdminMemberDetailDto
{
    public required Guid Id { get; init; }
    public required string MemberNo { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }

    /// <summary>生日：完整值為 <c>yyyy-MM-dd</c>；遮罩時為 <c>****-**-**</c>。</summary>
    public string? BirthOn { get; init; }
    public required string SignupSource { get; init; }
    public required string SignupSourceLabel { get; init; }
    public required bool LineBound { get; init; }
    public required string Status { get; init; }
    public required string DisplayStatus { get; init; }
    public required string DisplayStatusLabel { get; init; }
    public string? Locale { get; init; }
    public string? LocaleLabel { get; init; }
    public DateTime? EmailVerifiedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public string? InternalNote { get; init; }

    /// <summary>這個帳號被合併到哪個帳號（會員編號）；沒有被合併為 <c>null</c>。</summary>
    public string? MergedIntoMemberNo { get; init; }

    /// <summary>監護人同意紀錄（未滿 18 歲註冊者才有；成年註冊為 <c>null</c>）。監護人姓名屬受限個資，沒有解除遮罩時是遮罩值。</summary>
    public AdminMemberGuardianConsentDto? GuardianConsent { get; init; }
    public required IReadOnlyList<AdminMemberMembershipDetailDto> Memberships { get; init; }
    public required IReadOnlyList<AdminMemberJerseyDto> JerseyIssues { get; init; }
    public required bool IsMasked { get; init; }

    /// <summary>目前這個帳號能不能解除遮罩（有沒有持有檢視完整個資的權限）。畫面用來決定是否顯示「顯示完整資料」按鈕。</summary>
    public required bool CanReveal { get; init; }
}

public sealed record CreateAdminMemberRequest
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public string? Phone { get; init; }
    public DateOnly? BirthOn { get; init; }

    /// <summary><c>zh-Hant</c>／<c>en</c>；省略為 <c>zh-Hant</c>。</summary>
    public string? Locale { get; init; }
    public string? InternalNote { get; init; }
}

public sealed record UpdateAdminMemberStatusRequest
{
    /// <summary><c>active</c>（啟用）或 <c>suspended</c>（停用）。</summary>
    public required string Status { get; init; }
    public string? Reason { get; init; }
}

public sealed record UpdateAdminMemberNoteRequest
{
    public string? InternalNote { get; init; }
}

public sealed record AdminMemberDuplicateMemberDto
{
    public required Guid Id { get; init; }
    public required string MemberNo { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required int MembershipCount { get; init; }
}

public sealed record AdminMemberDuplicateGroupDto
{
    /// <summary><c>phone</c>（同一支電話）或 <c>email</c>（Email 換個寫法其實是同一個信箱）。</summary>
    public required string MatchKind { get; init; }
    public required string MatchKindLabel { get; init; }
    public required IReadOnlyList<AdminMemberDuplicateMemberDto> Members { get; init; }
}

public sealed record MergeAdminMembersRequest
{
    /// <summary>保留的帳號。</summary>
    public required Guid TargetMemberId { get; init; }

    /// <summary>被合併的帳號：會籍、付款、報名、訂單、球衣、寄信紀錄轉到保留的帳號，本身變成「已刪除」。</summary>
    public required Guid SourceMemberId { get; init; }
}

public sealed record MergeAdminMembersResultDto
{
    public required Guid TargetMemberId { get; init; }
    public required string TargetMemberNo { get; init; }
    public required string SourceMemberNo { get; init; }
    public required int MovedMemberships { get; init; }
    public required int MovedRegistrations { get; init; }
    public required int MovedOrders { get; init; }
    public required int MovedJerseys { get; init; }
}
