using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ComicCharactersI18n
{
    public Guid ComicCharacterId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Name { get; set; }

    public string? Description { get; set; }

    public virtual ComicCharacter ComicCharacter { get; set; } = null!;
}
