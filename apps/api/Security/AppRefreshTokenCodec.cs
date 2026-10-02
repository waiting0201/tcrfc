using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Tcrfc.Api.Security;

/// <summary>
/// App 更新權杖的字串格式與簽章（AP-3，2026-10-02；docs/19 §4、App 規劃書 §4.3／§10.1）。
///
/// 格式：<c>ad1.{裝置列 id（32 位十六進位）}.{簽發時間（Unix 毫秒）}.{32 bytes 亂數（Base64Url）}.{簽章}</c>。
/// 伺服器只存整串的 SHA-256 雜湊（<c>app_devices.refresh_token_hash</c>），原值只在簽發當下出現一次。
///
/// ── 為什麼權杖要帶「裝置 id＋簽發時間＋簽章」──
/// 規劃書 §10.1 只給 <c>app_devices</c> 四個欄位（雜湊、到期、上次輪替、撤銷），<b>沒有「前一把權杖的雜湊」</b>。
/// 輪替後舊權杖再被送來時，只靠「雜湊不相符」分辨不出它是「曾經合法、現已被輪替掉」（＝外洩，應撤銷整條鏈）
/// 還是「亂猜的垃圾字串」（不該害合法使用者被登出）。做法：權杖內嵌簽發時間並以伺服器金鑰簽章——
/// ① 簽章驗不過 → 一律當垃圾，<b>無副作用</b>（攻擊者不可能靠亂送權杖登出別人）；
/// ② 簽章通過但雜湊不是現行那把、且簽發時間早於 <c>refresh_token_rotated_at</c> → 這是真的曾經核發、已被輪替掉的權杖 → 重用偵測成立。
/// 不必動綱要、不必改規劃書。簽章金鑰由會員權杖金鑰以 HKDF 衍生（用途標籤不同，與存取權杖金鑰在密碼學上互相獨立）。
/// </summary>
public static class AppRefreshTokenCodec
{
    public const string Prefix = "ad1.";
    private const string DerivationLabel = "tcrfc-app-refresh-token-v1";
    private const int MaxLength = 200;

    public sealed record Parsed(Guid DeviceId, long StampMs);

    public static bool LooksLikeAppToken(string? raw) => raw is not null && raw.StartsWith(Prefix, StringComparison.Ordinal);

    public static byte[] DeriveKey(IConfiguration configuration)
        => HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm: MemberTokenService.DeriveKeyBytes(configuration), outputLength: 32,
            salt: null, info: Encoding.UTF8.GetBytes(DerivationLabel));

    public static string Create(byte[] key, Guid deviceId, long stampMs)
    {
        var body = $"{Prefix}{deviceId:N}.{stampMs}.{Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32))}";
        return $"{body}.{Sign(key, body)}";
    }

    /// <summary>格式正確且簽章通過才回傳；其餘一律 <c>null</c>（呼叫端不得因此產生任何副作用）。</summary>
    public static Parsed? TryParse(byte[] key, string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length > MaxLength || !LooksLikeAppToken(raw))
        {
            return null;
        }

        var lastDot = raw.LastIndexOf('.');
        if (lastDot <= Prefix.Length)
        {
            return null;
        }

        var body = raw[..lastDot];
        var signature = raw[(lastDot + 1)..];
        var expected = Sign(key, body);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(signature), Encoding.ASCII.GetBytes(expected)))
        {
            return null;
        }

        var parts = body[Prefix.Length..].Split('.');
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out var deviceId) || !long.TryParse(parts[1], out var stamp) || stamp <= 0)
        {
            return null;
        }

        return new Parsed(deviceId, stamp);
    }

    public static string Hash(string raw) => AdminTokenService.HashRefreshToken(raw);

    public static DateTime ToUtc(long stampMs) => DateTimeOffset.FromUnixTimeMilliseconds(stampMs).UtcDateTime;

    public static long ToStampMs(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static string Sign(byte[] key, string body)
        => Base64UrlEncoder.Encode(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(body)));
}
