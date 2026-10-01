using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class Locale
{
    public string Code { get; set; } = null!;

    public string NameZh { get; set; } = null!;

    public string? NameEn { get; set; }

    public bool IsDefault { get; set; }

    public string? FallbackLocale { get; set; }

    public int SortOrder { get; set; }

    public virtual ICollection<DonationProjectsI18n> DonationProjectsI18ns { get; set; } = new List<DonationProjectsI18n>();

    public virtual ICollection<DonationStoresI18n> DonationStoresI18ns { get; set; } = new List<DonationStoresI18n>();

    public virtual ICollection<EmailTemplatesI18n> EmailTemplatesI18ns { get; set; } = new List<EmailTemplatesI18n>();

    public virtual Locale? FallbackLocaleNavigation { get; set; }

    public virtual ICollection<Locale> InverseFallbackLocaleNavigation { get; set; } = new List<Locale>();

    public virtual ICollection<SettingsI18n> SettingsI18ns { get; set; } = new List<SettingsI18n>();

    public virtual ICollection<UiStringTranslation> UiStringTranslations { get; set; } = new List<UiStringTranslation>();
}
