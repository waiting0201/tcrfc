using System.Text.RegularExpressions;

namespace Tcrfc.Api.Features.AppPublic;

/// <summary>
/// App 版本號比較（docs/19 §7、docs/06 §3）：語意化版本 <c>主.次.修</c>，<b>建置號不參與比較</b>，預發布尾綴（<c>-beta</c>）視為同一個 <c>主.次.修</c>。
/// 缺少的段補 0（<c>1.2</c> ＝ <c>1.2.0</c>）。格式不合法回 <c>null</c>，由呼叫端決定要擋還是忽略。
/// </summary>
public static partial class AppVersion
{
    [GeneratedRegex(@"^(\d{1,4})(?:\.(\d{1,4}))?(?:\.(\d{1,4}))?(?:\.\d{1,6})?(?:[-+][0-9A-Za-z.\-]+)?$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? value) => TryParse(value, out _);

    public static bool TryParse(string? value, out (int Major, int Minor, int Patch) version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var m = Pattern().Match(value.Trim());
        if (!m.Success)
        {
            return false;
        }

        version = (int.Parse(m.Groups[1].Value), m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0, m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0);
        return true;
    }

    /// <summary>比較兩個版本；任一不合法回 <c>null</c>。</summary>
    public static int? Compare(string? left, string? right)
    {
        if (!TryParse(left, out var l) || !TryParse(right, out var r))
        {
            return null;
        }

        return l.CompareTo(r);
    }
}
