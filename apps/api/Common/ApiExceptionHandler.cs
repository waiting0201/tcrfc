using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
