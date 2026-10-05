using System.Text.RegularExpressions;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.AppPublic;

/// <summary>App 公開端點的輸入驗證（匿名呼叫端，一律嚴格檢查格式與長度）。錯誤訊息是日常中文，統一走 400。</summary>
public static partial class AppInput
{
    [GeneratedRegex(@"^[A-Za-z0-9\-]{8,64}$")]
    private static partial Regex DeviceIdFormat();

    [GeneratedRegex(@"^[A-Za-z0-9_\-:.]{1,64}$")]
    private static partial Regex TokenFormat();

    /// <summary>
    /// 匿名端點「須帶裝置識別」（App 規劃書 §9.3）的標頭名稱。值是 App 註冊裝置時使用的 <c>deviceInstallId</c>（同 <c>PUT /app/devices/{id}</c> 的路徑值）。
    /// 🔵 決定（2026-10-05）：帶在標頭，不放網址（網址會進存取日誌與分享連結）。查詢參數 <c>deviceInstallId</c> 仍接受（相容既有用戶端），標頭優先。
    /// 後端行為：有帶就驗證格式（不合 400）並用於「依裝置而異」的內容（公告條對象、通知中心、廣告頻次）；**沒帶不拒絕**——
    /// 官網（apps/web）與其他非 App 用戶端共用同一套公開端點、不會帶，且規劃書 M2 本來就定義「沒帶裝置識別時只回全體對象內容、可邊緣快取」。
    /// 其餘公開內容端點（賽程、新聞、球隊…）接受但忽略此標頭（內容不因裝置而異）。
    /// </summary>
    public const string DeviceHeaderName = "X-Device-Install-Id";

    /// <summary>標頭優先、其次查詢參數；有值就驗證格式；都沒有回 null（允許，見 <see cref="DeviceHeaderName"/>）。</summary>
    public static string? ResolveOptionalDeviceId(HttpContext http, string? queryValue)
    {
        var header = http.Request.Headers[DeviceHeaderName].ToString();
        var value = !string.IsNullOrEmpty(header) ? header : queryValue;
        return string.IsNullOrEmpty(value) ? null : RequireDeviceId(value);
    }

    public static string RequireDeviceId(string? value)
    {
        if (string.IsNullOrEmpty(value) || !DeviceIdFormat().IsMatch(value))
        {
            throw new AdminValidationException("裝置識別碼格式不正確（8 到 64 個英數字元或連字號）。");
        }

        return value;
    }

    public static string RequirePlatform(string? value)
    {
        if (value is not ("ios" or "android"))
        {
            throw new AdminValidationException("平台只能是 ios 或 android。");
        }

        return value;
    }

    public static string? OptionalVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length > 32 || !AppVersion.IsValid(value))
        {
            throw new AdminValidationException("App 版本號格式不正確（例如 1.2.0）。");
        }

        return value.Trim();
    }

    /// <summary>短識別字串（批次編號、曝光識別碼）：空白視為沒有；格式不合或過長 400。</summary>
    public static string? OptionalToken(string? value, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length > maxLength || !TokenFormat().IsMatch(value))
        {
            throw new AdminValidationException($"{label}格式不正確。");
        }

        return value;
    }
}
