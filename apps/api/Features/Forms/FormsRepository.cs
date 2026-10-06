using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.Email;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Forms;

/// <summary>
/// 7 類表單 ＋ 提案下載 ＋ 捐助洽詢的公開讀取與送出端點（主站規劃書 §3.10）。
///
/// ### 濫用防護（規劃書「防機器人（reCAPTCHA / Turnstile）」）
/// 三層：① <c>Program.cs</c> 對 <c>POST .../submissions</c> 掛 Rate Limiting（依訪客真實 IP 分區）；
/// ② <see cref="SubmitFormRequest.Website"/> 誘捕欄位（命中安靜回成功，且排在人機驗證之前）；
/// ③ Cloudflare Turnstile（<see cref="IClubTurnstileVerifier"/>）：該表單 <c>captcha_enabled = true</c> 且部署端設定了
/// <c>TURNSTILE_SECRET_KEY</c> 才驗證，缺 token／驗證失敗拋 <see cref="CaptchaFailedException"/>（422 <c>captcha_failed</c>）。
/// 只有公開表單端點傳 <c>captchaRemoteIp</c> 才會驗證；提案下載等內部重用 <c>SubmitAsync</c> 的呼叫不帶，不受影響。
///
/// ### 動態欄位驗證
/// <c>form_fields</c> 是 G1 表單設計器的動態欄位（10.1–10.7 與提案下載的欄位已鎖定、後台不得增刪改，見 <c>FormCatalog.FieldLockedCodes</c>），本檔依 <see cref="FormFieldTypes"/>
/// 逐型別驗證：<c>consent</c> 必填時必須是真值；<c>date</c> 必須是合法日期；<c>select</c>／
/// <c>multiselect</c> 的值必須落在 <c>options_json</c> 內；其餘型別若設定 <c>validation_rule</c>
/// 則視為正規表示式（帶逾時保護，避免惡意樣式拖垮伺服器——雖然 <c>validation_rule</c> 只有後台
/// 管理者能設定，非公開輸入，仍比照縱深防禦原則加上逾時）。**不接受未知的 <c>field_key</c>**：
/// 送出內容包含任何不屬於這張表單的鍵一律整批拒絕（400），避免累積垃圾資料。
/// </summary>
public sealed class FormsRepository(
    IClubSqlConnectionFactory connectionFactory, IQueryCache cache, IClubTurnstileVerifier turnstile,
    IEmailSender email, ILogger<FormsRepository> logger)
{
    private const string CacheEntity = "forms";

    private sealed record FormRow(Guid Id, string FormCode, bool CaptchaEnabled, string? RedirectPath, string? NotifyEmails);
    internal sealed record FieldRow(Guid Id, string FieldKey, string FieldType, bool IsRequired, string? ValidationRule, string? OptionsJson, int SortOrder);
    private sealed record FieldI18nRow(Guid FormFieldId, string Locale, string Label, string? OptionsJson);

    /// <summary><paramref name="dbLocale"/>——S1-10 修正（2026-09-25）新增：題目文字與選項顯示文字
    /// 依語系而異，快取維度必須跟著切分（原本用 <see cref="CacheDimensions.AnyLocale"/> 是修正前
    /// 「表單完全沒有題目文字」時的正確選擇，語系化之後繼續共用同一把快取 key 會讓後填入的語系
    /// 覆蓋另一個語系的結果）。</summary>
    public async Task<PublicFormDto?> GetFormDefinitionAsync(ClubScope scope, string formCode, string dbLocale, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            CacheEntity, scope.ClubCode, dbLocale, formCode,
            async ct =>
            {
                using var connection = connectionFactory.CreateConnection();

                var form = await LoadFormAsync(connection, scope.ClubId, formCode, ct);
                if (form is null)
                {
                    return null;
                }

                var fields = await LoadFieldsAsync(connection, form.Id, ct);
                var i18nByFieldId = await LoadFieldI18nAsync(connection, fields.Select(f => f.Id).ToList(), dbLocale, ct);

                return new PublicFormDto
                {
                    FormCode = form.FormCode,
                    FormNameZh = FormCatalog.DisplayNameZh(form.FormCode),
                    FormNameEn = FormCatalog.DisplayNameEn(form.FormCode),
                    CaptchaEnabled = form.CaptchaEnabled,
                    RedirectPath = SafeRedirectPath(form.RedirectPath),
                    Fields = fields.Select(f => ToPublicFieldDto(f, i18nByFieldId.GetValueOrDefault(f.Id), dbLocale)).ToList(),
                };
            },
            cancellationToken);
    }

    /// <param name="proposalId">E3（2026-09-30）：只有提案下載表單（<c>proposal_download</c>）由
    /// <c>Features/Proposals</c> 傳入，寫進 <c>enquiries.proposal_id</c>，讓 Lead 名單知道下載的是哪一份提案（A/B 版本）；
    /// 一般表單送出一律為 <c>null</c>。</param>
    public async Task<SubmitFormResultDto> SubmitAsync(
        ClubScope scope, string formCode, SubmitFormRequest request, CancellationToken cancellationToken, Guid? proposalId = null, string? captchaRemoteIp = null, bool verifyCaptcha = false)
    {
        // 誘捕欄位有值＝機器人：安靜回成功、不寫入任何資料，避免讓機器人知道自己被擋下。
        if (!string.IsNullOrEmpty(request.Website))
        {
            return new SubmitFormResultDto { Success = true };
        }

        using var connection = connectionFactory.CreateConnection();
        connection.Open();

        var form = await LoadFormAsync(connection, scope.ClubId, formCode, null, cancellationToken);
        if (form is null)
        {
            throw new PublicFormNotFoundException("找不到指定的表單。");
        }

        // 人機驗證排在 honeypot 之後、欄位驗證之前：便宜的擋法先做，且不讓機器人靠 400 訊息探測欄位。
        if (verifyCaptcha && form.CaptchaEnabled && turnstile.IsEnabled
            && !await turnstile.VerifyAsync(request.TurnstileToken, captchaRemoteIp, cancellationToken))
        {
            throw new CaptchaFailedException();
        }

        var fields = await LoadFieldsAsync(connection, form.Id, null, cancellationToken);
        var fieldsByKey = fields.ToDictionary(f => f.FieldKey, StringComparer.Ordinal);

        foreach (var key in request.Answers.Keys)
        {
            if (!fieldsByKey.ContainsKey(key))
            {
                throw new PublicFormSubmissionValidationException($"欄位「{key}」不屬於這張表單。");
            }
        }

        var validatedAnswers = new Dictionary<Guid, string>();
        foreach (var field in fields)
        {
            request.Answers.TryGetValue(field.FieldKey, out var rawValue);
            var hasValue = !string.IsNullOrWhiteSpace(rawValue);

            if (field.IsRequired && !hasValue)
            {
                throw new PublicFormSubmissionValidationException($"缺少必填欄位：{field.FieldKey}");
            }
            if (!hasValue)
            {
                continue; // 選填且未填：不寫入任何一列 EnquiryAnswer。
            }

            ValidateFieldValue(field, rawValue!);
            validatedAnswers[field.Id] = rawValue!;
        }

        using var transaction = connection.BeginTransaction();

        var enquiryId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        const string insertEnquirySql = """
            INSERT INTO enquiries (id, club_id, form_id, proposal_id, source_path, utm_source, utm_campaign, status, created_at, updated_at)
            VALUES (@Id, @ClubId, @FormId, @ProposalId, @SourcePath, @UtmSource, @UtmCampaign, N'新進', @Now, @Now)
            """;
        await connection.ExecuteAsync(new CommandDefinition(insertEnquirySql, new
        {
            Id = enquiryId,
            scope.ClubId,
            FormId = form.Id,
            ProposalId = proposalId,
            request.SourcePath,
            request.UtmSource,
            request.UtmCampaign,
            Now = now,
        }, transaction, cancellationToken: cancellationToken));

        if (validatedAnswers.Count > 0)
        {
            const string insertAnswerSql = """
                INSERT INTO enquiry_answers (enquiry_id, form_field_id, value)
                VALUES (@EnquiryId, @FormFieldId, @Value)
                """;
            foreach (var (fieldId, value) in validatedAnswers)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    insertAnswerSql, new { EnquiryId = enquiryId, FormFieldId = fieldId, Value = value }, transaction, cancellationToken: cancellationToken));
            }
        }

        transaction.Commit();

        // 寄信在 commit 之後、盡力而為：詢問已經寫入，寄信失敗（供應商掛掉、收件人格式怪）不得讓訪客看到「送出失敗」。
        await SendNotificationsAsync(connection, scope, form, fields, validatedAnswers, enquiryId, request.Lang, cancellationToken);

        return new SubmitFormResultDto { Success = true };
    }

    /// <summary>送出後導向頁只允許站內相對路徑（<c>/</c> 開頭，且不是 <c>//</c> 或 <c>/\</c> 這類會被瀏覽器當成外站的寫法）；
    /// 不符一律視為沒設定。後台寫入時已經用同一條規則擋過，這裡是輸出端的縱深防禦（舊資料可能是完整網址）。</summary>
    public static string? SafeRedirectPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var p = path.Trim();
        if (p.Length > 500 || p[0] != '/' || (p.Length > 1 && (p[1] == '/' || p[1] == '\\')) || p.Any(char.IsControl))
        {
            return null;
        }

        return p;
    }

    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    /// <summary>送件者 Email：表單沒有專屬 Email 型別，慣例欄位鍵是 <c>contact</c>（聯絡方式，可能是電話）或 <c>email</c>；
    /// 只有值真的長得像 Email 才寄，電話之類一律不寄。</summary>
    internal static string? FindSenderEmail(IReadOnlyList<FieldRow> fields, IReadOnlyDictionary<Guid, string> answers)
    {
        foreach (var key in new[] { "email", "contact" })
        {
            var field = fields.FirstOrDefault(f => f.FieldKey == key);
            if (field is not null && answers.TryGetValue(field.Id, out var value))
            {
                var candidate = value.Trim();
                try
                {
                    if (candidate.Length <= 255 && EmailPattern.IsMatch(candidate))
                    {
                        return candidate;
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // 逾時視為不是 Email。
                }
            }
        }

        return null;
    }

    private async Task SendNotificationsAsync(
        System.Data.IDbConnection connection, ClubScope scope, FormRow form, IReadOnlyList<FieldRow> fields,
        IReadOnlyDictionary<Guid, string> answers, Guid enquiryId, string? lang, CancellationToken cancellationToken)
    {
        try
        {
            var notifyTargets = (form.NotifyEmails ?? string.Empty)
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var senderEmail = FindSenderEmail(fields, answers);
            var dbLocale = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : RequestLocale.DefaultDbLocale;

            string? autoReply = null;
            if (senderEmail is not null)
            {
                const string bodySql = """
                    SELECT auto_reply_body FROM forms_i18n WHERE form_id = @FormId AND locale IN (@Locale, @Default)
                    ORDER BY CASE WHEN locale = @Locale THEN 0 ELSE 1 END
                    """;
                autoReply = (await connection.QueryAsync<string?>(new CommandDefinition(
                    bodySql, new { FormId = form.Id, Locale = dbLocale, Default = RequestLocale.DefaultDbLocale }, cancellationToken: cancellationToken)))
                    .FirstOrDefault(b => !string.IsNullOrWhiteSpace(b));
            }

            if (notifyTargets.Count == 0 && autoReply is null)
            {
                return;
            }

            var clubName = await LoadClubNameAsync(connection, scope.ClubId, dbLocale, scope.ClubCode, cancellationToken);

            if (notifyTargets.Count > 0)
            {
                var labels = await LoadFieldI18nAsync(connection, fields.Select(f => f.Id).ToList(), RequestLocale.DefaultDbLocale, cancellationToken);
                var rows = fields.Where(f => f.FieldType != FormFieldTypes.Consent && answers.ContainsKey(f.Id)).OrderBy(f => f.SortOrder)
                    .Select(f => new FormEmailTemplates.Answer(
                        labels.GetValueOrDefault(f.Id)?.GetValueOrDefault(RequestLocale.DefaultDbLocale)?.Label ?? f.FieldKey, answers[f.Id]))
                    .ToList();
                foreach (var target in notifyTargets)
                {
                    await TrySendAsync(FormEmailTemplates.StaffNotification(target, clubName, FormCatalog.DisplayNameZh(form.FormCode), enquiryId, rows), form.FormCode, cancellationToken);
                }
            }

            if (senderEmail is not null && autoReply is not null)
            {
                await TrySendAsync(FormEmailTemplates.AutoReply(senderEmail, clubName, autoReply, dbLocale == "en" ? "en" : "zh"), form.FormCode, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "表單 {FormCode} 送出後的通知信處理失敗（詢問已寫入，不影響送出）。", form.FormCode);
        }
    }

    private async Task TrySendAsync(EmailMessage message, string formCode, CancellationToken cancellationToken)
    {
        try
        {
            if (!await email.SendAsync(message, cancellationToken))
            {
                logger.LogWarning("表單 {FormCode} 的 {Kind} 信件未寄出（寄信供應商尚未串接或拒收）。", formCode, message.Kind);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "表單 {FormCode} 的 {Kind} 信件寄送失敗（不影響送出）。", formCode, message.Kind);
        }
    }

    private static async Task<string> LoadClubNameAsync(
        System.Data.IDbConnection connection, Guid clubId, string dbLocale, string fallback, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT name FROM clubs_i18n WHERE club_id = @ClubId AND locale IN (@Locale, @Default)
            ORDER BY CASE WHEN locale = @Locale THEN 0 ELSE 1 END
            """;
        var names = await connection.QueryAsync<string?>(new CommandDefinition(
            sql, new { ClubId = clubId, Locale = dbLocale, Default = RequestLocale.DefaultDbLocale }, cancellationToken: cancellationToken));
        return names.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? fallback;
    }

    private static void ValidateFieldValue(FieldRow field, string value)
    {
        switch (field.FieldType)
        {
            case FormFieldTypes.Consent:
                if (field.IsRequired && !FormFieldTypes.IsTruthy(value))
                {
                    throw new PublicFormSubmissionValidationException($"「{field.FieldKey}」需要勾選同意才能送出。");
                }
                break;

            case FormFieldTypes.Date:
                if (!DateOnly.TryParse(value, out _))
                {
                    throw new PublicFormSubmissionValidationException($"「{field.FieldKey}」不是合法的日期格式。");
                }
                break;

            case FormFieldTypes.Select:
            {
                var options = ParseOptions(field.OptionsJson);
                if (options is not null && !options.Contains(value))
                {
                    throw new PublicFormSubmissionValidationException($"「{field.FieldKey}」的值不在允許的選項內。");
                }
                break;
            }

            case FormFieldTypes.Multiselect:
            {
                var options = ParseOptions(field.OptionsJson);
                if (options is not null)
                {
                    var chosen = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (chosen.Any(c => !options.Contains(c)))
                    {
                        throw new PublicFormSubmissionValidationException($"「{field.FieldKey}」的值不在允許的選項內。");
                    }
                }
                break;
            }

            default:
                if (!string.IsNullOrWhiteSpace(field.ValidationRule) && !TryRegexMatch(field.ValidationRule, value))
                {
                    throw new PublicFormSubmissionValidationException($"「{field.FieldKey}」的格式不符合要求。");
                }
                break;
        }
    }

    private static bool TryRegexMatch(string pattern, string value)
    {
        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200));
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            // 樣式本身不合法或比對逾時：視為不符合，fail-closed（管理者應修正 validation_rule）。
            return false;
        }
    }

    private static HashSet<string>? ParseOptions(string? optionsJson)
        => optionsJson is null ? null : new HashSet<string>(JsonSerializer.Deserialize<List<string>>(optionsJson)!, StringComparer.Ordinal);

    /// <summary><paramref name="i18nByLocale"/> 是這個欄位 <c>form_fields_i18n</c> 的請求語系與
    /// zh-Hant 兩列（見 <see cref="LoadFieldI18nAsync"/>）——<paramref name="dbLocale"/> 只用來判斷
    /// 「要不要另外找 zh-Hant 回退列」，實際回退運算交給 <see cref="RequestLocale.Pick"/>。</summary>
    private static PublicFormFieldDto ToPublicFieldDto(FieldRow field, IReadOnlyDictionary<string, FieldI18nRow>? i18nByLocale, string dbLocale)
    {
        i18nByLocale ??= new Dictionary<string, FieldI18nRow>();
        i18nByLocale.TryGetValue(dbLocale, out var requested);
        i18nByLocale.TryGetValue(RequestLocale.DefaultDbLocale, out var fallback);

        var canonicalOptions = field.OptionsJson is null ? null : JsonSerializer.Deserialize<List<string>>(field.OptionsJson);
        var requestedOptionLabels = requested?.OptionsJson is null ? null : JsonSerializer.Deserialize<List<string>>(requested.OptionsJson);

        return new PublicFormFieldDto
        {
            FieldKey = field.FieldKey,
            FieldType = field.FieldType,
            // fallback?.Label 理論上一定存在（zh-Hant 列由後台寫入時強制必填），字面預設值只是
            // 型別系統要求的保底，不代表資料真的可能缺這一列。
            Label = RequestLocale.Pick(requested?.Label, fallback?.Label) ?? field.FieldKey,
            IsRequired = field.IsRequired,
            ValidationRule = field.ValidationRule,
            Options = canonicalOptions,
            // 選項顯示文字沒有請求語系翻譯時，回退成跟 Options 一樣的中文字面值（canonical 值本身
            // 就是 zh-Hant 的顯示文字，不需要 form_fields_i18n 另存一份 zh-Hant 選項列）。
            OptionLabels = canonicalOptions is null ? null : (requestedOptionLabels ?? canonicalOptions),
            SortOrder = field.SortOrder,
        };
    }

    private static Task<FormRow?> LoadFormAsync(System.Data.IDbConnection connection, Guid clubId, string formCode, CancellationToken cancellationToken)
        => LoadFormAsync(connection, clubId, formCode, null, cancellationToken);

    private static async Task<FormRow?> LoadFormAsync(
        System.Data.IDbConnection connection, Guid clubId, string formCode, System.Data.IDbTransaction? transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, form_code AS FormCode, captcha_enabled AS CaptchaEnabled, redirect_path AS RedirectPath, notify_emails AS NotifyEmails
            FROM forms
            WHERE club_id = @ClubId AND form_code = @FormCode
            """;
        return await connection.QuerySingleOrDefaultAsync<FormRow>(
            new CommandDefinition(sql, new { ClubId = clubId, FormCode = formCode }, transaction, cancellationToken: cancellationToken));
    }

    private static Task<IReadOnlyList<FieldRow>> LoadFieldsAsync(System.Data.IDbConnection connection, Guid formId, CancellationToken cancellationToken)
        => LoadFieldsAsync(connection, formId, null, cancellationToken);

    private static async Task<IReadOnlyList<FieldRow>> LoadFieldsAsync(
        System.Data.IDbConnection connection, Guid formId, System.Data.IDbTransaction? transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, field_key AS FieldKey, field_type AS FieldType, is_required AS IsRequired,
                   validation_rule AS ValidationRule, options_json AS OptionsJson, sort_order AS SortOrder
            FROM form_fields
            WHERE form_id = @FormId
            ORDER BY sort_order
            """;
        var rows = await connection.QueryAsync<FieldRow>(
            new CommandDefinition(sql, new { FormId = formId }, transaction, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    /// <summary>一次查出這張表單全部欄位的 <c>form_fields_i18n</c>，只抓請求語系與 zh-Hant 兩列
    /// （跟 <c>ArticlesRepository.LoadArticleI18nAsync</c> 同一種「一次查多筆、分組回傳」寫法）。
    /// 回傳形狀是 <c>Dictionary&lt;FormFieldId, Dictionary&lt;Locale, Row&gt;&gt;</c>，呼叫端
    /// （<see cref="ToPublicFieldDto"/>）自己決定回退規則，這裡只負責撈資料。</summary>
    private static async Task<Dictionary<Guid, Dictionary<string, FieldI18nRow>>> LoadFieldI18nAsync(
        System.Data.IDbConnection connection, IReadOnlyList<Guid> fieldIds, string dbLocale, CancellationToken cancellationToken)
    {
        if (fieldIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT form_field_id AS FormFieldId, locale AS Locale, label AS Label, options_json AS OptionsJson
            FROM form_fields_i18n
            WHERE form_field_id IN @FieldIds AND locale IN @Locales
            """;
        var locales = dbLocale == RequestLocale.DefaultDbLocale
            ? new[] { dbLocale }
            : new[] { dbLocale, RequestLocale.DefaultDbLocale };

        var rows = await connection.QueryAsync<FieldI18nRow>(new CommandDefinition(
            sql, new { FieldIds = fieldIds, Locales = locales }, cancellationToken: cancellationToken));

        return rows.GroupBy(r => r.FormFieldId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Locale, r => r));
    }
}
