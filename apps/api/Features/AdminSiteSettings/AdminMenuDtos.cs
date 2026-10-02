namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>後台選單項目。拖曳排序與多層級以「樹」表示：同一層的順序＝陣列順序（儲存時重新編號）。</summary>
public sealed record AdminMenuItemDto
{
    public required Guid Id { get; init; }

    public required string LabelZh { get; init; }

    public string? LabelEn { get; init; }

    /// <summary>內部連結是不含語系前綴的路徑（<c>/about/</c>）；外部連結是完整 http(s) 網址。群組標題可為 <c>null</c>。</summary>
    public string? Url { get; init; }

    public required bool IsExternal { get; init; }

    public required IReadOnlyList<AdminMenuItemDto> Children { get; init; }
}

public sealed record AdminMenuLocationDto
{
    /// <summary><c>main</c>（主選單）／<c>mega</c>（Mega Menu）／<c>footer</c>（頁尾選單）。</summary>
    public required string Location { get; init; }

    /// <summary>畫面顯示名稱。</summary>
    public required string Label { get; init; }

    public required IReadOnlyList<AdminMenuItemDto> Items { get; init; }
}

public sealed record AdminMenusDto
{
    public required IReadOnlyList<AdminMenuLocationDto> Locations { get; init; }
}

/// <summary>儲存一個選單位置的整棵樹。<see cref="Id"/> 有值＝沿用既有項目（必須屬於同一俱樂部、同一位置），沒有值＝新增；
/// 既有項目不在請求裡＝刪除（連同其子項目）。</summary>
public sealed record UpsertAdminMenuItemRequest
{
    public Guid? Id { get; init; }

    public string? LabelZh { get; init; }

    public string? LabelEn { get; init; }

    public string? Url { get; init; }

    public bool IsExternal { get; init; }

    public IReadOnlyList<UpsertAdminMenuItemRequest>? Children { get; init; }
}

public sealed record UpdateAdminMenuRequest
{
    public IReadOnlyList<UpsertAdminMenuItemRequest>? Items { get; init; }
}
