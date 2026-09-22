using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ProposalFile
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ProposalId { get; set; }

    public string Locale { get; set; } = null!;

    public string FileKey { get; set; } = null!;

    public int? FileBytes { get; set; }

    public int VersionNo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Proposal Proposal { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
