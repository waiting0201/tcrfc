using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Charity
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid? ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public string? LogoKey { get; set; }

    public string? WebsiteUrl { get; set; }

    public string? ContactName { get; set; }

    public string? ContactPhone { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<CharitiesI18n> CharitiesI18ns { get; set; } = new List<CharitiesI18n>();

    public virtual ICollection<CharityProgram> CharityPrograms { get; set; } = new List<CharityProgram>();

    public virtual Club? Club { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<ImpactRecord> ImpactRecords { get; set; } = new List<ImpactRecord>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
