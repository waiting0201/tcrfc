namespace Tcrfc.Api.Features.AdminVenues;

/// <summary>
/// 全站共用場地主檔（<c>venues</c>／<c>venues_i18n</c>）的清單項目。<c>Venue</c> 本身刻意不帶
/// <c>club_id</c>（docs/12 §4.7：場地是地理實體，兩俱樂部可能共用同一座球場，重複建會產生兩組
/// 人工標的座標），因此這支端點回傳的是**全站**場地，不是「這個俱樂部的場地」——呼叫端（例如
/// 網站設定 `I` 挑選主場、賽程 `C4` 挑選比賽地點）各自決定要怎麼用這份清單。
/// 2026-10-02（I5 場地管理）：新增 <see cref="Lat"/>／<see cref="Lng"/>／<see cref="PhotoUrl"/> 三個選填欄位（原本的欄位與意義不變）。
/// </summary>
public sealed record AdminVenueListItemDto
{
    public required Guid Id { get; init; }

    /// <summary>場地名稱（中文），既有列理論上必有值；缺漏時回傳空字串而不是 <c>null</c>，
    /// 避免呼叫端（下拉選單）要多處理一種例外形狀。</summary>
    public required string NameZh { get; init; }

    public string? NameEn { get; init; }

    /// <summary>地址（中文語系的 <c>venues_i18n.address</c>），可為 <c>null</c>——不是每一筆既有
    /// 場地都已核實地址。</summary>
    public string? Address { get; init; }

    public decimal? Lat { get; init; }

    public decimal? Lng { get; init; }

    /// <summary>照片網址（沒有照片為 <c>null</c>）。</summary>
    public string? PhotoUrl { get; init; }
}

public sealed record AdminVenueLocaleContent
{
    public required string Name { get; init; }

    public string? Address { get; init; }

    /// <summary>交通說明（純文字，空行分段）。</summary>
    public string? Directions { get; init; }

    /// <summary>照片替代文字（無障礙）。</summary>
    public string? PhotoAlt { get; init; }
}

public sealed record AdminVenueDetailDto
{
    public required Guid Id { get; init; }

    public decimal? Lat { get; init; }

    public decimal? Lng { get; init; }

    public string? PhotoUrl { get; init; }

    public int? PhotoWidth { get; init; }

    public int? PhotoHeight { get; init; }

    public required int SortOrder { get; init; }

    public required AdminVenueLocaleContent Zh { get; init; }

    public AdminVenueLocaleContent? En { get; init; }

    /// <summary>被多少筆賽事、梯次、試訓、行事曆事件、球迷會活動引用（刪除前參考；大於 0 不能刪）。</summary>
    public required int UsageCount { get; init; }

    /// <summary>是否被某個俱樂部登記為主場。</summary>
    public required bool IsHomeVenue { get; init; }

    public required DateTime UpdatedAt { get; init; }
}

/// <summary>建立／更新場地。以 <c>multipart/form-data</c> 送出：<c>payload</c>（本型別的 JSON）＋選填檔案欄位 <c>photo</c>。
/// 更新是整份取代；沒帶檔案且 <see cref="RemovePhoto"/> 為 false＝維持原照片。英文區塊的名稱空白＝刪除英文版。</summary>
public sealed record UpsertAdminVenueRequest
{
    public AdminVenueLocaleContent? Zh { get; init; }

    public AdminVenueLocaleContent? En { get; init; }

    /// <summary>緯度（-90–90）與經度（-180–180）要同時填或同時空白。</summary>
    public decimal? Lat { get; init; }

    public decimal? Lng { get; init; }

    public int SortOrder { get; init; }

    public bool RemovePhoto { get; init; }
}
