using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Data;

namespace Tcrfc.Api.CharityPlatform.Common;

public static class PaymentChannelTypes
{
    public const string LinePay = "line_pay";
    public const string EInvoice = "einvoice";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { LinePay, EInvoice };

    public static string Label(string channelType) => channelType == LinePay ? "LINE Pay" : "電子發票加值中心";
}

public static class PaymentEnvironments
{
    public const string Sandbox = "sandbox";
    public const string Production = "production";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Sandbox, Production };

    public static string Label(string environment) => environment == Production ? "正式" : "測試";
}

/// <summary>
/// 金流與發票加值中心目前「作用中」的環境（規劃書 §6.7 N7「環境切換（測試／正式）」）。存在 <c>settings</c>
/// （<c>payment.active_environment.{channel_type}</c>）；沒有設定時依執行環境推定（Production 主機 → 正式，其餘 → 測試）。
/// 🔴 這只決定「讀哪一組憑證與字軌」；假實作的環境防線（<see cref="CharityFakeGuard"/>）與它無關、不因切到測試而放寬。
/// </summary>
public static class CharityPaymentEnvironment
{
    public static string SettingKey(string channelType) => $"payment.active_environment.{channelType}";

    public static string DefaultFor(IHostEnvironment host) => host.IsProduction() ? PaymentEnvironments.Production : PaymentEnvironments.Sandbox;

    public static async Task<string> ResolveAsync(CharityDbContext db, IHostEnvironment host, string channelType, CancellationToken cancellationToken)
    {
        var key = SettingKey(channelType);
        var stored = await db.Settings.AsNoTracking().Where(s => s.SettingKey == key).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        return stored is not null && PaymentEnvironments.All.Contains(stored) ? stored : DefaultFor(host);
    }
}
