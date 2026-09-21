namespace Tcrfc.Api.Security;

/// <summary>
/// 路由帶的俱樂部代碼在 <c>clubs</c> 表查不到，或存在但狀態不是 <c>active</c>。
/// 對應 404，不吐出資料庫層細節（CLAUDE.md「不要把資料庫例外訊息吐給呼叫端」）。
/// </summary>
public sealed class ClubNotFoundException(string clubCode)
    : Exception($"找不到俱樂部代碼「{clubCode}」，或該俱樂部目前非啟用狀態。")
{
    public string ClubCode { get; } = clubCode;
}
