using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PartnerStore
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid? ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public string? ImageKey { get; set; }

    public int? ImageWidth { get; set; }

    public int? ImageHeight { get; set; }

    public string? Category { get; set; }

    public string? Address { get; set; }

    public string? Region { get; set; }

    public decimal? Lat { get; set; }

    public decimal? Lng { get; set; }

    public string? Phone { get; set; }

    public string? BusinessHours { get; set; }

    public string? MapUrl { get; set; }

    public string? WebsiteUrl { get; set; }

    public string ApplicableTier { get; set; } = null!;

    public DateOnly? StartOn { get; set; }

    public DateOnly? EndOn { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club? Club { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<PartnerStoresI18n> PartnerStoresI18ns { get; set; } = new List<PartnerStoresI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
