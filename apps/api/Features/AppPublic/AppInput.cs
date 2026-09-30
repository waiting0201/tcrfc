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
