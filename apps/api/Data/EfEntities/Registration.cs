using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Registration
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string RegistrationNo { get; set; } = null!;

    public Guid ClubId { get; set; }

    public Guid? SessionId { get; set; }

    public Guid? TrialId { get; set; }

    public Guid? MemberId { get; set; }

    public string ApplicantName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public DateOnly? BirthOn { get; set; }

    public string? GuardianName { get; set; }

    public string? GuardianPhone { get; set; }

    public string? HealthDeclaration { get; set; }

    public string? Note { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual Member? Member { get; set; }

    public virtual Session? Session { get; set; }

    public virtual Trial? Trial { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
