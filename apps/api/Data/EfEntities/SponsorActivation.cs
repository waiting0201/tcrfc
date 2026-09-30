using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class SponsorActivation
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid SponsorId { get; set; }

    public DateOnly? HappenedOn { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Sponsor Sponsor { get; set; } = null!;

    public virtual ICollection<SponsorActivationImage> SponsorActivationImages { get; set; } = new List<SponsorActivationImage>();

    public virtual ICollection<SponsorActivationsI18n> SponsorActivationsI18ns { get; set; } = new List<SponsorActivationsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
