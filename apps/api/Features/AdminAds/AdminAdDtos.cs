namespace Tcrfc.Api.Features.AdminAds;

// ═════════ E4 版位 ═════════

public sealed record AdSlotLocaleContent
{
    public required string Name { get; init; }
    public string? FallbackAlt { get; init; }
}

public sealed record AdSlotContentInput
{
    public required AdSlotLocaleContent Zh { get; init; }
    public AdSlotLocaleContent? En { get; init; }
}

/// <summary>建立／更新版位（multipart 的 payload；備援素材圖走檔案欄位 <c>fallbackImage</c>）。版位代號建立後不能修改。</summary>
public sealed record UpsertAdminAdSlotRequest
{
    public required string SlotCode { get; init; }
    public string? ScreenCode { get; init; }
    public int? BlockOrder { get; init; }
    public string? AspectRatio { get; init; }
    public int? MinWidth { get; init; }
    public int? MinHeight { get; init; }
    public int? MaxFileKb { get; init; }
    public string? AllowedFormats { get; init; }
    public bool AllowVideo { get; init; }
    public int? SessionImpressionCap { get; init; }
    public int? RotationCap { get; init; }
    public string? FallbackLink { get; init; }
    public bool IsActive { get; init; } = true;
    public bool RemoveFallbackImage { get; init; }
    public required AdSlotContentInput Content { get; init; }
}

public sealed record AdminAdSlotDto
{
    public required Guid Id { get; init; }
    public required string SlotCode { get; init; }
    public required string Surface { get; init; }
    public string? ScreenCode { get; init; }
    public int? BlockOrder { get; init; }
    public string? AspectRatio { get; init; }
    public int? MinWidth { get; init; }
    public int? MinHeight { get; init; }
    public int? MaxFileKb { get; init; }
    public string? AllowedFormats { get; init; }
    public required bool AllowVideo { get; init; }
    public int? SessionImpressionCap { get; init; }
    public required int RotationCap { get; init; }
    public string? FallbackImageKey { get; init; }
    public string? FallbackImageUrl { get; init; }
    public string? FallbackImageThumbUrl { get; init; }
    public string? FallbackLink { get; init; }
    public required bool IsActive { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? FallbackAltZh { get; init; }
    public string? FallbackAltEn { get; init; }
    public required int CampaignCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

// ═════════ E4 廣告主 ═════════

public sealed record AdvertiserLocaleContent
{
    public required string Name { get; init; }
}

public sealed record AdvertiserContentInput
{
    public required AdvertiserLocaleContent Zh { get; init; }
    public AdvertiserLocaleContent? En { get; init; }
}

public sealed record UpsertAdminAdvertiserRequest
{
    public string? TaxId { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContractNote { get; init; }
    public DateOnly? CooperationStartOn { get; init; }
    public DateOnly? CooperationEndOn { get; init; }

    /// <summary>可為空：指向既有贊助商，避免重複維護聯絡窗口；不是把兩者合併成一筆。</summary>
    public Guid? SponsorId { get; init; }
    public string? Status { get; init; }
    public required AdvertiserContentInput Content { get; init; }
}

public sealed record AdminAdvertiserDto
{
    public required Guid Id { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? TaxId { get; init; }
    public string? ContactName { get; init; }
    public string? ContactPhone { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContractNote { get; init; }
    public DateOnly? CooperationStartOn { get; init; }
    public DateOnly? CooperationEndOn { get; init; }
    public Guid? SponsorId { get; init; }
    public string? SponsorName { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required int CampaignCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminSponsorOptionDto
{
    public required Guid Id { get; init; }
    public string? Name { get; init; }
    public required string ClubCode { get; init; }
}

// ═════════ E5 檔期與素材 ═════════

public sealed record UpsertAdminAdCampaignRequest
{
    public required Guid AdvertiserId { get; init; }
    public required Guid SlotId { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset StartsAt { get; init; }
    public required DateTimeOffset EndsAt { get; init; }
    public int? Weight { get; init; }
    public int? DailyImpressionCap { get; init; }
    public int? PerDeviceDailyCap { get; init; }
    public string? GoalType { get; init; }
    public int? GoalImpressions { get; init; }

    /// <summary>合約金額（元）。須有「編輯合約金額」權限才能寫入；沒有權限的角色送非空值會被 403。</summary>
    public int? ContractAmount { get; init; }
    public bool? IsAmountHidden { get; init; }
}

public sealed record AdminAdCampaignListItemDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid AdvertiserId { get; init; }
    public string? AdvertiserName { get; init; }
    public required Guid SlotId { get; init; }
    public required string SlotCode { get; init; }
    public string? SlotName { get; init; }
    public required DateTime StartsAt { get; init; }
    public required DateTime EndsAt { get; init; }
    public required int Weight { get; init; }
    public required string GoalType { get; init; }
    public required string GoalTypeLabel { get; init; }
    public int? GoalImpressions { get; init; }
    public required int DeliveredTotal { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required int CreativeCount { get; init; }
    public required int ApprovedCreativeCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>曝光保證型的進度：目標 vs 已達成，以及依檔期天數平均分配的「應達成」量（pacing）。</summary>
public sealed record AdminAdPacingDto
{
    public required int GoalImpressions { get; init; }
    public required int Delivered { get; init; }
    public required int ExpectedByNow { get; init; }
    public required int DailyTarget { get; init; }

    /// <summary><c>ahead</c>（超前）／<c>on_track</c>（正常）／<c>behind</c>（落後）。</summary>
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
}

public sealed record AdminAdCampaignDetailDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid AdvertiserId { get; init; }
    public string? AdvertiserName { get; init; }
    public required Guid SlotId { get; init; }
    public required string SlotCode { get; init; }
    public string? SlotName { get; init; }
    public required DateTime StartsAt { get; init; }
    public required DateTime EndsAt { get; init; }
    public required int Weight { get; init; }
    public int? DailyImpressionCap { get; init; }
    public int? PerDeviceDailyCap { get; init; }
    public required string GoalType { get; init; }
    public required string GoalTypeLabel { get; init; }
    public int? GoalImpressions { get; init; }
    public required int DeliveredToday { get; init; }
    public required int DeliveredTotal { get; init; }

    /// <summary>合約金額；沒有「檢視合約金額」權限時為 <c>null</c>，畫面顯示「不公開」（<see cref="ContractAmountLabel"/>）。</summary>
    public int? ContractAmount { get; init; }
    public required string ContractAmountLabel { get; init; }
    public bool? IsAmountHidden { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? PauseReason { get; init; }
    public Guid? ReviewedBy { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public required IReadOnlyList<string> AvailableActions { get; init; }
    public AdminAdPacingDto? Pacing { get; init; }
    public required IReadOnlyList<AdminAdCreativeDto> Creatives { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdCampaignReasonRequest
{
    public string? Reason { get; init; }
}

public sealed record AdminAdScheduleItemDto
{
    public required Guid CampaignId { get; init; }
    public required string Name { get; init; }
    public string? AdvertiserName { get; init; }
    public required DateTime StartsAt { get; init; }
    public required DateTime EndsAt { get; init; }
    public required int Weight { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }

    /// <summary>在「同時投放的檔期」之中的權重佔比（百分比，取整數）；只有檔期期間內的輪播會依這個比例加權隨機。</summary>
    public required int WeightSharePercent { get; init; }
}

public sealed record AdminAdScheduleDto
{
    public required Guid SlotId { get; init; }
    public required string SlotCode { get; init; }
    public required int RotationCap { get; init; }

    /// <summary>期間內同時最多有幾個檔期在輪播；超過版位的輪播張數上限時 <see cref="ExceedsRotationCap"/> 為 true（只是提示，不擋存檔）。</summary>
    public required int MaxConcurrent { get; init; }
    public required bool ExceedsRotationCap { get; init; }
    public required IReadOnlyList<AdminAdScheduleItemDto> Items { get; init; }
}

public sealed record AdCreativeLocaleInput
{
    /// <summary>對外語系代碼：<c>zh</c> 或 <c>en</c>。</summary>
    public required string Locale { get; init; }
}

/// <summary>建立／更新素材（multipart 的 payload；圖走檔案欄位 <c>image</c>，影片走 <c>video</c>——影片素材仍須附一張海報圖）。</summary>
public sealed record UpsertAdminAdCreativeRequest
{
    public required string Locale { get; init; }
    public string? AltText { get; init; }
    public string? Title { get; init; }
    public string? CtaText { get; init; }
    public string? ClickUrl { get; init; }
    public string? Theme { get; init; }
    public string? VariantTag { get; init; }
    public bool RemoveVideo { get; init; }
}

public sealed record AdminAdCreativeDto
{
    public required Guid Id { get; init; }
    public required Guid CampaignId { get; init; }
    public required string Locale { get; init; }
    public string? ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public string? VideoKey { get; init; }
    public string? VideoUrl { get; init; }
    public string? AltText { get; init; }
    public string? Title { get; init; }
    public string? CtaText { get; init; }
    public string? ClickUrl { get; init; }
    public required string Theme { get; init; }
    public required string ThemeLabel { get; init; }
    public string? VariantTag { get; init; }
    public required string ReviewStatus { get; init; }
    public required string ReviewStatusLabel { get; init; }
    public string? RejectReason { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public required bool IsPaused { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

// ═════════ E6 報表 ═════════

public sealed record AdReportQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? CampaignId { get; init; }
    public Guid? SlotId { get; init; }
    public Guid? CreativeId { get; init; }
    public string? Platform { get; init; }

    /// <summary>對外語系代碼 <c>zh</c>／<c>en</c>。</summary>
    public string? Locale { get; init; }

    /// <summary>彙總維度：<c>campaign</c>（預設）／<c>slot</c>／<c>creative</c>／<c>platform</c>／<c>locale</c>／<c>date</c>。</summary>
    public string? GroupBy { get; init; }
    public string? Purpose { get; init; }
}

public sealed record AdminAdReportRowDto
{
    /// <summary>該列的維度值（檔期／版位／素材名稱、平台、語系或日期），畫面直接顯示。</summary>
    public required string Label { get; init; }
    public Guid? Id { get; init; }
    public required long Impressions { get; init; }
    public required long Clicks { get; init; }

    /// <summary>點擊率（百分比，兩位小數）；曝光為 0 時為 0。</summary>
    public required decimal Ctr { get; init; }

    /// <summary>不重複裝置數。⚠️ 跨多日彙總時是「每日不重複裝置數的加總」（裝置日），不是期間內的真正不重複人數——
    /// 原始事件只保存 90 天且不跨日去重，日聚合無法還原（見 README 已知限制）。</summary>
    public required long UniqueDevices { get; init; }
}

public sealed record AdminAdReportDto
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }
    public required string GroupBy { get; init; }
    public required IReadOnlyList<AdminAdReportRowDto> Rows { get; init; }
    public required AdminAdReportRowDto Total { get; init; }

    /// <summary>期間內尚未聚合的原始事件數（>0 表示報表數字還沒包含它們，可請系統管理員執行「重新彙整」）。</summary>
    public required int PendingEvents { get; init; }
    public required IReadOnlyList<AdminAdPacingRowDto> Pacing { get; init; }
}

public sealed record AdminAdPacingRowDto
{
    public required Guid CampaignId { get; init; }
    public required string CampaignName { get; init; }
    public required AdminAdPacingDto Pacing { get; init; }
}

public sealed record AdMaintenanceResultDto
{
    public required int CampaignsStarted { get; init; }
    public required int CampaignsEnded { get; init; }
    public required int EventsAggregated { get; init; }
    public required int DaysRebuilt { get; init; }
    public required int EventsPurged { get; init; }
    public required int DiagnosticsPurged { get; init; }

    /// <summary>超過 2 天仍未聚合的事件數（聚合失敗須告警，不得靜默跳過）。</summary>
    public required int OverdueUnaggregated { get; init; }
}
