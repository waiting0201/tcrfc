using System;
using System.Collections.Generic;
namespace Tcrfc.Api.Data.EfEntities;

public partial class ProposalsI18n
{
    public Guid ProposalId { get; set; }
    public string Locale { get; set; } = null!;
    public string? Title { get; set; }
    public virtual Proposal Proposal { get; set; } = null!;
}
