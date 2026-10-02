using System.Security.Cryptography;
using System.Text;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.Geocoding;

/// <summary><see cref="Provider"/>：供應商代碼（<c>fake</c>＝本機假實作），僅供日誌與後台顯示，不寫入資料庫。</summary>
public sealed record GeocodeResult(decimal Lat, decimal Lng, string Provider);

/// <summary>
/// 「由地址定位」的接縫（S2-5，2026-10-02；主站規劃書 §4.11 K4、App 規劃書 §3.8／§16.2 第 9 項）。
/// <b>規劃書沒有指定供應商</b>（只寫「由地址定位輔助按鈕，人工確認後儲存，不做執行期即時 geocoding」），
/// 所以比照 <c>IPaymentGateway</c>／<c>IInvoiceIssuer</c> 的專案慣例：只定義介面，預設註冊
/// <see cref="NotConfiguredGeocoder"/>（如實回報「尚未啟用」，不假裝成功），本機開發註冊 <see cref="LocalFakeGeocoder"/>。
/// 正式供應商（Google Geocoding／TGOS／Azure Maps…）列為待決，選定後<b>只換 <c>Program.cs</c> 的註冊與實作</b>，見 docs/17 §3。
/// 🔴 只供後台 K4 使用（管理者按鈕或存檔時由伺服器端呼叫）；<b>App 與前台訪客的任何請求都不得觸發本介面</b>
/// （規劃書：不做執行期 geocoding，使用者位置與查詢不得送出）。🔴 實作不得把地址寫進日誌（可能含個資性質的地址）。
/// </summary>
public interface IGeocoder
{
    /// <summary>false＝尚未串接，定位按鈕回 503。</summary>
    bool IsConfigured { get; }

    /// <returns>查得座標；<c>null</c>＝供應商查無此地址（不是錯誤）。供應商故障請拋例外，由呼叫端決定降級方式。</returns>
    Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken cancellationToken);
}

public sealed class NotConfiguredGeocoder : IGeocoder
{
    public bool IsConfigured => false;

    public Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken cancellationToken)
        => throw new FeatureNotConfiguredException("「由地址定位」尚未啟用，請直接輸入緯度與經度。", "geocoder_not_configured");
}

/// <summary>
/// 本機假定位：<b>只在 Development 註冊</b>（<c>GEOCODER=fake</c> 在 Production 啟動即失敗）。
/// 由地址的 SHA-256 決定性地算出一組落在臺灣範圍內的座標（同地址永遠同結果）；地址含「查無」二字時回 <c>null</c>
/// （模擬供應商查無此地址），含「故障」二字時拋例外（模擬供應商故障）。<b>絕不碰任何外部服務。</b>
/// </summary>
public sealed class LocalFakeGeocoder : IGeocoder
{
    public bool IsConfigured => true;

    public Task<GeocodeResult?> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        if (address.Contains("故障", StringComparison.Ordinal))
        {
            throw new HttpRequestException("假定位：模擬供應商故障");
        }

        if (address.Contains("查無", StringComparison.Ordinal))
        {
            return Task.FromResult<GeocodeResult?>(null);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(address.Trim()));
        // 臺灣本島大致範圍：緯度 22.0–25.2、經度 120.0–121.9；6 位小數與資料表 decimal(9,6) 一致。
        var latUnit = BitConverter.ToUInt32(hash, 0) / (decimal)uint.MaxValue;
        var lngUnit = BitConverter.ToUInt32(hash, 4) / (decimal)uint.MaxValue;
        var lat = Math.Round(22.0m + latUnit * 3.2m, 6);
        var lng = Math.Round(120.0m + lngUnit * 1.9m, 6);
        return Task.FromResult<GeocodeResult?>(new GeocodeResult(lat, lng, "fake"));
    }
}
