using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Partner
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public string? PartnerType { get; set; }

    public string? Country { get; set; }

    public string? LogoDarkKey { get; set; }

    public string? LogoLightKey { get; set; }

    public DateOnly? StartOn { get; set; }

    public DateOnly? EndOn { get; set; }

    public string? WebsiteUrl { get; set; }

    public bool ShowInFooter { get; set; }

    public bool ShowOnHome { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<PartnersI18n> PartnersI18ns { get; set; } = new List<PartnersI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<TrainingProgram> Programs { get; set; } = new List<TrainingProgram>();
}
