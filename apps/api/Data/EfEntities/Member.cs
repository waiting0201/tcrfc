using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class Member
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string MemberNo { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Phone { get; set; }

    public DateOnly? BirthOn { get; set; }

    public string? LineUserIdEncrypted { get; set; }

    public string SignupSource { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();

    public virtual ICollection<FanEventRegistration> FanEventRegistrations { get; set; } = new List<FanEventRegistration>();

    public virtual ICollection<JerseyIssue> JerseyIssues { get; set; } = new List<JerseyIssue>();

    public virtual ICollection<Membership> Memberships { get; set; } = new List<Membership>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
