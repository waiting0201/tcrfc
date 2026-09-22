using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class EnquiryAnswer
{
    public Guid EnquiryId { get; set; }

    public Guid FormFieldId { get; set; }

    public string? Value { get; set; }

    public virtual Enquiry Enquiry { get; set; } = null!;

    public virtual FormField FormField { get; set; } = null!;
}
