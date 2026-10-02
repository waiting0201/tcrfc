using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Geocoding;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// Google Geocoding 版定位（S2-5，不需要資料庫、<b>不打真實 Google API</b>）：以假的 <see cref="HttpMessageHandler"/> 覆蓋各狀態、逾時，
/// 並驗證金鑰不會出現在日誌、例外訊息或錯誤內容（含 HttpClient 預設的請求 URL 日誌）。
/// </summary>
public sealed class GoogleGeocoderTests
{
    private const string SecretKey = "AIzaSy-TEST-SECRET-KEY-0123456789";
    private const string Address = "台中市西屯區臺灣大道三段 99 號";

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return respond(request);
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Lines { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);
        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner.Lines)
                {
                    owner.Lines.Add(formatter(state, exception) + (exception is null ? string.Empty : "\n" + exception));
                }
            }
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string Ok(string locationType = "ROOFTOP", bool partial = false, double lat = 24.1788123456, double lng = 120.6445987654)
        => "{\"status\":\"OK\",\"results\":[{\"partial_match\":" + partial.ToString().ToLowerInvariant() +
           ",\"geometry\":{\"location\":{\"lat\":" + lat.ToString(System.Globalization.CultureInfo.InvariantCulture) +
           ",\"lng\":" + lng.ToString(System.Globalization.CultureInfo.InvariantCulture) + "},\"location_type\":\"" + locationType + "\"}}," +
           "{\"geometry\":{\"location\":{\"lat\":1.0,\"lng\":2.0},\"location_type\":\"ROOFTOP\"}}]}";

    /// <summary>用與正式相同的註冊方法（含 RemoveAllLoggers）組出 DI，primary handler 換成假的，回傳（定位器、日誌、handler）。</summary>
    private static (IGeocoder Geocoder, CapturingLoggerProvider Logs, StubHandler Handler) Build(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respond, string? apiKey = SecretKey)
    {
        var handler = new StubHandler(respond);
        var logs = new CapturingLoggerProvider();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [GoogleGeocoder.ApiKeyConfigName] = apiKey })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddGoogleGeocoder();
        services.AddHttpClient(GoogleGeocoder.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IGeocoder>(), logs, handler);
    }

    private static void AssertNoSecret(CapturingLoggerProvider logs, Exception? ex = null)
    {
        foreach (var line in logs.Lines)
        {
            Assert.DoesNotContain(SecretKey, line);
            Assert.DoesNotContain("key=", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("臺灣大道", line); // 地址原文同樣不進日誌
        }

        if (ex is not null)
        {
            Assert.DoesNotContain(SecretKey, ex.ToString());
            Assert.Null(ex.InnerException);
        }
    }

    [Fact]
    public async Task OK_回第一筆座標_請求帶region_language_components且金鑰只在查詢字串()
    {
        var (geocoder, logs, handler) = Build(_ => Task.FromResult(Json(Ok())));
        var result = await geocoder.GeocodeAsync(Address, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(24.178812m, result!.Lat); // 6 位小數，與 decimal(9,6) 一致
        Assert.Equal(120.644599m, result.Lng);
        Assert.Equal("google", result.Provider);

        var uri = Assert.Single(handler.Requests);
        Assert.Equal("maps.googleapis.com", uri.Host);
        Assert.Equal("/maps/api/geocode/json", uri.AbsolutePath);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal(Address, query["address"]);
        Assert.Equal("tw", query["region"]);
        Assert.Equal("zh-TW", query["language"]);
        Assert.Equal("country:TW", query["components"]);
        Assert.Equal(SecretKey, query["key"]);
        AssertNoSecret(logs); // 正式註冊（RemoveAllLoggers）下，HttpClient 不會把含 key 的 URL 寫進日誌
    }

    [Fact]
    public async Task ZERO_RESULTS_回null()
    {
        var (geocoder, _, _) = Build(_ => Task.FromResult(Json("""{"status":"ZERO_RESULTS","results":[]}""")));
        Assert.Null(await geocoder.GeocodeAsync(Address, CancellationToken.None));
    }

    [Theory]
    [InlineData("APPROXIMATE", false)]   // 只到行政區／路段中心 → 查無
    [InlineData("APPROXIMATE", true)]
    [InlineData("GEOMETRIC_CENTER", true)] // 部分相符且不是門牌等級 → 查無
    [InlineData("RANGE_INTERPOLATED", true)]
    public async Task 精度不足或部分相符_當查無(string locationType, bool partial)
    {
        var (geocoder, _, _) = Build(_ => Task.FromResult(Json(Ok(locationType, partial))));
        Assert.Null(await geocoder.GeocodeAsync(Address, CancellationToken.None));
    }

    [Theory]
    [InlineData("ROOFTOP", true)]
    [InlineData("RANGE_INTERPOLATED", false)]
    [InlineData("GEOMETRIC_CENTER", false)]
    public async Task 精度合格_回座標(string locationType, bool partial)
    {
        var (geocoder, _, _) = Build(_ => Task.FromResult(Json(Ok(locationType, partial))));
        Assert.NotNull(await geocoder.GeocodeAsync(Address, CancellationToken.None));
    }

    [Theory]
    [InlineData("OVER_QUERY_LIMIT")]
    [InlineData("OVER_DAILY_LIMIT")]
    [InlineData("REQUEST_DENIED")]
    [InlineData("INVALID_REQUEST")]
    [InlineData("UNKNOWN_ERROR")]
    public async Task 錯誤狀態_一律不可用503且不外洩金鑰(string status)
    {
        // error_message 實務上可能含金鑰相關描述或伺服器 IP：本實作不得把它寫進日誌或例外。
        var body = "{\"status\":\"" + status + "\",\"error_message\":\"The provided API key " + SecretKey + " is invalid. Your IP 203.0.113.9\",\"results\":[]}";
        var (geocoder, logs, _) = Build(_ => Task.FromResult(Json(body)));
        var ex = await Assert.ThrowsAsync<FeatureNotConfiguredException>(() => geocoder.GeocodeAsync(Address, CancellationToken.None));
        Assert.Equal("geocoder_unavailable", ex.Code);
        Assert.DoesNotContain("203.0.113.9", ex.Message);
        AssertNoSecret(logs, ex);
        Assert.DoesNotContain(logs.Lines, l => l.Contains("203.0.113.9"));
        Assert.Contains(logs.Lines, l => l.Contains(status)); // 狀態字串本身有記，方便維運
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task HTTP非2xx_不可用(HttpStatusCode status)
    {
        var (geocoder, logs, _) = Build(_ => Task.FromResult(Json("{}", status)));
        var ex = await Assert.ThrowsAsync<FeatureNotConfiguredException>(() => geocoder.GeocodeAsync(Address, CancellationToken.None));
        Assert.Equal("geocoder_unavailable", ex.Code);
        AssertNoSecret(logs, ex);
    }

    [Fact]
    public async Task 網路錯誤_不可用_且HttpRequestException訊息裡的金鑰不外洩()
    {
        // 模擬某些環境的例外訊息會帶完整請求網址：不得被記錄，也不得成為 inner exception。
        var (geocoder, logs, _) = Build(req => throw new HttpRequestException($"Connection refused: {req.RequestUri}"));
        var ex = await Assert.ThrowsAsync<FeatureNotConfiguredException>(() => geocoder.GeocodeAsync(Address, CancellationToken.None));
        Assert.Equal("geocoder_unavailable", ex.Code);
        Assert.DoesNotContain(SecretKey, ex.Message);
        AssertNoSecret(logs, ex);
        Assert.Contains(logs.Lines, l => l.Contains(nameof(HttpRequestException)));
    }

    [Fact]
    public async Task 逾時_不可用()
    {
        // 呼叫端沒有取消，但請求被內部逾時取消 → 供應商不可用
        var (geocoder, logs, _) = Build(req => throw new TaskCanceledException($"timeout {req.RequestUri}"));
        var ex = await Assert.ThrowsAsync<FeatureNotConfiguredException>(() => geocoder.GeocodeAsync(Address, CancellationToken.None));
        Assert.Equal("geocoder_unavailable", ex.Code);
        AssertNoSecret(logs, ex);
    }

    [Fact]
    public async Task 呼叫端自行取消_原樣傳遞_不當成供應商故障()
    {
        using var cts = new CancellationTokenSource();
        var (geocoder, _, _) = Build(async req =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.Infinite, cts.Token);
            return Json("{}");
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => geocoder.GeocodeAsync(Address, cts.Token));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"status":"OK","results":[{"geometry":{}}]}""")]
    [InlineData("""{"foo":1}""")]
    public async Task 回應無法解析_不可用(string body)
    {
        var (geocoder, logs, _) = Build(_ => Task.FromResult(Json(body)));
        var ex = await Assert.ThrowsAsync<FeatureNotConfiguredException>(() => geocoder.GeocodeAsync(Address, CancellationToken.None));
        Assert.Equal("geocoder_unavailable", ex.Code);
        AssertNoSecret(logs, ex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task 沒有金鑰_未啟用_且完全不發出請求(string? apiKey)
    {
        var (geocoder, _, handler) = Build(_ => Task.FromResult(Json(Ok())), apiKey);
        Assert.False(geocoder.IsConfigured);
        var ex = await Assert.ThrowsAsync<FeatureNotConfiguredException>(() => geocoder.GeocodeAsync(Address, CancellationToken.None));
        Assert.Equal("geocoder_not_configured", ex.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void 有金鑰_IsConfigured為true()
    {
        var (geocoder, _, _) = Build(_ => Task.FromResult(Json(Ok())));
        Assert.True(geocoder.IsConfigured);
    }
}
