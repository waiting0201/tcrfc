using Tcrfc.Api.Common;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// S1-11：L2 自建事件重複規則展開器的純單元測試（不需要資料庫、不需要 <c>WebApplicationFactory</c>）。
/// </summary>
public sealed class RecurrenceExpanderTests
{
    [Fact]
    public void 不重複的事件_落在範圍內回傳一筆()
    {
        var starts = new DateTime(2026, 10, 5, 19, 0, 0, DateTimeKind.Utc);
        var ends = starts.AddHours(2);

        var result = RecurrenceExpander.Expand(
            starts, ends, null, null, new HashSet<DateOnly>(),
            new DateTime(2026, 10, 1), new DateTime(2026, 11, 1));

        var occurrence = Assert.Single(result);
        Assert.Equal(starts, occurrence.Starts);
        Assert.Equal(ends, occurrence.Ends);
    }

    [Fact]
    public void 不重複的事件_範圍外不回傳()
    {
        var starts = new DateTime(2026, 9, 5, 19, 0, 0, DateTimeKind.Utc);

        var result = RecurrenceExpander.Expand(
            starts, null, null, null, new HashSet<DateOnly>(),
            new DateTime(2026, 10, 1), new DateTime(2026, 11, 1));

        Assert.Empty(result);
    }

    [Fact]
    public void 每週重複_展開出範圍內的全部次數()
    {
        var starts = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc); // 星期六

        var result = RecurrenceExpander.Expand(
            starts, null, "weekly", null, new HashSet<DateOnly>(),
            new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

        // 10/3, 10/10, 10/17, 10/24 落在 [10/1, 10/31)，10/31 本身是下一次但已達 ToExclusive 邊界外一週。
        Assert.Equal(4, result.Count);
        Assert.Equal(new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc), result[0].Starts);
        Assert.Equal(new DateTime(2026, 10, 24, 10, 0, 0, DateTimeKind.Utc), result[3].Starts);
    }

    [Fact]
    public void 每兩週重複_間隔正確()
    {
        var starts = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

        var result = RecurrenceExpander.Expand(
            starts, null, "biweekly", null, new HashSet<DateOnly>(),
            new DateTime(2026, 10, 1), new DateTime(2026, 11, 1));

        Assert.Equal([1, 15, 29], result.Select(r => r.Starts.Day).ToArray());
    }

    [Fact]
    public void 每月重複_月底日期自動夾到目標月最後一天()
    {
        var starts = new DateTime(2026, 1, 31, 9, 0, 0, DateTimeKind.Utc);

        var result = RecurrenceExpander.Expand(
            starts, null, "monthly", null, new HashSet<DateOnly>(),
            new DateTime(2026, 1, 1), new DateTime(2026, 4, 1));

        // 1/31 → 2/28（2026 非閏年）→ 3/28（不是 3/31，因為 DateTime.AddMonths 是逐次疊加，不是回到原始日）。
        Assert.Equal(3, result.Count);
        Assert.Equal(new DateTime(2026, 1, 31, 9, 0, 0, DateTimeKind.Utc), result[0].Starts);
        Assert.Equal(new DateTime(2026, 2, 28, 9, 0, 0, DateTimeKind.Utc), result[1].Starts);
    }

    [Fact]
    public void 例外日期被排除()
    {
        var starts = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc);
        var exceptions = new HashSet<DateOnly> { new(2026, 10, 10) };

        var result = RecurrenceExpander.Expand(
            starts, null, "weekly", null, exceptions,
            new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, r => DateOnly.FromDateTime(r.Starts) == new DateOnly(2026, 10, 10));
    }

    [Fact]
    public void repeat_until_之後不再展開()
    {
        var starts = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc);

        var result = RecurrenceExpander.Expand(
            starts, null, "weekly", new DateOnly(2026, 10, 10), new HashSet<DateOnly>(),
            new DateTime(2026, 10, 1), new DateTime(2026, 12, 1));

        Assert.Equal(2, result.Count); // 10/3、10/10，10/17 已超過 repeat_until
    }

    [Fact]
    public void 未知的重複規則值_視為不重複的單一事件_不丟例外()
    {
        var starts = new DateTime(2026, 10, 5, 19, 0, 0, DateTimeKind.Utc);

        var result = RecurrenceExpander.Expand(
            starts, null, "yearly", null, new HashSet<DateOnly>(),
            new DateTime(2026, 10, 1), new DateTime(2026, 11, 1));

        Assert.Single(result);
    }
}
