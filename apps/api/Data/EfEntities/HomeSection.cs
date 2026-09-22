using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class HomeSection
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string SectionCode { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public int SortOrder { get; set; }

    public Guid? FeaturedBannerId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Banner? FeaturedBanner { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
