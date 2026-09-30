using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Features.AdminSecurity;

public sealed record AdminAccountActivityDto
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required string DisplayName { get; init; }
    public required string Status { get; init; }
    public required bool IsSuperAdmin { get; init; }
    public required bool TwoFactorEnabled { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public required int FailedAttemptCount { get; init; }
    public DateTime? LockedUntil { get; init; }
    public required bool IsLockedNow { get; init; }

    /// <summary>距離最後登入幾天；從未登入過為 <c>null</c>。</summary>
    public int? DaysSinceLastLogin { get; init; }
    public DateTime? PasswordChangedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public sealed record AdminSecurityAlertDto
{
    /// <summary><c>locked</c>（帳號被鎖定）／<c>failed_attempts</c>（連續登入失敗）／<c>dormant</c>（久未登入）／<c>never_logged_in</c>（建立後從未登入）。</summary>
    public required string Kind { get; init; }
    public required string KindLabel { get; init; }
    public required Guid AccountId { get; init; }
    public required string Username { get; init; }
    public required string Message { get; init; }
}

/// <summary>J3 稽核與備份的「可查閱部分」。🔴 <b>本庫沒有稽核日誌表</b>（委託方指示，<c>docs/12</c> §13.1；使用者 2026-09-23 裁決撤回）：
/// 規劃書 J3 的「誰在何時對哪筆資料做了什麼」「登入紀錄」在客戶重新確認前無法提供，這裡只回得出「帳號目前的狀態」——
/// 最後登入時間、連續失敗次數、鎖定狀態，並據此產生登入異常提醒。<see cref="AuditTrailAvailable"/> 固定為 <c>false</c>，畫面必須如實說明，
/// 不得把這個頁面呈現成完整的操作稽核。</summary>
public sealed record AdminSecurityOverviewDto
{
    public required DateTime GeneratedAt { get; init; }
    public required bool AuditTrailAvailable { get; init; }
    public required string AuditTrailMessage { get; init; }
    public required int ActiveAccounts { get; init; }
    public required int LockedAccounts { get; init; }
    public required int DormantAccounts { get; init; }
    public required IReadOnlyList<AdminSecurityAlertDto> Alerts { get; init; }
    public required IReadOnlyList<AdminAccountActivityDto> Accounts { get; init; }
}

/// <summary>「久未登入」的門檻天數與「連續失敗」提醒的次數（執行層決定，規劃書只寫「登入紀錄與異常提醒」，沒有給數字）。</summary>
public sealed class AdminSecurityOverviewRepository(ClubDbContext dbContext)
{
    public const int DormantDays = 90;
    public const int FailedAttemptAlert = 3;
    public const int NeverLoggedInGraceDays = 7;
    public const string AuditMessage = "目前系統沒有保存操作稽核記錄與登入歷程（依委託方指示，資料庫不建立日誌表）；這裡只顯示帳號目前的狀態。若需要「誰在何時做了什麼」的稽核記錄，需要客戶重新確認後另行建置。";

    public async Task<AdminSecurityOverviewDto> GetAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var rows = await dbContext.AdminUsers.AsNoTracking().OrderBy(u => u.RowSeq).ToListAsync(cancellationToken);
        var accounts = rows.Select(u => new AdminAccountActivityDto
        {
            Id = u.Id, Username = u.Username, DisplayName = u.DisplayName, Status = u.Status, IsSuperAdmin = u.IsSuperAdmin,
            TwoFactorEnabled = u.TwoFactorEnabled, LastLoginAt = u.LastLoginAt, FailedAttemptCount = u.FailedAttemptCount, LockedUntil = u.LockedUntil,
            IsLockedNow = u.LockedUntil is { } l && l > now, DaysSinceLastLogin = u.LastLoginAt is { } t ? (int)(now - t).TotalDays : null,
            PasswordChangedAt = u.PasswordChangedAt, CreatedAt = u.CreatedAt,
        }).ToList();

        var alerts = new List<AdminSecurityAlertDto>();
        foreach (var a in accounts.Where(a => a.Status == "active"))
        {
            if (a.IsLockedNow)
            {
                alerts.Add(Alert("locked", "帳號被鎖定", a, $"帳號因連續登入失敗被鎖定，到 {a.LockedUntil:yyyy-MM-dd HH:mm}（UTC）自動解除。"));
            }
            else if (a.FailedAttemptCount >= FailedAttemptAlert)
            {
                alerts.Add(Alert("failed_attempts", "連續登入失敗", a, $"目前連續登入失敗 {a.FailedAttemptCount} 次，可能有人在嘗試猜密碼。"));
            }

            if (a.LastLoginAt is null && (now - a.CreatedAt).TotalDays >= NeverLoggedInGraceDays)
            {
                alerts.Add(Alert("never_logged_in", "從未登入", a, $"帳號建立超過 {NeverLoggedInGraceDays} 天，一直沒有登入過；不需要的帳號請停用。"));
            }
            else if (a.DaysSinceLastLogin is { } d && d >= DormantDays)
            {
                alerts.Add(Alert("dormant", "久未登入", a, $"已經 {d} 天沒有登入；不需要的帳號請停用。"));
            }
        }

        return new AdminSecurityOverviewDto
        {
            GeneratedAt = now, AuditTrailAvailable = false, AuditTrailMessage = AuditMessage,
            ActiveAccounts = accounts.Count(a => a.Status == "active"), LockedAccounts = accounts.Count(a => a.IsLockedNow),
            DormantAccounts = alerts.Count(a => a.Kind is "dormant" or "never_logged_in"), Alerts = alerts, Accounts = accounts,
        };
    }

    private static AdminSecurityAlertDto Alert(string kind, string label, AdminAccountActivityDto a, string message)
        => new() { Kind = kind, KindLabel = label, AccountId = a.Id, Username = a.Username, Message = message };
}
