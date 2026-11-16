using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class CharityRef
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public string RefCode { get; set; } = null!;

    public string Name { get; set; } = null!;

    public DateTime ImportedAt { get; set; }

    public string? Source { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }
    /// <summary>主站 id（同步的對照鍵）。空＝同步機制之前的列，同步不碰。</summary>
    public Guid? ExternalId { get; set; }

    public string? NameEn { get; set; }

    /// <summary>主站已下架或刪除 → false（不刪列：既有項目與憑證引用其快照）。</summary>
    public bool IsActive { get; set; } = true;

    public DateTime? DeactivatedAt { get; set; }

    public DateTime? LastSyncedAt { get; set; }


    public virtual ICollection<CharityProgramRef> CharityProgramRefs { get; set; } = new List<CharityProgramRef>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
