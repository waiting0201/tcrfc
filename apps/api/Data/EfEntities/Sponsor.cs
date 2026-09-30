using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Sponsor
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public string? Tier { get; set; }

    public string? LogoDarkKey { get; set; }

    public string? LogoLightKey { get; set; }

    public DateOnly? ContractStartOn { get; set; }

    public DateOnly? ContractEndOn { get; set; }

    public string? ContactName { get; set; }

    public string? ContactPhone { get; set; }

    public string? ContactEmail { get; set; }

    public DateOnly? ExpiryAlertOn { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<SponsorActivation> SponsorActivations { get; set; } = new List<SponsorActivation>();

    public virtual ICollection<SponsorArticle> SponsorArticles { get; set; } = new List<SponsorArticle>();

    public virtual ICollection<SponsorsI18n> SponsorsI18ns { get; set; } = new List<SponsorsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<CharityProgram> CharityPrograms { get; set; } = new List<CharityProgram>();

    public virtual ICollection<SponsorPackage> SponsorPackages { get; set; } = new List<SponsorPackage>();
    public virtual ICollection<Advertiser> Advertisers { get; set; } = new List<Advertiser>();
}
