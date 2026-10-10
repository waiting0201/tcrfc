using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class ProgramsI18n
{
    public Guid ProgramId { get; set; }

    public string Locale { get; set; } = null!;

    public string? CoverAlt { get; set; }

    public string? Name { get; set; }

    public string? Intro { get; set; }

    public string? Content { get; set; }
    public string? Audience { get; set; }

    public virtual TrainingProgram TrainingProgram { get; set; } = null!;
}
