using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Tcrfc.Api.Features.AdminAccounts;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Features.AdminClubs;
using Tcrfc.Api.Features.AdminCompetitions;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.AdminRoles;
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

            // ── 本輪新增（S1：J1 帳號管理、Features/AdminAccounts）──────────────────────
            AdminAccountValidationException accountValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", accountValidation.Message),
            AdminAccountUsernameConflictException accountConflict =>
                (StatusCodes.Status409Conflict, "帳號重複", accountConflict.Message),
            AdminAccountLastSuperAdminException lastSuperAdmin =>
                (StatusCodes.Status409Conflict, "操作被擋下", lastSuperAdmin.Message),

            // ── 本輪新增（S1：J2 角色與權限、Features/AdminRoles）───────────────────────
            AdminRoleValidationException roleValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", roleValidation.Message),
            AdminRoleSysadminOnlyPermissionException sysadminOnlyPermission =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", sysadminOnlyPermission.Message),
            AdminRoleCodeConflictException roleConflict =>
                (StatusCodes.Status409Conflict, "角色代碼重複", roleConflict.Message),
            AdminRoleSystemDeleteException systemRoleDelete =>
                (StatusCodes.Status403Forbidden, "系統角色不可刪除", systemRoleDelete.Message),
            AdminRoleInUseException roleInUse =>
                (StatusCodes.Status409Conflict, "角色仍在使用中", roleInUse.Message),

            // ── 本輪新增（S1：J4 俱樂部主檔、Features/AdminClubs）───────────────────────
            AdminClubValidationException clubValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", clubValidation.Message),
            AdminClubCodeConflictException clubCodeConflict =>
                (StatusCodes.Status409Conflict, "俱樂部代碼重複", clubCodeConflict.Message),
            AdminClubDomainConflictException clubDomainConflict =>
                (StatusCodes.Status409Conflict, "網域重複", clubDomainConflict.Message),

            // ── 本輪新增（S1：J4 賽事系列、Features/AdminCompetitions）─────────────────
            AdminCompetitionValidationException competitionValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", competitionValidation.Message),
            AdminCompetitionCodeConflictException competitionConflict =>
                (StatusCodes.Status409Conflict, "賽事系列代號重複", competitionConflict.Message),

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
