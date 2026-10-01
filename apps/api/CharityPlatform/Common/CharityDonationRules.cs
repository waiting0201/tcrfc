using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Tcrfc.Api.CharityPlatform.Common;

/// <summary>捐款單狀態（<c>donations.status</c>，規劃書 §4.1 狀態機）。</summary>
public static class DonationStatus
{
    public const string Created = "created";
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Failed = "failed";
    public const string Expired = "expired";
    public const string Refunded = "refunded";
}

/// <summary>金流交易狀態（<c>donation_payments.status</c>，docs/16 §10 已定案四值）。</summary>
public static class PaymentStatus
{
    public const string Requested = "requested";
    public const string Confirmed = "confirmed";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class InvoiceModes
{
    public const string B2cInvoice = "b2c_invoice";
    public const string DonationReceipt = "donation_receipt";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { B2cInvoice, DonationReceipt };
}

/// <summary>發票載具／憑證類型（B2C 三選一，規劃書 §5.2；<c>donation_invoices.carrier_type</c>）。</summary>
public static class CarrierTypes
{
    public const string MobileCarrier = "mobile_carrier";
    public const string LoveCode = "love_code";
    public const string TaxId = "tax_id";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { MobileCarrier, LoveCode, TaxId };

    /// <summary>
    /// 種子資料（CH-1b）把 <c>carrier_type</c> 存成中文標籤；本 API 寫入一律用上面的代碼。讀取端遇到舊的中文標籤
    /// 轉回代碼，不讓兩種寫法的資料在畫面上變成兩種東西。
    /// </summary>
    public static string? Normalize(string? stored) => stored switch
    {
        null or "" => null,
        "手機條碼載具" => MobileCarrier,
        "捐贈發票" => LoveCode,
        "統一編號" => TaxId,
        _ => stored,
    };
}

/// <summary>
/// 捐款的業務規則（純函式，無 I/O，可單元測試）。分潤、單號、憑證欄位格式都集中在這裡。
/// </summary>
public static partial class CharityDonationRules
{
    /// <summary>
    /// 分潤計算（規劃書 §8.1／§8.3）：兩筆分潤各自以捐款金額（毛額）× 百分比，<b>無條件捨去至整數元</b>，
    /// 捨去的尾差併入協會留存；三者相加必等於 <paramref name="amount"/>（資料庫 CHECK 約束也要求）。
    /// 約束 <c>storePct + projectPct &lt;= 100</c> 由呼叫端保證（N1／N2 儲存時驗證），不成立時丟 <see cref="ArgumentOutOfRangeException"/>
    /// ——寧可讓呼叫端炸掉，也不要默默算出負的協會留存。
    /// </summary>
    public static (int StoreAmount, int ProjectAmount, int AssociationAmount) ComputeSplit(int amount, decimal storePct, decimal projectPct)
    {
        if (storePct < 0 || projectPct < 0 || storePct + projectPct > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(storePct), "店家分潤與項目分潤不得為負，且合計不得超過 100%。");
        }

        var store = (int)Math.Floor(amount * storePct / 100m);
        var project = (int)Math.Floor(amount * projectPct / 100m);
        return (store, project, amount - store - project);
    }

    private const string Base32Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford，去掉易混淆的 I L O U

    /// <summary>
    /// 由「冪等鍵」決定性地推導單號：<c>CH</c> ＋ HMAC-SHA256(秘密, "order|" ＋ 冪等鍵) 前 80 bits 的 Crockford Base32（16 碼）。
    /// 🔴 為什麼這樣做：資料庫沒有 <c>idempotency_key</c> 欄位（規劃書與 docs/16 都沒有，不自己發明欄位），而「同一次送出重複請求
    /// 不得建兩張單」必須在資料庫層有原子保證——單號有唯一鍵（<c>UQ_donations_order_no</c>），所以讓單號本身成為冪等鍵的函式：
    /// 同一個冪等鍵永遠算出同一個單號，並發的兩個請求會撞唯一鍵，輸的那個回頭讀贏的那張單。
    /// 單號同時是結果頁網址的一部分（<c>/result/&lt;order_no&gt;</c>，知道單號等於可以查詢該筆結果），所以必須不可猜測：
    /// 沒有伺服器端秘密就無法由冪等鍵或其他單號算出單號（80 bits）。
    /// </summary>
    public static string DeriveOrderNo(string secret, string idempotencyKey)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes("order|" + idempotencyKey));
        var sb = new StringBuilder("CH", 18);
        // 取前 10 bytes = 80 bits = 16 個 5-bit 字元
        ulong buffer = 0;
        var bits = 0;
        for (var i = 0; i < 10; i++)
        {
            buffer = (buffer << 8) | hash[i];
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                sb.Append(Base32Alphabet[(int)((buffer >> bits) & 0x1F)]);
            }
        }

        return sb.ToString();
    }

    [GeneratedRegex(@"^[A-Za-z0-9_\-]{16,64}$")]
    private static partial Regex IdempotencyKeyFormat();

    /// <summary>冪等鍵格式：16–64 個英數、底線、連字號（前端用 UUID 即可）。太短容易碰撞、太長沒有意義。</summary>
    public static bool IsValidIdempotencyKey(string? key) => key is not null && IdempotencyKeyFormat().IsMatch(key);

    [GeneratedRegex(@"^/[0-9A-Z.+\-]{7}$")]
    private static partial Regex MobileCarrierFormat();

    /// <summary>手機條碼載具：<c>/</c> ＋ 7 碼英數（規劃書 §5.2）。</summary>
    public static bool IsValidMobileCarrier(string? value) => value is not null && MobileCarrierFormat().IsMatch(value);

    [GeneratedRegex(@"^[0-9]{3,7}$")]
    private static partial Regex LoveCodeFormat();

    /// <summary>捐贈碼：3–7 位數字碼（規劃書 §5.2「數字碼」，財政部愛心碼為 3–7 碼）。</summary>
    public static bool IsValidLoveCode(string? value) => value is not null && LoveCodeFormat().IsMatch(value);

    /// <summary>統一編號：8 碼數字並通過檢核碼（規劃書 §5.2「須做檢核碼驗證」）。權重 1,2,1,2,1,2,4,1；第 7 碼為 7 時另有 +1 的例外。</summary>
    public static bool IsValidTaxId(string? value)
    {
        if (value is null || value.Length != 8 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        ReadOnlySpan<int> weights = [1, 2, 1, 2, 1, 2, 4, 1];
        var sum = 0;
        for (var i = 0; i < 8; i++)
        {
            var product = (value[i] - '0') * weights[i];
            sum += product / 10 + product % 10;
        }

        return sum % 10 == 0 || (value[6] == '7' && (sum + 1) % 10 == 0);
    }

    private static readonly Dictionary<char, int> NationalIdLetterValues = new()
    {
        ['A'] = 10, ['B'] = 11, ['C'] = 12, ['D'] = 13, ['E'] = 14, ['F'] = 15, ['G'] = 16, ['H'] = 17, ['I'] = 34,
        ['J'] = 18, ['K'] = 19, ['L'] = 20, ['M'] = 21, ['N'] = 22, ['O'] = 35, ['P'] = 23, ['Q'] = 24, ['R'] = 25,
        ['S'] = 26, ['T'] = 27, ['U'] = 28, ['V'] = 29, ['W'] = 32, ['X'] = 30, ['Y'] = 31, ['Z'] = 33,
    };

    /// <summary>身分證字號（含檢核碼）：1 個大寫英文字母 ＋ 性別碼（1、2，新式居留證 8、9）＋ 8 碼數字。
    /// 舊式外來人口統一證號（兩個英文字母）採另一套演算法，不在範圍——該欄位為選填，不支援者留空即可。</summary>
    public static bool IsValidNationalId(string? value)
    {
        if (value is null || value.Length != 10 || !NationalIdLetterValues.TryGetValue(value[0], out var letter)
            || value[1] is not ('1' or '2' or '8' or '9') || !value.Skip(1).All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = letter / 10 + (letter % 10) * 9;
        for (var i = 1; i <= 8; i++)
        {
            sum += (value[i] - '0') * (9 - i);
        }

        sum += value[9] - '0';
        return sum % 10 == 0;
    }

    /// <summary>
    /// 以「當期」判斷憑證是作廢還是折讓（規劃書 §5.4「當期內作廢，跨期則開立折讓」）。台灣電子發票的一期是
    /// 單數月起的兩個月（1–2、3–4……），以<b>台灣時間</b>的年月計算。
    /// </summary>
    public static bool IsSameInvoicePeriod(DateTime issuedAtUtc, DateTime nowUtc)
    {
        var a = issuedAtUtc.AddHours(8);
        var b = nowUtc.AddHours(8);
        return a.Year == b.Year && (a.Month + 1) / 2 == (b.Month + 1) / 2;
    }
}
