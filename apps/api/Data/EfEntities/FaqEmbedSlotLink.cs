using System;

namespace Tcrfc.Api.Data.EfEntities;

/// <summary>該題「額外」指定出現於哪個 G-12 掛載點，疊加在分類自動對應之上（不是取代）。
/// 形狀比照 <c>StaffTeam</c>：複合主鍵的關聯表，帶一個 payload 欄位（<see cref="SortOrder"/>），
/// 因此宣告成獨立實體而不是 EF 的 skip-navigation 多對多。</summary>
public partial class FaqEmbedSlotLink
{
    public Guid FaqId { get; set; }

    public Guid FaqEmbedSlotId { get; set; }

    public int SortOrder { get; set; }

    public virtual Faq Faq { get; set; } = null!;

    public virtual FaqEmbedSlot FaqEmbedSlot { get; set; } = null!;
}
