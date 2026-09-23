using Microsoft.AspNetCore.DataProtection;

namespace Tcrfc.Api.Security;

/// <summary>
/// 加密／解密 <c>admin_users.two_factor_secret_encrypted</c>。用 ASP.NET Core 內建的
/// Data Protection API，不是自己刻 AES——金鑰輪替、版本相容都是框架處理，且不需要
/// 額外套件（已經是 ASP.NET Core 的一部分）。
///
/// 🔴🔴🔴 **正式環境部署前置條件（不是本次程式碼能保證的事，寫在這裡提醒下一位接手者）**：
/// Data Protection 預設把金鑰環存在行程所在容器的本機檔案系統（Linux 容器內是
/// <c>~/.aspnet/DataProtection-Keys</c>）。單一 VM 的 Docker 部署（docs/17-deployment.md）
/// 若容器重建（不是重啟，是整個容器被換掉，例如部署新版映像檔）沒有把這個目錄掛到持久化
/// volume，金鑰環會重新產生，**所有既有使用者的 2FA 密鑰會變成永久無法解密**——不是「登入失敗」
/// 這種可恢復的錯誤，是資料實質遺失，使用者必須整個重新走一次 2FA 設定流程。
/// **部署時務必設定 <c>DATA_PROTECTION_KEYS_PATH</c> 指向一個持久化 volume 路徑**
/// （見 Program.cs 的註冊程式碼），本機開發沒有這個環境變數時退回行程預設位置（僅供本機測試，
/// 每次容器重建 2FA 都要重設，這是刻意的本機限制不是 bug）。
/// </summary>
public sealed class TwoFactorSecretProtector(IDataProtectionProvider provider)
{
    private const string Purpose = "Tcrfc.Admin.TwoFactorSecret.v1";

    public string Encrypt(byte[] secret)
        => provider.CreateProtector(Purpose).Protect(Convert.ToBase64String(secret));

    public byte[] Decrypt(string encrypted)
        => Convert.FromBase64String(provider.CreateProtector(Purpose).Unprotect(encrypted));
}
