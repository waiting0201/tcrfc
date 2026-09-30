using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminAds;

/// <summary>呼叫者在廣告模組的細項權限（用來決定回應內容：合約金額可見與否、可用的操作按鈕）。
/// 伺服器端每個操作仍各自用授權器檢查，這裡只用於「回應長什麼樣」。</summary>
public sealed record AdCaller(bool CanUpdate, bool CanReview, bool CanPause, bool CanViewAmount, bool CanEditAmount)
{
    public static async Task<AdCaller> ResolveAsync(AdminSystemScope scope, IPermissionChecker checker, CancellationToken cancellationToken)
    {
        async Task<bool> Has(string code) => await checker.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, code, cancellationToken);
        return new AdCaller(
            await Has("ad.campaign.update"), await Has("ad.campaign.review"), await Has("ad.campaign.pause"),
            await Has("ad.contract.view"), await Has("ad.contract.update"));
    }
}
