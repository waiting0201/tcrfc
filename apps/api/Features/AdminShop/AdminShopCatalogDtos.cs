using System.Text.Json;

namespace Tcrfc.Api.Features.AdminShop;

// ───────────── 商品系列（Collection）─────────────

public sealed record AdminCollectionLocaleContent
{
    public required string Name { get; init; }

    /// <summary>系列介紹文（品牌敘事，前台 8.3 櫥窗質感由此維護）。</summary>
    public string? Narrative { get; init; }
}

public sealed record AdminCollectionContentInput
{
    public required AdminCollectionLocaleContent Zh { get; init; }
    public AdminCollectionLocaleContent? En { get; init; }
}

public sealed record AdminCollectionListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required int ProductCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminCollectionDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required AdminCollectionLocaleContent Zh { get; init; }
    public AdminCollectionLocaleContent? En { get; init; }
    public required int ProductCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminCollectionRequest
{
    public string? Slug { get; init; }
    public int? SortOrder { get; init; }

    /// <summary><c>draft</c>（不顯示）或 <c>published</c>（顯示）。</summary>
    public required string Status { get; init; }
    public required AdminCollectionContentInput Content { get; init; }
}

// ───────────── 商品 ─────────────

public sealed record AdminProductLocaleContent
{
    public required string Name { get; init; }
    public string? Narrative { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }

    /// <summary>標籤，逗號分隔（例如「新款,主場」）。</summary>
    public string? Tags { get; init; }
}

public sealed record AdminProductContentInput
{
    public required AdminProductLocaleContent Zh { get; init; }
    public AdminProductLocaleContent? En { get; init; }
}

public sealed record AdminProductImageDto
{
    public required Guid Id { get; init; }
    public required string ImageKey { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageThumbUrl { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public required int SortOrder { get; init; }
}

public sealed record AdminVariantDto
{
    public required Guid Id { get; init; }
    public required Guid ProductId { get; init; }
    public required string Sku { get; init; }
    public string? Size { get; init; }
    public string? Colour { get; init; }
    public required string Label { get; init; }
    public required int Price { get; init; }
    public int? SalePrice { get; init; }

    /// <summary>實際售價（有促銷價用促銷價）。</summary>
    public required int EffectivePrice { get; init; }

    /// <summary>成本：只有持有「檢視商品成本」權限的角色看得到，其餘為 <c>null</c>。</summary>
    public int? Cost { get; init; }
    public required int StockQty { get; init; }
    public required int ReservedQty { get; init; }
    public required int AvailableQty { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public int? LowStockThreshold { get; init; }
    public required bool IsLowStock { get; init; }
    public required int SortOrder { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminProductListItemDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public Guid? CollectionId { get; init; }
    public string? CollectionName { get; init; }
    public required bool IsNewArrival { get; init; }
    public required int SortOrder { get; init; }

    /// <summary>資料庫狀態：<c>draft</c>／<c>published</c>。</summary>
    public required string Status { get; init; }

    /// <summary>顯示狀態：已上架且所有販售中規格都沒有可售量時為 <c>sold_out</c>（缺貨由庫存自動判定）。</summary>
    public required string DisplayStatus { get; init; }
    public required string DisplayStatusLabel { get; init; }
    public required string OutOfStockBehavior { get; init; }
    public required string OutOfStockBehaviorLabel { get; init; }
    public string? CoverThumbUrl { get; init; }
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public required int VariantCount { get; init; }

    /// <summary>價格與可售量只有持有「檢視商品規格與售價」權限的角色看得到，其餘為 <c>null</c>。</summary>
    public int? PriceMin { get; init; }
    public int? PriceMax { get; init; }
    public int? AvailableTotal { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminProductDetailDto
{
    public required Guid Id { get; init; }
    public required string Slug { get; init; }
    public Guid? CollectionId { get; init; }
    public string? CollectionName { get; init; }
    public required bool IsNewArrival { get; init; }
    public required int SortOrder { get; init; }
    public required string Status { get; init; }
    public required string DisplayStatus { get; init; }
    public required string DisplayStatusLabel { get; init; }
    public required string OutOfStockBehavior { get; init; }
    public required string OutOfStockBehaviorLabel { get; init; }
    public JsonElement? SizeChart { get; init; }
    public required AdminProductLocaleContent Zh { get; init; }
    public AdminProductLocaleContent? En { get; init; }
    public required IReadOnlyList<AdminProductImageDto> Images { get; init; }

    /// <summary>規格清單；沒有「檢視商品規格與售價」權限時為空陣列（<c>canViewVariants</c> 為 <c>false</c>）。</summary>
    public required IReadOnlyList<AdminVariantDto> Variants { get; init; }
    public required bool CanViewVariants { get; init; }
    public required bool CanViewCost { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record UpsertAdminProductRequest
{
    public string? Slug { get; init; }
    public Guid? CollectionId { get; init; }
    public bool IsNewArrival { get; init; }
    public int? SortOrder { get; init; }

    /// <summary><c>draft</c>（下架）或 <c>published</c>（上架，至少要有一個販售中的規格）。</summary>
    public required string Status { get; init; }

    /// <summary><c>show_unavailable</c>（缺貨時顯示但不可購買，預設）或 <c>hide</c>（缺貨時自動隱藏）。</summary>
    public string? OutOfStockBehavior { get; init; }

    /// <summary>尺碼表（結構自由的 JSON，如 <c>{"columns":[…],"rows":[…]}</c>）。省略或 <c>null</c>＝清除。</summary>
    public JsonElement? SizeChart { get; init; }
    public required AdminProductContentInput Content { get; init; }
}

public sealed record UpsertAdminVariantRequest
{
    /// <summary>貨號，全站唯一。</summary>
    public required string Sku { get; init; }
    public string? Size { get; init; }
    public string? Colour { get; init; }
    public required int Price { get; init; }
    public int? SalePrice { get; init; }

    /// <summary>成本。要填或改需要「編輯商品成本」權限；沒有權限的角色只能省略（<c>null</c>＝不變）。</summary>
    public int? Cost { get; init; }

    /// <summary>設為 <c>true</c> 時清除成本（同樣需要權限）。</summary>
    public bool ClearCost { get; init; }

    /// <summary><c>active</c> 販售中（預設）／<c>inactive</c> 停售。</summary>
    public string? Status { get; init; }

    /// <summary>低庫存門檻；省略＝沿用俱樂部的預設門檻。</summary>
    public int? LowStockThreshold { get; init; }
    public int? SortOrder { get; init; }

    /// <summary>只在新增時有效：初始庫存，會記成一筆「進貨」異動。庫存之後只能透過庫存管理調整。</summary>
    public int? InitialStock { get; init; }
}
