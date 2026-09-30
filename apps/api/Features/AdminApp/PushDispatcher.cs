using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AppPublic;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminApp;

public sealed record PushDispatchResult(string Status, int Sent, int Delivered, int Failed, string? Message);

/// <summary>
/// 推播發送（docs/19 §5）：分眾在 .NET 端解析成裝置清單，依裝置語系選文案，逐台直送；不使用 FCM topic。
/// 一批 500 台、逐批推進游標（<c>push_messages.send_cursor</c> ＝已處理到的裝置 <c>row_seq</c>），所以中途失敗後「重送」從游標續送、不會重送已處理的裝置。
/// 統計只有三個彙總數字（送出／送達／開啟，批次 × 平台 × 語系）——不記錄任何個人層級的投遞或開啟紀錄（規劃書 §6.6）。
/// 「送達」＝推播服務接受且未回報權杖失效，<b>不等於已到達裝置</b>。權杖失效（APNs 410／FCM UNREGISTERED）標記該裝置權杖失效；暫時性失敗不標記。
/// 傳輸尚未串接時（<see cref="PushSendOutcome.NotConfigured"/>）整批停在「失敗」並保留，不動任何裝置、不動游標，串接後可重送。
/// </summary>
public sealed class PushDispatcher(
    ClubDbContext dbContext, IPushTransport transport, PushTokenProtector protector, IImagePublicUrlResolver imageUrls, ILogger<PushDispatcher> logger)
{
    public const int BatchSize = 500;
    private const int Concurrency = 16;

    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var ids = await dbContext.PushMessages.AsNoTracking().Where(m => m.Status == "scheduled" && m.ScheduledAt != null && m.ScheduledAt <= now)
            .OrderBy(m => m.ScheduledAt).Select(m => m.Id).Take(20).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            await DispatchAsync(id, cancellationToken);
        }

        return ids.Count;
    }

    public async Task<PushDispatchResult> DispatchAsync(Guid messageId, CancellationToken cancellationToken)
    {
        // 原子領取：同一批次同時只會有一個行程在送。
        var claimed = await dbContext.PushMessages.Where(m => m.Id == messageId && m.Status == "scheduled")
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, "sending").SetProperty(m => m.UpdatedAt, DateTime.UtcNow), cancellationToken);
        var message = await dbContext.PushMessages.AsNoTracking().Include(m => m.PushMessagesI18ns).FirstAsync(m => m.Id == messageId, cancellationToken);
        if (claimed == 0)
        {
            return new PushDispatchResult(message.Status, message.SentCount, message.DeliveredCount, message.FailedCount, "這個批次目前不能發送。");
        }

        var zh = message.PushMessagesI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = message.PushMessagesI18ns.FirstOrDefault(i => i.Locale == "en");
        var imageUrl = imageUrls.Resolve(message.ImageKey);
        var spec = PushAudienceSpec.From(message);
        var cursor = message.SendCursor;
        string? abortMessage = null;
        var sentTotal = 0;
        var deliveredTotal = 0;
        var failedTotal = 0;

        while (true)
        {
            var batch = await PushAudience.Devices(dbContext, spec).Where(d => d.RowSeq > cursor).OrderBy(d => d.RowSeq).Take(BatchSize)
                .Select(d => new { d.Id, d.RowSeq, d.Platform, d.Locale, d.PushTokenEncrypted }).ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            var outcomes = new (Guid Id, string Platform, string Locale, PushSendOutcome Outcome)[batch.Count];
            var gate = new SemaphoreSlim(Concurrency);
            var probe = true;
            for (var i = 0; i < batch.Count && abortMessage is null; i++)
            {
                var d = batch[i];
                var locale = d.Locale == "en" ? "en" : RequestLocale.DefaultDbLocale;
                var token = protector.TryDecrypt(d.PushTokenEncrypted);
                if (token is null)
                {
                    outcomes[i] = (d.Id, d.Platform, locale, PushSendOutcome.InvalidToken);
                    continue;
                }

                var content = locale == "en" && !string.IsNullOrWhiteSpace(en?.Title) ? en : zh;
                var payload = new PushPayload(message.Id, content?.Title ?? string.Empty, content?.Body ?? string.Empty, imageUrl, message.DeepLink);
                if (probe)
                {
                    // 先送第一台探路：傳輸尚未串接時整批立即中止，不逐台重複嘗試。
                    probe = false;
                    var first = await transport.SendAsync(new PushTarget(d.Id, d.Platform, token), payload, cancellationToken);
                    if (first.Outcome == PushSendOutcome.NotConfigured)
                    {
                        abortMessage = first.Detail ?? "推播服務尚未串接。";
                        break;
                    }

                    outcomes[i] = (d.Id, d.Platform, locale, first.Outcome);
                    continue;
                }

                await gate.WaitAsync(cancellationToken);
                var index = i;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var r = await transport.SendAsync(new PushTarget(d.Id, d.Platform, token), payload, cancellationToken);
                        outcomes[index] = (d.Id, d.Platform, locale, r.Outcome);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "推播傳輸發生未預期錯誤（批次 {MessageId}）。", messageId);
                        outcomes[index] = (d.Id, d.Platform, locale, PushSendOutcome.Failed);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, cancellationToken);
            }

            for (var i = 0; i < Concurrency; i++)
            {
                await gate.WaitAsync(cancellationToken); // 等全部送完
            }

            if (abortMessage is not null)
            {
                break;
            }

            var done = outcomes.Where(o => o.Platform is not null).ToList();
            var invalid = done.Where(o => o.Outcome == PushSendOutcome.InvalidToken).Select(o => o.Id).ToList();
            if (invalid.Count > 0)
            {
                await dbContext.AppDevices.Where(d => invalid.Contains(d.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.PushTokenStatus, "invalid").SetProperty(d => d.PushTokenEncrypted, (string?)null).SetProperty(d => d.PushTokenHash, (string?)null), cancellationToken);
            }

            var batchSent = done.Count;
            var batchDelivered = done.Count(o => o.Outcome == PushSendOutcome.Accepted);
            foreach (var g in done.GroupBy(o => (o.Platform, o.Locale)))
            {
                var s = g.Count();
                var dlv = g.Count(o => o.Outcome == PushSendOutcome.Accepted);
                var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE push_message_stats SET sent = sent + {s}, delivered = delivered + {dlv} WHERE push_message_id = {messageId} AND platform = {g.Key.Platform} AND locale = {g.Key.Locale}", cancellationToken);
                if (updated == 0)
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"INSERT INTO push_message_stats (push_message_id, platform, locale, sent, delivered, opened) VALUES ({messageId}, {g.Key.Platform}, {g.Key.Locale}, {s}, {dlv}, 0)", cancellationToken);
                }
            }

            cursor = batch.Max(d => d.RowSeq);
            var batchFailed = batchSent - batchDelivered;
            await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE push_messages SET sent_count = sent_count + {batchSent}, delivered_count = delivered_count + {batchDelivered},
  failed_count = failed_count + {batchFailed}, send_cursor = {cursor}, updated_at = SYSUTCDATETIME() WHERE id = {messageId}", cancellationToken);
            sentTotal += batchSent;
            deliveredTotal += batchDelivered;
            failedTotal += batchFailed;
        }

        // 🔴 一律用不追蹤的查詢與 set-based 更新：同一個請求裡 repository 可能已經追蹤了這個批次（核可流程），
        // 追蹤中的實體不會被重新讀取，會拿到舊的計數而算錯狀態。
        var counts = await dbContext.PushMessages.AsNoTracking().Where(m => m.Id == messageId)
            .Select(m => new { m.SentCount, m.DeliveredCount, m.FailedCount }).FirstAsync(cancellationToken);
        var now = DateTime.UtcNow;
        string status;
        string? failure;
        DateTime? sentAt = null;
        if (abortMessage is not null)
        {
            status = "failed";
            failure = abortMessage.Length > 255 ? abortMessage[..255] : abortMessage;
        }
        else
        {
            status = counts.FailedCount == 0 ? "sent" : counts.DeliveredCount > 0 ? "partial" : "failed";
            sentAt = now;
            failure = status == "sent" ? null : "部分或全部裝置沒有送出成功（權杖失效或推播服務暫時失敗）。";
        }

        await dbContext.PushMessages.Where(m => m.Id == messageId).ExecuteUpdateAsync(
            s => s.SetProperty(m => m.Status, status).SetProperty(m => m.FailureMessage, failure)
                  .SetProperty(m => m.SentAt, m => sentAt ?? m.SentAt).SetProperty(m => m.UpdatedAt, now), cancellationToken);
        return new PushDispatchResult(status, counts.SentCount, counts.DeliveredCount, counts.FailedCount, failure);
    }
}
