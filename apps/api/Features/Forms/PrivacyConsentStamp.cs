using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.SiteSettings;

namespace Tcrfc.Api.Features.Forms;

/// <summary>
/// 隱私同意留存（主站規劃書 v3.25 §3.5／§3.10／§5.1 <c>Registration</c>、<c>Enquiry</c>；App 規劃書 v3.18 §3.9）。
///
/// 帶隱私同意勾選的送出（課程與營隊報名、試訓報名、七類表單、提案下載），伺服器在<b>送出當下</b>以 UTC 現在時間，
/// 與「該俱樂部 <c>settings</c> 中的隱私權政策版本編號」（<see cref="SiteSettingKeys.LegalPrivacyPolicyVersion"/>，
/// 後台 I3 全域設定維護）寫進 <c>privacy_consented_at</c>／<c>privacy_policy_version</c>。
/// 🔴 <b>兩個值都不信任客戶端</b>：請求只帶「有沒有勾」，時間與版本一律由這裡決定；客戶端若多傳同名欄位會被忽略（DTO 沒有這兩個屬性）。
/// 🔴 勾選是先決條件：沒勾（false）由各送出路徑自己擋下（400），不會走到寫入。
/// 未設定版本編號時用 <see cref="DefaultVersion"/>，讓全新環境也有可追溯的值。
/// </summary>
public static class PrivacyConsentStamp
{
    /// <summary>該俱樂部尚未在全域設定填版本編號時的固定預設值。</summary>
    public const string DefaultVersion = "1.0";

    /// <summary>與 <c>registrations.privacy_policy_version</c>／<c>enquiries.privacy_policy_version</c> 的欄位長度一致（nvarchar(50)）。</summary>
    public const int MaxVersionLength = 50;

    public const string NotConsentedMessage = "請先閱讀並勾選同意隱私權政策，才能送出。";

    /// <summary>空白或全空白視為未設定，回傳預設版本；有值則去頭尾空白。</summary>
    public static string Normalize(string? configured)
    {
        var trimmed = configured?.Trim();
        return string.IsNullOrEmpty(trimmed) ? DefaultVersion : trimmed.Length > MaxVersionLength ? trimmed[..MaxVersionLength] : trimmed;
    }

    public static async Task<string> LoadVersionAsync(
        IDbConnection connection, IDbTransaction? transaction, Guid clubId, CancellationToken cancellationToken)
    {
        var configured = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT setting_value FROM settings WHERE club_id = @ClubId AND setting_key = @Key",
            new { ClubId = clubId, Key = SiteSettingKeys.LegalPrivacyPolicyVersion }, transaction, cancellationToken: cancellationToken));
        return Normalize(configured);
    }

    public static async Task<string> LoadVersionAsync(ClubDbContext db, Guid clubId, CancellationToken cancellationToken)
    {
        var configured = await db.Settings.AsNoTracking()
            .Where(s => s.ClubId == clubId && s.SettingKey == SiteSettingKeys.LegalPrivacyPolicyVersion)
            .Select(s => s.SettingValue)
            .FirstOrDefaultAsync(cancellationToken);
        return Normalize(configured);
    }
}
