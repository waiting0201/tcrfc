using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ComicCharacter
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid? PlayerId { get; set; }

    public string? ImageKey { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual ICollection<ComicCharactersI18n> ComicCharactersI18ns { get; set; } = new List<ComicCharactersI18n>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Player? Player { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
