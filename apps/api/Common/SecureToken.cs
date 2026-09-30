using System.Security.Cryptography;

namespace Tcrfc.Api.Common;

/// <summary>會員卡 QR 等對外憑證用的隨機字串（規劃書 §4.11「不得由會員編號推導」）：32 位元組密碼學亂數，
/// URL 安全的 Base64（43 字元，符合 <c>member_cards.token</c> 的 nvarchar(64)）。</summary>
public static class SecureToken
{
    public static string Generate()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
