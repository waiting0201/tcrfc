namespace Tcrfc.Api.Common;

/// <summary>
/// 台灣時間（<c>Asia/Taipei</c>，UTC+8，無夏令時間）與 UTC 的固定換算，供「今天」「到期日」「領獎期限」這類以日期為單位的判斷使用。
/// 資料庫的時間戳一律存 UTC；日期（<c>date</c>）欄位一律是台灣當地日期。不使用 <see cref="TimeZoneInfo"/>，
/// 避免容器缺時區資料庫時行為不同（同 <c>CalendarIcsRepository</c> 的既有作法）。
/// </summary>
public static class TaiwanClock
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(8);

    public static DateOnly Today => ToDate(DateTime.UtcNow);

    /// <summary>UTC 時間戳 → 台灣當地日期。</summary>
    public static DateOnly ToDate(DateTime utc) => DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Offset));

    /// <summary>台灣當地日期的 00:00 → UTC 時間戳。</summary>
    public static DateTime StartOfDayUtc(DateOnly localDate) => DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue) - Offset, DateTimeKind.Utc);

    /// <summary>
    /// 賽事「日期＋開賽時間」（<c>matches.match_on</c>＋<c>kickoff</c>，台北當地牆上時間，docs/12 §12 第 31 點）→ UTC 時刻。
    /// <paramref name="kickoff"/> 為空或不是 <c>H:mm</c>／<c>HH:mm</c> 時回傳 <c>null</c>（沒有開賽時間就算不出時刻，不猜 00:00）。
    /// </summary>
    public static DateTime? KickoffToUtc(DateOnly matchOn, string? kickoff)
    {
        if (string.IsNullOrWhiteSpace(kickoff)
            || !TimeOnly.TryParseExact(kickoff.Trim(), ["H:mm", "HH:mm"], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time))
        {
            return null;
        }

        return DateTime.SpecifyKind(matchOn.ToDateTime(time) - Offset, DateTimeKind.Utc);
    }

    /// <summary>UTC 時間戳 → 台灣當地時間文字 <c>yyyy-MM-dd HH:mm</c>（CSV 匯出用；docs/06 的日期時間格式，且統一用台灣時間，不再輸出無標示的 UTC）。</summary>
    public static string ToText(DateTime utc)
        => DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Offset).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}
