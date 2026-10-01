using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Tcrfc.Api.Features.MemberAuth;

/// <summary>
/// 簽章式、有時效的一次性權杖（Data Protection <c>ITimeLimitedDataProtector</c>）：Email 驗證、密碼重設、LINE 授權的 state 與註冊票據。
/// <b>不建資料表</b>——權杖本身就帶著要驗證的內容與到期時間，伺服器端不必記任何狀態（docs/12 §12 第 47 點）。
/// 每種用途一個獨立的 purpose 字串，<b>跨用途不能互換</b>（拿驗證信權杖去重設密碼會解不開）。
/// 「用過即失效」的做法：重設密碼的權杖內含目前密碼雜湊的指紋，密碼一改指紋就變、舊連結立刻失效；
/// Email 驗證本身是冪等的（重複點擊結果相同）。
/// ⚠️ 金鑰環要持久化（<c>DATA_PROTECTION_KEYS_PATH</c>，docs/17）：金鑰環遺失只會讓「尚未使用的連結」失效，使用者重寄一次即可，不是資料遺失。
/// </summary>
public sealed class MemberSecureTokens(IDataProtectionProvider provider)
{
    public const string PurposeEmailVerify = "tcrfc.member.email-verify.v1";
    public const string PurposePasswordReset = "tcrfc.member.password-reset.v1";
    public const string PurposeLineState = "tcrfc.member.line-state.v1";
    public const string PurposeLineTicket = "tcrfc.member.line-ticket.v1";
    public const string PurposeLineUserId = "tcrfc.member.line-user-id.v1";

    public static readonly TimeSpan EmailVerifyLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan PasswordResetLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan LineStateLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan LineTicketLifetime = TimeSpan.FromMinutes(15);

    public string Protect<T>(string purpose, T payload, TimeSpan lifetime)
        => provider.CreateProtector(purpose).ToTimeLimitedDataProtector()
            .Protect(JsonSerializer.Serialize(payload), lifetime);

    /// <summary>解不開、過期、被竄改、用途不符一律回 null（不分原因，避免當成探測工具）。</summary>
    public T? TryUnprotect<T>(string purpose, string? token) where T : class
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096)
        {
            return null;
        }

        try
        {
            var json = provider.CreateProtector(purpose).ToTimeLimitedDataProtector().Unprotect(token);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null;
        }
    }

    /// <summary>LINE userId 的加密（<c>members.line_user_id_encrypted</c>，不得匯出）。</summary>
    public string ProtectLineUserId(string lineUserId) => provider.CreateProtector(PurposeLineUserId).Protect(lineUserId);

    /// <summary>LINE userId 的查找雜湊（<c>members.line_user_id_hash</c>，SHA-256 小寫十六進位）。</summary>
    public static string HashLineUserId(string lineUserId)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lineUserId))).ToLowerInvariant();

    /// <summary>密碼雜湊的指紋（放進重設密碼權杖；密碼一改就不同）。只取前 16 碼，不洩漏雜湊本身。</summary>
    public static string PasswordFingerprint(string passwordHash)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)))[..16].ToLowerInvariant();

    public sealed record EmailVerifyPayload(Guid MemberId, string Email, string Club);

    public sealed record PasswordResetPayload(Guid MemberId, string Fingerprint);

    /// <summary>LINE 授權 state：<c>Mode</c>＝login／bind；<c>Nonce</c> 會原樣送給 LINE、回來的 id_token 必須帶同一個值；<c>MemberId</c> 只有 bind 才有。</summary>
    public sealed record LineStatePayload(string Mode, string Club, string Nonce, string RedirectUri, Guid? MemberId);

    /// <summary>LINE 登入找不到會員時的註冊票據（15 分鐘）：帶著已驗證過的 LINE 身分，讓使用者補 Email 後完成註冊。</summary>
    public sealed record LineTicketPayload(string LineUserId, string? DisplayName, string? Email, string Club);
}

/// <summary>會員密碼規則（規劃書沒有寫密碼強度，執行層決定：8–128 字元、至少一個英文字母與一個數字、不得是常見弱密碼）。</summary>
public static class MemberPasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "12345678", "123456789", "1234567890", "password", "password1", "password123", "qwerty123", "11111111", "abc12345", "a1234567", "iloveyou1",
    };

    /// <summary>回傳錯誤訊息（日常中文）；合格回 null。</summary>
    public static string? Validate(string? password, string? email = null)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return $"密碼至少需要 {MinLength} 個字元。";
        }

        if (password.Length > MaxLength)
        {
            return $"密碼不可超過 {MaxLength} 個字元。";
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return "密碼需要同時包含英文字母與數字。";
        }

        if (Common.Contains(password) || (email is not null && string.Equals(password, email, StringComparison.OrdinalIgnoreCase)))
        {
            return "這個密碼太容易被猜到，請換一個。";
        }

        return null;
    }
}
