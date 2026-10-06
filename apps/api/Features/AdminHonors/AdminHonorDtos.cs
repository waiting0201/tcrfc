namespace Tcrfc.Api.Features.AdminHonors;

public sealed record AdminAchievementDto
{
    public required Guid Id { get; init; }
    public required Guid SeasonId { get; init; }
    public required string SeasonCode { get; init; }
    public required Guid TeamId { get; init; }

    /// <summary>隊別代號（僅供畫面辨識，介面不應直接顯示給一般使用者，顯示請用球隊名稱）。</summary>
    public required string TeamCode { get; init; }
    public string? TeamNameZh { get; init; }
    public int? Year { get; init; }
    public string? CompetitionName { get; init; }
    public string? Placing { get; init; }

    /// <summary>英文賽事名稱（<c>achievements_i18n(en)</c>）；未填為 <c>null</c>，前台回退繁中。</summary>
    public string? CompetitionNameEn { get; init; }

    /// <summary>英文名次（<c>achievements_i18n(en)</c>）；未填為 <c>null</c>。</summary>
    public string? PlacingEn { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>榮譽（規劃書 C5：年份、賽事、名次、關聯球隊）。<c>SeasonId</c> 與 <c>TeamId</c> 必須屬於本俱樂部。
/// <c>Year</c> 省略時取所屬球季開始日的西元年。</summary>
public sealed record UpsertAdminAchievementRequest
{
    public required Guid SeasonId { get; init; }
    public required Guid TeamId { get; init; }
    public int? Year { get; init; }
    public required string CompetitionName { get; init; }

    /// <summary>名次（自由文字，例如「冠軍」「亞軍」「第四名」；最長 64 字）。</summary>
    public required string Placing { get; init; }

    /// <summary>英文賽事名稱（選填，最長 128 字）。</summary>
    public string? CompetitionNameEn { get; init; }

    /// <summary>英文名次（選填，最長 64 字）。</summary>
    public string? PlacingEn { get; init; }
}

public sealed record AdminMilestoneLocaleContent
{
    public required string Title { get; init; }
    public string? Description { get; init; }

    /// <summary>圖片替代文字（有上傳圖片時建議填寫）。</summary>
    public string? ImageAlt { get; init; }
}

public sealed record AdminMilestoneContentInput
{
    public required AdminMilestoneLocaleContent Zh { get; init; }
    public AdminMilestoneLocaleContent? En { get; init; }
}

public sealed record AdminMilestoneDto
{
    public required Guid Id { get; init; }
    public required DateOnly HappenedOn { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>是否顯示於前台時間軸（規劃書 C5）。</summary>
    public required bool IsVisible { get; init; }
    public string? ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public int? ImageWidth { get; init; }
    public int? ImageHeight { get; init; }
    public required AdminMilestoneLocaleContent Zh { get; init; }
    public AdminMilestoneLocaleContent? En { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>建立與更新共用（multipart：<c>payload</c> ＋ 選填 <c>image</c>）。</summary>
public sealed record UpsertAdminMilestoneRequest
{
    public required DateOnly HappenedOn { get; init; }
    public int SortOrder { get; init; }

    /// <summary>省略時預設 <c>true</c>（顯示）。</summary>
    public bool IsVisible { get; init; } = true;
    public required AdminMilestoneContentInput Content { get; init; }
    public bool RemoveImage { get; init; }
}
