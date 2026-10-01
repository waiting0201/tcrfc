using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Security;

namespace Tcrfc.Api.CharityPlatform.Admin;

/// <summary>
/// 稽核紀錄寫入（<c>audit_logs</c>）。🔴 與主站相反：慈善平台<b>刻意有稽核</b>，退款、分潤百分比設定、含個資的明細匯出三類操作
/// 全數要留下軌跡（規劃書 §11.2，勸募法遵要求），另外也記錄個資明文檢視、店家 QR 網址重產、憑證重開等敏感操作。
///
/// 🔴 <b>稽核與被稽核的變更在同一次 <c>SaveChanges</c> 提交</b>：<see cref="Stage"/> 只把稽核列加進 DbContext（不呼叫 SaveChanges），
/// 由呼叫端與資料變更一起存——不會有「改了卻沒留軌跡」或「留了軌跡卻沒改成」的狀態。
/// 🔴 <b>append-only</b>：本類別沒有更新與刪除的方法，資料表也刻意沒有 updated_at／updated_by（docs/16 §2.4）。
/// 🔴 變更摘要與用途備註<b>不得含個資明文</b>（Email、身分證字號）——稽核紀錄本身會被很多人檢視。
/// </summary>
public sealed class CharityAuditLogger(CharityDbContext db)
{
    private const int MaxSummary = 500;
    private const int MaxPurpose = 255;
    private const int MaxIp = 45;
    private const int MaxAction = 32;
    private const int MaxTargetType = 32;

    public void Stage(
        CharityAdminScope scope, string action, string targetType, Guid? targetId,
        string? changeSummary, string? purposeNote, string? sourceIp)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            AdminUserId = scope.Identity.AdminUserId,
            OccurredAt = DateTime.UtcNow,
            Action = Truncate(action, MaxAction)!,
            TargetType = Truncate(targetType, MaxTargetType)!,
            TargetId = targetId,
            ChangeSummary = Truncate(changeSummary, MaxSummary),
            PurposeNote = Truncate(purposeNote, MaxPurpose),
            SourceIp = Truncate(sourceIp, MaxIp),
        });
    }

    private static string? Truncate(string? value, int max)
        => value is null ? null : value.Length <= max ? value : value[..max];
}
