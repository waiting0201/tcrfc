namespace Tcrfc.Api.Common;

/// <summary>
/// E1a（2026-09-30）起新增後台模組（E1 夥伴、E2 贊助、E3 提案與 Lead、B5 慈善、B6 媒體專區、C5 榮譽與里程碑）
/// 共用的三個例外，集中由 <see cref="ApiExceptionHandler"/> 轉成狀態碼。既有模組各自宣告一組
/// <c>AdminXxxValidationException</c>，本批六個模組型別與語意完全相同（400／409／403），不再逐模組複製六份。
/// 訊息一律是日常中文（規劃書 §4.0：介面不得出現資料表名、欄位名或英文技術詞）。
/// </summary>
public sealed class AdminValidationException(string message) : Exception(message);

/// <summary>唯一性衝突或「仍被引用所以不能刪」。對應 409；<see cref="Title"/> 是給畫面的短標題。</summary>
public sealed class AdminConflictException(string title, string message) : Exception(message)
{
    public string Title { get; } = title;
}

/// <summary>共同內容（<c>club_id IS NULL</c>，兩隊共用）透過俱樂部範圍端點寫入時一律擋下，
/// 形狀比照 <c>SharedStaffReadOnlyException</c>。對應 403。</summary>
public sealed class SharedContentReadOnlyException(string subject)
    : Exception($"這是兩隊共用的{subject}，目前僅系統管理員可以編輯。");

/// <summary>這個功能對目前的俱樂部不適用（例如台中藍鯨不設漫畫，藍鯨規劃書 §1.3／§2.1）。對應 403；
/// 訊息是給畫面顯示的日常中文，由呼叫端提供。</summary>
public sealed class FeatureNotAvailableException(string message) : Exception(message);
