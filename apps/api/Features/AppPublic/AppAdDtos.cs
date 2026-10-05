namespace Tcrfc.Api.Features.AppPublic;

/// <summary>公開投放回應的單一素材。沒有物件鍵欄位（只給完整網址）。<c>CreativeId</c> 為 <c>null</c> 表示這是備援素材——
/// 備援素材<b>不計曝光</b>（App 規劃書 §7.5），App 端的量測器對它根本不啟動。</summary>
public sealed record AppAdItemDto
{
    public Guid? CreativeId { get; init; }
    public Guid? CampaignId { get; init; }
    public required bool IsFallback { get; init; }
    /// <summary>圖片**主檔**網址（長邊 ≤2560 的 WebP）。</summary>
    public string? ImageUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }

    /// <summary>
    /// 圖片衍生檔網址（Android 缺口 C5，2026-10-05）：廣告素材與備援圖都走 §4.0 圖片上傳管線，**一定有** 320／640／1280 三個等比衍生檔
    /// （規則同 <c>shared/image-derivatives.json</c>）；這裡直接帶完整網址，用戶端依版位尺寸挑最小夠用的，不必自己推導。沒有圖片為 null。
    /// </summary>
    public AppAdImageVariantsDto? ImageVariants { get; init; }
    public string? VideoUrl { get; init; }
    public string? AltText { get; init; }
    public string? Title { get; init; }
    public string? CtaText { get; init; }
    public string? ClickUrl { get; init; }
    public string? Theme { get; init; }
}

/// <summary>廣告圖片的等比衍生檔網址（長邊 320／640／1280，原圖較小時以原圖尺寸為上限）。</summary>
public sealed record AppAdImageVariantsDto
{
    public required string Url320 { get; init; }
    public required string Url640 { get; init; }
    public required string Url1280 { get; init; }
}

/// <summary>預載清單中的一筆：素材加上它所屬檔期的有效期間與權重。</summary>
public sealed record AppAdPrefetchItemDto
{
    public required AppAdItemDto Item { get; init; }

    /// <summary>檔期起（UTC）。當日稍晚才開始的檔期也列出，用戶端在開始時間之前不得顯示。</summary>
    public required DateTime StartsAt { get; init; }

    /// <summary>檔期迄（UTC）。**檔期結束即清除**（App 規劃書 §2.4）：超過此時間就從本機刪除這個素材。</summary>
    public required DateTime EndsAt { get; init; }

    /// <summary>檔期權重（1–100），離線時同版位多個素材依權重加權隨機。</summary>
    public required int Weight { get; init; }
}

public sealed record AppAdPrefetchSlotDto
{
    public required string SlotCode { get; init; }
    public int? RotationCap { get; init; }
    public int? SessionImpressionCap { get; init; }

    /// <summary>此版位的備援素材（沒有任何檔期可顯示時用）；沒設定為 null。備援不計曝光。</summary>
    public AppAdItemDto? Fallback { get; init; }
    public required IReadOnlyList<AppAdPrefetchItemDto> Items { get; init; }
}

/// <summary>
/// 當日檔期素材清單（App 規劃書 §2.4「廣告素材：預先下載當日檔期素材，檔期結束即清除；離線不計曝光」）。
/// 🔴 這只是**預載目錄**，不是投放決策：每人頻次上限、每日曝光上限、曝光保證 pacing 都在 <c>GET /app/ads/{slotCode}</c> 即時判斷，
/// 這裡一律不套用。離線時的曝光事件須帶原始發生時間，伺服器拒收超過 24 小時的（§2.4 硬規則 1）。
/// </summary>
public sealed record AppAdPrefetchResponse
{
    public required DateTime GeneratedAt { get; init; }

    /// <summary>這份清單涵蓋到哪個時間（台北當日結束，UTC）；之後請重新取得。</summary>
    public required DateTime ValidUntil { get; init; }
    public required string DisclosureLabel { get; init; }
    public required IReadOnlyList<AppAdPrefetchSlotDto> Slots { get; init; }
}

public sealed record AppAdResponse
{
    public required string SlotCode { get; init; }

    /// <summary>沒有可投放的檔期，回的是備援素材（版位永不空白）。</summary>
    public required bool IsFallback { get; init; }

    /// <summary>每個版位必須有的揭露標示（規劃書 §7.9 第 1 點），依請求語系給「廣告」或「Ad」；App 必須顯示，不得誤導為編輯內容。</summary>
    public required string DisclosureLabel { get; init; }
    public int? SessionImpressionCap { get; init; }
    public required IReadOnlyList<AppAdItemDto> Items { get; init; }
}

public sealed record AppAdEventInput
{
    /// <summary><c>impression</c> 或 <c>click</c>。</summary>
    public required string Type { get; init; }
    public required Guid CreativeId { get; init; }

    /// <summary>事件發生時間（含時區的 ISO 8601）。以發生時間記錄，不是上傳時間；離線暫存的事件要帶原始時間。</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>每次素材裝載進版位的識別碼（UUID）；同一個 <c>presentationId</c> 的曝光只算一次。</summary>
    public string? PresentationId { get; init; }
}

public sealed record AppAdEventBatchRequest
{
    public string? BatchId { get; init; }
    public required string DeviceInstallId { get; init; }
    public required string Platform { get; init; }
    public string? AppVersion { get; init; }

    /// <summary><c>zh</c> 或 <c>en</c>。</summary>
    public string? Locale { get; init; }
    public required IReadOnlyList<AppAdEventInput> Events { get; init; }
}

public sealed record AppAdEventRejectionDto
{
    public required int Index { get; init; }

    /// <summary><c>invalid</c>／<c>too_old</c>／<c>future</c>／<c>unknown_creative</c>／<c>not_serving</c>。</summary>
    public required string Reason { get; init; }
}

public sealed record AppAdEventBatchResult
{
    public required int Accepted { get; init; }
    public required int Duplicates { get; init; }
    public required IReadOnlyList<AppAdEventRejectionDto> Rejected { get; init; }
}
