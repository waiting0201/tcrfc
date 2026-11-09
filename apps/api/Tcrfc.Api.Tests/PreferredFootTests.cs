using Tcrfc.Api.Features.Players;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>慣用腳代碼正規化：後台曾把中文「左腳」直接存進資料庫，英文版因此顯示中文（2026-11-09）。</summary>
public sealed class PreferredFootTests
{
    [Theory]
    [InlineData("左腳", "left")]
    [InlineData("右腳", "right")]
    [InlineData("雙腳", "both")]
    [InlineData("LEFT", "left")]
    [InlineData(" right ", "right")]
    [InlineData("both", "both")]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("其他", "其他")]
    public void Normalize_maps_chinese_and_codes(string? input, string? expected)
        => Assert.Equal(expected, PreferredFoot.Normalize(input));
}
