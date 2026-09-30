namespace Tcrfc.Api.Features.AdminPartnerStores;

public sealed record AdminStoreLocaleContent
{
    public required string Name { get; init; }

    /// <summary>地址。中文地址存在店家主檔（也是「由地址定位」的依據），英文地址存在英文版。</summary>
    public string? Address { get; init; }

    /// <summary>優惠內容。</summary>
    public string? OfferContent { get; init; }
}

public sealed record AdminStoreContentInput
{
    public required AdminStoreLocaleContent Zh { get; init; }
    public AdminStoreLocaleContent? En { get; init; }
}

/// <summary>新增與更新共用（multipart：<c>payload</c> JSON ＋ 選填檔案欄位 <c>image</c>）。
/// <c>ApplicableTier</c>：<c>all</c>（全會員）／<c>fan_club</c>（限付費）。<c>Status</c>：<c>published</c>（上架）／<c>draft</c>（下架）。
/// <c>Lat</c>／<c>Lng</c> 兩個一起填或一起省略（人工確認後儲存；本系統不做即時地址轉座標）。
/// <c>BusinessHours</c> 是自由文字（例：「週一至週五 11:00–21:00」）。</summary>
public sealed record UpsertAdminPartnerStoreRequest
{
    /// <summary>省略＝自動產生。更新時省略＝維持不變。</summary>
    public string? Slug { get; init; }
    public string? Category { get; init; }
    public string? Region { get; init; }
    public decimal? Lat { get; init; }
    public decimal? Lng { get; init; }
    public string? Phone { get; init; }
    public string? BusinessHours { get; init; }
    public string? MapUrl { get; init; }
    public string? WebsiteUrl { get; init; }
    public string ApplicableTier { get; init; } = "all";
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public int SortOrder { get; init; }
    public string Status { get; init; } = "draft";

    /// <summary>建立時設為兩隊共同的店家（資料庫 <c>club_id</c> 為空）。只有系統管理員可以；更新時忽略。</summary>
    public bool IsShared { get; init; }
    public bool RemoveImage { get; init; }
    public required AdminStoreContentInput Content { get; init; }
}

public sealed record AdminPartnerStoreListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }

    /// <summary>兩隊共同的店家：所有俱樂部看得到，但只有系統管理員能編輯。</summary>
    public required bool IsShared { get; init; }
    public string? Category { get; init; }
    public string? Region { get; init; }
    public string? Address { get; init; }
    public decimal? Lat { get; init; }
    public decimal? Lng { get; init; }
    public string? Phone { get; init; }
    public required string ApplicableTier { get; init; }
    public required string ApplicableTierLabel { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }

    /// <summary>合作期間涵蓋今天（沒填起訖視為進行中）。</summary>
    public required bool IsActive { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? OfferZh { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminPartnerStoreDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required bool IsShared { get; init; }
    public string? Category { get; init; }
    public string? Region { get; init; }
    public string? Address { get; init; }
    public decimal? Lat { get; init; }
    public decimal? Lng { get; init; }
    public string? Phone { get; init; }
    public string? BusinessHours { get; init; }
    public string? MapUrl { get; init; }
    public string? WebsiteUrl { get; init; }
    public required string ApplicableTier { get; init; }
    public required string ApplicableTierLabel { get; init; }
    public DateOnly? StartOn { get; init; }
    public DateOnly? EndOn { get; init; }
    public required bool IsActive { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public required AdminStoreLocaleContent Zh { get; init; }
    public AdminStoreLocaleContent? En { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>8.4 清單頁的分類與地區篩選項目：實際用過的值（自由文字，由店家資料自然形成）。</summary>
public sealed record AdminPartnerStoreFiltersDto
{
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<string> Regions { get; init; }
}
