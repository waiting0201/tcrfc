namespace Tcrfc.Api.Security;

/// <summary>
/// 個資檢視、匯出、帳號合併等敏感操作的留痕（規劃書 §6「匯出寫入稽核日誌：誰、何時、匯出幾筆、用途備註」）。
/// 🔴 本庫依委託方指示<b>沒有任何日誌表</b>（<c>db/club-schema.sql</c> 檔頭第 8 點），無法把這些紀錄落成資料列；
/// 目前做法是寫進應用程式的結構化日誌（正式環境由 Application Insights 收；「何時」由日誌時間戳提供），
/// 欄位固定為：操作、帳號、俱樂部、對象、筆數、用途。<b>日誌內容一律不含個資本身</b>（只有會員 id／編號與筆數）。
/// 這是暫行落點，等客戶重新確認稽核表的政策後改寫入資料庫（見 apps/api/README.md「B1」節「待裁決」）。
/// </summary>
public sealed class SensitiveActionLogger(ILogger<SensitiveActionLogger> logger)
{
    public void Record(AdminClubScope scope, string action, string? subject = null, int? count = null, string? purpose = null)
        => logger.LogInformation(
            "敏感操作 {Action}｜帳號 {AdminUserId}（{Username}）｜俱樂部 {Club}｜對象 {Subject}｜筆數 {Count}｜用途 {Purpose}",
            action, scope.Identity.AdminUserId, scope.Identity.Username, scope.ClubCode, subject, count, purpose);
}
