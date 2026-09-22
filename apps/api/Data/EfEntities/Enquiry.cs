using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Enquiry
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public Guid FormId { get; set; }

    public Guid? AssigneeAdminUserId { get; set; }

    public string? SourcePath { get; set; }

    public string? UtmSource { get; set; }

    public string? UtmCampaign { get; set; }

    public string? Status { get; set; }

    public string? InternalNote { get; set; }

    public string? Tags { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? AssigneeAdminUser { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<EnquiryAnswer> EnquiryAnswers { get; set; } = new List<EnquiryAnswer>();

    public virtual Form Form { get; set; } = null!;

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
