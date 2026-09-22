using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Banner
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string ImageKey { get; set; } = null!;

    public DateTime? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<BannersI18n> BannersI18ns { get; set; } = new List<BannersI18n>();

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<HomeSection> HomeSections { get; set; } = new List<HomeSection>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
