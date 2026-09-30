using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PushTopicSubscription
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid DeviceId { get; set; }

    public Guid? MemberId { get; set; }

    public string TopicType { get; set; } = null!;

    public string TopicValue { get; set; } = null!;

    public bool IsFollowing { get; set; }

    public bool IsPushEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual AppDevice Device { get; set; } = null!;

    public virtual Member? Member { get; set; }
}
