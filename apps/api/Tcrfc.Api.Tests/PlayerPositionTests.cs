using Tcrfc.Api.Features.Players;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>球員位置代碼正規化：後台曾讓人填中文「門將」，英文版顯示中文、App 歸到「其他」（docs/18 W-6／M-11）。</summary>
public sealed class PlayerPositionTests
{
    [Theory]
    [InlineData("門將", "GK")]
    [InlineData("守門員", "GK")]
    [InlineData("Goalkeeper", "GK")]
    [InlineData("gk", "GK")]
    [InlineData("後衛", "DF")]
    [InlineData("Defender", "DF")]
    [InlineData("df", "DF")]
    [InlineData("中場", "MF")]
    [InlineData("Midfielder", "MF")]
    [InlineData(" mf ", "MF")]
    [InlineData("前鋒", "FW")]
    [InlineData("Forward", "FW")]
    [InlineData("fw", "FW")]
    [InlineData("FW", "FW")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("邊鋒", "邊鋒")]
    public void Normalize_maps_chinese_and_codes(string? input, string? expected)
        => Assert.Equal(expected, PlayerPosition.Normalize(input));
}
