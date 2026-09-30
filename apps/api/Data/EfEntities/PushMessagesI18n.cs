using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class PushMessagesI18n
{
    public Guid PushMessageId { get; set; }

    public string Locale { get; set; } = null!;

    public string? Title { get; set; }

    public string? Body { get; set; }

    public string? ImageAlt { get; set; }

    public virtual PushMessage PushMessage { get; set; } = null!;
}
