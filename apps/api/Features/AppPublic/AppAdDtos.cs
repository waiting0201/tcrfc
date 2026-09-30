namespace Tcrfc.Api.Features.AppPublic;

/// <summary>公開投放回應的單一素材。沒有物件鍵欄位（只給完整網址）。<c>CreativeId</c> 為 <c>null</c> 表示這是備援素材——
/// 備援素材<b>不計曝光</b>（App 規劃書 §7.5），App 端的量測器對它根本不啟動。</summary>
public sealed record AppAdItemDto
{
    public Guid? CreativeId { get; init; }
    public Guid? CampaignId { get; init; }
    public required bool IsFallback { get; init; }
    public string? ImageUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public string? VideoUrl { get; init; }
    public string? AltText { get; init; }
    public string? Title { get; init; }
    public string? CtaText { get; init; }
    public string? ClickUrl { get; init; }
    public string? Theme { get; init; }
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
