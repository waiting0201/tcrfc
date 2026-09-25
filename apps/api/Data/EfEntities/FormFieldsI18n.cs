using System;
using System.Collections.Generic;

namespace Tcrfc.Api.Data.EfEntities;

/// <summary>G1 表單設計器的動態欄位題目文字（<see cref="Label"/>，zh-Hant 列必存、en 列可缺）＋
/// 下拉／多選選項的顯示文字（<see cref="OptionsJson"/>，只有 en 列會有值，見
/// <c>Features/AdminForms/AdminFormsRepository.cs</c> 與 docs/12-database-schema.md §12 第 40 點）。
/// S1-10 修正（2026-09-25）新增，補齊「規劃書明文要求前台可見內容皆需雙語」（CLAUDE.md 全域規定
/// 第 4 條）在動態表單欄位上的落差。</summary>
public partial class FormFieldsI18n
{
    public Guid FormFieldId { get; set; }

    public string Locale { get; set; } = null!;

    public string Label { get; set; } = null!;

    /// <summary>下拉／多選選項的**顯示文字**，與 <c>form_fields.options_json</c>（canonical、
    /// 用來驗證與儲存送出值）同順序、同筆數的 JSON 字串陣列。**只有這個語系有自訂顯示文字時才會有
    /// 這一列**（通常是 en）——canonical 值本身就是 zh-Hant 的顯示文字，不需要另外存一份 zh-Hant
    /// 選項顯示文字側表列。見 <c>Features/Forms/FormsRepository.cs</c> 的讀取回退邏輯。</summary>
    public string? OptionsJson { get; set; }

    public virtual FormField FormField { get; set; } = null!;
}
