namespace Tcrfc.Api.Common;

/// <summary>
/// 個資遮罩的唯一實作（規劃書 §6「會員個資為受限資料」「Member 主檔一律遮罩」）。K1 會員名單、K3 球衣收件資訊共用，
/// 不在各處另寫一份。遮罩是「缺少解除遮罩權限」時的回傳值，不是加密；呼叫端負責判斷要不要遮。
/// 姓名採規劃書 K5 公布稿的寫法（王○明）；Email 保留第一個字元與網域（a***@gmail.com）；電話保留前 2 碼與後 2 碼；
/// 生日與地址不保留任何可用的片段（地址只留到縣市區的前幾個字，方便客服判斷區域）。
/// </summary>
public static class PiiMasking
{
    public const string MaskedBirthOn = "****-**-**";

    public static string? MaskName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        var chars = System.Globalization.StringInfo.ParseCombiningCharacters(trimmed);
        var length = chars.Length;
        if (length == 1)
        {
            return "○";
        }

        var elements = new List<string>(length);
        for (var i = 0; i < length; i++)
        {
            var end = i + 1 < length ? chars[i + 1] : trimmed.Length;
            elements.Add(trimmed[chars[i]..end]);
        }

        if (length == 2)
        {
            return elements[0] + "○";
        }

        return elements[0] + new string('○', length - 2) + elements[^1];
    }

    public static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "***";
        }

        return email[0] + "***" + email[at..];
    }

    public static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length <= 4)
        {
            return new string('*', Math.Max(digits.Length, 3));
        }

        return digits[..2] + new string('*', digits.Length - 4) + digits[^2..];
    }

    /// <summary>載具號碼（手機條碼 /ABC+123、自然人憑證 AB12345678901234）只留前 2 碼與最後 2 碼。</summary>
    public static string? MaskCarrier(string? carrier)
    {
        if (string.IsNullOrWhiteSpace(carrier))
        {
            return null;
        }

        var t = carrier.Trim();
        return t.Length <= 4 ? "***" : t[..2] + new string('*', t.Length - 4) + t[^2..];
    }

    /// <summary>地址只留前 6 個字（通常涵蓋縣市與區），其餘以 *** 取代。</summary>
    public static string? MaskAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var trimmed = address.Trim();
        return trimmed.Length <= 6 ? "***" : trimmed[..6] + "***";
    }
}
