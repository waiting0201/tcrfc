namespace Tcrfc.Api.Data.EfEntities;

/// <summary>會員（網頁）登入的更新權杖（E 批）。機制同 <see cref="AdminRefreshToken"/>：只存雜湊、輪替鏈、重放偵測。</summary>
public partial class MemberRefreshToken
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid MemberId { get; set; }

    public string TokenHash { get; set; } = null!;

    /// <summary>「記住我」：Cookie 帶到期日、較長效期。</summary>
    public bool IsPersistent { get; set; }

    public DateTime IssuedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid? ReplacedById { get; set; }

    public virtual Member Member { get; set; } = null!;

    public virtual MemberRefreshToken? ReplacedBy { get; set; }
}
