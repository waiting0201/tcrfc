using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Net.Http.Headers;

namespace Tcrfc.Api.Common;

/// <summary>
/// 公開列表端點的 <c>ETag</c>／<c>If-None-Match</c> → <c>304 Not Modified</c>（行動 App 規劃書 §2.4 快取時效、docs/19 §3；
/// iOS／Android 缺口 A4）。
///
/// 🔴 為什麼這不會讓 App 讀到舊資料：ETag 是**這次實際產生的回應本文**的 SHA-256。每個請求照常走完整個處理管線
/// （含既有的 <c>IQueryCache</c> 與資料庫），本文沒變才回 304；資料庫或快取一有變，本文雜湊就變，用戶端拿到的一定是新內容。
/// 這個中介軟體只省「重複下載相同本文」的流量，不新增任何一層快取，也不延長任何資料的有效期。
///
/// 🔴 <b>只掛在白名單路徑</b>（<see cref="Allowed"/>），刻意不涵蓋 docs/14／docs/17「五類資料不得讀快取」與其他個人化或即時資料：
/// 商店（庫存、購物車、我的訂單）、會員一族（<c>/member/*</c>、會員卡驗證頁 <c>/m/*</c>、會籍與訂單狀態、我的報名）、
/// 課程與梯次（含即時名額）、廣告投放、通知中心、裝置訂閱、後台。這些端點完全不經過本中介軟體，不會有 ETag，也不會回 304。
///
/// 只處理 <c>GET</c>、狀態碼 200、<c>application/json</c> 的回應；本文超過 <see cref="MaxBufferedBytes"/> 不做（直接放行）。
/// 回應帶 <c>ETag</c>（強驗證）；請求的 <c>If-None-Match</c> 命中（含 <c>*</c> 與多值清單、弱驗證前綴 <c>W/</c>）→ 304、無本文，
/// 並保留 CORS／<c>ETag</c> 標頭。不改 <c>Cache-Control</c>（維持各端點與 Cloudflare 的既有行為）。
/// </summary>
public sealed partial class ConditionalGetMiddleware(RequestDelegate next)
{
    /// <summary>超過這個大小的本文不緩衝計算雜湊（直接串流放行）。App 列表遠小於此。</summary>
    public const int MaxBufferedBytes = 2 * 1024 * 1024;

    /// <summary>
    /// 允許 ETag 的公開、非個人化、非即時端點。<c>{club}</c> 之後的第一段決定是否涵蓋；
    /// 新增端點預設<b>不</b>涵蓋，要加必須先確認它不屬於「不得讀快取」的五類。
    /// </summary>
    [GeneratedRegex(
        @"^/api/v1/(clubs(/[^/]+)?|app/(layout|config)|[^/]+/(schedule(/[^/]+)?|competitions|teams|players(/[^/]+)?|staff|news(/[^/]+)?|faqs(/[^/]+)?|partners|sponsors|partner-stores(/[^/]+)?|standings|venues|achievements|milestones))/?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex Allowed();

    public static bool IsEligiblePath(PathString path) => Allowed().IsMatch(path.Value ?? "");

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) || !IsEligiblePath(request.Path))
        {
            await next(context);
            return;
        }

        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = original;
        }

        var response = context.Response;
        var isJson200 = response.StatusCode == StatusCodes.Status200OK
            && response.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true
            && buffer.Length > 0
            && buffer.Length <= MaxBufferedBytes;

        if (isJson200)
        {
            var etag = "\"" + Convert.ToBase64String(SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)))[..22] + "\"";
            response.Headers.ETag = etag;
            // 回應內容會因請求的 Accept-Language／?lang 不同，但 lang 在網址上，不需要 Vary；只補 Origin 之外的不改。
            if (MatchesIfNoneMatch(request.Headers.IfNoneMatch, etag))
            {
                response.StatusCode = StatusCodes.Status304NotModified;
                response.ContentLength = null;
                response.ContentType = null;
                return;
            }
        }

        if (buffer.Length > 0)
        {
            response.ContentLength = buffer.Length;
            buffer.Position = 0;
            await buffer.CopyToAsync(original, context.RequestAborted);
        }
    }

    private static bool MatchesIfNoneMatch(Microsoft.Extensions.Primitives.StringValues header, string etag)
    {
        if (header.Count == 0)
        {
            return false;
        }

        foreach (var value in header)
        {
            if (value is null)
            {
                continue;
            }

            if (value.Trim() == "*")
            {
                return true;
            }

            if (EntityTagHeaderValue.TryParseList(new[] { value }, out var tags)
                && tags.Any(t => t.Compare(EntityTagHeaderValue.Parse(etag), useStrongComparison: false)))
            {
                return true;
            }
        }

        return false;
    }
}
