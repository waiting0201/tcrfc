using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Mail;
using Tcrfc.Api.CharityPlatform.Public;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.CharityPlatform.Admin;

// N7 站台設定（規劃書 §6.7）：站台文案（zh／en 雙欄位）、系統信樣板（四封）、全站金額上下限預設值、徵信名單是否開放、
// 金流與發票加值中心的環境切換（金鑰不明碼顯示）。

/// <summary>站台設定。文案欄位 <c>null</c> 代表不變、空字串代表清空（英文可空但欄位必須存在，缺漏時前台回退繁中）。</summary>
public sealed record AdminSiteSettingsDto
{
    public required string? HomeIntroZh { get; init; }
    public required string? HomeIntroEn { get; init; }
    public required string? ThankYouTemplateZh { get; init; }
    public required string? ThankYouTemplateEn { get; init; }
    public required string? NoticeZh { get; init; }
    public required string? NoticeEn { get; init; }
    public required string? PrivacyPolicyZh { get; init; }
    public required string? PrivacyPolicyEn { get; init; }

    /// <summary>俱樂部官網的網址（成果回顧頁導回主站用）。未設定為 <c>null</c>。</summary>
    public required string? ClubSiteUrl { get; init; }

    public required int DefaultMinAmount { get; init; }
    public required int DefaultMaxAmount { get; init; }

    /// <summary>徵信名單是否開放（關閉時公開頁不顯示任何名單）。</summary>
    public required bool CreditListEnabled { get; init; }
}

public sealed record UpdateSiteSettingsRequest
{
    public string? HomeIntroZh { get; init; }
    public string? HomeIntroEn { get; init; }
    public string? ThankYouTemplateZh { get; init; }
    public string? ThankYouTemplateEn { get; init; }
    public string? NoticeZh { get; init; }
    public string? NoticeEn { get; init; }
    public string? PrivacyPolicyZh { get; init; }
    public string? PrivacyPolicyEn { get; init; }
    public string? ClubSiteUrl { get; init; }
    public int? DefaultMinAmount { get; init; }
    public int? DefaultMaxAmount { get; init; }
    public bool? CreditListEnabled { get; init; }
}

public sealed record AdminEmailTemplateContentDto(string Subject, string Body);

public sealed record AdminEmailTemplateDto
{
    /// <summary><c>donation_thanks</c>／<c>invoice_issued</c>／<c>invoice_failed</c>／<c>refund_notice</c>（程式識別用；畫面請顯示 <c>label</c>）。</summary>
    public required string Code { get; init; }

    public required string Label { get; init; }
    public required bool IsActive { get; init; }

    /// <summary>本樣板可用的代換標記（寫在主旨或本文的 <c>{標記}</c>），以及各自的說明。</summary>
    public required IReadOnlyList<AdminEmailTokenDto> Tokens { get; init; }

    public required AdminEmailTemplateContentDto? Zh { get; init; }
    public required AdminEmailTemplateContentDto? En { get; init; }
}

public sealed record AdminEmailTokenDto(string Token, string Description);

public sealed record UpdateEmailTemplateRequest
{
    public bool? IsActive { get; init; }
    public string? SubjectZh { get; init; }
    public string? BodyZh { get; init; }

    /// <summary>英文主旨與本文要<b>同時</b>填或同時留空；同時留空代表沒有英文版。目前系統信一律寄繁中（捐款單沒有記錄語系），英文版先保存供日後使用。</summary>
    public string? SubjectEn { get; init; }

    public string? BodyEn { get; init; }
}

public sealed record AdminPaymentEnvironmentDto(string Environment, string Label, bool HasCredential, string? InvoicePrefix, DateTime? RotatedAt);

public sealed record AdminPaymentChannelDto
{
    /// <summary><c>line_pay</c>／<c>einvoice</c>。</summary>
    public required string ChannelType { get; init; }

    public required string Label { get; init; }

    /// <summary>目前作用中的環境（<c>sandbox</c> 測試／<c>production</c> 正式）。</summary>
    public required string ActiveEnvironment { get; init; }

    public required IReadOnlyList<AdminPaymentEnvironmentDto> Environments { get; init; }
}

public sealed record SetPaymentCredentialRequest
{
    public string? Environment { get; init; }

    /// <summary>金流商店憑證或加值中心金鑰（寫入後<b>不再回傳</b>，畫面只顯示「已設定」）。</summary>
    public string? Credential { get; init; }

    /// <summary>發票字軌（僅電子發票管道；協會自己的字軌，不得與俱樂部共用）。</summary>
    public string? InvoicePrefix { get; init; }
}

public sealed record SwitchPaymentEnvironmentRequest
{
    public string? Environment { get; init; }

    /// <summary>二次確認。切換環境會讓之後的收款與開票改用另一組憑證，必須為 <c>true</c>。</summary>
    public bool Confirm { get; init; }
}

/// <summary>N7 站台設定。🔴 金流與發票憑證只寫不讀（密文儲存，回應只有「是否已設定」），切換環境與寫入憑證都是 <c>sysadmin_only</c> 的受限操作並寫稽核。</summary>
public sealed class CharitySettingsAdminService(
    CharityDbContext db, CharityPublicCatalog catalog, CharityAuditLogger audit, CharityDataProtector protector, IHostEnvironment environment)
{
    private const int MaxIntroChars = 2_000;
    private const int MaxThankYouChars = 1_000;
    private const int MaxNoticeChars = 10_000;
    private const int MaxPrivacyChars = 100_000;
    private const int MaxAmountCeiling = 10_000_000;

    private const string ClubSiteUrlKey = "donation.club_site_url";

    // 文案類 key → (中文標籤, 長度上限)
    private static readonly (string Key, string Label, int Max)[] CopyKeys =
    [
        ("donation.home_intro", "首頁說明", MaxIntroChars),
        ("donation.thank_you_message_template", "感謝語樣板", MaxThankYouChars),
        ("donation.notice", "捐款須知", MaxNoticeChars),
        ("donation.privacy_policy", "隱私權政策", MaxPrivacyChars),
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // 站台文案與規則
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<AdminSiteSettingsDto> GetAsync(CharityAdminScope scope, CancellationToken cancellationToken)
    {
        var settings = await db.Settings.AsNoTracking().Include(s => s.SettingsI18ns).ToListAsync(cancellationToken);
        string? Copy(string key, string locale) => settings.FirstOrDefault(s => s.SettingKey == key)?.SettingsI18ns.FirstOrDefault(i => i.Locale == locale)?.Value;
        string? Plain(string key) => settings.FirstOrDefault(s => s.SettingKey == key)?.Value;

        var (min, max) = await catalog.GetDefaultAmountRangeAsync(cancellationToken);
        return new AdminSiteSettingsDto
        {
            HomeIntroZh = Copy(CopyKeys[0].Key, RequestLocale.DefaultDbLocale), HomeIntroEn = Copy(CopyKeys[0].Key, "en"),
            ThankYouTemplateZh = Copy(CopyKeys[1].Key, RequestLocale.DefaultDbLocale), ThankYouTemplateEn = Copy(CopyKeys[1].Key, "en"),
            NoticeZh = Copy(CopyKeys[2].Key, RequestLocale.DefaultDbLocale), NoticeEn = Copy(CopyKeys[2].Key, "en"),
            PrivacyPolicyZh = Copy(CopyKeys[3].Key, RequestLocale.DefaultDbLocale), PrivacyPolicyEn = Copy(CopyKeys[3].Key, "en"),
            ClubSiteUrl = Plain(ClubSiteUrlKey),
            DefaultMinAmount = min,
            DefaultMaxAmount = max,
            CreditListEnabled = !string.Equals(Plain(CharityPublicCatalog.CreditListRuleKey), "off", StringComparison.Ordinal),
        };
    }

    public async Task<AdminSiteSettingsDto> UpdateAsync(
        CharityAdminScope scope, UpdateSiteSettingsRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        var changed = new List<string>();
        var copyInputs = new (string Key, string Locale, string? Value, string Label, int Max)[]
        {
            (CopyKeys[0].Key, RequestLocale.DefaultDbLocale, request.HomeIntroZh, CopyKeys[0].Label + "（繁中）", CopyKeys[0].Max),
            (CopyKeys[0].Key, "en", request.HomeIntroEn, CopyKeys[0].Label + "（英文）", CopyKeys[0].Max),
            (CopyKeys[1].Key, RequestLocale.DefaultDbLocale, request.ThankYouTemplateZh, CopyKeys[1].Label + "（繁中）", CopyKeys[1].Max),
            (CopyKeys[1].Key, "en", request.ThankYouTemplateEn, CopyKeys[1].Label + "（英文）", CopyKeys[1].Max),
            (CopyKeys[2].Key, RequestLocale.DefaultDbLocale, request.NoticeZh, CopyKeys[2].Label + "（繁中）", CopyKeys[2].Max),
            (CopyKeys[2].Key, "en", request.NoticeEn, CopyKeys[2].Label + "（英文）", CopyKeys[2].Max),
            (CopyKeys[3].Key, RequestLocale.DefaultDbLocale, request.PrivacyPolicyZh, CopyKeys[3].Label + "（繁中）", CopyKeys[3].Max),
            (CopyKeys[3].Key, "en", request.PrivacyPolicyEn, CopyKeys[3].Label + "（英文）", CopyKeys[3].Max),
        };

        // 先全部驗證完再動資料庫。
        foreach (var input in copyInputs.Where(i => i.Value is not null))
        {
            if (input.Value!.Length > input.Max)
            {
                throw new AdminValidationException($"{input.Label}不可超過 {input.Max:N0} 個字。");
            }
        }

        string? clubUrl = null;
        var clubUrlProvided = request.ClubSiteUrl is not null;
        if (clubUrlProvided)
        {
            clubUrl = AdminInput.OptionalHttpUrl(request.ClubSiteUrl, "俱樂部官網網址");
        }

        var (currentMin, currentMax) = await catalog.GetDefaultAmountRangeAsync(cancellationToken);
        var newMin = request.DefaultMinAmount ?? currentMin;
        var newMax = request.DefaultMaxAmount ?? currentMax;
        if (request.DefaultMinAmount is not null || request.DefaultMaxAmount is not null)
        {
            if (newMin < 1 || newMax > MaxAmountCeiling)
            {
                throw new AdminValidationException($"單筆金額的預設範圍要介於 1 到 {MaxAmountCeiling:N0} 元之間。");
            }

            if (newMax < newMin)
            {
                throw new AdminValidationException("單筆金額的預設上限不可小於下限。");
            }
        }

        foreach (var input in copyInputs.Where(i => i.Value is not null))
        {
            var setting = await GetOrCreateSettingAsync(input.Key, scope, cancellationToken);
            var row = setting.SettingsI18ns.FirstOrDefault(i => i.Locale == input.Locale);
            var value = input.Value!.Length == 0 ? null : input.Value;
            if (row is null)
            {
                // 🔴 明確 Add（不只加進導覽集合），避免子列被當成 Modified（E-85）。
                db.SettingsI18ns.Add(new SettingsI18n { SettingId = setting.Id, Locale = input.Locale, Value = value });
            }
            else
            {
                row.Value = value;
            }

            changed.Add(input.Label);
        }

        if (clubUrlProvided)
        {
            (await GetOrCreateSettingAsync(ClubSiteUrlKey, scope, cancellationToken)).Value = clubUrl;
            changed.Add("俱樂部官網網址");
        }

        if (request.DefaultMinAmount is not null)
        {
            (await GetOrCreateSettingAsync(CharityPublicCatalog.DefaultMinAmountKey, scope, cancellationToken)).Value = newMin.ToString(System.Globalization.CultureInfo.InvariantCulture);
            changed.Add($"單筆金額預設下限 {newMin}");
        }

        if (request.DefaultMaxAmount is not null)
        {
            (await GetOrCreateSettingAsync(CharityPublicCatalog.DefaultMaxAmountKey, scope, cancellationToken)).Value = newMax.ToString(System.Globalization.CultureInfo.InvariantCulture);
            changed.Add($"單筆金額預設上限 {newMax}");
        }

        if (request.CreditListEnabled is { } enabled)
        {
            (await GetOrCreateSettingAsync(CharityPublicCatalog.CreditListRuleKey, scope, cancellationToken)).Value = enabled ? "named_unless_anonymous" : "off";
            changed.Add(enabled ? "開放徵信名單" : "關閉徵信名單");
        }

        if (changed.Count > 0)
        {
            audit.Stage(scope, CharityAuditActions.SettingsUpdate, CharityAuditTargets.Setting, null, $"更新站台設定：{string.Join("、", changed)}", null, sourceIp);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(scope, cancellationToken);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 系統信樣板
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly IReadOnlyDictionary<string, (string Label, (string Token, string Description)[] Tokens)> TemplateMeta =
        new Dictionary<string, (string, (string, string)[])>(StringComparer.Ordinal)
        {
            [EmailTemplateCodes.DonationThanks] = ("捐款感謝信", [
                ("{donor_name}", "捐款人姓名"), ("{order_no}", "捐款單號"), ("{amount}", "捐款金額"), ("{project_name}", "捐款項目"),
                ("{fund_usage}", "款項用途"), ("{paid_at}", "付款時間"), ("{order_details}", "單號、金額、項目與款項用途的整塊摘要")]),
            [EmailTemplateCodes.InvoiceIssued] = ("發票／收據通知", [
                ("{donor_name}", "捐款人姓名"), ("{order_no}", "捐款單號"), ("{amount}", "捐款金額"), ("{project_name}", "捐款項目"),
                ("{invoice_no}", "憑證號碼"), ("{order_details}", "單號、金額、項目與款項用途的整塊摘要")]),
            [EmailTemplateCodes.InvoiceFailed] = ("開立失敗通知（寄給協會）", [("{order_no}", "捐款單號"), ("{amount}", "捐款金額")]),
            [EmailTemplateCodes.RefundNotice] = ("退款通知", [
                ("{donor_name}", "捐款人姓名"), ("{order_no}", "捐款單號"), ("{amount}", "捐款金額"), ("{project_name}", "捐款項目"), ("{refund_reason}", "退款原因")]),
        };

    public async Task<IReadOnlyList<AdminEmailTemplateDto>> ListEmailTemplatesAsync(CharityAdminScope scope, CancellationToken cancellationToken)
    {
        var templates = await db.EmailTemplates.AsNoTracking().Include(t => t.EmailTemplatesI18ns).ToListAsync(cancellationToken);
        return TemplateMeta.Keys
            .Select(code => templates.FirstOrDefault(t => t.Code == code) is { } t ? ToDto(t) : null)
            .Where(t => t is not null).Select(t => t!).ToList();
    }

    public async Task<AdminEmailTemplateDto> UpdateEmailTemplateAsync(
        CharityAdminScope scope, string code, UpdateEmailTemplateRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        if (!TemplateMeta.ContainsKey(code))
        {
            throw new CharityNotFoundException("找不到這封系統信。");
        }

        var template = await db.EmailTemplates.Include(t => t.EmailTemplatesI18ns).SingleOrDefaultAsync(t => t.Code == code, cancellationToken)
            ?? throw new CharityNotFoundException("找不到這封系統信。");

        var subjectZh = request.SubjectZh is null ? null : AdminInput.RequireText(request.SubjectZh, "主旨（繁中）", 255);
        var bodyZh = request.BodyZh is null ? null : RequireBody(request.BodyZh, "本文（繁中）");
        var enProvided = request.SubjectEn is not null || request.BodyEn is not null;
        string? subjectEn = null, bodyEn = null;
        var clearEn = false;
        if (enProvided)
        {
            var s = request.SubjectEn?.Trim() ?? string.Empty;
            var b = request.BodyEn?.Trim() ?? string.Empty;
            if (s.Length == 0 && b.Length == 0)
            {
                clearEn = true;
            }
            else if (s.Length == 0 || b.Length == 0)
            {
                throw new AdminValidationException("英文版的主旨與本文要同時填寫，或同時留空。");
            }
            else
            {
                subjectEn = AdminInput.RequireText(s, "主旨（英文）", 255);
                bodyEn = RequireBody(b, "本文（英文）");
            }
        }

        var zh = template.EmailTemplatesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        if (subjectZh is not null || bodyZh is not null)
        {
            if (zh is null)
            {
                if (subjectZh is null || bodyZh is null)
                {
                    throw new AdminValidationException("這封信還沒有繁中內容，主旨與本文都要填寫。");
                }

                db.EmailTemplatesI18ns.Add(new EmailTemplatesI18n { EmailTemplateId = template.Id, Locale = RequestLocale.DefaultDbLocale, Subject = subjectZh, Body = bodyZh });
            }
            else
            {
                zh.Subject = subjectZh ?? zh.Subject;
                zh.Body = bodyZh ?? zh.Body;
            }
        }

        if (request.IsActive is { } active)
        {
            if (active && zh is null && (subjectZh is null || bodyZh is null))
            {
                throw new AdminValidationException("啟用這封信之前，請先填好繁中的主旨與本文。");
            }

            template.IsActive = active;
        }

        var en = template.EmailTemplatesI18ns.FirstOrDefault(i => i.Locale == "en");
        if (clearEn && en is not null)
        {
            db.EmailTemplatesI18ns.Remove(en);
        }
        else if (subjectEn is not null && bodyEn is not null)
        {
            if (en is null)
            {
                db.EmailTemplatesI18ns.Add(new EmailTemplatesI18n { EmailTemplateId = template.Id, Locale = "en", Subject = subjectEn, Body = bodyEn });
            }
            else
            {
                en.Subject = subjectEn;
                en.Body = bodyEn;
            }
        }

        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = scope.Identity.AdminUserId;
        audit.Stage(scope, CharityAuditActions.EmailTemplateUpdate, CharityAuditTargets.EmailTemplate, template.Id,
            $"更新系統信樣板「{TemplateMeta[code].Label}」", null, sourceIp);
        await db.SaveChangesAsync(cancellationToken);

        var fresh = await db.EmailTemplates.AsNoTracking().Include(t => t.EmailTemplatesI18ns).SingleAsync(t => t.Code == code, cancellationToken);
        return ToDto(fresh);
    }

    private static string RequireBody(string value, string label)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new AdminValidationException($"{label}為必填欄位。");
        }

        if (trimmed.Length > 20_000)
        {
            throw new AdminValidationException($"{label}不可超過 20,000 個字。");
        }

        return trimmed;
    }

    private static AdminEmailTemplateDto ToDto(EmailTemplate t)
    {
        var zh = t.EmailTemplatesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = t.EmailTemplatesI18ns.FirstOrDefault(i => i.Locale == "en");
        var meta = TemplateMeta[t.Code];
        return new AdminEmailTemplateDto
        {
            Code = t.Code,
            Label = meta.Label,
            IsActive = t.IsActive,
            Tokens = meta.Tokens.Select(x => new AdminEmailTokenDto(x.Token, x.Description)).ToList(),
            Zh = zh is null ? null : new AdminEmailTemplateContentDto(zh.Subject, zh.Body),
            En = en is null ? null : new AdminEmailTemplateContentDto(en.Subject, en.Body),
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 金流與發票加值中心：環境與憑證
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<AdminPaymentChannelDto>> ListPaymentChannelsAsync(CharityAdminScope scope, CancellationToken cancellationToken)
    {
        var rows = await db.PaymentChannels.AsNoTracking().ToListAsync(cancellationToken);
        var result = new List<AdminPaymentChannelDto>();
        foreach (var type in new[] { PaymentChannelTypes.LinePay, PaymentChannelTypes.EInvoice })
        {
            var active = await CharityPaymentEnvironment.ResolveAsync(db, environment, type, cancellationToken);
            result.Add(new AdminPaymentChannelDto
            {
                ChannelType = type,
                Label = PaymentChannelTypes.Label(type),
                ActiveEnvironment = active,
                Environments = new[] { PaymentEnvironments.Sandbox, PaymentEnvironments.Production }
                    .Select(env =>
                    {
                        var row = rows.FirstOrDefault(r => r.ChannelType == type && r.Environment == env);
                        return new AdminPaymentEnvironmentDto(env, PaymentEnvironments.Label(env), row is not null && !string.IsNullOrWhiteSpace(row.CredentialEncrypted), row?.InvoicePrefix, row?.RotatedAt);
                    }).ToList(),
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<AdminPaymentChannelDto>> SetCredentialAsync(
        CharityAdminScope scope, string channelType, SetPaymentCredentialRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        RequireChannelType(channelType);
        var env = RequireEnvironment(request.Environment);
        var credential = request.Credential?.Trim();
        if (string.IsNullOrEmpty(credential))
        {
            throw new AdminValidationException("請填寫憑證內容。");
        }

        if (credential.Length > 240)
        {
            throw new AdminValidationException("憑證內容不可超過 240 個字。");
        }

        var prefix = channelType == PaymentChannelTypes.EInvoice ? AdminInput.OptionalText(request.InvoicePrefix, "發票字軌", 16) : null;
        var now = DateTime.UtcNow;
        var row = await db.PaymentChannels.SingleOrDefaultAsync(c => c.ChannelType == channelType && c.Environment == env, cancellationToken);
        if (row is null)
        {
            db.PaymentChannels.Add(new PaymentChannel
            {
                Id = Guid.NewGuid(), ChannelType = channelType, Environment = env, CredentialEncrypted = protector.EncryptChannelCredential(credential),
                InvoicePrefix = prefix, RotatedAt = now, CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
            });
        }
        else
        {
            row.CredentialEncrypted = protector.EncryptChannelCredential(credential);
            if (channelType == PaymentChannelTypes.EInvoice && request.InvoicePrefix is not null)
            {
                row.InvoicePrefix = prefix;
            }

            row.RotatedAt = now;
            row.UpdatedAt = now;
            row.UpdatedBy = scope.Identity.AdminUserId;
        }

        // 稽核不記憑證內容，只記「更新了哪一組」。
        audit.Stage(scope, CharityAuditActions.PaymentCredentialSet, CharityAuditTargets.PaymentChannel, null,
            $"更新{PaymentChannelTypes.Label(channelType)}{PaymentEnvironments.Label(env)}環境的憑證", null, sourceIp);
        await db.SaveChangesAsync(cancellationToken);
        return await ListPaymentChannelsAsync(scope, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminPaymentChannelDto>> SwitchEnvironmentAsync(
        CharityAdminScope scope, string channelType, SwitchPaymentEnvironmentRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        RequireChannelType(channelType);
        var env = RequireEnvironment(request.Environment);
        if (!request.Confirm)
        {
            throw new AdminValidationException("切換環境後，之後的收款與開票會改用另一組憑證，請先確認後再送出。");
        }

        if (env == PaymentEnvironments.Production)
        {
            var row = await db.PaymentChannels.AsNoTracking().SingleOrDefaultAsync(c => c.ChannelType == channelType && c.Environment == env, cancellationToken);
            if (row is null || string.IsNullOrWhiteSpace(row.CredentialEncrypted))
            {
                throw new CharityConflictException("尚未設定正式憑證", "正式環境的憑證還沒有設定，不能切換過去。");
            }

            if (channelType == PaymentChannelTypes.EInvoice && string.IsNullOrWhiteSpace(row.InvoicePrefix))
            {
                throw new CharityConflictException("尚未設定發票字軌", "正式環境的發票字軌還沒有設定，不能切換過去。");
            }
        }

        var key = CharityPaymentEnvironment.SettingKey(channelType);
        var previous = await CharityPaymentEnvironment.ResolveAsync(db, environment, channelType, cancellationToken);
        (await GetOrCreateSettingAsync(key, scope, cancellationToken)).Value = env;
        audit.Stage(scope, CharityAuditActions.PaymentEnvironmentSwitch, CharityAuditTargets.PaymentChannel, null,
            $"{PaymentChannelTypes.Label(channelType)}環境：{PaymentEnvironments.Label(previous)} → {PaymentEnvironments.Label(env)}", null, sourceIp);
        await db.SaveChangesAsync(cancellationToken);
        return await ListPaymentChannelsAsync(scope, cancellationToken);
    }

    // ───────────────────────────────────────────────────────────────────────

    private static void RequireChannelType(string channelType)
    {
        if (!PaymentChannelTypes.All.Contains(channelType))
        {
            throw new CharityNotFoundException("找不到這個金流或發票管道。");
        }
    }

    private static string RequireEnvironment(string? env)
    {
        if (env is null || !PaymentEnvironments.All.Contains(env))
        {
            throw new AdminValidationException("環境只能是「測試」或「正式」。");
        }

        return env;
    }

    private async Task<Setting> GetOrCreateSettingAsync(string key, CharityAdminScope scope, CancellationToken cancellationToken)
    {
        var existing = db.Settings.Local.FirstOrDefault(s => s.SettingKey == key)
                       ?? await db.Settings.Include(s => s.SettingsI18ns).SingleOrDefaultAsync(s => s.SettingKey == key, cancellationToken);
        if (existing is not null)
        {
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = scope.Identity.AdminUserId;
            return existing;
        }

        var now = DateTime.UtcNow;
        var created = new Setting { Id = Guid.NewGuid(), SettingKey = key, CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId };
        db.Settings.Add(created);
        return created;
    }
}
