using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Features.AdminAds;

/// <summary>
/// 投放檔期的狀態機與自動推進（App 規劃書 §7.4）。
/// <code>
/// 草稿 → 待審核 → 已排程 → 投放中 → 已結束 → 已結案
///                              ↓ ↑
///                           已暫停（回到暫停前的狀態）        另有：已作廢（任一狀態可轉入，不可逆）
/// </code>
/// 「已排程 → 投放中 → 已結束」由起訖時間自動推進，其餘為人工操作。
/// 🔴 寫死的規則：<b>素材未通過審核的檔期不得進入「投放中」</b>——自動推進與「恢復」都要有至少一個「已通過審核且未暫停」的素材。
/// </summary>
public static class AdCampaignLifecycle
{
    public const string Draft = "draft";
    public const string PendingReview = "pending_review";
    public const string Scheduled = "scheduled";
    public const string Running = "running";
    public const string Paused = "paused";
    public const string Ended = "ended";
    public const string Closed = "closed";
    public const string Voided = "voided";

    private static long _lastAdvanceTicks;

    /// <summary>目前狀態下，依「呼叫者的權限」可以做哪些人工操作（給畫面決定按鈕，伺服器端仍會各自再檢查）。</summary>
    public static IReadOnlyList<string> AvailableActions(string status, bool canUpdate, bool canReview, bool canPause)
    {
        var actions = new List<string>();
        switch (status)
        {
            case Draft when canUpdate:
                actions.AddRange(["edit", "submit", "delete", "void"]);
                break;
            case PendingReview:
                if (canReview) { actions.AddRange(["approve", "return"]); }
                if (canUpdate) { actions.Add("void"); }
                break;
            case Scheduled or Running:
                if (canUpdate) { actions.Add("edit"); }
                if (canPause) { actions.Add("pause"); }
                if (canUpdate) { actions.Add("void"); }
                break;
            case Paused:
                if (canUpdate) { actions.Add("edit"); }
                if (canPause) { actions.Add("resume"); }
                if (canUpdate) { actions.Add("void"); }
                break;
            case Ended when canUpdate:
                actions.AddRange(["close", "void"]);
                break;
            case Closed when canUpdate:
                actions.Add("void");
                break;
        }

        return actions;
    }

    /// <summary>依起訖時間推進狀態（set-based，可重複執行）。回傳 (啟動幾個, 結束幾個)。</summary>
    public static async Task<(int Started, int Ended)> AdvanceAsync(ClubDbContext db, DateTime utcNow, CancellationToken cancellationToken)
    {
        var started = await db.AdCampaigns
            .Where(c => c.Status == Scheduled && c.StartsAt <= utcNow && c.EndsAt > utcNow
                        && c.AdCreatives.Any(x => x.ReviewStatus == "approved" && !x.IsPaused))
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, Running).SetProperty(c => c.UpdatedAt, utcNow), cancellationToken);
        var ended = await db.AdCampaigns
            .Where(c => (c.Status == Scheduled || c.Status == Running || c.Status == Paused) && c.EndsAt <= utcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, Ended).SetProperty(c => c.PausedFrom, (string?)null).SetProperty(c => c.UpdatedAt, utcNow), cancellationToken);
        return (started, ended);
    }

    /// <summary>節流版（同一行程 30 秒內最多推進一次），給高頻的公開投放端點與後台清單呼叫。</summary>
    public static async Task AdvanceIfDueAsync(ClubDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var last = Interlocked.Read(ref _lastAdvanceTicks);
        if (now.Ticks - last < TimeSpan.FromSeconds(30).Ticks)
        {
            return;
        }

        Interlocked.Exchange(ref _lastAdvanceTicks, now.Ticks);
        await AdvanceAsync(db, now, cancellationToken);
    }

    /// <summary>測試用：讓下一次 <see cref="AdvanceIfDueAsync"/> 一定執行。</summary>
    public static void ResetThrottle() => Interlocked.Exchange(ref _lastAdvanceTicks, 0);
}
