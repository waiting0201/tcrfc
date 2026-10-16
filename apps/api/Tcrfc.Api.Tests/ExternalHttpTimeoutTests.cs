using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Tcrfc.Api.Common;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 對外 HTTP 呼叫的逾時集中在 <see cref="ExternalHttp"/>（E-338 後續）。開立憑證、商店發票、退款連動作廢都是「持有交易層級 sp_getapplock 期間呼叫外部服務」，
/// HttpClient 預設逾時 100 秒會讓外部服務卡住時整把鎖被占住；持鎖路徑的鎖等待上限是 20 秒，所以逾時必須 ≤ 15 秒。不需要資料庫。
/// </summary>
public sealed class ExternalHttpTimeoutTests
{
    private static string ApiDir([CallerFilePath] string thisFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    [Fact]
    public void 專案內不得直接註冊HttpClient_一律經ExternalHttp集中設定逾時()
    {
        var violations = new List<string>();
        foreach (var file in Directory.EnumerateFiles(ApiDir(), "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}Tcrfc.Api.Tests{Path.DirectorySeparatorChar}")
                                 && !f.EndsWith($"{Path.DirectorySeparatorChar}ExternalHttp.cs", StringComparison.Ordinal)))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                if (!line.StartsWith("//", StringComparison.Ordinal) && !line.StartsWith("///", StringComparison.Ordinal)
                    && (line.Contains(".AddHttpClient(", StringComparison.Ordinal) || line.Contains("new HttpClient(", StringComparison.Ordinal)))
                {
                    violations.Add($"{Path.GetRelativePath(ApiDir(), file)}:{i + 1}  {line}");
                }
            }
        }

        Assert.True(violations.Count == 0,
            "對外 HTTP 呼叫必須用 services.AddExternalHttpClient(名稱) 註冊（逾時集中在 Common/ExternalHttp.cs）；直接 AddHttpClient／new HttpClient 會落回預設 100 秒：\n" + string.Join("\n", violations));
    }

    [Theory]
    [InlineData(ExternalHttp.InvoiceIssuer, 15)]
    [InlineData(ExternalHttp.LinePay, 15)]
    [InlineData(ExternalHttp.LineLogin, 10)]
    [InlineData(ExternalHttp.ClubTurnstile, 5)]
    [InlineData(ExternalHttp.CharityTurnstile, 5)]
    [InlineData(ExternalHttp.GoogleGeocoding, 5)]
    public void 註冊的用戶端套用集中設定的逾時_且不超過15秒(string name, int seconds)
    {
        var services = new ServiceCollection();
        services.AddExternalHttpClient(name);
        using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(name);

        Assert.Equal(TimeSpan.FromSeconds(seconds), client.Timeout);
        Assert.True(client.Timeout <= ExternalHttp.MaxTimeout);
    }

    [Fact]
    public void 加值中心與LINE_Pay預先註冊_串接時不會漏設逾時()
    {
        var services = new ServiceCollection();
        services.AddExternalHttpClientDefaults();
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        Assert.Equal(TimeSpan.FromSeconds(15), factory.CreateClient(ExternalHttp.InvoiceIssuer).Timeout);
        Assert.Equal(TimeSpan.FromSeconds(15), factory.CreateClient(ExternalHttp.LinePay).Timeout);
    }

    [Fact]
    public void 未列出的名稱使用預設逾時_仍受上限約束()
    {
        Assert.Equal(ExternalHttp.DefaultTimeout, ExternalHttp.TimeoutFor("some-new-client"));
        Assert.True(ExternalHttp.DefaultTimeout <= ExternalHttp.MaxTimeout);
    }

    [Fact]
    public void 持鎖呼叫的逾時必須小於退款連動作廢的等鎖上限()
    {
        // 退款等鎖 20 秒（慈善 CharityInvoiceService.RefundLockWaitMilliseconds、商店 ShopInvoiceService.RefundLockWaitMilliseconds）要大於加值中心逾時，
        // 否則退款會在開立「還沒逾時」之前就放棄等待。
        Assert.True(ExternalHttp.TimeoutFor(ExternalHttp.InvoiceIssuer) < TimeSpan.FromMilliseconds(20_000));
        Assert.True(ExternalHttp.TimeoutFor(ExternalHttp.LinePay) < TimeSpan.FromMilliseconds(20_000));
    }
}
