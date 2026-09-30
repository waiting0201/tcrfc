using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Features.AdminMembers;

/// <summary>
/// 會員編號產生（K2「會員編號產生規則設定」）。規則存在該俱樂部的設定（<c>member.no_prefix</c>／<c>member.no_digits</c>，
/// 預設 <c>M</c>＋6 位數字）：前綴＋流水號，流水號為「同前綴目前最大號＋1」。會員帳號不分俱樂部，編號全站唯一
/// （<c>UQ_members_member_no</c>）；規則只影響<b>後台建立</b>的新會員（現場入會），已存在的編號不會變。
/// 真正的唯一性由資料庫唯一鍵保證，並發撞號時由呼叫端重試。
/// </summary>
public sealed class MemberNumberGenerator(ClubDbContext db, ClubSettingsStore settings)
{
    public const string PrefixKey = "member.no_prefix";
    public const string DigitsKey = "member.no_digits";
    public const string DefaultPrefix = "M";
    public const int DefaultDigits = 6;

    public async Task<(string Prefix, int Digits)> GetRuleAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var values = await settings.GetManyAsync(clubId, [PrefixKey, DigitsKey], cancellationToken);
        var prefix = values.GetValueOrDefault(PrefixKey) ?? DefaultPrefix;
        var digits = int.TryParse(values.GetValueOrDefault(DigitsKey), NumberStyles.None, CultureInfo.InvariantCulture, out var d)
            ? d : DefaultDigits;
        return (prefix, digits is >= 4 and <= 10 ? digits : DefaultDigits);
    }

    public async Task<string> NextAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var (prefix, digits) = await GetRuleAsync(clubId, cancellationToken);
        var existing = await db.Members.AsNoTracking()
            .Where(m => m.MemberNo.StartsWith(prefix))
            .Select(m => m.MemberNo)
            .ToListAsync(cancellationToken);

        long max = 0;
        foreach (var no in existing)
        {
            var tail = no[prefix.Length..];
            if (tail.Length > 0 && tail.All(char.IsAsciiDigit) && long.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > max)
            {
                max = n;
            }
        }

        return prefix + (max + 1).ToString("D" + digits, CultureInfo.InvariantCulture);
    }
}
