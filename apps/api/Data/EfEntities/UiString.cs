using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class UiString
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string StringKey { get; set; } = null!;

    public string? StringGroup { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<UiStringTranslation> UiStringTranslations { get; set; } = new List<UiStringTranslation>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
