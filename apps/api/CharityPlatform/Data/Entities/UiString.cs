using System;
using System.Collections.Generic;

namespace Tcrfc.Api.CharityPlatform.Data.Entities;

public partial class UiString
{
    public long Seq { get; set; }

    public Guid Id { get; set; }

    public string StringKey { get; set; } = null!;

    public string? GroupName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<UiStringTranslation> UiStringTranslations { get; set; } = new List<UiStringTranslation>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
