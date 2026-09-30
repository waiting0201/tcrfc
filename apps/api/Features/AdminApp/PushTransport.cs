namespace Tcrfc.Api.Features.AdminApp;

/// <summary>要送出的內容。🔴 payload 不放任何個資（會經過 Apple 與 Google，docs/19 §5）：只有標題、內文、圖片網址、深連結與批次識別。不使用靜默推播。</summary>
public sealed record PushPayload(Guid MessageId, string Title, string Body, string? ImageUrl, string? DeepLink);

/// <summary>一個送達對象。<c>Token</c> 是解密後的推播權杖，只存在於記憶體、不寫入日誌。</summary>
public sealed record PushTarget(Guid DeviceId, string Platform, string Token);

public enum PushSendOutcome
{
    /// <summary>推播服務接受（APNs／FCM 回 2xx）。</summary>
    Accepted,

    /// <summary>權杖失效（APNs 410 Unregistered、FCM UNREGISTERED／INVALID_ARGUMENT）——呼叫端把裝置權杖標記失效。</summary>
    InvalidToken,

    /// <summary>暫時性失敗（APNs 429、FCM RESOURCE_EXHAUSTED、網路錯誤）——<b>不標記失效</b>。</summary>
    RetryLater,

    Failed,

    /// <summary>推播傳輸尚未串接（APNs／FCM 金鑰尚未建立）。</summary>
    NotConfigured,
}

public sealed record PushSendResult(PushSendOutcome Outcome, string? Detail = null);

/// <summary>
/// 推播傳輸接縫（docs/19 §5：自家 .NET 直送 APNs HTTP/2 <c>.p8</c> token 認證與 FCM HTTP v1，不接推播平台、不使用 FCM topic）。
/// 🔴 <b>本期不串接</b>（APNs／FCM 金鑰尚未建立，App 也尚未開發）——預設註冊 <see cref="NotConfiguredPushTransport"/>，任何送出都回
/// <see cref="PushSendOutcome.NotConfigured"/>；排程、分眾、覆核、紀錄與統計等後台邏輯照常運作，批次會停在「失敗（尚未串接）」並保留，
/// 串接後按「重送」即可。實作必須自行處理 429／5xx 的指數退避，並且<b>永遠不得把權杖或內容寫進日誌</b>。
/// 使用推播＝權杖與訊息內容經境外處理，會員條款完成推播蒐集告知（§16.2 第 12 項）前不得啟用（§6.7）。
/// </summary>
public interface IPushTransport
{
    /// <summary>是否已串接（連線檢查用，不會真的送出任何東西）。</summary>
    bool IsConfigured { get; }

    Task<PushSendResult> SendAsync(PushTarget target, PushPayload payload, CancellationToken cancellationToken);
}

public sealed class NotConfiguredPushTransport : IPushTransport
{
    public bool IsConfigured => false;

    public Task<PushSendResult> SendAsync(PushTarget target, PushPayload payload, CancellationToken cancellationToken)
        => Task.FromResult(new PushSendResult(PushSendOutcome.NotConfigured, "推播服務尚未串接（APNs 與 FCM 金鑰尚未建立）。"));
}
