using Tcrfc.Api.Features.Players;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>球員國籍代碼：正規化、驗證、遷移別名對照（2026-11-09，後台改下拉、資料庫存代碼）。</summary>
public sealed class NationalityCodeTests
{
    [Fact]
    public void All_has_249_iso_codes_plus_four_british_nations()
    {
        Assert.Equal(253, NationalityCode.All.Count);
        foreach (var c in new[] { "TW", "JP", "KR", "US", "GB", "GB-ENG", "GB-SCT", "GB-WLS", "GB-NIR" })
            Assert.Contains(c, NationalityCode.All);
        Assert.All(NationalityCode.All, c => Assert.True(c.Length <= NationalityCode.MaxLength));
    }

    [Theory]
    [InlineData(" tw ", "TW")]
    [InlineData("gb-eng", "GB-ENG")]
    [InlineData("JP", "JP")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Normalize_trims_and_uppercases(string? input, string? expected)
        => Assert.Equal(expected, NationalityCode.Normalize(input));

    [Theory]
    [InlineData("TW", true)]
    [InlineData("tw", true)]
    [InlineData("GB-NIR", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("台灣", false)]
    [InlineData("Taiwan", false)]
    [InlineData("ZZ", false)]
    [InlineData("GB-XXX", false)]
    [InlineData("XK", false)]
    public void IsValid_checks_list(string? input, bool expected)
        => Assert.Equal(expected, NationalityCode.IsValid(input));

    [Fact]
    public void Legacy_names_all_point_to_valid_codes_and_cover_common_countries()
    {
        var map = NationalityLegacyNames.Build();
        Assert.All(map.Values, c => Assert.Contains(c, NationalityCode.All));
        Assert.True(map.Values.Distinct().Count() >= 70, "常見國家至少涵蓋 70 國");
        Assert.Equal("TW", map["台灣"]);
        Assert.Equal("TW", map["臺灣"]);
        Assert.Equal("TW", map["中華台北"]);
        Assert.Equal("TW", map["中華民國"]);
        Assert.Equal("KR", map["南韓"]);
        Assert.Equal("GB-ENG", map["英格蘭"]);
        Assert.Equal("JP", map["japan"]);
    }

    [Fact]
    public void Migration_sql_embeds_every_alias()
    {
        var root = FindRepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "db", "migrations", "20261109_player-nationality-code.sql"));
        var migration = File.ReadAllText(Directory.GetFiles(Path.Combine(root, "apps", "api", "Data", "Migrations"), "*_PlayerNationalityToCode.cs").Single());
        foreach (var (alias, code) in NationalityLegacyNames.Build())
        {
            var row = $"(N'{alias.Replace("'", "''")}', N'{code}')";
            Assert.Contains(row, sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(row, migration, StringComparison.OrdinalIgnoreCase);
        }
        foreach (var code in NationalityCode.All)
            Assert.Contains($"N'{code}')", sql);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "db", "migrations"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("找不到 repo 根目錄");
    }
}
