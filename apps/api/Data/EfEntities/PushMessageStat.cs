using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PushMessageStat
{
    public Guid PushMessageId { get; set; }

    public string Platform { get; set; } = null!;

    public string Locale { get; set; } = null!;

    public int Sent { get; set; }

    public int Delivered { get; set; }

    public int Opened { get; set; }

    public virtual PushMessage PushMessage { get; set; } = null!;
}
