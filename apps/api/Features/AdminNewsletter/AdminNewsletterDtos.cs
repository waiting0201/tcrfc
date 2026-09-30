namespace Tcrfc.Api.Features.AdminNewsletter;

/// <summary>G3 電子報訂閱者一列。<c>Status</c>：<c>subscribed</c>（已訂閱）／<c>unsubscribed</c>（已退訂）；<c>StatusLabel</c> 是畫面用的日常中文。</summary>
public sealed record AdminNewsletterSubscriberDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public string? Source { get; init; }
    public string? SourceLabel { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public DateTime? SubscribedAt { get; init; }
    public DateTime? UnsubscribedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminNewsletterSourceCountDto
{
    public string? Source { get; init; }
    public required string SourceLabel { get; init; }
    public required int Count { get; init; }
}

/// <summary>名單摘要：目前訂閱／已退訂人數與來源分布（只算本俱樂部，電子報名單兩站各自獨立）。</summary>
public sealed record AdminNewsletterSummaryDto
{
    public required int SubscribedCount { get; init; }
    public required int UnsubscribedCount { get; init; }
    public required IReadOnlyList<AdminNewsletterSourceCountDto> Sources { get; init; }
}

public sealed record CreateAdminNewsletterSubscriberRequest
{
    public required string Email { get; init; }

    /// <summary>來源說明（選填，例如「活動現場填單」）；沒填就記為「後台新增」。</summary>
    public string? Source { get; init; }
}

public sealed record UpdateAdminNewsletterStatusRequest
{
    /// <summary><c>subscribed</c> 或 <c>unsubscribed</c>。</summary>
    public required string Status { get; init; }

    /// <summary>原因（重新訂閱時必填：須說明是訂閱者本人要求）。</summary>
    public string? Reason { get; init; }
}

public sealed record AdminNewsletterListQuery
{
    public string? Status { get; init; }
    public string? Source { get; init; }
    public string? Keyword { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

/// <summary>EDM 平台串接狀態。<c>Configured</c> 為 <c>false</c> 表示供應商尚未確定、沒有串接。</summary>
public sealed record AdminNewsletterEdmStatusDto
{
    public required bool Configured { get; init; }
    public string? Provider { get; init; }
    public required string Message { get; init; }
}

public sealed record AdminNewsletterEdmSyncResultDto
{
    public required bool Configured { get; init; }
    public required int SubscribedCount { get; init; }
    public required int UnsubscribedCount { get; init; }
    public required int SyncedCount { get; init; }
    public required string Message { get; init; }
}
