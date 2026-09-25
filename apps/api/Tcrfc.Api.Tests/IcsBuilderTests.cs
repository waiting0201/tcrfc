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
}
