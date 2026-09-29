namespace Tcrfc.Api.Features.AdminVenues;

/// <summary>
/// 全站共用場地主檔（<c>venues</c>／<c>venues_i18n</c>）的唯讀清單項目。<c>Venue</c> 本身刻意不帶
/// <c>club_id</c>（docs/12 §4.7：場地是地理實體，兩俱樂部可能共用同一座球場，重複建會產生兩組
/// 人工標的座標），因此這支端點回傳的是**全站**場地，不是「這個俱樂部的場地」——呼叫端（例如
/// 網站設定 `I` 挑選主場、賽程 `C4` 挑選比賽地點）各自決定要怎麼用這份清單。
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
}
