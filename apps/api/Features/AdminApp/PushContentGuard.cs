using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminDraws;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>
/// M3 的系統層阻擋（App 規劃書 §6.8、§8.3）：<b>推播不得成為繞過「中獎只以最新消息公布」承諾的後門</b>。
/// 阻擋兩類：① 內容看起來是在通知中獎（標題或內文含「中獎」「得獎」「winner」等）；② 深連結指向帶「球迷會員抽獎」標籤的文章。
/// 分眾條件本身沒有「以中獎名單為對象」的維度（只有會籍層級、追蹤球隊、俱樂部），所以不可能用分眾送到中獎人。
/// 自動推播（新聞發布）在建立推播前必須呼叫 <see cref="IsMemberDrawArticleAsync"/>：帶抽獎標籤的文章一律不自動推播。
/// </summary>
public sealed class PushContentGuard(ClubDbContext dbContext)
{
    internal static readonly string[] WinnerWords = ["中獎", "得獎", "獲獎", "抽中", "winner", "you won", "drawn as"];
    public const string BlockedMessage = "抽獎結果只能以最新消息公布，推播不得用來通知中獎（含以中獎名單為對象的推播）。請改用最新消息公布，個別聯繫由客服處理。";

    public async Task EnsureAllowedAsync(PushContentInput content, string? deepLink, CancellationToken cancellationToken)
    {
        var text = string.Join(' ', new[] { content.Zh.Title, content.Zh.Body, content.En?.Title, content.En?.Body }.Where(t => !string.IsNullOrWhiteSpace(t)));
        if (WinnerWords.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AdminConflictException("不能推播中獎通知", BlockedMessage);
        }

        var slug = ExtractNewsSlug(deepLink);
        if (slug is not null && await IsMemberDrawSlugAsync(slug, cancellationToken))
        {
            throw new AdminConflictException("不能推播抽獎公布文章", BlockedMessage);
        }
    }

    public Task<bool> IsMemberDrawArticleAsync(Guid articleId, CancellationToken cancellationToken)
        => dbContext.Articles.AsNoTracking().AnyAsync(a => a.Id == articleId && a.Tags.Any(t => t.Slug == AdminDrawsRepository.MemberDrawTagSlug), cancellationToken);

    private Task<bool> IsMemberDrawSlugAsync(string slug, CancellationToken cancellationToken)
        => dbContext.Articles.AsNoTracking().AnyAsync(a => a.Slug == slug && a.Tags.Any(t => t.Slug == AdminDrawsRepository.MemberDrawTagSlug), cancellationToken);

    /// <summary>從 <c>tcrfc://news/{slug}</c> 或官網新聞網址（<c>…/news/{slug}</c>）取出文章網址名稱；不是新聞連結回 <c>null</c>。</summary>
    internal static string? ExtractNewsSlug(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        var trimmed = link.Trim().TrimEnd('/');
        var marker = trimmed.LastIndexOf("/news/", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return null;
        }

        var slug = trimmed[(marker + "/news/".Length)..];
        var cut = slug.IndexOfAny(['?', '#']);
        return cut >= 0 ? slug[..cut] : slug;
    }
}
