using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Tcrfc.Api.Features.Newsletter;

/// <summary>
/// 電子報退訂連結的憑證（規劃書 §3.14「行銷性質信件須含退訂連結」）。內容是「俱樂部＋Email」，用 Data Protection 簽章加密，
/// 所以拿到憑證的人既不能偽造別人的、也看不到內容；與 <see cref="Security.PushTokenProtector"/> 同一套金鑰環
/// （持久化前提見 docs/14、E-109），purpose 不同。
/// <b>不設到期時間</b>：退訂連結失效等於讓人退不掉訂，是法遵風險；憑證只能退訂（冪等），不能做任何其他事。
/// 憑證不放進日誌與錯誤訊息。目前沒有寄信的呼叫端（EDM 寄送在外部平台，見 docs/17 §3）；
/// 日後 EDM 供應商串接時由 <see cref="Create"/> 為每位訂閱者產生專屬連結。
/// </summary>
public sealed class NewsletterUnsubscribeTokens(IDataProtectionProvider provider)
{
    private const string Purpose = "Tcrfc.Newsletter.Unsubscribe.v1";

    // IDataProtector.Protect(string) 的輸出已是 URL 安全的 Base64（WebEncoders），可直接放進連結。
    public string Create(Guid clubId, string email)
        => provider.CreateProtector(Purpose).Protect($"{clubId:N}|{email.Trim().ToLowerInvariant()}");

    /// <summary>驗證並取出（俱樂部, Email）；憑證被竄改、金鑰環遺失或格式不對一律回 <c>null</c>，不外洩原因。</summary>
    public (Guid ClubId, string Email)? TryParse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 1024)
        {
            return null;
        }

        try
        {
            var plain = provider.CreateProtector(Purpose).Unprotect(token.Trim());
            var parts = plain.Split('|', 2);
            return parts.Length == 2 && Guid.TryParseExact(parts[0], "N", out var clubId) && parts[1].Length > 0
                ? (clubId, parts[1])
                : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
