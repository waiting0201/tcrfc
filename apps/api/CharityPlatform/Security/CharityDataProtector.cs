using Microsoft.AspNetCore.DataProtection;

namespace Tcrfc.Api.CharityPlatform.Security;

/// <summary>
/// 慈善庫的加密欄位（規劃書 §5.2／§11.1「身分證字號必須加密儲存」、docs/16 §4.4）：
/// <c>donation_invoices.national_id_encrypted</c>、<c>carrier_id_encrypted</c> 與
/// <c>admin_users.two_factor_secret_encrypted</c>。用 ASP.NET Core Data Protection，
/// <b>用途字串（purpose）互相隔離</b>：身分證、載具／捐贈碼、2FA 密鑰各一個，且都與主站的
/// <c>Tcrfc.Admin.TwoFactorSecret.v1</c> 不同，所以主站的密文在這裡解不開，反之亦然。
///
/// 🔴🔴 <b>部署前置條件（比主站 2FA 嚴重，不是本程式碼能保證的事）</b>：金鑰環預設存在容器本機檔案系統。
/// 容器重建且沒有掛持久化 volume（<c>DATA_PROTECTION_KEYS_PATH</c>）時，金鑰環重新產生，
/// <b>所有已加密的身分證字號永久無法解密</b>——主站 2FA 密鑰遺失可以重新設定，捐款人身分證字號
/// 遺失則無法補回（捐款人不登入，沒有管道要求重填）。正式環境應改接 Azure Key Vault 保護的金鑰環，
/// 見 docs/17 §5 與 apps/api/README.md「慈善 CH-2／CH-3」。
/// </summary>
public sealed class CharityDataProtector(IDataProtectionProvider provider)
{
    private const string NationalIdPurpose = "Tcrfc.Charity.NationalId.v1";
    private const string CarrierPurpose = "Tcrfc.Charity.CarrierId.v1";
    private const string TwoFactorPurpose = "Tcrfc.Charity.TwoFactorSecret.v1";
    private const string ChannelCredentialPurpose = "Tcrfc.Charity.PaymentChannelCredential.v1";

    public string EncryptNationalId(string plain) => provider.CreateProtector(NationalIdPurpose).Protect(plain);

    public string? TryDecryptNationalId(string? cipher) => TryUnprotect(NationalIdPurpose, cipher);

    public string EncryptCarrierId(string plain) => provider.CreateProtector(CarrierPurpose).Protect(plain);

    public string? TryDecryptCarrierId(string? cipher) => TryUnprotect(CarrierPurpose, cipher);

    /// <summary>N7 金流／發票憑證（<c>payment_channels.credential_encrypted</c>）。後台永遠不回傳明文，只有串接實作在呼叫金流時解密。</summary>
    public string EncryptChannelCredential(string plain) => provider.CreateProtector(ChannelCredentialPurpose).Protect(plain);

    public string? TryDecryptChannelCredential(string? cipher) => TryUnprotect(ChannelCredentialPurpose, cipher);

    public string EncryptTwoFactorSecret(byte[] secret)
        => provider.CreateProtector(TwoFactorPurpose).Protect(Convert.ToBase64String(secret));

    public byte[] DecryptTwoFactorSecret(string encrypted)
        => Convert.FromBase64String(provider.CreateProtector(TwoFactorPurpose).Unprotect(encrypted));

    /// <summary>解不開（金鑰環遺失、種子占位字串）回傳 <c>null</c>，由呼叫端決定怎麼顯示，不丟例外讓整個列表 500。</summary>
    private string? TryUnprotect(string purpose, string? cipher)
    {
        if (string.IsNullOrEmpty(cipher))
        {
            return null;
        }

        try
        {
            return provider.CreateProtector(purpose).Unprotect(cipher);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    /// <summary>身分證字號遮罩：保留首字母與末 3 碼（<c>A1*****678</c>）。長度不足 5 一律全遮。</summary>
    public static string MaskNationalId(string? plain)
    {
        if (string.IsNullOrEmpty(plain) || plain.Length < 5)
        {
            return "***";
        }

        return plain[..1] + new string('*', plain.Length - 4) + plain[^3..];
    }
}
