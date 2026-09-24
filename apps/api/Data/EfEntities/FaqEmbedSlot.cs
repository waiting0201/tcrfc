using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

/// <summary>FAQ 快捷區塊（G-12）掛載點字典（S1-8／S1-7a）。字典本身由種子 DML 灌入四筆固定值
/// （academy_admission／program_detail／trials／sponsorship），不提供後台新增／刪除 CRUD——
/// 見 db/club-schema.sql 該表註解與 apps/api/README.md。</summary>
public partial class FaqEmbedSlot
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<FaqEmbedSlotLink> FaqEmbedSlotLinks { get; set; } = new List<FaqEmbedSlotLink>();
}
