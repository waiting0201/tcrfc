using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>
/// S6 金流與電子發票憑證（規劃書 §4.13 S6）：LINE Pay（Channel ID／Secret）、測試（Sandbox）模式開關、金鑰輪替時間、
/// 電子發票（字軌、服務金鑰）與開立／作廢的重試設定。<b>僅系統管理員</b>（<c>shop.credential.*</c> 為 sysadmin_only＋受限）：憑證外洩等同商店被接管，
/// 填錯商店號則款項進錯法人。
/// 🔴 <b>憑證屬於「收款主體」俱樂部</b>（<c>payment_channels.owner_club_id</c>），不論從哪個站台操作都是同一組；<b>不得填入協會的商店號</b>
/// （回應一律帶 <see cref="ShopLabels.CollectingSubjectNotice"/>）。密鑰以 Data Protection 加密存放，<b>任何回應與日誌都不含密鑰</b>，識別碼只回遮罩值。
/// 🔴 <b>B-10：LINE Pay 商店號與電子發票服務尚未到位，本期不串接</b>——憑證可先存放，但系統目前不會用它連線
/// （<see cref="AdminShopCredentialsDto.IntegrationConnected"/> 恆為 <c>false</c>）。
/// 金流環境開關與發票重試設定存在收款主體俱樂部的 <c>settings</c>。刻意不注入快取服務。
/// </summary>
public sealed partial class AdminShopCredentialsRepository(
    ClubDbContext db, ClubTextSettings texts, IDataProtectionProvider protection, AdminShopSettingsRepository subjects, SensitiveActionLogger audit)
{
    private const string Purpose = "Tcrfc.Shop.Credentials.v1";
    private static readonly HashSet<string> Environments = new(["sandbox", "production"], StringComparer.Ordinal);
    private static readonly string[] SettingKeys = [ShopSettingKeys.PaymentEnvironment, ShopSettingKeys.InvoiceRetryMax, ShopSettingKeys.InvoiceRetryIntervalMinutes];

    [GeneratedRegex(@"^[A-Z]{2}$")]
    private static partial Regex PrefixFormat();

    private async Task<Guid> OwnerAsync(CancellationToken cancellationToken)
    {
        var subject = await subjects.CollectingSubjectAsync(cancellationToken);
        return subject.ClubId != Guid.Empty ? subject.ClubId : throw new AdminConflictException("尚未設定收款主體", "系統裡沒有設定收款主體的俱樂部，請先到俱樂部設定指定收款主體。");
    }

    public async Task<AdminShopCredentialsDto> GetAsync(CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(cancellationToken);
        var channels = await db.PaymentChannels.AsNoTracking().Where(c => c.OwnerClubId == owner).ToListAsync(cancellationToken);
        var map = await texts.LoadAsync(owner, SettingKeys, cancellationToken);
        AdminCredentialStatusDto Status(string type, string env)
        {
            var row = channels.FirstOrDefault(c => c.ChannelType == type && c.Environment == env);
            return row is null
                ? new AdminCredentialStatusDto { Configured = false }
                : new AdminCredentialStatusDto { Configured = true, IdentifierMasked = MaskIdentifier(Read(row).Identifier), InvoicePrefix = row.InvoicePrefix, RotatedAt = row.RotatedAt };
        }

        return new AdminShopCredentialsDto
        {
            CollectingSubject = await subjects.CollectingSubjectAsync(cancellationToken),
            Environment = ClubTextSettings.GetValue(map, ShopSettingKeys.PaymentEnvironment) is "production" ? "production" : "sandbox",
            LinePay = new AdminCredentialEnvironmentsDto { Sandbox = Status("linepay", "sandbox"), Production = Status("linepay", "production") },
            EInvoice = new AdminCredentialEnvironmentsDto { Sandbox = Status("einvoice", "sandbox"), Production = Status("einvoice", "production") },
            InvoiceRetry = new AdminInvoiceRetryDto
            {
                MaxRetries = ParseInt(map, ShopSettingKeys.InvoiceRetryMax) ?? 3, IntervalMinutes = ParseInt(map, ShopSettingKeys.InvoiceRetryIntervalMinutes) ?? 30,
            },
            IntegrationConnected = false,
        };
    }

    public async Task<AdminShopCredentialsDto> UpdateLinePayAsync(AdminClubScope scope, UpdateAdminLinePayCredentialRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Environment, Environments, "環境", "「測試」或「正式」");
        var channelId = AdminInput.RequireText(request.ChannelId, "Channel ID", 64);
        var secret = AdminInput.OptionalText(request.ChannelSecret, "Channel Secret", 200);
        var owner = await OwnerAsync(cancellationToken);
        var row = await db.PaymentChannels.FirstOrDefaultAsync(c => c.OwnerClubId == owner && c.ChannelType == "linepay" && c.Environment == request.Environment, cancellationToken);
        var existing = row is null ? null : Read(row);
        if (secret is null && (existing is null || string.IsNullOrEmpty(existing.Secret)))
        {
            throw new AdminValidationException("第一次設定必須填寫 Channel Secret。");
        }

        var changedSecret = secret is not null && secret != existing?.Secret;
        Upsert(ref row, owner, "linepay", request.Environment, new Stored(channelId, secret ?? existing!.Secret), null, changedSecret || existing?.Identifier != channelId, scope.Identity.AdminUserId);
        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "更新 LINE Pay 憑證", $"環境 {request.Environment}", 1, changedSecret ? "已更換密鑰" : "密鑰未變");
        return await GetAsync(cancellationToken);
    }

    public async Task<AdminShopCredentialsDto> UpdateEInvoiceAsync(AdminClubScope scope, UpdateAdminEInvoiceCredentialRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Environment, Environments, "環境", "「測試」或「正式」");
        var prefix = AdminInput.RequireText(request.InvoicePrefix, "字軌", 2).ToUpperInvariant();
        if (!PrefixFormat().IsMatch(prefix))
        {
            throw new AdminValidationException("字軌必須是兩位大寫英文字母，例如 AB。");
        }

        var merchant = AdminInput.OptionalText(request.MerchantId, "店家代號", 64);
        var apiKey = AdminInput.OptionalText(request.ApiKey, "服務金鑰", 200);
        var owner = await OwnerAsync(cancellationToken);
        var row = await db.PaymentChannels.FirstOrDefaultAsync(c => c.OwnerClubId == owner && c.ChannelType == "einvoice" && c.Environment == request.Environment, cancellationToken);
        var existing = row is null ? null : Read(row);
        var changedSecret = apiKey is not null && apiKey != existing?.Secret;
        Upsert(ref row, owner, "einvoice", request.Environment, new Stored(merchant ?? existing?.Identifier ?? "", apiKey ?? existing?.Secret ?? ""), prefix,
            changedSecret || (row?.InvoicePrefix is not null && row.InvoicePrefix != prefix), scope.Identity.AdminUserId);
        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "更新電子發票設定", $"環境 {request.Environment}／字軌 {prefix}", 1, changedSecret ? "已更換金鑰" : "金鑰未變");
        return await GetAsync(cancellationToken);
    }

    public async Task<AdminShopCredentialsDto> UpdateModeAsync(AdminClubScope scope, UpdateAdminPaymentModeRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Environment, Environments, "環境", "「測試」或「正式」");
        var owner = await OwnerAsync(cancellationToken);
        var map = await texts.LoadAsync(owner, SettingKeys, cancellationToken);
        texts.SetValue(map, owner, ShopSettingKeys.PaymentEnvironment, ShopSettingKeys.Group, request.Environment, scope.Identity.AdminUserId);
        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "切換金流環境", request.Environment, 1);
        return await GetAsync(cancellationToken);
    }

    public async Task<AdminShopCredentialsDto> UpdateInvoiceRetryAsync(AdminClubScope scope, UpdateAdminInvoiceRetryRequest request, CancellationToken cancellationToken)
    {
        if (request.MaxRetries is < 0 or > 10 || request.IntervalMinutes is < 1 or > 1440)
        {
            throw new AdminValidationException("重試次數必須介於 0 與 10 之間，間隔必須介於 1 與 1440 分鐘之間。");
        }

        var owner = await OwnerAsync(cancellationToken);
        var map = await texts.LoadAsync(owner, SettingKeys, cancellationToken);
        texts.SetValue(map, owner, ShopSettingKeys.InvoiceRetryMax, ShopSettingKeys.Group, request.MaxRetries.ToString(CultureInfo.InvariantCulture), scope.Identity.AdminUserId);
        texts.SetValue(map, owner, ShopSettingKeys.InvoiceRetryIntervalMinutes, ShopSettingKeys.Group, request.IntervalMinutes.ToString(CultureInfo.InvariantCulture), scope.Identity.AdminUserId);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    // ═════════════ 內部 ═════════════

    private sealed record Stored(string Identifier, string Secret);

    private Stored Read(PaymentChannel row)
    {
        try
        {
            var json = protection.CreateProtector(Purpose).Unprotect(row.CredentialEncrypted);
            return JsonSerializer.Deserialize<Stored>(json) ?? new Stored("", "");
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException)
        {
            // 金鑰環換過或內容損毀：視為未設定識別碼，要求重新輸入，不把例外細節帶出去。
            return new Stored("", "");
        }
    }

    private void Upsert(ref PaymentChannel? row, Guid owner, string type, string environment, Stored stored, string? prefix, bool rotated, Guid? operatorId)
    {
        var now = DateTime.UtcNow;
        var encrypted = protection.CreateProtector(Purpose).Protect(JsonSerializer.Serialize(stored));
        if (row is null)
        {
            row = new PaymentChannel
            {
                Id = Guid.NewGuid(), OwnerClubId = owner, ChannelType = type, Environment = environment, CredentialEncrypted = encrypted, InvoicePrefix = prefix,
                RotatedAt = now, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
            };
            db.PaymentChannels.Add(row);
            return;
        }

        row.CredentialEncrypted = encrypted;
        row.InvoicePrefix = prefix ?? row.InvoicePrefix;
        if (rotated)
        {
            row.RotatedAt = now;
        }

        row.UpdatedAt = now;
        row.UpdatedBy = operatorId;
    }

    private static string? MaskIdentifier(string value)
        => string.IsNullOrEmpty(value) ? null : value.Length <= 4 ? new string('*', value.Length) : new string('*', value.Length - 4) + value[^4..];

    private static int? ParseInt(IReadOnlyDictionary<string, Setting> map, string key)
        => int.TryParse(ClubTextSettings.GetValue(map, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
}
