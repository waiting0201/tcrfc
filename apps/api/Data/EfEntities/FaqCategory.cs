using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FaqCategory
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public string Slug { get; set; } = null!;

    /// <summary>軟停用（S1-7a）：<c>false</c>＝從導覽消失，但既有題目與關聯不受影響，可重新啟用——
    /// 取代先前「用刪除湊停用」的做法（刪除經 <c>ON DELETE CASCADE</c> 不可逆），見
    /// db/club-schema.sql 該表註解。</summary>
    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<FaqCategoriesI18n> FaqCategoriesI18ns { get; set; } = new List<FaqCategoriesI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }

    public virtual ICollection<Faq> Faqs { get; set; } = new List<Faq>();
}
