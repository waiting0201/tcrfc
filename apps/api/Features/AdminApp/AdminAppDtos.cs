using Tcrfc.Api.Features.AppPublic;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>M1／M5 寫入回應的統一外殼：<c>Value</c> 是儲存後的資料，<c>EdgePublish</c> 說明「設定有沒有同步到 Cloudflare 靜態設定」
/// （docs/19 §7 第 1 層來源；尚未串接時 <c>published=false</c> 並附說明——存檔本身一律成功）。</summary>
public sealed record AppConfigChangeDto<T>
{
    public required T Value { get; init; }
    public required AppConfigPublishResult EdgePublish { get; init; }
}

// ═════════ M1 版本發布 ═════════

public sealed record ReleaseLocaleContent
{
    /// <summary>版本更新說明（What's New）。</summary>
    public string? WhatsNew { get; init; }

    /// <summary>強制更新提示文案（低於最低支援版本時顯示）。</summary>
    public string? ForceMessage { get; init; }

    /// <summary>建議更新提示文案（低於建議版本時顯示，可略過）。</summary>
    public string? RecommendMessage { get; init; }
}

public sealed record ReleaseContentInput
{
    public required ReleaseLocaleContent Zh { get; init; }
    public ReleaseLocaleContent? En { get; init; }
}

public sealed record UpsertAdminAppReleaseRequest
{
    /// <summary><c>ios</c> 或 <c>android</c>。</summary>
    public required string Platform { get; init; }

    /// <summary>版本號 <c>主.次.修</c>，建置號不參與比較。</summary>
    public required string Version { get; init; }
    public string? BuildNumber { get; init; }
    public DateOnly? ReleasedOn { get; init; }

    /// <summary><c>testing</c>（測試）／<c>live</c>（已上架）／<c>withdrawn</c>（已下架）。</summary>
    public string? Status { get; init; }
    public required ReleaseContentInput Content { get; init; }
}

public sealed record AdminAppReleaseDto
{
    public required Guid Id { get; init; }
    public required string Platform { get; init; }
    public required string PlatformLabel { get; init; }
    public required string Version { get; init; }
    public string? BuildNumber { get; init; }
    public DateOnly? ReleasedOn { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required bool IsMinSupported { get; init; }
    public required bool IsRecommended { get; init; }
    public ReleaseLocaleContent? Zh { get; init; }
    public ReleaseLocaleContent? En { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>設定「最低支援版本」與「建議版本」旗標。每個平台各至多一筆為 true（設定新的會自動取消舊的）。
/// 🔴 <c>IsMinSupported=true</c> 會讓低於這個版本的全部使用者被強制更新，必須帶 <c>ConfirmForceUpdate=true</c>（二次確認）。</summary>
public sealed record SetReleaseFlagsRequest
{
    public bool IsMinSupported { get; init; }
    public bool IsRecommended { get; init; }
    public bool ConfirmForceUpdate { get; init; }
}

public sealed record AdminAppMaintenanceDto
{
    /// <summary><c>all</c>（全域）／<c>ios</c>／<c>android</c>。</summary>
    public required string Scope { get; init; }
    public required string ScopeLabel { get; init; }
    public required bool Enabled { get; init; }
    public string? MessageZh { get; init; }
    public string? MessageEn { get; init; }
}

public sealed record SetAppMaintenanceRequest
{
    public bool Enabled { get; init; }
    public string? MessageZh { get; init; }
    public string? MessageEn { get; init; }
}

// ═════════ M2 內容編排 ═════════

public sealed record AppLabelInput
{
    public required string Zh { get; init; }
    public string? En { get; init; }
}

public sealed record UpsertAdminAppLayoutItemRequest
{
    /// <summary>建立時必填：<c>quick_entry</c>（快捷入口）或 <c>more_item</c>（「更多」分頁項目）。首頁區塊是固定的九個，不能新增。</summary>
    public string? Kind { get; init; }

    /// <summary>建立時必填：識別代碼（小寫英文、數字、底線）。</summary>
    public string? ItemKey { get; init; }
    public Guid? DeepLinkId { get; init; }
    public string? IconKey { get; init; }
    public bool IsEnabled { get; init; } = true;
    public required AppLabelInput Label { get; init; }
}

public sealed record AdminAppLayoutItemDto
{
    public required Guid Id { get; init; }
    public required string Kind { get; init; }
    public required string KindLabel { get; init; }
    public required string ItemKey { get; init; }
    public Guid? DeepLinkId { get; init; }
    public string? DeepLinkCode { get; init; }
    public string? IconKey { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsEnabled { get; init; }

    /// <summary>首頁區塊是固定的，不能刪除也不能改代碼。</summary>
    public required bool IsFixed { get; init; }
    public string? LabelZh { get; init; }
    public string? LabelEn { get; init; }
}

public sealed record ReorderAppLayoutRequest
{
    public required string Kind { get; init; }
    public required IReadOnlyList<Guid> Ids { get; init; }
}

public sealed record UpsertAdminAppDeepLinkRequest
{
    public required string Code { get; init; }
    public required string AppLink { get; init; }
    public string? WebUrl { get; init; }
    public bool RequiresLogin { get; init; }
    public bool IsActive { get; init; } = true;
    public required AppLabelInput Label { get; init; }
}

public sealed record AdminAppDeepLinkDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string AppLink { get; init; }
    public string? WebUrl { get; init; }
    public required bool RequiresLogin { get; init; }
    public required bool IsActive { get; init; }
    public required int SortOrder { get; init; }
    public string? LabelZh { get; init; }
    public string? LabelEn { get; init; }
    public required int UsedByCount { get; init; }
}

public sealed record UpsertAdminAppAnnouncementRequest
{
    public string? LinkUrl { get; init; }
    public DateTimeOffset? StartsAt { get; init; }
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>目標對象：<c>all</c>／<c>fan_club</c>／<c>registered</c>／<c>anonymous</c>。</summary>
    public string? AudienceTier { get; init; }

    /// <summary>俱樂部代碼（<c>tcrfc</c>／<c>bw</c>），空＝兩隊。</summary>
    public string? AudienceClubCode { get; init; }
    public bool IsEnabled { get; init; } = true;
    public required AppLabelInput Message { get; init; }
}

public sealed record AdminAppAnnouncementDto
{
    public required Guid Id { get; init; }
    public string? MessageZh { get; init; }
    public string? MessageEn { get; init; }
    public string? LinkUrl { get; init; }
    public DateTime? StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public required string AudienceTier { get; init; }
    public required string AudienceTierLabel { get; init; }
    public string? AudienceClubCode { get; init; }
    public required bool IsEnabled { get; init; }

    /// <summary>目前是否正在顯示（啟用且在顯示期間內）。</summary>
    public required bool IsActiveNow { get; init; }
}

// ═════════ M3 推播 ═════════

public sealed record PushLocaleContent
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? ImageAlt { get; init; }
}

public sealed record PushContentInput
{
    public required PushLocaleContent Zh { get; init; }

    /// <summary>推播文案須雙語（規劃書 §10.4）；英文可留空但欄位必須存在，英文語系裝置會收到繁中。</summary>
    public PushLocaleContent? En { get; init; }
}

/// <summary>建立／更新推播批次（multipart 的 payload；圖片走檔案欄位 <c>image</c>）。</summary>
public sealed record UpsertAdminPushMessageRequest
{
    /// <summary><c>announcement</c>（一般公告，預設）／<c>news</c>／<c>match</c>。</summary>
    public string? Kind { get; init; }

    /// <summary>深連結（<c>tcrfc://…</c>）或官網網址；可用深連結對照表的項目。</summary>
    public string? DeepLink { get; init; }

    /// <summary>會籍層級：<c>all</c>／<c>fan_club</c>／<c>registered</c>／<c>anonymous</c>。</summary>
    public string? AudienceTier { get; init; }

    /// <summary>俱樂部歸屬（代碼），空＝全部。用於一般公告，<b>不得用於商業訊息的差別投放</b>。</summary>
    public string? AudienceClubCode { get; init; }

    /// <summary>追蹤球隊（球隊代碼）；只有「推播開啟」的追蹤才算。空＝不限。</summary>
    public IReadOnlyList<string>? AudienceTeamCodes { get; init; }
    public DateTimeOffset? ScheduledAt { get; init; }
    public bool RemoveImage { get; init; }
    public required PushContentInput Content { get; init; }
}

public sealed record PushAudienceRequest
{
    public string? AudienceTier { get; init; }
    public string? AudienceClubCode { get; init; }
    public IReadOnlyList<string>? AudienceTeamCodes { get; init; }
}

public sealed record AdminPushAudienceEstimateDto
{
    /// <summary>可實際送出的裝置數（權杖有效、推播權限已允許、符合分眾條件）。只回人數，不寫入、不回傳裝置清單。</summary>
    public required int Total { get; init; }
    public required IReadOnlyList<AdminPushStatRowDto> Breakdown { get; init; }
}

public sealed record AdminPushStatRowDto
{
    public required string Platform { get; init; }
    public required string PlatformLabel { get; init; }
    public required string Locale { get; init; }
    public required string LocaleLabel { get; init; }
    public required int Sent { get; init; }
    public required int Delivered { get; init; }
    public required int Opened { get; init; }
}

public sealed record AdminPushMessageListItemDto
{
    public required Guid Id { get; init; }
    public string? TitleZh { get; init; }
    public required string Kind { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public DateTime? SentAt { get; init; }
    public required int SentCount { get; init; }
    public required int DeliveredCount { get; init; }
    public required int OpenedCount { get; init; }
    public Guid? CreatedBy { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record AdminPushMessageDto
{
    public required Guid Id { get; init; }
    public required string Kind { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required PushContentInput Content { get; init; }
    public string? ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? DeepLink { get; init; }
    public required string AudienceTier { get; init; }
    public required string AudienceTierLabel { get; init; }
    public string? AudienceClubCode { get; init; }
    public required IReadOnlyList<string> AudienceTeamCodes { get; init; }
    public DateTime? ScheduledAt { get; init; }
    public int? AudienceEstimate { get; init; }
    public Guid? CreatedBy { get; init; }
    public Guid? ReviewedBy { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? RejectNote { get; init; }
    public DateTime? SentAt { get; init; }
    public required int SentCount { get; init; }
    public required int DeliveredCount { get; init; }
    public required int FailedCount { get; init; }
    public required int OpenedCount { get; init; }
    public string? FailureMessage { get; init; }
    public required IReadOnlyList<AdminPushStatRowDto> Stats { get; init; }

    /// <summary>三個數字的語意說明（規劃書 §6.6）：畫面必須照這個措辭呈現，不得讓「送達」被讀成送到使用者手上。</summary>
    public required string StatsNote { get; init; }
    public required IReadOnlyList<string> AvailableActions { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record ApprovePushMessageRequest
{
    /// <summary>二次確認：操作者在畫面上看到的「預估觸及裝置數」。與伺服器當下重新計算的人數不同時拒絕核可（分眾在核可前變動了），須重新確認。</summary>
    public required int ExpectedAudience { get; init; }
}

public sealed record PushNoteRequest
{
    public string? Note { get; init; }
}

public sealed record PushTestSendRequest
{
    /// <summary>指定測試裝置（裝置識別碼）。</summary>
    public required IReadOnlyList<string> DeviceInstallIds { get; init; }
}

public sealed record AdminPushPreviewDto
{
    public required PushLocaleContent Zh { get; init; }
    public PushLocaleContent? En { get; init; }

    /// <summary>沒有英文文案時，英文語系的裝置實際會看到的內容（回退繁中）。</summary>
    public required PushLocaleContent EnEffective { get; init; }
    public string? DeepLink { get; init; }
    public string? ImageUrl { get; init; }
    public required string AudienceSummary { get; init; }
}

public sealed record AdminPushTestSendResultDto
{
    public required bool Configured { get; init; }
    public required int Sent { get; init; }
    public required int Failed { get; init; }
    public required int UnknownDevices { get; init; }
    public required string Message { get; init; }
}

/// <summary>自動推播規則（規劃書 §6.2、§8.3）：賽事提醒提前小時數、會籍到期提醒天數與各類自動推播的開關。這裡只保存規則；觸發（排程掃描賽事與到期）屬 App 開發階段。</summary>
public sealed record AdminPushRulesDto
{
    public required int MatchReminderHours { get; init; }
    public required IReadOnlyList<int> MembershipExpiryDays { get; init; }
    public required IReadOnlyList<AdminPushRuleToggleDto> Toggles { get; init; }
}

public sealed record AdminPushRuleToggleDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required bool Enabled { get; init; }
}

public sealed record UpdateAdminPushRulesRequest
{
    public int? MatchReminderHours { get; init; }
    public IReadOnlyList<int>? MembershipExpiryDays { get; init; }

    /// <summary>逐項開關（鍵見 <see cref="AdminPushRulesDto.Toggles"/>）；沒列出的維持原值。</summary>
    public IReadOnlyDictionary<string, bool>? Toggles { get; init; }
}

// ═════════ M4 裝置 ═════════

public sealed record AdminAppDeviceListItemDto
{
    public required Guid Id { get; init; }

    /// <summary>裝置識別碼（遮罩，只留前 4 碼與後 2 碼）；完整值須「檢視完整值」權限。</summary>
    public required string DeviceInstallIdMasked { get; init; }
    public required string Platform { get; init; }
    public required string PlatformLabel { get; init; }
    public string? OsVersion { get; init; }
    public string? AppVersion { get; init; }
    public string? Locale { get; init; }
    public string? LocaleLabel { get; init; }
    public required string PushPermission { get; init; }
    public required string PushPermissionLabel { get; init; }
    public required string PushTokenStatus { get; init; }
    public required string PushTokenStatusLabel { get; init; }
    public required bool IsMemberBound { get; init; }
    public required DateTime FirstSeenAt { get; init; }
    public required DateTime LastActiveAt { get; init; }
}

public sealed record AdminAppDeviceDetailDto
{
    public required AdminAppDeviceListItemDto Device { get; init; }

    /// <summary>只有 <c>reveal=true</c> 且有權限時才有值。</summary>
    public string? DeviceInstallId { get; init; }
    public string? PushToken { get; init; }
    public required int SubscriptionCount { get; init; }
    public required bool Revealed { get; init; }
}

public sealed record AdminAppDeviceStatsDto
{
    public required int TotalDevices { get; init; }
    public required int ActiveLast7Days { get; init; }
    public required int ActiveLast30Days { get; init; }
    public required int InvalidTokenCount { get; init; }
    public required IReadOnlyList<AdminAppDeviceVersionRowDto> ByVersion { get; init; }
    public required IReadOnlyList<AdminAppDeviceCountRowDto> ByPermission { get; init; }

    /// <summary>帶了 <c>platform</c> 與 <c>belowVersion</c> 時：低於該版本的裝置數（供決定最低支援版本）。</summary>
    public int? DevicesBelowVersion { get; init; }
}

public sealed record AdminAppDeviceVersionRowDto
{
    public required string Platform { get; init; }
    public required string PlatformLabel { get; init; }
    public string? AppVersion { get; init; }
    public required int Count { get; init; }
}

public sealed record AdminAppDeviceCountRowDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required int Count { get; init; }
}

public sealed record AdminAppDeviceCleanupResultDto
{
    public required int TokensCleared { get; init; }
}

// ═════════ M5 設定、憑證、診斷 ═════════

public sealed record UpsertAdminAppFeatureFlagRequest
{
    /// <summary>命名 <c>{模組}_{功能}</c> 小寫蛇形，例如 <c>ads_enabled</c>。</summary>
    public required string FlagKey { get; init; }
    public bool IsEnabled { get; init; }

    /// <summary>三態旗標的值（目前只有 <c>payment_mode</c>：<c>off</c>／<c>external</c>／<c>inapp</c>）；布林旗標不要填。</summary>
    public string? StringValue { get; init; }

    /// <summary><c>all</c>（預設）／<c>ios</c>／<c>android</c>。</summary>
    public string? Platform { get; init; }
    public string? Description { get; init; }
}

public sealed record AdminAppFeatureFlagDto
{
    public required Guid Id { get; init; }
    public required string FlagKey { get; init; }
    public required bool IsEnabled { get; init; }
    public string? StringValue { get; init; }
    public required string Platform { get; init; }
    public required string PlatformLabel { get; init; }
    public string? Description { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminAppCredentialRequest
{
    /// <summary><c>apns_key</c>／<c>fcm_credential</c>／<c>apple_developer_program</c>／<c>google_play_account</c>／<c>maps_api_key</c>／<c>other</c>。</summary>
    public required string Kind { get; init; }
    public required string Label { get; init; }

    /// <summary>外部識別（例如 APNs Key ID、專案編號）。<b>不是金鑰本身</b>——金鑰不進資料庫。</summary>
    public string? ExternalRef { get; init; }
    public required DateOnly CreatedOn { get; init; }
    public DateOnly? LastRotatedOn { get; init; }

    /// <summary>屆期日；沒有到期日的金鑰留空，改用輪替週期。</summary>
    public DateOnly? ExpiresOn { get; init; }
    public int? RotationPeriodDays { get; init; }
    public string? Note { get; init; }
}

public sealed record AdminAppCredentialDto
{
    public required Guid Id { get; init; }
    public required string Kind { get; init; }
    public required string KindLabel { get; init; }
    public required string Label { get; init; }
    public string? ExternalRef { get; init; }
    public required DateOnly CreatedOn { get; init; }
    public DateOnly? LastRotatedOn { get; init; }
    public DateOnly? ExpiresOn { get; init; }
    public int? RotationPeriodDays { get; init; }

    /// <summary>下次屆期日：有屆期日用屆期日，沒有就用「上次輪替日（沒有就建立日）＋輪替週期」；兩者都沒有為 <c>null</c>。</summary>
    public DateOnly? NextDueOn { get; init; }
    public int? DaysUntilDue { get; init; }

    /// <summary><c>ok</c>／<c>due_soon</c>（屆期前 60 天內）／<c>overdue</c>（已屆期）／<c>untracked</c>（沒有屆期日也沒有輪替週期，無法告警）。</summary>
    public required string Health { get; init; }
    public required string HealthLabel { get; init; }
    public string? Note { get; init; }
}

public sealed record AdminAppDiagnosticListItemDto
{
    public required Guid Id { get; init; }
    public required string Platform { get; init; }
    public required string PlatformLabel { get; init; }
    public required string AppVersion { get; init; }
    public string? BuildNumber { get; init; }
    public string? OsVersion { get; init; }
    public required DateTime OccurredAt { get; init; }
    public required string ReportType { get; init; }
    public required string ReportTypeLabel { get; init; }
    public int? MetricValue { get; init; }
    public string? Summary { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
}

public sealed record AdminAppDiagnosticDetailDto
{
    public required AdminAppDiagnosticListItemDto Report { get; init; }
    public string? Detail { get; init; }
}

public sealed record UpdateAdminAppDiagnosticStatusRequest
{
    /// <summary><c>new</c>／<c>reviewing</c>／<c>resolved</c>／<c>ignored</c>。</summary>
    public required string Status { get; init; }
}

public sealed record AdminAppDiagnosticSummaryDto
{
    public required int Days { get; init; }
    public required IReadOnlyList<AdminAppDiagnosticVersionRowDto> ByVersion { get; init; }
    public required IReadOnlyList<AdminAppDeviceCountRowDto> ByType { get; init; }

    /// <summary>啟動耗時的中位數與第 90 百分位（毫秒），依最近 <c>Days</c> 天的 <c>startup_time</c> 回報。</summary>
    public int? StartupMedianMs { get; init; }
    public int? StartupP90Ms { get; init; }
    public string ApiErrorNote { get; init; } = "API 錯誤率需要分母（總請求數），目前診斷回報只收到錯誤次數，這裡只彙總次數；正式的錯誤率由伺服器端請求監控提供。";
}

public sealed record AdminAppDiagnosticVersionRowDto
{
    public required string Platform { get; init; }
    public required string PlatformLabel { get; init; }
    public required string AppVersion { get; init; }
    public required int Crashes { get; init; }
    public required int ActiveDevices { get; init; }
    public required int DevicesWithCrash { get; init; }

    /// <summary>無崩潰裝置比例（%）＝ 1 － 有崩潰回報的裝置數 ／ 活躍裝置數（近似值，只算有註冊裝置識別的回報）。</summary>
    public decimal? CrashFreeDevicePercent { get; init; }
}

public sealed record AdminAppConnectionCheckItemDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }

    /// <summary><c>ok</c>／<c>warning</c>／<c>not_configured</c>（尚未串接）／<c>error</c>。</summary>
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required string Message { get; init; }
}

public sealed record AdminAppConnectionCheckDto
{
    public required DateTime CheckedAt { get; init; }
    public required IReadOnlyList<AdminAppConnectionCheckItemDto> Items { get; init; }
}
