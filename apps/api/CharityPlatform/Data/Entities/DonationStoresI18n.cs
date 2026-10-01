using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class DonationStoresI18n
{
    public Guid DonationStoreId { get; set; }

    public string Locale { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? LogoAlt { get; set; }

    public virtual DonationStore DonationStore { get; set; } = null!;

    public virtual Locale LocaleNavigation { get; set; } = null!;
}
