using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class SponsorPackage
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public int? PriceMin { get; set; }

    public int? PriceMax { get; set; }

    public bool IsPricePublic { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<SponsorPackagesI18n> SponsorPackagesI18ns { get; set; } = new List<SponsorPackagesI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<Sponsor> Sponsors { get; set; } = new List<Sponsor>();
}
