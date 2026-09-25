using System.Text;

namespace Tcrfc.Api.Common;

/// <summary>
/// 單一事件的 <c>.ics</c>（RFC 5545 <c>VCALENDAR</c>／<c>VEVENT</c>）產生器，供「加入我的行事曆」
/// 下載使用（主站規劃書 §3.13「單一賽事下載 .ics」）。
///
/// 🔴 全系統沒有安裝任何第三方 iCalendar 套件——本輪需求範圍只有**單一事件的靜態下載**，不是
/// 訂閱／週期性 webcal feed（那是 L4「iCal 訂閱網址管理」，S2-6 範圍，需要處理的是持續更新的
/// 完整行事曆而不是一次性檔案），手寫一個只產生單一 <c>VEVENT</c> 的最小實作比引入依賴更省成本，
/// 也不需要處理套件不支援中文全形字元編碼等額外風險。
/// </summary>
public static class IcsBuilder
{
    public static string BuildSingleEvent(IcsEvent calendarEvent)
    {
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//TCRFC//Calendar//ZH-TW",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:{Escape(calendarEvent.Uid)}",
            $"DTSTAMP:{FormatUtc(DateTime.UtcNow)}",
        };

        if (calendarEvent.IsAllDay)
        {
            // 全天事件依 RFC 5545 §3.6.1 用 VALUE=DATE，不帶時間與時區——比對照組
            // matches.original_match_on 等既有欄位的「牆上日期」語意一致，不因為輸出 .ics
            // 而額外引入時區轉換的複雜度。
            lines.Add($"DTSTART;VALUE=DATE:{calendarEvent.StartsAtUtc:yyyyMMdd}");
            if (calendarEvent.EndsAtUtc is { } endsAllDay)
            {
                // .ics 的全天事件 DTEND 依規格是「不含」的下一天，呼叫端傳入的已是實際結束日期，
                // 這裡補上一天以符合規格語意（顯示上才會涵蓋到結束當天）。
                lines.Add($"DTEND;VALUE=DATE:{endsAllDay.AddDays(1):yyyyMMdd}");
            }
        }
        else
        {
            lines.Add($"DTSTART:{FormatUtc(calendarEvent.StartsAtUtc)}");
            if (calendarEvent.EndsAtUtc is { } endsAt)
            {
                lines.Add($"DTEND:{FormatUtc(endsAt)}");
            }
        }

        lines.Add($"SUMMARY:{Escape(calendarEvent.Summary)}");
        if (!string.IsNullOrWhiteSpace(calendarEvent.Location))
        {
            lines.Add($"LOCATION:{Escape(calendarEvent.Location)}");
        }
        if (!string.IsNullOrWhiteSpace(calendarEvent.Description))
        {
            lines.Add($"DESCRIPTION:{Escape(calendarEvent.Description)}");
        }
        if (!string.IsNullOrWhiteSpace(calendarEvent.Url))
        {
            lines.Add($"URL:{Escape(calendarEvent.Url)}");
        }
        lines.Add($"STATUS:{calendarEvent.Status}");
        lines.Add($"CREATED:{FormatUtc(calendarEvent.CreatedAtUtc)}");
        lines.Add($"LAST-MODIFIED:{FormatUtc(calendarEvent.UpdatedAtUtc)}");
        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            foreach (var folded in FoldLine(line))
            {
                sb.Append(folded).Append("\r\n"); // RFC 5545 §3.1 規定行結尾一律 CRLF。
            }
        }

        return sb.ToString();
    }

    private static string FormatUtc(DateTime value) => value.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");

    /// <summary>RFC 5545 §3.3.11 逸出規則：反斜線、分號、逗號、換行皆須逸出。</summary>
    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n");

    /// <summary>RFC 5545 §3.1 行折疊：內容行以八位元組計超過 75 就要折行，延續行以單一空白開頭。
    /// 用 UTF-8 位元組數判斷（中文字元佔 3 個位元組），避免中文標題被從字元中間切斷產生亂碼。</summary>
    private static IEnumerable<string> FoldLine(string line)
    {
        const int maxOctets = 75;
        var bytes = Encoding.UTF8.GetBytes(line);
        if (bytes.Length <= maxOctets)
        {
            yield return line;
            yield break;
        }

        var offset = 0;
        var first = true;
        while (offset < bytes.Length)
        {
            var limit = Math.Min(maxOctets - (first ? 0 : 1), bytes.Length - offset);
            // 不得把一個多位元組 UTF-8 字元從中間切斷——往回找到安全的切點。
            while (limit > 1 && (bytes[offset + limit] & 0xC0) == 0x80)
            {
                limit--;
            }

            var chunk = Encoding.UTF8.GetString(bytes, offset, limit);
            yield return first ? chunk : " " + chunk;
            offset += limit;
            first = false;
        }
    }
}

/// <summary>單一 <c>VEVENT</c> 的輸入形狀。<see cref="StartsAtUtc"/>／<see cref="EndsAtUtc"/> 一律要求
/// 呼叫端先轉換成 UTC（本站賽事／自建事件的牆上時間一律視為 <c>Asia/Taipei</c>，見
/// <c>Features/Calendar/CalendarIcsRepository.cs</c> 檔頭的時區換算說明），本類別不做任何時區判斷，
/// 只負責格式化與逸出。</summary>
public sealed record IcsEvent
{
    public required string Uid { get; init; }
    public required DateTime StartsAtUtc { get; init; }
    public DateTime? EndsAtUtc { get; init; }
    public bool IsAllDay { get; init; }
    public required string Summary { get; init; }
    public string? Location { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }

    /// <summary><c>CONFIRMED</c>／<c>CANCELLED</c>／<c>TENTATIVE</c>（RFC 5545 §3.8.1.11）。</summary>
    public string Status { get; init; } = "CONFIRMED";
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}
