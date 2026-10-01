namespace Tcrfc.Api.Data.EfEntities;

// E 批（2026-10-01）：會員前台登入所需的欄位。與 Member.cs（scaffold 產生檔）分開，避免重新 scaffold 時被覆寫。
public partial class Member
{
    /// <summary>連續登入失敗次數（成功登入歸零）。</summary>
    public int FailedAttemptCount { get; set; }

    /// <summary>鎖定到期時間（UTC）；空或已過＝未鎖定。</summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>LINE userId 的 SHA-256（小寫十六進位）。只供查找，不得匯出。</summary>
    public string? LineUserIdHash { get; set; }
}

public partial class MembershipPayment
{
    /// <summary>由付款訂單開通時的來源訂單（手動開通為空）。</summary>
    public Guid? MembershipOrderId { get; set; }
}
