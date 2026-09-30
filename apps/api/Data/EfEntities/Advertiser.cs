using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Advertiser
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string? TaxId { get; set; }

    public string? ContactName { get; set; }

    public string? ContactPhone { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContractNote { get; set; }

    public DateOnly? CooperationStartOn { get; set; }

    public DateOnly? CooperationEndOn { get; set; }

    public Guid? SponsorId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<AdCampaign> AdCampaigns { get; set; } = new List<AdCampaign>();

    public virtual ICollection<AdvertisersI18n> AdvertisersI18ns { get; set; } = new List<AdvertisersI18n>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Sponsor? Sponsor { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
