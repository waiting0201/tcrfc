using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Tcrfc.Api.Security;

/// <summary>
/// 推播權杖加密儲存（App 規劃書 §12.1：推播權杖視同個資，加密儲存、不對外顯示）。與 <see cref="TwoFactorSecretProtector"/>
/// 同一套 Data Protection API，purpose 不同（一把鑰匙解不開另一種資料）。金鑰環持久化的部署前置條件同該類別檔頭。
/// <see cref="Hash"/> 另存 SHA-256（小寫十六進位）供去重與失效清理比對，不可逆。
/// </summary>
public sealed class PushTokenProtector(IDataProtectionProvider provider)
{
    private const string Purpose = "Tcrfc.App.PushToken.v1";

    public string Encrypt(string token) => provider.CreateProtector(Purpose).Protect(token);

    public string? TryDecrypt(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted))
        {
            return null;
        }

        try
        {
            return provider.CreateProtector(Purpose).Unprotect(encrypted);
        }
        catch (CryptographicException)
        {
            return null; // 金鑰環遺失時解不開，視為沒有可用權杖，不外洩例外細節
        }
    }

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
