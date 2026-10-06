using System.Text.RegularExpressions;

namespace Tcrfc.Api.Features.AdminPlayers;

/// <summary>
/// 球員網址代稱（<c>players.slug</c>，後台介面稱「網址代稱」）的格式與自動產生規則。
/// 依據：App 規劃書 §2.3 深連結 <c>tcrfc://player/{slug}</c> → <c>/zh/club/first-team/player/{slug}</c>。
/// 格式與其他內容表的 slug 一致：小寫英文字母、數字與連字號（<c>[a-z0-9]+(-[a-z0-9]+)*</c>），上限 160。
/// 唯一範圍是同一個俱樂部（<c>(club_id, slug)</c>，docs/12b §11.1）。
/// </summary>
internal static partial class PlayerSlug
{
    public const int MaxLength = 160;

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Format();

    public static void Validate(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new AdminPlayerValidationException("網址代稱不能是空白。", "slug");
        }

        if (slug.Length > MaxLength || !Format().IsMatch(slug))
        {
            throw new AdminPlayerValidationException(
                $"網址代稱「{slug}」格式不正確：只能使用小寫英文字母、數字與連字號（-），開頭與結尾不能是連字號，" +
                $"不能連續兩個連字號，長度最多 {MaxLength} 個字元。", "slug");
        }
    }

    /// <summary>英文姓名或任意文字 → <c>[a-z0-9-]</c>；沒有任何可用字元時回傳空字串。</summary>
    public static string Slugify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var lowered = text.ToLowerInvariant();
        var replaced = NonSlugChars().Replace(lowered, "-");
        return MultiDash().Replace(replaced, "-").Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlugChars();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultiDash();

    /// <summary>
    /// 沒指定代稱時的預設值：有英文姓名用姓名；沒有用「隊別代號-背號」；兩者都沒有用 <c>player-{id 前 8 碼}</c>。
    /// 中文姓名不做轉拼音（不亂猜），由後台人員在「網址代稱」欄位補上。
    /// </summary>
    public static string Suggest(string? nameEn, string teamCode, int? shirtNo, Guid playerId)
    {
        var fromName = Slugify(nameEn);
        if (fromName.Length > 0)
        {
            return Truncate(fromName);
        }

        var fromTeam = Slugify(shirtNo is { } n ? $"{teamCode}-{n}" : "");
        return fromTeam.Length > 0 ? Truncate(fromTeam) : $"player-{playerId.ToString("N")[..8]}";
    }

    public static string Truncate(string slug)
        => slug.Length <= MaxLength ? slug : slug[..MaxLength].TrimEnd('-');
}
