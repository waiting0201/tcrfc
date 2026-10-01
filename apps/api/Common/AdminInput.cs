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

    public static string RequireText(string? value, string label, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new AdminValidationException($"{label}為必填欄位。");
        }

        if (trimmed.Length > maxLength)
        {
            throw new AdminValidationException($"{label}不可超過 {maxLength} 個字。");
        }

        return trimmed;
    }

    /// <summary>選填文字：空白視為沒有（回傳 <c>null</c>）。</summary>
    public static string? OptionalText(string? value, string label, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new AdminValidationException($"{label}不可超過 {maxLength} 個字。");
        }

        return trimmed;
    }

    public static string? OptionalHttpUrl(string? value, string label, int maxLength = 500)
    {
        var text = OptionalText(value, label, maxLength);
        if (text is null)
        {
            return null;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new AdminValidationException($"{label}必須是以 http:// 或 https:// 開頭的完整網址。");
        }

        return text;
    }

    public static string? OptionalEmail(string? value, string label)
    {
        var text = OptionalText(value, label, 255);
        if (text is not null && !EmailFormat().IsMatch(text))
        {
            throw new AdminValidationException($"{label}的格式不正確。");
        }

        return text;
    }

    public static void DateRange(DateOnly? start, DateOnly? end, string label)
    {
        if (start is not null && end is not null && end < start)
        {
            throw new AdminValidationException($"{label}的結束日期不可早於開始日期。");
        }
    }

    public static int? OptionalNonNegative(int? value, string label)
    {
        if (value is < 0)
        {
            throw new AdminValidationException($"{label}不可為負數。");
        }

        return value;
    }

    public static string OneOf(string? value, IReadOnlySet<string> allowed, string label, string allowedText)
    {
        if (value is null || !allowed.Contains(value))
        {
            throw new AdminValidationException($"{label}只能是{allowedText}。");
        }

        return value;
    }

    /// <summary>網址名稱：小寫英文字母、數字與連字號，比照 <c>FaqSlugPolicy</c> 的格式規則。</summary>
    public static string Slug(string slug, string label = "網址名稱")
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new AdminValidationException($"{label}為必填欄位。");
        }

        if (slug.Length > 160 || !SlugFormat().IsMatch(slug))
        {
            throw new AdminValidationException(
                $"{label}「{slug}」格式不正確：只能使用小寫英文字母、數字與連字號（-），" +
                "開頭與結尾不能是連字號，也不能連續兩個連字號，長度不可超過 160 字。");
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
    public static string? OptionalJson(string? content, string label)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        if (!JsonColumn.IsObjectOrArray(content))
        {
            throw new AdminValidationException($"{label}不是合法的區塊內容格式，請確認編輯器的輸出。");
        }

        return content;
    }
}
