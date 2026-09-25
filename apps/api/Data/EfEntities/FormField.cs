using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

public partial class FormField
{
    public Guid Id { get; set; }

    public long RowSeq { get; set; }

    public Guid FormId { get; set; }

    public string FieldKey { get; set; } = null!;

    public string FieldType { get; set; } = null!;

    public bool IsRequired { get; set; }

    public string? ValidationRule { get; set; }

    /// <summary>下拉／多選的選項清單（JSON 字串陣列），<see cref="FieldType"/> 不是
    /// <c>select</c>／<c>multiselect</c> 時維持 <c>null</c>。S1-10 新增，見
    /// db/club-schema.sql「form_fields」表註解與 docs/12-database-schema.md §12 第 37 點。</summary>
    public string? OptionsJson { get; set; }

    /// <summary>G2 收件匣「內容摘要」欄的來源鍵，同一張表單最多一個欄位可標記為 <c>true</c>。
    /// S1-10 新增，見 db/club-schema.sql 註解與 docs/12-database-schema.md §12 第 38 點。</summary>
    public bool IsSummary { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual AdminUser? CreatedByNavigation { get; set; }

    public virtual ICollection<EnquiryAnswer> EnquiryAnswers { get; set; } = new List<EnquiryAnswer>();

    public virtual Form Form { get; set; } = null!;

    /// <summary>題目文字（<see cref="FormFieldsI18n.Label"/>）與下拉／多選選項的英文顯示文字
    /// （<see cref="FormFieldsI18n.OptionsJson"/>）。S1-10 修正（2026-09-25）新增，見
    /// db/club-schema.sql「form_fields_i18n」表註解與 docs/12-database-schema.md §12 第 40 點。</summary>
    public virtual ICollection<FormFieldsI18n> FormFieldsI18ns { get; set; } = new List<FormFieldsI18n>();

    public virtual AdminUser? UpdatedByNavigation { get; set; }
}
