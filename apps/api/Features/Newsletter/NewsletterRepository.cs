using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.Newsletter;

/// <summary>
/// G-09 頁尾電子報訂閱的公開寫入（主站規劃書 §3.0 G-09、後台 G3 名單）。名單以 <c>(club_id, email)</c> 為唯一鍵，
/// 兩個俱樂部各自一份——同一個信箱可以只退訂其中一站。
///
/// ### 執行層決定（規劃書只寫「Footer 常駐，串接 EDM 平台」，沒寫確認信流程）
/// 1. <b>單一確認（勾選同意即訂閱），不寄雙重確認信。</b>規劃書沒有要求；全系統的寄信通路只承接五封會員系統信（§3.14）。
///    代價是無法證明信箱屬於填表的人——所以第 2 點。若日後要求雙重確認，需要新增「待確認」狀態值（目前資料庫只有訂閱／退訂兩態），見 README 待決。
/// 2. <b>退訂是黏著的</b>：信箱曾經退訂，公開表單再次送出<b>不會</b>改回訂閱（回應與成功相同，不透露狀態）。
///    理由與後台一致（<c>AdminNewsletterRepository.UpdateStatusAsync</c>）：退訂是法遵事實，沒有信箱驗證時，任何人都能替別人的信箱
///    重新訂閱，等於替第三人違反他的退訂意願。要重新訂閱走後台（須註明原因）。
/// 3. <b>回應不透露名單狀態</b>：新訂閱、已訂閱、已退訂三種情況的回應完全相同（避免被拿來探測某信箱是否在名單內）。
/// 4. 同意紀錄：<c>subscribed_at</c> 是勾選同意的時間，<c>source</c> 是入口標籤；後台名單與匯出看得到。
///
/// 🔴 本檔不寄信、不呼叫 EDM（EDM 同步仍是後台手動 <c>/newsletter/edm/sync</c>，介面 <c>INewsletterEdmSync</c> 不變）。
/// </summary>
public sealed partial class NewsletterRepository(ClubDbContext db, NewsletterUnsubscribeTokens tokens)
{
    public const string StatusSubscribed = "subscribed";
    public const string StatusUnsubscribed = "unsubscribed";

    private static readonly IReadOnlyDictionary<string, string> SourceLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["footer"] = "官網頁尾",
        ["home"] = "官網首頁",
        ["news"] = "官網新聞頁",
        ["app"] = "行動 App",
    };

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailFormat();

    public async Task SubscribeAsync(ClubScope scope, SubscribeNewsletterRequest request, CancellationToken cancellationToken)
    {
        // 蜜罐：有值就是機器人，靜默丟棄（回應與成功相同）。
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return;
        }

        var email = NormalizeEmail(request.Email);
        if (!request.Consent)
        {
            throw new PublicValidationException("請先勾選同意個人資料蒐集與隱私權政策，才能訂閱電子報。");
        }

        var source = ResolveSource(request.Source);

        var exists = await db.NewsletterSubscribers.AsNoTracking()
            .AnyAsync(s => s.ClubId == scope.ClubId && s.Email == email, cancellationToken);
        if (exists)
        {
            return; // 已訂閱：冪等；已退訂：黏著（見類別說明第 2 點）。兩者對呼叫端都是成功。
        }

        var now = DateTime.UtcNow;
        db.NewsletterSubscribers.Add(new NewsletterSubscriber
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, Email = email, Source = source, Status = StatusSubscribed,
            SubscribedAt = now, CreatedAt = now, UpdatedAt = now,
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // 同一信箱的並行送出：另一個請求先寫入了，結果等同已訂閱。
        }
    }

    /// <summary>以退訂憑證退訂。憑證無效 → 400（不說明原因）；找不到名單列或已退訂 → 視為成功（冪等，不透露名單狀態）。</summary>
    public async Task<NewsletterUnsubscribeResultDto> UnsubscribeAsync(ClubScope scope, string? token, CancellationToken cancellationToken)
    {
        var parsed = tokens.TryParse(token);
        if (parsed is null || parsed.Value.ClubId != scope.ClubId)
        {
            throw new PublicValidationException("這個退訂連結無效或已損毀，請改用信件中最新的退訂連結，或與我們聯絡。");
        }

        var row = await db.NewsletterSubscribers
            .FirstOrDefaultAsync(s => s.ClubId == scope.ClubId && s.Email == parsed.Value.Email, cancellationToken);
        if (row is null || row.Status == StatusUnsubscribed)
        {
            return new NewsletterUnsubscribeResultDto { Changed = false };
        }

        var now = DateTime.UtcNow;
        row.Status = StatusUnsubscribed;
        row.UnsubscribedAt = now;
        row.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return new NewsletterUnsubscribeResultDto { Changed = true };
    }

    private static string NormalizeEmail(string? raw)
    {
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new PublicValidationException("請填寫電子郵件地址。");
        }

        if (text.Length > 255 || !EmailFormat().IsMatch(text))
        {
            throw new PublicValidationException("電子郵件地址的格式不正確，請檢查後再送出。");
        }

        return text.ToLowerInvariant();
    }

    private static string ResolveSource(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return SourceLabels["footer"];
        }

        return SourceLabels.TryGetValue(code.Trim(), out var label)
            ? label
            : throw new PublicValidationException("訂閱入口不正確。");
    }
}
