namespace Tcrfc.Api.Features.MemberCenter;

/// <summary>俱樂部識別（會員卡卡面與會籍列表用）。標誌與品牌色已不由後端提供（主站規劃書 v3.20、App 規劃書 v3.16：App 內建兩隊標誌與品牌色，以 <c>Code</c> 對應）。</summary>
public sealed record MemberClubBrandDto
{
    public required string Code { get; init; }
    public required string Name { get; init; }
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

    /// <summary>
    /// 伺服器產生這份回應的時刻（UTC，帶 <c>Z</c>；2026-10-05，缺口 B3）。行動 App 用它記「最後同步時間」並算會員卡 7 天提醒
    /// （App 規劃書 §4：取牆鐘差與單調時鐘差較大者），**不依賴 HTTP <c>Date</c> 標頭**（標頭可能被代理改寫或快取，也可能缺）。
    /// 會員卡端點永不快取（docs/14：會員卡驗證／會籍狀態不得讀快取），所以這個時刻就是「這張卡的有效狀態被確認的時刻」。
    /// <see cref="IsValid"/>／<see cref="Status"/> 是以這個時刻判定的結果。
    /// </summary>
    public required DateTime ServerTime { get; init; }
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

    /// <summary>報名狀態，中文字面值（<c>待確認</c>／<c>已確認</c>／<c>已繳費</c>／<c>完成</c>／<c>取消</c>／<c>候補</c>，相容保留；新用戶端請改用 <see cref="StatusCode"/>）。</summary>
    public required string Status { get; init; }

    /// <summary>穩定代碼：<c>pending</c>／<c>confirmed</c>／<c>paid</c>／<c>completed</c>／<c>cancelled</c>／<c>waitlisted</c>（<c>shared/enums.json</c>）。
    /// <b>繳費狀態就在這裡</b>：報名沒有獨立的付款資料表（課程報名不走線上金流，由後台確認款項後把狀態改成 <c>paid</c>），所以 <c>paid</c>／<c>completed</c> 代表已繳費；
    /// 應繳金額見 <see cref="MemberRegistrationCourseDto.Price"/>。</summary>
    public required string StatusCode { get; init; }
    public required string StatusLabelZh { get; init; }
    public required string StatusLabelEn { get; init; }
    public required string ApplicantName { get; init; }
    public Guid? SessionId { get; init; }
    public Guid? TrialId { get; init; }
    public required DateTime CreatedAt { get; init; }

    /// <summary><c>session</c>（課程梯次）或 <c>trial</c>（試訓）。</summary>
    public required string Kind { get; init; }

    /// <summary>課程梯次報名的課程摘要；試訓報名為 null。</summary>
    public MemberRegistrationCourseDto? Course { get; init; }

    /// <summary>試訓報名的場次摘要；課程報名為 null。</summary>
    public MemberRegistrationTrialDto? Trial { get; init; }

    /// <summary>未翻譯標示：請求英文而課程名稱（試訓為球隊名稱）沒有英文版，摘要裡的名稱是回退的繁中。</summary>
    public required bool IsFallbackLocale { get; init; }
}

/// <summary>我的報名裡的課程摘要（App 規劃書 §3.9、§9.2「我的報名」）。只用既有欄位：課程名稱、梯次期間與每週時段、場地、價格、梯次狀態。</summary>
public sealed record MemberRegistrationCourseDto
{
    public required string ProgramSlug { get; init; }
    public string? ProgramName { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }

    /// <summary>每週上課時段（JSON 文字，同 <c>ProgramSessionDto.weeklySchedule</c>）。</summary>
    public string? WeeklySchedule { get; init; }
    public string? VenueName { get; init; }
    public string? VenueAddress { get; init; }

    /// <summary>梯次定價（新台幣元）；沒定價為 null。早鳥價與期限一併帶出，用戶端自行判斷顯示。</summary>
    public int? Price { get; init; }
    public int? EarlyBirdPrice { get; init; }
    public DateOnly? EarlyBirdUntil { get; init; }
    public required string SessionStatusCode { get; init; }
    public required string SessionStatusLabelZh { get; init; }
    public required string SessionStatusLabelEn { get; init; }
}

public sealed record MemberRegistrationTrialDto
{
    public required DateOnly TrialOn { get; init; }
    public string? TeamName { get; init; }
    public string? VenueName { get; init; }
    public string? VenueAddress { get; init; }
    public required string TrialStatusCode { get; init; }
    public required string TrialStatusLabelZh { get; init; }
    public required string TrialStatusLabelEn { get; init; }
}
