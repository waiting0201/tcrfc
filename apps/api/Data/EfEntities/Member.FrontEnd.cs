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

    /// <summary>監護人同意時間（伺服器時間，UTC；2026-10-05）。成年註冊為 null。未滿 18 歲註冊必填（主站規劃書「會員資料安全要求」、App 規劃書 §4.5）。</summary>
    public DateTime? GuardianConsentedAt { get; set; }

    /// <summary>監護人姓名——🔒 受限個資（docs/12b §8），後台遮罩、刪帳號時清除。</summary>
    public string? GuardianName { get; set; }

    /// <summary>與當事人關係：<c>parent</c>（父母）／<c>legal_guardian</c>（法定監護人）。</summary>
    public string? GuardianRelationship { get; set; }

    /// <summary>同意文案版本（文案本身待法務 B-9）；可為 null。</summary>
    public string? GuardianConsentVersion { get; set; }
}

public partial class MembershipPayment
{
    /// <summary>由付款訂單開通時的來源訂單（手動開通為空）。</summary>
    public Guid? MembershipOrderId { get; set; }
}
