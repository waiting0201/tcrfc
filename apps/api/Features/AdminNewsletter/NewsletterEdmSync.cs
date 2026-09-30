namespace Tcrfc.Api.Features.AdminNewsletter;

/// <summary>要同步給 EDM 平台的名單快照。<b>退訂名單也必須一併送出</b>（抑制清單）：平台端才不會再寄給已退訂的人。</summary>
public sealed record EdmSyncRequest(string ClubCode, IReadOnlyList<string> SubscribedEmails, IReadOnlyList<string> UnsubscribedEmails);

public sealed record EdmSyncResult(bool Configured, int SyncedCount, string Message);

/// <summary>
/// EDM 平台串接接縫（規劃書 §4.7 G3「串接 EDM 平台」）。🔴 <b>供應商尚未確定，本期不串接</b>——預設註冊
/// <see cref="NotConfiguredEdmSync"/>，同步永遠回「尚未串接」；訂閱者名單、退訂、匯出等後台邏輯照常運作。
/// 日後選定供應商後只需換掉這個實作與 <c>Program.cs</c> 的註冊。全系統沒有寄信通路，官網仍不做電子報群發
/// （規劃書 §1.3、App 規劃書 §6.1），這個接縫只負責把名單交給外部平台，寄送在平台端。
/// </summary>
public interface INewsletterEdmSync
{
    /// <summary>目前設定的供應商名稱；沒有串接時為 <c>null</c>。</summary>
    string? ProviderName { get; }

    Task<EdmSyncResult> SyncAsync(EdmSyncRequest request, CancellationToken cancellationToken);
}

public sealed class NotConfiguredEdmSync : INewsletterEdmSync
{
    public string? ProviderName => null;

    public Task<EdmSyncResult> SyncAsync(EdmSyncRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new EdmSyncResult(false, 0, "EDM 平台尚未串接（供應商尚未確定），名單目前只保存在系統內，可先匯出 CSV 手動匯入。"));
}
