namespace Tcrfc.Api.Features.AppPublic;

public sealed record RegisterAppDeviceRequest
{
    /// <summary><c>ios</c> 或 <c>android</c>。</summary>
    public required string Platform { get; init; }
    public string? OsVersion { get; init; }
    public string? AppVersion { get; init; }

    /// <summary><c>zh</c> 或 <c>en</c>；推播語系以這裡為準（App 規劃書 §2.5）。</summary>
    public string? Locale { get; init; }

    /// <summary>推播權杖。視同個資：加密儲存、不對外顯示。沒有帶就不動既有權杖。</summary>
    public string? PushToken { get; init; }

    /// <summary><c>not_determined</c>／<c>granted</c>／<c>denied</c>／<c>provisional</c>。</summary>
    public string? PushPermission { get; init; }
}

public sealed record AppDeviceRegisteredDto
{
    public required string DeviceInstallId { get; init; }
    public required bool IsNew { get; init; }
    public required string PushTokenStatus { get; init; }
}

public sealed record AppSubscriptionInput
{
    /// <summary><c>team</c>（值＝球隊代碼）／<c>news_category</c>（值＝新聞分類代碼）／<c>club</c>（值＝俱樂部代碼）。</summary>
    public required string TopicType { get; init; }
    public required string TopicValue { get; init; }
    public bool IsFollowing { get; init; } = true;
    public bool IsPushEnabled { get; init; } = true;
}

public sealed record UpdateAppSubscriptionsRequest
{
    public required IReadOnlyList<AppSubscriptionInput> Items { get; init; }

    /// <summary>為 <c>true</c> 時，清單就是完整的訂閱集合（沒列出的訂閱會被移除）；預設 <c>false</c> 只新增或更新列出的項目。</summary>
    public bool ReplaceAll { get; init; }
}

public sealed record AppSubscriptionDto
{
    public required string TopicType { get; init; }
    public required string TopicValue { get; init; }
    public required bool IsFollowing { get; init; }
    public required bool IsPushEnabled { get; init; }
}

public sealed record AppLayoutItemDto
{
    /// <summary>項目識別代碼（首頁區塊固定九個：next_match、ad_home_top、latest_news、member_card、recent_matches、ad_home_mid、nearby_stores、quick_entries、sponsor_wall）。</summary>
    public required string Code { get; init; }
    public string? Label { get; init; }

    /// <summary>圖示識別（App 內建圖示的名稱，不是物件儲存鍵）。</summary>
    public string? Icon { get; init; }
    public string? DeepLink { get; init; }
    public string? WebUrl { get; init; }
}

public sealed record AppAnnouncementDto
{
    public required Guid Id { get; init; }
    public string? Message { get; init; }
    public string? LinkUrl { get; init; }
    public DateTime? EndsAt { get; init; }
}

public sealed record AppDeepLinkDto
{
    public required string Code { get; init; }
    public string? Label { get; init; }
    public required string AppLink { get; init; }
    public string? WebUrl { get; init; }
    public required bool RequiresLogin { get; init; }
}

public sealed record AppLayoutResponse
{
    public required DateTime GeneratedAt { get; init; }
    public required IReadOnlyList<AppLayoutItemDto> HomeSections { get; init; }
    public required IReadOnlyList<AppLayoutItemDto> QuickEntries { get; init; }
    public required IReadOnlyList<AppLayoutItemDto> MoreItems { get; init; }
    public required IReadOnlyList<AppAnnouncementDto> Announcements { get; init; }
    public required IReadOnlyList<AppDeepLinkDto> DeepLinks { get; init; }
}

public sealed record AppNotificationDto
{
    public required Guid Id { get; init; }
    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? ImageUrl { get; init; }
    public string? DeepLink { get; init; }
    public required DateTime SentAt { get; init; }
}

public sealed record AppPushOpenedRequest
{
    public required string DeviceInstallId { get; init; }
}

public sealed record AppDiagnosticInput
{
    public string? DeviceInstallId { get; init; }
    public required string Platform { get; init; }
    public required string AppVersion { get; init; }
    public string? BuildNumber { get; init; }
    public string? OsVersion { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary><c>crash</c>／<c>abnormal_exit</c>／<c>api_error</c>／<c>startup_time</c>／<c>user_report</c>。</summary>
    public required string Type { get; init; }

    /// <summary>啟動耗時（毫秒）或錯誤次數。</summary>
    public int? MetricValue { get; init; }
    public string? Summary { get; init; }
    public string? Detail { get; init; }
}

public sealed record AppDiagnosticBatchRequest
{
    public required IReadOnlyList<AppDiagnosticInput> Reports { get; init; }
}
