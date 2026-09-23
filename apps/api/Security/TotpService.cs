using System.Security.Cryptography;

namespace Tcrfc.Api.Security;

/// <summary>
/// TOTP（RFC 6238，以 HMAC-SHA1 為基礎，是 Google Authenticator／Microsoft Authenticator／
/// 1Password 等主流驗證器 App 共同支援的演算法）——落地 docs/12b-database-tables.md §7.6
/// 「§8 非功能性需求明訂後台強制 2FA」與 admin_users 既有的
/// <c>two_factor_secret_encrypted</c>／<c>two_factor_confirmed_at</c> 欄位。
///
/// 手刻而不是引套件：RFC 6238／4226 演算法本身只有一頁 HMAC＋動態截斷，
/// .NET 內建 <see cref="HMACSHA1"/> 就夠，不需要為了一個常見演算法多帶一個依賴。
/// Base32（RFC 4648）編解碼同理——驗證器 App 的「輸入設定金鑰」欄位吃的是 Base32 字串，
/// 不是 Base64，兩者不能互換。
/// </summary>
public static class TotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const int SecretBytes = 20; // 160 bits，RFC 4226 建議的 HMAC-SHA1 金鑰長度
    private static readonly char[] Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    /// <summary>產生一組新的隨機密鑰（原始位元組，呼叫端負責用 Data Protection 加密後存入
    /// <c>two_factor_secret_encrypted</c>，本服務不碰儲存）。</summary>
    public static byte[] GenerateSecret() => RandomNumberGenerator.GetBytes(SecretBytes);

    /// <summary>Base32 字串還原回原始位元組——測試與（未來若有）匯入既有金鑰的情境會用到，
    /// 一般 App 端使用者流程只會用到 <see cref="ToBase32"/> 那個方向。</summary>
    public static byte[] FromBase32(string base32)
    {
        var bits = new List<bool>();
        foreach (var c in base32.TrimEnd('=').ToUpperInvariant())
        {
            var index = Array.IndexOf(Base32Alphabet, c);
            if (index < 0)
            {
                throw new FormatException($"不是合法的 Base32 字元：{c}");
            }
            for (var i = 4; i >= 0; i--)
            {
                bits.Add(((index >> i) & 1) == 1);
            }
        }

        var bytes = new List<byte>();
        for (var i = 0; i + 8 <= bits.Count; i += 8)
        {
            byte b = 0;
            for (var j = 0; j < 8; j++)
            {
                b = (byte)((b << 1) | (bits[i + j] ? 1 : 0));
            }
            bytes.Add(b);
        }
        return [.. bytes];
    }

    /// <summary>目前時間視窗的驗證碼。⚠️ 僅供測試（模擬驗證器 App 的行為）使用，
    /// 正式流程一律是「使用者從自己的驗證器 App 讀碼」，伺服器端不會有理由主動算出
    /// 「現在該輸入什麼」並拿去做任何非測試用途——那會讓 2FA 形同虛設。</summary>
    public static string GenerateCurrentCodeForTesting(byte[] secret)
        => ComputeCode(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds);

    /// <summary>供驗證器 App 掃碼／手動輸入用的 Base32 字串。</summary>
    public static string ToBase32(byte[] secret)
    {
        var result = new System.Text.StringBuilder((secret.Length * 8 + 4) / 5);
        int buffer = 0, bitsLeft = 0;
        foreach (var b in secret)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }
        if (bitsLeft > 0)
        {
            result.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }
        return result.ToString();
    }

    /// <summary>
    /// 驗證使用者輸入的 6 碼是否與目前時間視窗吻合。
    /// <paramref name="allowedDriftSteps"/> 允許前後各 N 個 30 秒視窗的時鐘漂移
    /// （預設 1，即前後各 30 秒，共 90 秒視窗）——這是可用性與安全性的取捨，
    /// 業界慣例（Google Authenticator 的實作行為）也是允許 ±1 個視窗。
    /// </summary>
    public static bool Verify(byte[] secret, string code, int allowedDriftSteps = 1)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != Digits || !code.All(char.IsDigit))
        {
            return false;
        }

        var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
        for (var drift = -allowedDriftSteps; drift <= allowedDriftSteps; drift++)
        {
            var candidate = ComputeCode(secret, currentStep + drift);
            // 固定時間比較，避免時序攻擊透露「前幾碼已經對了」。
            if (System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(candidate),
                    System.Text.Encoding.ASCII.GetBytes(code)))
            {
                return true;
            }
        }
        return false;
    }

    private static string ComputeCode(byte[] secret, long timeStep)
    {
        var counter = new byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(timeStep & 0xFF);
            timeStep >>= 8;
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counter);

        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
                        | ((hash[offset + 1] & 0xFF) << 16)
                        | ((hash[offset + 2] & 0xFF) << 8)
                        | (hash[offset + 3] & 0xFF);

        var otp = binaryCode % (int)Math.Pow(10, Digits);
        return otp.ToString(new string('0', Digits));
    }
}
