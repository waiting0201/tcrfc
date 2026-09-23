using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Common;

/// <summary>
/// 全站最後一道例外處理防線。⛔ 一律回傳結構化 <see cref="ProblemDetails"/>，
/// ⛔ 絕不把資料庫例外訊息（連線字串殘片、SQL 片段、資料表名）吐給呼叫端——
/// 那些訊息只進 <see cref="ILogger"/>，供內部查錯用。
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            ClubNotFoundException clubNotFound =>
                (StatusCodes.Status404NotFound, "找不到俱樂部", clubNotFound.Message),

            // ── 本輪新增：登入與授權（Security/、Features/AdminAuth）─────────────────────
            AdminUnauthenticatedException unauthenticated =>
                (StatusCodes.Status401Unauthorized, "請先登入", unauthenticated.Message),
            AdminForbiddenException forbidden =>
                (StatusCodes.Status403Forbidden, "沒有權限", forbidden.Message),
            AdminAuthValidationException authValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", authValidation.Message),

            // ── 本輪新增：後台新聞寫入端點的例外（Features/AdminNews），集中在這裡轉狀態碼 ──────
            AdminArticleValidationException validation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", validation.Message),
            ArticleSlugConflictException slugConflict =>
                (StatusCodes.Status409Conflict, "網址名稱重複", slugConflict.Message),
            ArticleConcurrencyConflictException concurrencyConflict =>
                (StatusCodes.Status409Conflict, "資料已被變更", concurrencyConflict.Message),
            SharedArticleReadOnlyException sharedReadOnly =>
                (StatusCodes.Status403Forbidden, "共用內容唯讀", sharedReadOnly.Message),
            ArticleInvalidStatusTransitionException invalidTransition =>
                (StatusCodes.Status409Conflict, "狀態轉換不允許", invalidTransition.Message),
            ArticleFeaturedLimitExceededException featuredLimit =>
                (StatusCodes.Status409Conflict, "置頂精選已達上限", featuredLimit.Message),

            // ── S0-8 圖片上傳共用元件（Features/Uploads、Images）─────────────────────────
            ImageProcessingException imageProcessing =>
                (StatusCodes.Status400BadRequest, "圖片無法處理", imageProcessing.Message),
            UploadSlotNotAllowedException slotNotAllowed =>
                (StatusCodes.Status400BadRequest, "不支援的圖片欄位", slotNotAllowed.Message),

            _ =>
                (StatusCodes.Status500InternalServerError, "伺服器發生未預期的錯誤", "請稍後再試；若持續發生請聯繫系統管理員。"),
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            // 完整例外只進日誌（含堆疊），呼叫端拿到的是上面那句通用訊息。
            logger.LogError(exception, "未處理的例外，路徑：{Path}", httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        }, cancellationToken);

        return true;
    }
}
