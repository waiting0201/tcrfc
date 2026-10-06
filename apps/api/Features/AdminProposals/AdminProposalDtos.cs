namespace Tcrfc.Api.Features.AdminProposals;

public sealed record AdminProposalFileDto
{
    public required Guid Id { get; init; }

    /// <summary><c>zh</c>／<c>en</c>。</summary>
    public required string Locale { get; init; }
    public required int VersionNo { get; init; }
    public int? FileBytes { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record AdminProposalListItemDto
{
    public required Guid Id { get; init; }

    /// <summary>繁中提案名稱（<c>proposals_i18n(zh-Hant)</c>）。</summary>
    public required string Title { get; init; }

    /// <summary>英文提案名稱（<c>proposals_i18n(en)</c>）；未填為 <c>null</c>。</summary>
    public string? TitleEn { get; init; }
    public required int VersionNo { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<string> Locales { get; init; }
    public required int FileCount { get; init; }

    /// <summary>這份提案累計被下載（留下 Lead）的次數，用來比較 A/B 版本成效。</summary>
    public required int LeadCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminProposalDetailDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? TitleEn { get; init; }
    public required int VersionNo { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<AdminProposalFileDto> Files { get; init; }
    public required int LeadCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminProposalRequest
{
    public required string Title { get; init; }

    /// <summary>英文提案名稱（選填，最長 128 字）。</summary>
    public string? TitleEn { get; init; }

    /// <summary>提案版本號（≥ 1），A/B 版本可用不同標題或不同版本號區分。</summary>
    public int VersionNo { get; init; } = 1;

    /// <summary><c>draft</c>（前台不可下載）或 <c>published</c>（前台可下載；至少要有一份檔案才能發布）。</summary>
    public required string Status { get; init; }
}

/// <summary>上傳提案檔案的 payload（檔案在 multipart 的 <c>file</c> 欄位，PDF 或 ZIP）。</summary>
public sealed record AddAdminProposalFileRequest
{
    /// <summary><c>zh</c> 或 <c>en</c>。</summary>
    public required string Locale { get; init; }

    /// <summary>省略＝沿用提案本身的版本號。同一提案、同一語系、同一版本號只能有一份檔案。</summary>
    public int? VersionNo { get; init; }
}

// ───────────── Lead ─────────────

public sealed record AdminLeadListItemDto
{
    public required Guid Id { get; init; }
    public string? Company { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public Guid? ProposalId { get; init; }
    public string? ProposalTitle { get; init; }
    public string? SourcePath { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmCampaign { get; init; }
    public string? Status { get; init; }
    public Guid? AssigneeAdminUserId { get; init; }
    public string? Tags { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record AdminLeadDetailDto
{
    public required Guid Id { get; init; }
    public string? Company { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public Guid? ProposalId { get; init; }
    public string? ProposalTitle { get; init; }
    public int? ProposalVersionNo { get; init; }
    public string? SourcePath { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmCampaign { get; init; }
    public string? Status { get; init; }
    public Guid? AssigneeAdminUserId { get; init; }
    public string? InternalNote { get; init; }
    public string? Tags { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>標記跟進狀態：新進／處理中／已回覆／已結案／無效（與 G2 詢問收件匣同一組狀態值）。</summary>
public sealed record UpdateAdminLeadRequest
{
    public required string Status { get; init; }
    public Guid? AssigneeAdminUserId { get; init; }
    public string? InternalNote { get; init; }
    public string? Tags { get; init; }
}

public sealed record AdminLeadAssigneeDto
{
    public required Guid Id { get; init; }
    public required string DisplayName { get; init; }
}
