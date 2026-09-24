using Tcrfc.Api.Common;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>S1-6 續作（B4 CSV 匯入／匯出）：<see cref="CsvUtils"/> 純函式單元測試，不需要資料庫或 HTTP。</summary>
public sealed class CsvUtilsTests
{
    [Fact]
    public void EncodeField_一般文字不加引號()
    {
        Assert.Equal("hello", CsvUtils.EncodeField("hello"));
        Assert.Equal("", CsvUtils.EncodeField(null));
    }

    [Fact]
    public void EncodeField_含逗號雙引號換行時加引號並跳脫雙引號()
    {
        Assert.Equal("\"a,b\"", CsvUtils.EncodeField("a,b"));
        Assert.Equal("\"a\"\"b\"", CsvUtils.EncodeField("a\"b"));
        Assert.Equal("\"a\nb\"", CsvUtils.EncodeField("a\nb"));
    }

    [Fact]
    public void BuildCsv與Parse可以來回還原()
    {
        var rows = new List<IEnumerable<string?>>
        {
            new string?[] { "網址名稱", "所屬分類", "狀態", "排序", "中文問題", "中文答案", "英文問題", "英文答案" },
            new string?[] { "join-team", "加入球隊、試訓", "顯示", "0", "怎麼加入？", "請填寫報名表。", "How to join?", null },
            new string?[] { "comma-test", "其他", "隱藏", "1", "含逗號,的問題", "含\"引號\"的答案\n第二行", "", "" },
        };

        var csvText = CsvUtils.BuildCsv(rows);
        var parsed = CsvUtils.Parse(csvText);

        Assert.Equal(3, parsed.Count);
        Assert.Equal(new[] { "網址名稱", "所屬分類", "狀態", "排序", "中文問題", "中文答案", "英文問題", "英文答案" }, parsed[0]);
        Assert.Equal("加入球隊、試訓", parsed[1][1]);
        Assert.Equal("How to join?", parsed[1][6]);
        Assert.Equal("", parsed[1][7]); // null 編碼後、解析回來是空字串
        Assert.Equal("含逗號,的問題", parsed[2][4]);
        Assert.Equal("含\"引號\"的答案\n第二行", parsed[2][5]);
    }

    [Fact]
    public void Parse_開頭BOM會被移除()
    {
        var withBom = "﻿a,b\n1,2\n";
        var parsed = CsvUtils.Parse(withBom);
        Assert.Equal(new[] { "a", "b" }, parsed[0]);
        Assert.Equal(new[] { "1", "2" }, parsed[1]);
    }

    [Fact]
    public void Parse_忽略檔案結尾的空白行()
    {
        var content = "a,b\n1,2\n\n\n";
        var parsed = CsvUtils.Parse(content);
        Assert.Equal(2, parsed.Count);
    }

    [Fact]
    public void Parse_CRLF與LF皆可正確斷行()
    {
        var content = "a,b\r\n1,2\r\n3,4\n";
        var parsed = CsvUtils.Parse(content);
        Assert.Equal(3, parsed.Count);
        Assert.Equal(new[] { "3", "4" }, parsed[2]);
    }

    [Fact]
    public void ToUtf8BytesWithBom_開頭三個位元組是UTF8的BOM()
    {
        var bytes = CsvUtils.ToUtf8BytesWithBom("測試");
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
    }
}
