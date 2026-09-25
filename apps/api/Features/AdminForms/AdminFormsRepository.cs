using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminForms;

/// <summary>
/// G1「表單設計器」——俱樂部範圍的表單設定與動態欄位管理（主站規劃書 §4.7 G1）。
///
/// 🔴 **表單本身是固定目錄，沒有建立／刪除表單的端點**：9 個 <c>form_code</c>
/// （見 <see cref="FormCatalog"/>）由種子腳本各俱樂部各種一份，本檔只服務「編輯既有表單的設定」
/// 與「管理底下的動態欄位」——這是 2026-09-22 已拍板「表單顯示名稱維持規劃書 §3.10 固定表格
/// 寫死，不開放後台編輯」（docs/12-database-schema.md §4.6）在應用層的延伸：**名稱不能改，
/// 代碼更不能改／不能新增**，能改的只有通知信收件者、自動回覆信、CAPTCHA 開關、送出後導向，
/// 以及底下欄位的組成。
///
/// 🔴 **沒有 `UNIQUE (form_id, field_key)` 的資料庫層防線**（`db/club-schema.sql` 原本就沒有，
/// 本輪判斷不額外新增這個約束——見任務回報「規劃書沒寫清楚、自行判斷」），<see cref="CreateFieldAsync"/>／
/// <see cref="UpdateFieldAsync"/> 在應用層擋重複，這是唯一防線。
///
/// 🔴 **刪除欄位若已有 <c>enquiry_answers</c> 引用會被擋下（409）**：
/// `FK_enquiry_answers_field` 沒有 `ON DELETE CASCADE`（`db/club-schema.sql` 註解），
/// 這裡先查一次，給出中文訊息，不讓呼叫端直接撞到 SQL Server 的 547 錯誤。
/// </summary>
public sealed class AdminFormsRepository(ClubDbContext dbContext)
{
    public async Task<IReadOnlyList<AdminFormListItemDto>> ListAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => await dbContext.Forms.AsNoTracking()
            .Where(f => f.ClubId == scope.ClubId)
            .OrderBy(f => f.FormCode)
            .Select(f => new AdminFormListItemDto
            {
                Id = f.Id,
                FormCode = f.FormCode,
                NotifyEmails = f.NotifyEmails,
                CaptchaEnabled = f.CaptchaEnabled,
                RedirectPath = f.RedirectPath,
                FieldCount = f.FormFields.Count,
                UpdatedAt = f.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

    public async Task<AdminFormDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var form = await dbContext.Forms.AsNoTracking()
            .Include(f => f.FormFields).ThenInclude(ff => ff.FormFieldsI18ns)
            .Include(f => f.FormsI18ns)
            .FirstOrDefaultAsync(f => f.Id == id && f.ClubId == scope.ClubId, cancellationToken);

        return form is null ? null : ToDetailDto(form);
    }

    public async Task<AdminFormDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminFormRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var form = await dbContext.Forms
            .Include(f => f.FormFields).ThenInclude(ff => ff.FormFieldsI18ns)
            .Include(f => f.FormsI18ns)
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        if (form is null || form.ClubId != scope.ClubId)
        {
            return null; // 跨俱樂部：回 404，不洩漏存在與否——比照既有 AdminRegistrationsRepository 慣例。
        }

        var normalizedEmails = ParseAndValidateEmails(request.NotifyEmails);
        ValidateRedirectPath(request.RedirectPath);

        form.NotifyEmails = normalizedEmails;
        form.CaptchaEnabled = request.CaptchaEnabled;
        form.RedirectPath = request.RedirectPath;
        form.UpdatedAt = DateTime.UtcNow;
        form.UpdatedBy = operatorId;

        UpsertI18n(form, RequestLocale.DefaultDbLocale, request.AutoReplyBodyZh);
        UpsertI18n(form, "en", request.AutoReplyBodyEn);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetailDto(form);
    }

    public async Task<AdminFormFieldDto?> CreateFieldAsync(
        AdminClubScope scope, Guid formId, CreateAdminFormFieldRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var form = await dbContext.Forms.Include(f => f.FormFields)
            .FirstOrDefaultAsync(f => f.Id == formId, cancellationToken);
        if (form is null || form.ClubId != scope.ClubId)
        {
            return null;
        }

        var fieldKey = ValidateFieldKey(request.FieldKey);
        var fieldType = ValidateFieldType(request.FieldType);
        var validationRule = ValidateValidationRule(request.ValidationRule);
        var optionsJson = ValidateOptions(fieldType, request.Options);
        var labelZh = ValidateLabelZh(request.LabelZh);
        var labelEn = NormalizeLabelEn(request.LabelEn);
        var optionLabelsEnJson = ValidateOptionLabelsEn(request.Options, request.OptionLabelsEn);

        if (form.FormFields.Any(f => string.Equals(f.FieldKey, fieldKey, StringComparison.Ordinal)))
        {
            throw new AdminFormFieldKeyConflictException($"這張表單已經有欄位代碼「{fieldKey}」，請換一個名稱。");
        }

        var sortOrder = request.SortOrder ?? (form.FormFields.Count == 0 ? 0 : form.FormFields.Max(f => f.SortOrder) + 1);
        var now = DateTime.UtcNow;

        var field = new FormField
        {
            Id = Guid.NewGuid(),
            FormId = form.Id,
            FieldKey = fieldKey,
            FieldType = fieldType,
            IsRequired = request.IsRequired,
            ValidationRule = validationRule,
            OptionsJson = optionsJson,
            IsSummary = request.IsSummary,
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        field.FormFieldsI18ns.Add(new FormFieldsI18n { FormFieldId = field.Id, Locale = RequestLocale.DefaultDbLocale, Label = labelZh });
        if (labelEn is not null || optionLabelsEnJson is not null)
        {
            field.FormFieldsI18ns.Add(new FormFieldsI18n { FormFieldId = field.Id, Locale = "en", Label = labelEn ?? labelZh, OptionsJson = optionLabelsEnJson });
        }

        if (request.IsSummary)
        {
            UnmarkOtherSummaryFields(form, exceptFieldId: null);
        }

        dbContext.FormFields.Add(field);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToFieldDto(field);
    }

    public async Task<AdminFormFieldDto?> UpdateFieldAsync(
        AdminClubScope scope, Guid formId, Guid fieldId, UpdateAdminFormFieldRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var form = await dbContext.Forms.Include(f => f.FormFields).ThenInclude(ff => ff.FormFieldsI18ns)
            .FirstOrDefaultAsync(f => f.Id == formId, cancellationToken);
        if (form is null || form.ClubId != scope.ClubId)
        {
            return null;
        }

        var field = form.FormFields.FirstOrDefault(f => f.Id == fieldId);
        if (field is null)
        {
            return null;
        }

        var fieldKey = ValidateFieldKey(request.FieldKey);
        var fieldType = ValidateFieldType(request.FieldType);
        var validationRule = ValidateValidationRule(request.ValidationRule);
        var optionsJson = ValidateOptions(fieldType, request.Options);
        var labelZh = ValidateLabelZh(request.LabelZh);
        var labelEn = NormalizeLabelEn(request.LabelEn);
        var optionLabelsEnJson = ValidateOptionLabelsEn(request.Options, request.OptionLabelsEn);

        if (form.FormFields.Any(f => f.Id != fieldId && string.Equals(f.FieldKey, fieldKey, StringComparison.Ordinal)))
        {
            throw new AdminFormFieldKeyConflictException($"這張表單已經有欄位代碼「{fieldKey}」，請換一個名稱。");
        }

        if (request.IsSummary)
        {
            UnmarkOtherSummaryFields(form, exceptFieldId: fieldId);
        }

        field.FieldKey = fieldKey;
        field.FieldType = fieldType;
        field.IsRequired = request.IsRequired;
        field.ValidationRule = validationRule;
        field.OptionsJson = optionsJson;
        field.IsSummary = request.IsSummary;
        field.SortOrder = request.SortOrder;
        field.UpdatedAt = DateTime.UtcNow;
        field.UpdatedBy = operatorId;

        UpsertFieldI18n(field, RequestLocale.DefaultDbLocale, labelZh, null);
        if (labelEn is not null || optionLabelsEnJson is not null)
        {
            UpsertFieldI18n(field, "en", labelEn ?? labelZh, optionLabelsEnJson);
        }
        else
        {
            // 沒有英文題目也沒有英文選項文字：清掉既有的 en 列（若曾經設定過又被清空，公開端點
            // 應該回退顯示中文，不是留著一個永遠讀不到選項變動的舊 en 列殘影）。
            var existingEn = field.FormFieldsI18ns.FirstOrDefault(i => i.Locale == "en");
            if (existingEn is not null)
            {
                dbContext.FormFieldsI18ns.Remove(existingEn);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToFieldDto(field);
    }

    /// <returns><c>null</c>＝找不到表單或欄位（404）；<c>true</c>＝刪除成功。</returns>
    public async Task<bool?> DeleteFieldAsync(AdminClubScope scope, Guid formId, Guid fieldId, CancellationToken cancellationToken)
    {
        var form = await dbContext.Forms.FirstOrDefaultAsync(f => f.Id == formId, cancellationToken);
        if (form is null || form.ClubId != scope.ClubId)
        {
            return null;
        }

        var field = await dbContext.FormFields.FirstOrDefaultAsync(f => f.Id == fieldId && f.FormId == formId, cancellationToken);
        if (field is null)
        {
            return null;
        }

        var inUse = await dbContext.EnquiryAnswers.AsNoTracking().AnyAsync(a => a.FormFieldId == fieldId, cancellationToken);
        if (inUse)
        {
            throw new AdminFormFieldInUseException("這個欄位已經有詢問資料引用，無法刪除；如需停用請改調整表單設計，暫時不要在前台顯示。");
        }

        dbContext.FormFields.Remove(field);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ───────────────────────────── 驗證 ─────────────────────────────

    private static readonly Regex EmailPattern = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    private static readonly Regex FieldKeyPattern = new(
        @"^[a-z][a-z0-9_]{0,63}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    /// <summary>省略或空字串＝清空（回傳 <c>null</c>）；否則以逗號或分號分隔，逐一驗證格式、
    /// 去除空白項，重新以逗號組回單一字串存回 <c>forms.notify_emails</c>。</summary>
    internal static string? ParseAndValidateEmails(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var emails = raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (emails.Length == 0)
        {
            return null;
        }

        foreach (var email in emails)
        {
            if (!TryMatch(EmailPattern, email))
            {
                throw new AdminFormValidationException($"「{email}」不是合法的 Email 格式。");
            }
        }

        return string.Join(",", emails);
    }

    private static void ValidateRedirectPath(string? redirectPath)
    {
        if (string.IsNullOrWhiteSpace(redirectPath))
        {
            return;
        }
        if (!redirectPath.StartsWith('/') && !redirectPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !redirectPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new AdminFormValidationException("送出後導向的網址要用 / 開頭的相對路徑，或完整的 http(s):// 網址。");
        }
    }

    private static string ValidateFieldKey(string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(fieldKey) || !TryMatch(FieldKeyPattern, fieldKey))
        {
            throw new AdminFormValidationException("欄位代碼只能是英文小寫字母開頭，接英文小寫字母、數字或底線，長度 1–64。");
        }
        return fieldKey;
    }

    private static string ValidateFieldType(string fieldType)
    {
        if (!FormFieldTypes.Allowed.Contains(fieldType))
        {
            throw new AdminFormValidationException("欄位型別只能是文字、多行文字、下拉、多選、日期、檔案上傳或同意條款其中一種。");
        }
        return fieldType;
    }

    private static string? ValidateValidationRule(string? validationRule)
    {
        if (string.IsNullOrWhiteSpace(validationRule))
        {
            return null;
        }
        try
        {
            _ = new Regex(validationRule, RegexOptions.None, TimeSpan.FromMilliseconds(200));
        }
        catch (ArgumentException)
        {
            throw new AdminFormValidationException("驗證規則不是合法的正規表示式。");
        }
        return validationRule;
    }

    /// <summary>select／multiselect 必須提供至少一個選項；其餘型別不得提供選項
    /// （避免留下前端永遠讀不到、跟欄位型別對不上的殘留資料）。</summary>
    private static string? ValidateOptions(string fieldType, IReadOnlyList<string>? options)
    {
        var requiresOptions = FormFieldTypes.RequiresOptions.Contains(fieldType);

        if (!requiresOptions)
        {
            if (options is { Count: > 0 })
            {
                throw new AdminFormValidationException("只有下拉或多選欄位才能設定選項。");
            }
            return null;
        }

        if (options is null || options.Count == 0)
        {
            throw new AdminFormValidationException("下拉或多選欄位至少要有一個選項。");
        }

        var trimmed = options.Select(o => o.Trim()).ToList();
        if (trimmed.Any(string.IsNullOrEmpty))
        {
            throw new AdminFormValidationException("選項不能是空字串。");
        }
        if (trimmed.Distinct(StringComparer.Ordinal).Count() != trimmed.Count)
        {
            throw new AdminFormValidationException("選項內容不能重複。");
        }

        return JsonSerializer.Serialize(trimmed);
    }

    /// <summary>題目文字（中文）——必填，前台一定要有東西可顯示（S1-10 修正，2026-09-25）。</summary>
    private static string ValidateLabelZh(string labelZh)
    {
        if (string.IsNullOrWhiteSpace(labelZh))
        {
            throw new AdminFormValidationException("題目文字（中文）為必填欄位。");
        }
        if (labelZh.Length > 255)
        {
            throw new AdminFormValidationException("題目文字（中文）長度不能超過 255 個字元。");
        }
        return labelZh.Trim();
    }

    private static string? NormalizeLabelEn(string? labelEn)
    {
        if (string.IsNullOrWhiteSpace(labelEn))
        {
            return null;
        }
        if (labelEn.Length > 255)
        {
            throw new AdminFormValidationException("題目文字（英文）長度不能超過 255 個字元。");
        }
        return labelEn.Trim();
    }

    /// <summary>選項英文顯示文字——省略＝尚未翻譯（公開端點回退顯示中文）；提供時筆數必須跟
    /// <paramref name="canonicalOptionsJson"/>（<see cref="ValidateOptions"/> 算出的 canonical
    /// 選項，已序列化成 JSON）解析出的筆數一致，否則前台會拿到「選項與顯示文字對不上」的錯位資料。</summary>
    private static string? ValidateOptionLabelsEn(IReadOnlyList<string>? canonicalOptions, IReadOnlyList<string>? optionLabelsEn)
    {
        if (optionLabelsEn is null || optionLabelsEn.Count == 0)
        {
            return null;
        }

        if (canonicalOptions is null || canonicalOptions.Count == 0)
        {
            throw new AdminFormValidationException("這個欄位沒有選項，不能設定選項的英文顯示文字。");
        }
        if (optionLabelsEn.Count != canonicalOptions.Count)
        {
            throw new AdminFormValidationException("選項的英文顯示文字筆數必須跟選項本身的筆數一致。");
        }

        var trimmed = optionLabelsEn.Select(o => o.Trim()).ToList();
        if (trimmed.Any(string.IsNullOrEmpty))
        {
            throw new AdminFormValidationException("選項的英文顯示文字不能是空字串。");
        }

        return JsonSerializer.Serialize(trimmed);
    }

    private static bool TryMatch(Regex pattern, string input)
    {
        try
        {
            return pattern.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static void UpsertI18n(Form form, string locale, string? autoReplyBody)
    {
        var existing = form.FormsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            form.FormsI18ns.Add(new FormsI18n { FormId = form.Id, Locale = locale, AutoReplyBody = autoReplyBody });
        }
        else
        {
            existing.AutoReplyBody = autoReplyBody;
        }
    }

    /// <summary>題目文字／選項英文顯示文字的 upsert——同一套「有列就更新、沒有就新增」精神，跟
    /// <see cref="UpsertI18n(Form,string,string?)"/>（表單層級的自動回覆信）是同一個模式，差別是
    /// <c>form_fields_i18n</c> 走「en 列可以整列不存在」（<see cref="Label"/> 必填、不是可為
    /// <c>null</c> 的欄位），呼叫端（<see cref="UpdateFieldAsync"/>）在沒有英文內容時改呼叫
    /// <see cref="Microsoft.EntityFrameworkCore.DbContext.Remove"/> 整列移除，不是傳 <c>null</c>
    /// 進來。</summary>
    private static void UpsertFieldI18n(FormField field, string locale, string label, string? optionsJson)
    {
        var existing = field.FormFieldsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (existing is null)
        {
            field.FormFieldsI18ns.Add(new FormFieldsI18n { FormFieldId = field.Id, Locale = locale, Label = label, OptionsJson = optionsJson });
        }
        else
        {
            existing.Label = label;
            existing.OptionsJson = optionsJson;
        }
    }

    private static AdminFormDetailDto ToDetailDto(Form form) => new()
    {
        Id = form.Id,
        FormCode = form.FormCode,
        NotifyEmails = form.NotifyEmails,
        CaptchaEnabled = form.CaptchaEnabled,
        RedirectPath = form.RedirectPath,
        AutoReplyBodyZh = form.FormsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.AutoReplyBody,
        AutoReplyBodyEn = form.FormsI18ns.FirstOrDefault(i => i.Locale == "en")?.AutoReplyBody,
        Fields = form.FormFields.OrderBy(f => f.SortOrder).Select(ToFieldDto).ToList(),
        UpdatedAt = form.UpdatedAt,
    };

    private static AdminFormFieldDto ToFieldDto(FormField field)
    {
        var labelZh = field.FormFieldsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Label ?? string.Empty;
        var enRow = field.FormFieldsI18ns.FirstOrDefault(i => i.Locale == "en");

        return new AdminFormFieldDto
        {
            Id = field.Id,
            FieldKey = field.FieldKey,
            FieldType = field.FieldType,
            LabelZh = labelZh,
            LabelEn = enRow?.Label,
            IsRequired = field.IsRequired,
            ValidationRule = field.ValidationRule,
            Options = field.OptionsJson is null ? null : JsonSerializer.Deserialize<List<string>>(field.OptionsJson),
            OptionLabelsEn = enRow?.OptionsJson is null ? null : JsonSerializer.Deserialize<List<string>>(enRow.OptionsJson),
            IsSummary = field.IsSummary,
            SortOrder = field.SortOrder,
        };
    }

    /// <summary>同一張表單最多一個「內容摘要」欄位（DB 層還有
    /// <c>UQ_form_fields_one_summary_per_form</c> 過濾唯一索引當第二道防線）——標記新的一個時，
    /// **自動取代**舊的那個（不是回錯誤要求先手動取消），對後台操作者比較直覺：切換摘要來源就是
    /// 單選行為，不是先關後開兩個步驟。</summary>
    private static void UnmarkOtherSummaryFields(Form form, Guid? exceptFieldId)
    {
        foreach (var other in form.FormFields.Where(f => f.IsSummary && f.Id != exceptFieldId))
        {
            other.IsSummary = false;
        }
    }
}
