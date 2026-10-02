using System.Text.Json;

namespace Tcrfc.Api.CharityPlatform.Public;

// ═══════════════════════════════════════════════════════════════════════════
// 慈善捐款平台公開端點（不需要登入）的請求與回應形狀。
// 🔴 公開回應<b>絕不包含</b>：分潤百分比、店家與項目的撥付設定、捐款人完整姓名／Email、金流交易識別碼、
// 任何 *_encrypted 欄位、物件儲存的原始鍵（一律換成完整網址）。募款進度也不存在（規劃書 §3.2 v1.2：
// 無進度條、目標金額、已募得金額與捐款筆數）。
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>站台文案與前台需要的少量全站設定（N7 站台設定，規劃書 §6.7）。文案依 <c>?lang=</c> 回傳，英文缺漏回退繁中。</summary>
public sealed record PublicSettingsDto
{
    public required string? HomeIntro { get; init; }
    public required string? ThankYouTemplate { get; init; }
    public required string? Notice { get; init; }
    public required string? PrivacyPolicy { get; init; }

    /// <summary>全站預設單筆金額下限（項目沒有自己的下限時採用）。</summary>
    public required int DefaultMinAmount { get; init; }

    public required int DefaultMaxAmount { get; init; }

    /// <summary>徵信名單是否開放（規劃書 §3.6「可於後台整站關閉」）。</summary>
    public required bool CreditListEnabled { get; init; }

    /// <summary>任一文案欄位因目前語系缺漏而回退到繁中（前台據此標示「本頁尚無此語系版本」）。</summary>
    public required bool IsFallback { get; init; }
}

/// <summary>掃碼落地頁的店家識別。<c>store</c> 為 <c>null</c> 代表網址對不到<b>有效</b>的店家（不存在、已停止合作、
/// 不在合作期間）——視同無店家歸屬、落地頁降級成一般入口，<b>不得報錯中斷捐款流程</b>（規劃書 §2.2 第 5 點、§3.1）。
/// 不區分「不存在」與「已停止」，避免外人探測。</summary>
public sealed record PublicStoreLandingDto(PublicStoreDto? Store);

public sealed record PublicStoreDto
{
    public required string Slug { get; init; }
    public required string Name { get; init; }

    /// <summary>店家 Logo 或照片的完整網址；店家未上傳時為 <c>null</c>，前台降級為純文字店名區塊，不留空框（規劃書 §3.1）。</summary>
    public required string? LogoUrl { get; init; }

    public required string? LogoAlt { get; init; }
    public required bool IsFallback { get; init; }
}

/// <summary>項目卡片（掃碼落地頁與一般入口的卡片牆）。⛔ 不顯示募款進度。</summary>
public sealed record PublicProjectCardDto
{
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public required string? OneLiner { get; init; }
    public required string? CoverUrl { get; init; }
    public required string? CoverAlt { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsFallback { get; init; }
}

public sealed record PublicProjectDetailDto
{
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public required string? OneLiner { get; init; }

    /// <summary>說明內文（區塊編輯器輸出的 JSON，原樣回傳）；沒有內文為 <c>null</c>。</summary>
    public required JsonElement? Description { get; init; }

    /// <summary>款項用途（規劃書 §3.2 第 3 點）。</summary>
    public required string? FundUsage { get; init; }

    public required string? CoverUrl { get; init; }
    public required string? CoverAlt { get; init; }

    /// <summary>金額選項卡（由小到大）。另可自由輸入「其他金額」，範圍見 <see cref="MinAmount"/>／<see cref="MaxAmount"/>。</summary>
    public required IReadOnlyList<int> AmountOptions { get; init; }

    /// <summary>實際生效的單筆下限／上限（項目有設就用項目的，否則採全站預設）。</summary>
    public required int MinAmount { get; init; }

    public required int MaxAmount { get; init; }

    /// <summary>憑證模式：<c>b2c_invoice</c> 時表單要收發票類型與載具／統編／捐贈碼；<c>donation_receipt</c> 時收收據抬頭、身分證字號與地址。</summary>
    public required string InvoiceMode { get; init; }

    /// <summary>撥付對象的名稱快照與參照碼（值複製自主站，不即時查詢，規劃書 §3.2 第 4 點）。</summary>
    public required string? CharityName { get; init; }

    public required string? CharityProgramName { get; init; }
    public required string? CharityProgramRefCode { get; init; }
    public required bool IsFallback { get; init; }
}

/// <summary>建立捐款單的請求。<c>Idempotency-Key</c> 放在 HTTP 標頭（見端點）。</summary>
public sealed record CreateDonationRequest
{
    public string? ProjectSlug { get; init; }

    /// <summary>店家歸屬（掃碼落地頁帶來的 <c>store_slug</c>）。對不到有效店家時視同無店家歸屬、捐款照常成立（§2.2 第 4、5 點）。
    /// 網址與請求<b>都不得攜帶分潤參數</b>：分潤率永遠由伺服器依店家與項目的設定計算。</summary>
    public string? StoreSlug { get; init; }

    /// <summary>捐款金額（整數新台幣元）。</summary>
    public int Amount { get; init; }

    public string? DonorName { get; init; }
    public string? DonorEmail { get; init; }

    /// <summary>具名／匿名：具名者列於捐款徵信名單（僅顯示姓名）。</summary>
    public bool IsAnonymous { get; init; }

    /// <summary>個資同意（必勾）。</summary>
    public bool ConsentPrivacy { get; init; }

    /// <summary>發票／收據欄位，依項目的 <c>invoiceMode</c> 填寫對應那一組。</summary>
    public DonationInvoiceInput? Invoice { get; init; }

    /// <summary>前端語系（<c>zh</c>／<c>en</c>），決定付款返回網址的語系前綴。</summary>
    public string? Lang { get; init; }

    /// <summary>Turnstile 驗證權杖（尚未啟用時可省略，見 README 的待裁決）。</summary>
    public string? TurnstileToken { get; init; }
}

/// <summary>發票／收據欄位。<c>b2c_invoice</c>：<see cref="Type"/> 必填，並依類型填 <see cref="MobileCarrier"/>／<see cref="LoveCode"/>／
/// <see cref="TaxId"/>＋<see cref="InvoiceTitle"/>。<c>donation_receipt</c>：<see cref="ReceiptTitle"/>（預設帶入捐款人姓名）、
/// 選填 <see cref="NationalId"/>（會加密儲存）與 <see cref="Address"/>、<see cref="IsAnnualSummary"/>。</summary>
public sealed record DonationInvoiceInput
{
    /// <summary><c>mobile_carrier</c>／<c>love_code</c>／<c>tax_id</c>。</summary>
    public string? Type { get; init; }

    public string? MobileCarrier { get; init; }
    public string? LoveCode { get; init; }
    public string? TaxId { get; init; }
    public string? InvoiceTitle { get; init; }
    public string? ReceiptTitle { get; init; }
    public string? NationalId { get; init; }
    public string? Address { get; init; }
    public bool IsAnnualSummary { get; init; }
}

public sealed record StartPaymentRequest
{
    /// <summary>前端語系（<c>zh</c>／<c>en</c>），決定付款返回網址的語系前綴。</summary>
    public string? Lang { get; init; }
}

public sealed record ConfirmPaymentRequest
{
    /// <summary>LINE Pay 返回網址帶回的交易識別碼，必須與這張單最近一次發起的付款一致。</summary>
    public string? TransactionId { get; init; }
}

/// <summary>建立捐款單的回應。<see cref="Created"/> 為 <c>false</c> 表示這是同一個冪等鍵的重複請求（沿用原單，沒有重複建單）。</summary>
public sealed record CreateDonationResponse
{
    public required string OrderNo { get; init; }
    public required string Status { get; init; }
    public required int Amount { get; init; }
    public required bool Created { get; init; }
}

public sealed record StartPaymentResponse
{
    public required string OrderNo { get; init; }
    public required string Status { get; init; }

    /// <summary>要把使用者導向的付款網址。</summary>
    public required string PaymentUrl { get; init; }
}

/// <summary>
/// 結果頁資料（規劃書 §3.5）。<b>不含完整個資</b>：Email 與姓名一律遮罩。
/// <see cref="Status"/>：<c>created</c>（尚未發起付款）／<c>pending</c>／<c>paid</c>／<c>failed</c>／<c>expired</c>／<c>refunded</c>。
/// <see cref="Processing"/> 為 <c>true</c> 時前台顯示「處理中」並輪詢（付款結果尚在確認；文案不得讓人誤以為失敗而重複付款）。
/// </summary>
public sealed record PublicDonationResultDto
{
    public required string OrderNo { get; init; }
    public required string Status { get; init; }
    public required int Amount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime? PaidAt { get; init; }
    public required string ProjectSlug { get; init; }
    public required string ProjectName { get; init; }
    public required string? StoreName { get; init; }
    public required string DonorNameMasked { get; init; }
    public required string DonorEmailMasked { get; init; }

    /// <summary>憑證模式與開立狀態（<c>pending</c>／<c>issued</c>／<c>failed</c>）；<c>failed</c> 對外一律顯示成「處理中」，
    /// 失敗細節是協會內部的人工補開佇列，不嚇捐款人。</summary>
    public required string InvoiceMode { get; init; }

    public required string InvoiceStatus { get; init; }

    /// <summary>已開立時的憑證號碼。</summary>
    public required string? InvoiceNo { get; init; }

    /// <summary>付款結果仍在確認中（<c>pending</c>，含「已送出確認但尚無定論」的待人工處理單）。</summary>
    public required bool Processing { get; init; }

    /// <summary>可以重試付款（<c>created</c>／<c>failed</c>／<c>expired</c>）：重試沿用原單，不重新建單、不要求重填（規劃書 §3.3、§4.3）。</summary>
    public required bool CanRetry { get; init; }
}

/// <summary>
/// 捐款徵信名單（規劃書 §3.6）。🔴 <b>只有姓名</b>：不含金額、Email、店家、單號、時間——連「這個人捐了幾次」都不能被推出來
/// （同名去重，順序依姓名排序而非時間）。只列捐款人在捐款表單明示選擇「具名」、且後台沒有逐筆隱藏、且付款成立（<c>paid</c>）的捐款。
/// <c>enabled = false</c>（後台整站關閉）時不回傳任何名單。
/// </summary>
public sealed record PublicCreditListDto
{
    public required bool Enabled { get; init; }
    public required IReadOnlyList<string> Names { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
}

/// <summary>成果回顧頁（規劃書 §2.1 <c>/{lang}/impact/</c>）：已上架項目關聯的慈善計畫摘要，並導回主站。
/// 🔴 計畫名稱與公益團體名稱是<b>快照</b>（不即時查主站、不做 join，規劃書 §9.3）；成果數據與故事在主站，這裡只負責導流。</summary>
public sealed record PublicImpactDto
{
    /// <summary>俱樂部官網網址（後台 N7 設定）；未設定時為 <c>null</c>，前台不顯示導回連結。</summary>
    public required string? ClubSiteUrl { get; init; }

    public required IReadOnlyList<PublicImpactProgramDto> Programs { get; init; }
    public required bool IsFallback { get; init; }
}

public sealed record PublicImpactProgramDto
{
    /// <summary>慈善計畫的參照碼（主站 <c>CharityProgram</c>）；項目只指定公益團體、沒有指定計畫時為 <c>null</c>。</summary>
    public required string? ProgramRefCode { get; init; }

    public required string? ProgramName { get; init; }
    public required string? CharityName { get; init; }
    public required IReadOnlyList<PublicImpactProjectDto> Projects { get; init; }
}

public sealed record PublicImpactProjectDto
{
    public required string Slug { get; init; }
    public required string Name { get; init; }
    public required string? OneLiner { get; init; }
    public required string? CoverUrl { get; init; }
    public required string? CoverAlt { get; init; }
}
