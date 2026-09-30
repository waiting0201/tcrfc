namespace Tcrfc.Api.Features.AppPublic;

public sealed record AppConfigPublishResult(bool Published, string Message);

/// <summary>
/// 設定下發第 1 層來源的接縫（docs/19 §7）：後台 M1／M5 存檔後，把 <see cref="AppConfigDocument"/> write-through 推到 Cloudflare 上的靜態 JSON
/// （Workers KV／R2），讓 API 整台掛掉時 App 仍讀得到「維護中」與最低支援版本。🔴 <b>本期不串接</b>（Cloudflare 端的 KV／R2 尚未建立）——
/// 預設註冊 <see cref="NotConfiguredAppConfigPublisher"/>，存檔照常成功，只是回應裡的 <c>edgePublish</c> 會說明「尚未推送到邊緣」。
/// 日後只需換這個實作與 <c>Program.cs</c> 的註冊。實作必須 fail-open：推送失敗不得讓後台存檔失敗（資料庫才是真實來源），只回報結果。
/// </summary>
public interface IAppConfigPublisher
{
    /// <summary>是否已串接邊緣靜態設定（連線檢查用，不會真的推送）。</summary>
    bool IsConfigured { get; }

    Task<AppConfigPublishResult> PublishAsync(AppConfigDocument document, CancellationToken cancellationToken);
}

public sealed class NotConfiguredAppConfigPublisher : IAppConfigPublisher
{
    public bool IsConfigured => false;

    public Task<AppConfigPublishResult> PublishAsync(AppConfigDocument document, CancellationToken cancellationToken)
        => Task.FromResult(new AppConfigPublishResult(false, "設定已儲存，但尚未串接 Cloudflare 靜態設定（邊緣備援來源）；目前 App 只能從 API 讀取這份設定。"));
}
