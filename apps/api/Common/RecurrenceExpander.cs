namespace Tcrfc.Api.Common;

/// <summary>
/// L2 自建事件「每週／每兩週／每月」重複規則的展開器（主站規劃書 §4.12 L2、行 1381）。
///
/// 🔴 沒有另建「事件實例」資料表——<c>docs/12-database-schema.md</c> 明文「行事曆是彙整層而非
/// 資料源」，<c>CalendarCustomEvent</c> 本身已經是行事曆唯一的自有資料，materialize 出第二份
/// 實例資料等於又多一個真實來源。改為**在讀取當下、針對呼叫端要求的日期範圍即時展開**——範圍
/// 本身已經是呼叫端的必要輸入（月曆檢視一次只看一個月、公開行事曆列表也有 <c>from</c>／<c>to</c>
/// 篩選），迭代次數天然有界，不需要背景工作或快取預先產生。
/// </summary>
public static class RecurrenceExpander
{
    /// <summary>合法的 <c>calendar_custom_events.repeat_rule</c> 值域——規劃書只給「每週／每兩週／
    /// 每月」三種頻率的中文敘述，沒有給對應代碼或是否採 RRULE 格式，本輪定案為這三個英文字面值
    /// （比照 <c>matches.status</c> 同一套「挑最直白的英文單字」風格），<c>db/club-schema.sql</c>
    /// 已加上 <c>CK_calendar_custom_events_repeat_rule</c> 約束同一組值。</summary>
    public static readonly IReadOnlySet<string> AllowedRepeatRules =
        new HashSet<string>(StringComparer.Ordinal) { "weekly", "biweekly", "monthly" };

    /// <summary>即使 <c>repeat_until</c> 未設定，最多展開這麼多次（涵蓋每週規則跑滿超過一整年），
    /// 純屬防呆上限，避免資料異常（例如重複規則欄位打錯、起訖時間顛倒）造成迴圈跑太久。</summary>
    private const int MaxIterations = 400;

    /// <summary>
    /// 單一事件展開後，落在 <c>[rangeFrom, rangeToExclusive)</c> 內的全部次數起訖時間。
    /// <paramref name="repeatRule"/> 為 <c>null</c>／空字串＝不重複，只回傳原始一筆（若落在範圍內）。
    /// </summary>
    public static IReadOnlyList<(DateTime Starts, DateTime? Ends)> Expand(
        DateTime startsAt,
        DateTime? endsAt,
        string? repeatRule,
        DateOnly? repeatUntil,
        IReadOnlySet<DateOnly> exceptionDates,
        DateTime rangeFrom,
        DateTime rangeToExclusive)
    {
        var duration = endsAt is { } end ? end - startsAt : (TimeSpan?)null;

        if (string.IsNullOrEmpty(repeatRule))
        {
            return Overlaps(startsAt, endsAt, rangeFrom, rangeToExclusive)
                ? [(startsAt, endsAt)]
                : [];
        }

        if (!AllowedRepeatRules.Contains(repeatRule))
        {
            // 資料庫層已有 CHECK 約束擋住非法值，這裡只是防禦性處理（例如舊資料、手動改過的列）——
            // 視同不重複的單一事件，不讓一筆壞資料讓整個展開流程丟例外拖垮整個回應。
            return Overlaps(startsAt, endsAt, rangeFrom, rangeToExclusive)
                ? [(startsAt, endsAt)]
                : [];
        }

        var results = new List<(DateTime, DateTime?)>();
        var occurrenceStart = startsAt;

        for (var i = 0; i < MaxIterations; i++)
        {
            if (repeatUntil is { } until && DateOnly.FromDateTime(occurrenceStart) > until)
            {
                break;
            }

            if (occurrenceStart >= rangeToExclusive)
            {
                break; // 起始時間單調遞增，超過查詢範圍後不會再有任何一次落入範圍。
            }

            var occurrenceDate = DateOnly.FromDateTime(occurrenceStart);
            if (!exceptionDates.Contains(occurrenceDate))
            {
                var occurrenceEnd = duration is { } d ? occurrenceStart + d : (DateTime?)null;
                if (Overlaps(occurrenceStart, occurrenceEnd, rangeFrom, rangeToExclusive))
                {
                    results.Add((occurrenceStart, occurrenceEnd));
                }
            }

            occurrenceStart = repeatRule switch
            {
                "weekly" => occurrenceStart.AddDays(7),
                "biweekly" => occurrenceStart.AddDays(14),
                // DateTime.AddMonths 對「目標月份沒有那一天」（如 1/31 加一個月）會自動夾到
                // 目標月份的最後一天，不會丟例外，行為符合一般人對「每月同一天」的直覺期待。
                "monthly" => occurrenceStart.AddMonths(1),
                _ => throw new InvalidOperationException($"未知的重複規則「{repeatRule}」。"),
            };
        }

        return results;
    }

    private static bool Overlaps(DateTime starts, DateTime? ends, DateTime rangeFrom, DateTime rangeToExclusive)
        => starts < rangeToExclusive && (ends ?? starts) >= rangeFrom;
}
