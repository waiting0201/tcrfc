using System.Text.RegularExpressions;

namespace Tcrfc.Api.Common;

/// <summary>
/// 後台輸入驗證的共用小工具（E1a 起新增模組共用）。全部丟 <see cref="AdminValidationException"/>（400），
/// 訊息是日常中文、不含英文技術詞。<paramref name="label"/> 是畫面上看得到的欄位名稱。
/// </summary>
public static partial class AdminInput
{
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugFormat();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailFormat();

    /// <summary>標籤以英文字母或數字結尾（如「Email」）時，與後面的中文之間補一個空格（docs/06 §1 中英文間距）。</summary>
    private static string Spaced(string label)
        => label.Length > 0 && char.IsAsciiLetterOrDigit(label[^1]) ? label + " " : label;

    public static string RequireText(string? value, string label, int maxLength, string? field = null)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new AdminValidationException($"{Spaced(label)}為必填欄位。", field);
        }

        if (trimmed.Length > maxLength)
        {
            throw new AdminValidationException($"{Spaced(label)}不可超過 {maxLength} 個字。", field);
        }

        return trimmed;
    }

    /// <summary>選填文字：空白視為沒有（回傳 <c>null</c>）。</summary>
    public static string? OptionalText(string? value, string label, int maxLength, string? field = null)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new AdminValidationException($"{Spaced(label)}不可超過 {maxLength} 個字。", field);
        }

        return trimmed;
    }

    public static string? OptionalHttpUrl(string? value, string label, int maxLength = 500, string? field = null)
    {
        var text = OptionalText(value, label, maxLength, field);
        if (text is null)
        {
            return null;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new AdminValidationException($"{Spaced(label)}必須是以 http:// 或 https:// 開頭的完整網址。", field);
        }

        return text;
    }

    /// <summary>選填連結：只收 <c>https://</c>／<c>http://</c> 完整網址，或站內 <c>/</c> 開頭的相對路徑
    /// （<c>//</c>、<c>/\</c> 會被瀏覽器當成外站，一併拒絕）。前台會把它直接放進 <c>href</c>，
    /// <c>javascript:</c>、<c>data:</c> 之類的協定必須在這裡擋掉（稽核 E-2）。</summary>
    public static string? OptionalHttpOrSitePath(string? value, string label, int maxLength = 500, string? field = null)
    {
        var text = OptionalText(value, label, maxLength, field);
        if (text is null)
        {
            return null;
        }

        if (text[0] == '/')
        {
            if (text.Length > 1 && (text[1] == '/' || text[1] == '\\') || text.Any(char.IsControl))
            {
                throw new AdminValidationException($"{Spaced(label)}的站內路徑格式不正確。", field);
            }

            return text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new AdminValidationException($"{Spaced(label)}必須是以 http://、https:// 開頭的完整網址，或以 / 開頭的站內路徑。", field);
        }

        return text;
    }

    public static string? OptionalEmail(string? value, string label, string? field = null)
    {
        var text = OptionalText(value, label, 255, field);
        if (text is not null && !EmailFormat().IsMatch(text))
        {
            throw new AdminValidationException($"{Spaced(label)}的格式不正確。", field);
        }

        return text;
    }

    /// <summary>選填電話：只允許數字、+、-、空白與括號（同會員註冊的規則），至少要有 6 碼數字，32 字以內。</summary>
    public static string? OptionalPhone(string? value, string label, string? field = null)
    {
        var text = OptionalText(value, label, 32, field);
        if (text is not null
            && (!text.All(c => char.IsAsciiDigit(c) || c is '+' or '-' or ' ' or '(' or ')') || text.Count(char.IsAsciiDigit) < 6))
        {
            throw new AdminValidationException($"{Spaced(label)}的格式不正確，只能包含數字、+、-、空白與括號，且至少 6 碼數字。", field);
        }

        return text;
    }

    public static void DateRange(DateOnly? start, DateOnly? end, string label, string? field = null)
    {
        if (start is not null && end is not null && end < start)
        {
            throw new AdminValidationException($"{Spaced(label)}的結束日期不可早於開始日期。", field);
        }
    }

    public static int? OptionalNonNegative(int? value, string label, string? field = null)
    {
        if (value is < 0)
        {
            throw new AdminValidationException($"{Spaced(label)}不可為負數。", field);
        }

        return value;
    }

    public static string OneOf(string? value, IReadOnlySet<string> allowed, string label, string allowedText, string? field = null)
    {
        if (value is null || !allowed.Contains(value))
        {
            throw new AdminValidationException($"{Spaced(label)}只能是{allowedText}。", field);
        }

        return value;
    }

    /// <summary>網址名稱：小寫英文字母、數字與連字號，比照 <c>FaqSlugPolicy</c> 的格式規則。</summary>
    public static string Slug(string slug, string label = "網址名稱", string? field = "slug")
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new AdminValidationException($"{Spaced(label)}為必填欄位。", field);
        }

        if (slug.Length > 160 || !SlugFormat().IsMatch(slug))
        {
            throw new AdminValidationException(
                $"{Spaced(label)}「{slug}」格式不正確：只能使用小寫英文字母、數字與連字號（-），" +
                "開頭與結尾不能是連字號，也不能連續兩個連字號，長度不可超過 160 字。", field);
        }

        return slug;
    }

    /// <summary>沒有指定網址名稱時自動產生：優先用英文名稱轉出的小寫連字號寫法，轉不出東西（例如只有中文名稱）
    /// 就用前綴加隨機字串。呼叫端仍須做唯一性檢查（極小機率撞名）。</summary>
    public static string GenerateSlug(string prefix, string? englishName)
    {
        if (!string.IsNullOrWhiteSpace(englishName))
        {
            var lowered = Regex.Replace(englishName.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            if (lowered.Length > 0)
            {
                return lowered.Length > 120 ? lowered[..120].TrimEnd('-') : lowered;
            }
        }

        return $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];
    }

    /// <summary>區塊編輯器整段 JSON 的檢查（不驗證區塊結構），理由同 <c>AdminProgramsRepository.ValidateContentJson</c>。
    /// 除了語法，還要求根節點是<b>物件或陣列</b>：正式環境的 json 欄位是原生 json 型別，純量（字串、數字、
    /// true、null）會被資料庫拒絕而變成 500（docs/18 E-111），所以在這裡擋成 400。</summary>
    public static string? OptionalJson(string? content, string label, string? field = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        if (!JsonColumn.IsObjectOrArray(content))
        {
            throw new AdminValidationException($"{Spaced(label)}不是合法的區塊內容格式，請確認編輯器的輸出。", field);
        }

        return content;
    }
}
