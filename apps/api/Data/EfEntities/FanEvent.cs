using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FanEvent
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid ClubId { get; set; }

    public string Slug { get; set; } = null!;

    public DateTime? StartsAt { get; set; }

    public int? Capacity { get; set; }

    public bool IsPaidMembersOnly { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public string? CoverKey { get; set; }

    public int? CoverWidth { get; set; }

    public int? CoverHeight { get; set; }

    public DateTime? EndsAt { get; set; }

    public DateTime? RegistrationDeadlineAt { get; set; }

    public Guid? VenueId { get; set; }

    public string Status { get; set; } = null!;

    public virtual Club Club { get; set; } = null!;

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<FanEventArticle> FanEventArticles { get; set; } = new List<FanEventArticle>();

    public virtual ICollection<FanEventImage> FanEventImages { get; set; } = new List<FanEventImage>();

    public virtual ICollection<FanEventRegistration> FanEventRegistrations { get; set; } = new List<FanEventRegistration>();

    public virtual ICollection<FanEventsI18n> FanEventsI18ns { get; set; } = new List<FanEventsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual Venue? Venue { get; set; }
}
