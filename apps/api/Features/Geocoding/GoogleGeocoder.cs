using System.Globalization;
using System.Text.Json;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.Geocoding;

/// <summary>
/// Google Maps Geocoding API 版 <see cref="IGeocoder"/>（S2-5，2026-10-02 使用者拍板正式供應商用 Google Maps）。
/// <c>GEOCODER=google</c> 啟用，金鑰從 <c>GOOGLE_MAPS_GEOCODING_API_KEY</c> 讀（不進版控；放 VM 的 <c>club.env</c>）。
/// <para><b>狀態對應</b>：<c>OK</c>＋精度合格→座標；<c>ZERO_RESULTS</c>、或只定位到行政區／部分相符→<c>null</c>（呼叫端回 404／<c>not_found</c>）；
/// <c>OVER_QUERY_LIMIT</c>／<c>OVER_DAILY_LIMIT</c>／<c>REQUEST_DENIED</c>／<c>INVALID_REQUEST</c>／<c>UNKNOWN_ERROR</c>／HTTP 非 2xx／網路錯誤／逾時／回應無法解析
/// →一律拋 <see cref="FeatureNotConfiguredException"/>（<c>geocoder_unavailable</c>，503／<c>unavailable</c>，存檔不被阻擋）。</para>
/// <para><b>精度規則</b>（DTO 只表達 lat／lng，無法標註精度，所以寧可查無也不回誤導的點）：
/// <c>location_type=APPROXIMATE</c>（只到行政區／路段中心）一律當查無；<c>partial_match=true</c> 且不是 <c>ROOFTOP</c> 也當查無
/// （Google 沒能完整比對地址，常是門牌不同）。多筆結果只看第一筆（Google 依相關度排序）。</para>
/// <para>🔴 <b>金鑰不得外洩</b>：Geocoding 只接受 <c>key</c> 查詢字串，所以 ① 專用具名 HttpClient 以 <c>RemoveAllLoggers()</c> 關掉預設的請求 URL 日誌；
/// ② 本類別只記狀態碼／狀態字串，不記 URL、不記 Google 的 <c>error_message</c>（可能含伺服器 IP）、不記例外訊息；
/// ③ 拋出的例外一律是新建的、不帶 inner exception。🔴 地址原文同樣不進日誌。</para>
/// </summary>
public sealed class GoogleGeocoder(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<GoogleGeocoder> logger) : IGeocoder
{
    public const string HttpClientName = "google-geocoding";
    public const string ApiKeyConfigName = "GOOGLE_MAPS_GEOCODING_API_KEY";
    internal const string Endpoint = "https://maps.googleapis.com/maps/api/geocode/json";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private const string UnavailableMessage = "定位服務暫時無法使用，請稍後再試，或直接輸入緯度與經度。";

    private string? ApiKey => configuration[ApiKeyConfigName];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public async Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        var key = ApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new FeatureNotConfiguredException("「由地址定位」尚未啟用，請直接輸入緯度與經度。", "geocoder_not_configured");
        }

        var url = $"{Endpoint}?address={Uri.EscapeDataString(address.Trim())}&region=tw&language=zh-TW" +
                  $"&components={Uri.EscapeDataString("country:TW")}&key={Uri.EscapeDataString(key.Trim())}";

        string body;
        try
        {
            var http = httpClientFactory.CreateClient(HttpClientName);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseContentRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google Geocoding 回 HTTP {Status}，視為服務不可用。", (int)response.StatusCode);
                throw Unavailable();
            }

            body = await response.Content.ReadAsStringAsync(timeout.Token);
        }
        catch (FeatureNotConfiguredException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // 呼叫端自己取消，不是供應商故障
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or TimeoutException)
        {
            // 🔴 只記例外型別；訊息與 inner exception 可能帶請求 URL（含金鑰）。
            logger.LogWarning("Google Geocoding 呼叫失敗（{ExceptionType}），視為服務不可用。", ex.GetType().Name);
            throw Unavailable();
        }

        return Parse(body);
    }

    private GeocodeResult? Parse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            switch (status)
            {
                case "ZERO_RESULTS":
                    return null;
                case "OK":
                    break;
                default:
                    // OVER_QUERY_LIMIT／OVER_DAILY_LIMIT／REQUEST_DENIED／INVALID_REQUEST／UNKNOWN_ERROR／其他：只記狀態字串。
                    logger.LogWarning("Google Geocoding 回狀態 {GoogleStatus}，視為服務不可用。", Sanitize(status));
                    throw Unavailable();
            }

            if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
            {
                return null;
            }

            var first = results[0];
            var geometry = first.GetProperty("geometry");
            var locationType = geometry.TryGetProperty("location_type", out var lt) ? lt.GetString() : null;
            var partial = first.TryGetProperty("partial_match", out var pm) && pm.ValueKind == JsonValueKind.True;
            if (string.Equals(locationType, "APPROXIMATE", StringComparison.Ordinal)
                || (partial && !string.Equals(locationType, "ROOFTOP", StringComparison.Ordinal)))
            {
                return null; // 精度不足以標在地圖上的一個點：當查無，讓管理者手動輸入
            }

            var location = geometry.GetProperty("location");
            var lat = Math.Round(location.GetProperty("lat").GetDecimal(), 6); // 與 decimal(9,6) 一致
            var lng = Math.Round(location.GetProperty("lng").GetDecimal(), 6);
            if (lat is < -90 or > 90 || lng is < -180 or > 180)
            {
                throw Unavailable();
            }

            return new GeocodeResult(lat, lng, "google");
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            logger.LogWarning("Google Geocoding 回應無法解析（{ExceptionType}），視為服務不可用。", ex.GetType().Name);
            throw Unavailable();
        }
    }

    private static FeatureNotConfiguredException Unavailable() => new(UnavailableMessage, "geocoder_unavailable");

    /// <summary>狀態字串只允許大寫英文與底線、最長 32 字，避免回應內容被當日誌注入。</summary>
    private static string Sanitize(string? status)
        => status is { Length: > 0 and <= 32 } && status.All(c => c is (>= 'A' and <= 'Z') or '_') ? status : "(unrecognized)";
}

public static class GoogleGeocoderRegistration
{
    /// <summary>
    /// 註冊 Google 版定位。🔴 <c>RemoveAllLoggers()</c> 關掉 HttpClient 預設會把完整請求 URL（含 <c>key=</c>）寫進日誌的行為；
    /// 測試以同一個方法註冊，確保「金鑰不進日誌」驗的是正式設定。
    /// </summary>
    public static IServiceCollection AddGoogleGeocoder(this IServiceCollection services)
    {
        services.AddHttpClient(GoogleGeocoder.HttpClientName).RemoveAllLoggers();
        services.AddSingleton<IGeocoder, GoogleGeocoder>();
        return services;
    }
}
