using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Tcrfc.Api.Common;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>E-109：Production 的 Data Protection 金鑰環路徑啟動檢查。純單元測試，不起 WebApplication。</summary>
public sealed class DataProtectionKeyRingTests : IDisposable
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tcrfc.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private readonly string _tmp = Directory.CreateTempSubdirectory("tcrfc-dpkr-").FullName;

    public void Dispose()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(_tmp, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            Directory.Delete(_tmp, true);
        }
        catch (IOException) { }
    }

    private static IConfiguration Config(string? path) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [DataProtectionKeyRing.ConfigKey] = path }).Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Production_MissingOrBlank_Throws(string? value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionKeyRing.Resolve(Config(value), new Env("Production")));
        Assert.Contains("DATA_PROTECTION_KEYS_PATH", ex.Message);
        Assert.Contains("infra/README.md §4.3", ex.Message);
    }

    [Fact]
    public void Production_DirectoryMissing_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DataProtectionKeyRing.Resolve(Config(Path.Combine(_tmp, "nope")), new Env("Production")));
        Assert.Contains("不存在", ex.Message);
        Assert.Contains("infra/README.md §4.3", ex.Message);
    }

    [Fact]
    public void Production_DirectoryNotWritable_Throws()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return; // 權限位元在 Windows／root 下無法造出不可寫目錄
        }

        var ro = Directory.CreateDirectory(Path.Combine(_tmp, "ro")).FullName;
        File.SetUnixFileMode(ro, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var ex = Assert.Throws<InvalidOperationException>(() => DataProtectionKeyRing.Resolve(Config(ro), new Env("Production")));
        Assert.Contains("不可寫", ex.Message);
        Assert.Contains("infra/README.md §4.3", ex.Message);
    }

    [Fact]
    public void Production_ValidDirectory_ReturnsItAndLeavesNoProbeFile()
    {
        var dir = DataProtectionKeyRing.Resolve(Config(_tmp), new Env("Production"));
        Assert.NotNull(dir);
        Assert.Equal(new DirectoryInfo(_tmp).FullName, dir!.FullName);
        Assert.Empty(Directory.GetFileSystemEntries(_tmp));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void NonProduction_MissingValue_ReturnsNull_NoThrow(string env)
        => Assert.Null(DataProtectionKeyRing.Resolve(Config(null), new Env(env)));

    [Fact]
    public void Development_NonexistentDirectory_NotChecked()
    {
        var path = Path.Combine(_tmp, "later");
        var dir = DataProtectionKeyRing.Resolve(Config(path), new Env("Development"));
        Assert.NotNull(dir);
        Assert.False(Directory.Exists(path));
    }
}
