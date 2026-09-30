using Tcrfc.Api.Common;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>S1-11：單場賽事 <c>.ics</c> 產生器的純單元測試（RFC 5545 基本形狀，不需要資料庫）。</summary>
public sealed class IcsBuilderTests
{
    [Fact]
    public void 基本欄位皆輸出_且以CRLF結尾()
    {
        var content = IcsBuilder.BuildSingleEvent(new IcsEvent
        {
            Uid = "match-abc@tcrfc",
            StartsAtUtc = new DateTime(2026, 10, 3, 11, 0, 0, DateTimeKind.Utc),
            EndsAtUtc = new DateTime(2026, 10, 3, 13, 0, 0, DateTimeKind.Utc),
            Summary = "台中磐石 vs 測試對手",
            Location = "台中足球場",
            CreatedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
        });

        Assert.Contains("BEGIN:VCALENDAR\r\n", content);
        Assert.Contains("BEGIN:VEVENT\r\n", content);
        Assert.Contains("UID:match-abc@tcrfc\r\n", content);
        Assert.Contains("DTSTART:20261003T110000Z\r\n", content);
        Assert.Contains("DTEND:20261003T130000Z\r\n", content);
        Assert.Contains("STATUS:CONFIRMED\r\n", content);
        Assert.Contains("END:VEVENT\r\n", content);
        Assert.Contains("END:VCALENDAR\r\n", content);
    }

    [Fact]
    public void 全天事件_用VALUEDATE_結束日期補一天()
    {
        var content = IcsBuilder.BuildSingleEvent(new IcsEvent
        {
            Uid = "custom-abc@tcrfc",
            StartsAtUtc = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
            EndsAtUtc = new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc),
            IsAllDay = true,
            Summary = "球迷見面會",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        Assert.Contains("DTSTART;VALUE=DATE:20261003\r\n", content);
        // 結束日期依 RFC 5545 是「不含」的下一天，輸入 10/4（實際結束當天）要再補一天變成 10/5。
        Assert.Contains("DTEND;VALUE=DATE:20261005\r\n", content);
    }

    [Fact]
    public void 特殊字元逸出()
    {
        var content = IcsBuilder.BuildSingleEvent(new IcsEvent
        {
            Uid = "match-esc@tcrfc",
            StartsAtUtc = DateTime.UtcNow,
            Summary = "台中磐石 vs A; B, C\\D",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        Assert.Contains("SUMMARY:台中磐石 vs A\\; B\\, C\\\\D\r\n", content);
    }

    [Fact]
    public void 取消狀態輸出CANCELLED()
    {
        var content = IcsBuilder.BuildSingleEvent(new IcsEvent
        {
            Uid = "match-cancelled@tcrfc",
            StartsAtUtc = DateTime.UtcNow,
            Summary = "賽事已取消",
            Status = "CANCELLED",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        Assert.Contains("STATUS:CANCELLED\r\n", content);
    }

    [Fact]
    public void 沒有地點與說明時不輸出對應欄位()
    {
        var content = IcsBuilder.BuildSingleEvent(new IcsEvent
        {
            Uid = "match-noloc@tcrfc",
            StartsAtUtc = DateTime.UtcNow,
            Summary = "測試",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        Assert.DoesNotContain("LOCATION:", content);
        Assert.DoesNotContain("DESCRIPTION:", content);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(80)]
    public void 長行折疊_每行不超過75位元組_且不切斷中文字_摺疊還原後與原文一致(int chineseChars)
    {
        // 回歸：折疊最後一段剛好取到行尾時，舊寫法會讀到陣列外一格（S2-6 訂閱 feed 才第一次遇到需要折疊的長標題）。
        var description = new string('賽', chineseChars);
        var content = IcsBuilder.BuildCalendar("行事曆", [new IcsEvent
        {
            Uid = "match-long@tcrfc",
            StartsAtUtc = DateTime.UtcNow,
            Summary = "長行折疊測試",
            Description = description,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        }]);

        var lines = content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.All(lines, line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75, $"超過 75 位元組：{line}"));
        Assert.Contains("DESCRIPTION:" + description, content.Replace("\r\n ", string.Empty));
    }

    [Fact]
    public void 多事件日曆_帶名稱與重新整理間隔_不帶名稱時與單一事件輸出同形()
    {
        var one = new IcsEvent { Uid = "a@tcrfc", StartsAtUtc = DateTime.UtcNow, Summary = "甲", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var two = new IcsEvent { Uid = "b@tcrfc", StartsAtUtc = DateTime.UtcNow, Summary = "乙", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };

        var named = IcsBuilder.BuildCalendar("台中磐石", [one, two]);
        Assert.Contains("X-WR-CALNAME:台中磐石\r\n", named);
        Assert.Contains("REFRESH-INTERVAL;VALUE=DURATION:PT6H\r\n", named);
        Assert.Equal(2, named.Split("BEGIN:VEVENT").Length - 1);

        var plain = IcsBuilder.BuildCalendar(null, [one]);
        Assert.DoesNotContain("X-WR-CALNAME", plain);
        Assert.Equal(plain.Split("\r\n").Length, IcsBuilder.BuildSingleEvent(one).Split("\r\n").Length);
    }
}
