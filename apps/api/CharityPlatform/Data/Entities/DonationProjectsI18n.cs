using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class DonationProjectsI18n
{
    public Guid DonationProjectId { get; set; }

    public string Locale { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? OneLiner { get; set; }

    public string? Description { get; set; }

    public string? FundUsage { get; set; }

    public string? CoverAlt { get; set; }

    public virtual DonationProject DonationProject { get; set; } = null!;

    public virtual Locale LocaleNavigation { get; set; } = null!;
}
