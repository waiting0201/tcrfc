namespace Tcrfc.Api.Features.AdminForms;

/// <summary>G1 表單設計器清單項目——9 個固定 <c>form_code</c> 之一（見
/// <c>Features.Forms.FormCatalog</c>）。**不含表單顯示名稱**：規劃書 §3.10 的固定表格中英名稱
/// 已於 2026-09-22 拍板不建 <c>forms_i18n.name</c>、不開放後台編輯（docs/12-database-schema.md
/// §4.6），顯示名稱由前端依 <see cref="FormCode"/> 對照規劃書固定表格自行呈現，不是本 API 的職責。</summary>
public sealed record AdminFormListItemDto
{
    public required Guid Id { get; init; }
    public required string FormCode { get; init; }
    public string? NotifyEmails { get; init; }
    public required bool CaptchaEnabled { get; init; }
    public string? RedirectPath { get; init; }
    public required int FieldCount { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public sealed record AdminFormFieldDto
{
    public required Guid Id { get; init; }
    public required string FieldKey { get; init; }
    public required string FieldType { get; init; }

    /// <summary>題目文字（中文）——<c>form_fields_i18n</c> zh-Hant 列，必存（S1-10 修正，
    /// 2026-09-25，補齊 CLAUDE.md 全域規定第 4 條的落差）。</summary>
    public required string LabelZh { get; init; }

    /// <summary>題目文字（英文）——<c>form_fields_i18n</c> en 列，可缺（<c>null</c>＝尚未翻譯，
    /// 公開端點會回退顯示中文）。</summary>
    public string? LabelEn { get; init; }

    public required bool IsRequired { get; init; }
    public string? ValidationRule { get; init; }

    /// <summary>只有 <c>select</c>／<c>multiselect</c> 會有值，其餘型別一律 <c>null</c>
    /// （對應 <c>form_fields.options_json</c> 解析後的陣列——**canonical 值**，公開送出端點拿這份
    /// 驗證與儲存，不因語系而變，語意上等同這些選項的中文顯示文字）。</summary>
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>選項的英文顯示文字，與 <see cref="Options"/> 同順序、同筆數；<c>null</c>＝這個
    /// 欄位沒有選項，或選項尚未提供英文翻譯（公開端點會回退顯示中文 <see cref="Options"/>）。</summary>
    public IReadOnlyList<string>? OptionLabelsEn { get; init; }

    /// <summary>是否為 G2 收件匣「內容摘要」欄的來源欄位——同一張表單最多一個欄位為
    /// <c>true</c>，設定第二個會自動取代第一個（不是回錯誤），見
    /// <c>AdminFormsRepository.CreateFieldAsync</c>／<c>UpdateFieldAsync</c>。</summary>
    public required bool IsSummary { get; init; }

    public required int SortOrder { get; init; }
}

public sealed record AdminFormDetailDto
{
    public required Guid Id { get; init; }
    public required string FormCode { get; init; }
    public string? NotifyEmails { get; init; }
    public required bool CaptchaEnabled { get; init; }
    public string? RedirectPath { get; init; }

    /// <summary>自動回覆信文案——<c>forms_i18n.auto_reply_body</c> 是本表單唯一的雙語欄位
    /// （2026-09-22 拍板已限縮範圍，見 docs/12-database-schema.md §4.6）。</summary>
    public string? AutoReplyBodyZh { get; init; }
    public string? AutoReplyBodyEn { get; init; }

    public required IReadOnlyList<AdminFormFieldDto> Fields { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>更新表單設定——**不含 <see cref="AdminFormDetailDto.FormCode"/>**：9 個表單是固定目錄，
/// 不開放改代碼（見 <c>Features/Forms/FormCatalog.cs</c> 檔頭）。</summary>
public sealed record UpdateAdminFormRequest
{
    /// <summary>收件通知 Email，可多人——以逗號或分號分隔，見
    /// <c>AdminFormsRepository.ParseAndValidateEmails</c>。省略或空字串＝清空。</summary>
    public string? NotifyEmails { get; init; }

    public required bool CaptchaEnabled { get; init; }
    public string? RedirectPath { get; init; }
    public string? AutoReplyBodyZh { get; init; }
    public string? AutoReplyBodyEn { get; init; }
}

/// <summary>建立動態欄位。<see cref="SortOrder"/> 省略時自動接在最後一個欄位之後。
/// <see cref="LabelZh"/> 為必填（題目文字，前台一定要有東西可顯示）；<see cref="LabelEn"/>
/// 可省略（英文尚未翻譯時公開端點回退顯示中文）。</summary>
public sealed record CreateAdminFormFieldRequest
{
    public required string FieldKey { get; init; }
    public required string FieldType { get; init; }
    public required string LabelZh { get; init; }
    public string? LabelEn { get; init; }
    public bool IsRequired { get; init; }
    public string? ValidationRule { get; init; }
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>選項的英文顯示文字，省略或 <c>null</c>＝尚未翻譯（公開端點回退顯示 <see cref="Options"/>
    /// 中文）。提供時筆數必須與 <see cref="Options"/> 一致，見 <c>AdminFormsRepository.ValidateOptionLabelsEn</c>。</summary>
    public IReadOnlyList<string>? OptionLabelsEn { get; init; }

    public bool IsSummary { get; init; }
    public int? SortOrder { get; init; }
}

public sealed record UpdateAdminFormFieldRequest
{
    public required string FieldKey { get; init; }
    public required string FieldType { get; init; }
    public required string LabelZh { get; init; }
    public string? LabelEn { get; init; }
    public bool IsRequired { get; init; }
    public string? ValidationRule { get; init; }
    public IReadOnlyList<string>? Options { get; init; }
    public IReadOnlyList<string>? OptionLabelsEn { get; init; }
    public bool IsSummary { get; init; }
    public required int SortOrder { get; init; }
}
