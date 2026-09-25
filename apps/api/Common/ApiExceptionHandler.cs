using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Tcrfc.Api.Features.AdminAccounts;
using Tcrfc.Api.Features.AdminAuth;
using Tcrfc.Api.Features.AdminBanners;
using Tcrfc.Api.Features.AdminClubs;
using Tcrfc.Api.Features.AdminCompetitions;
using Tcrfc.Api.Features.AdminEnquiries;
using Tcrfc.Api.Features.AdminFaqs;
using Tcrfc.Api.Features.AdminForms;
using Tcrfc.Api.Features.AdminHomeSections;
using Tcrfc.Api.Features.AdminMatches;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.AdminPages;
using Tcrfc.Api.Features.AdminPlayers;
using Tcrfc.Api.Features.AdminPrograms;
using Tcrfc.Api.Features.AdminRegistrations;
using Tcrfc.Api.Features.AdminRoles;
using Tcrfc.Api.Features.AdminSessions;
using Tcrfc.Api.Features.AdminStaff;
using Tcrfc.Api.Features.AdminStandings;
using Tcrfc.Api.Features.AdminTeams;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Features.Programs;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Videos;
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

            // ── 本輪新增（S1-4：B1 頁面管理，Features/AdminPages）───────────────────────
            AdminPageValidationException pageValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", pageValidation.Message),
            PageSlugConflictException pageSlugConflict =>
                (StatusCodes.Status409Conflict, "網址名稱重複", pageSlugConflict.Message),
            PageConcurrencyConflictException pageConcurrencyConflict =>
                (StatusCodes.Status409Conflict, "資料已被變更", pageConcurrencyConflict.Message),
            PageInvalidStatusTransitionException pageInvalidTransition =>
                (StatusCodes.Status409Conflict, "狀態轉換不允許", pageInvalidTransition.Message),
            PageVersionNotFoundException pageVersionNotFound =>
                (StatusCodes.Status404NotFound, "找不到版本", pageVersionNotFound.Message),

            // ── S0-8 圖片上傳共用元件（Features/Uploads、Images）─────────────────────────
            ImageProcessingException imageProcessing =>
                (StatusCodes.Status400BadRequest, "圖片無法處理", imageProcessing.Message),
            UploadSlotNotAllowedException slotNotAllowed =>
                (StatusCodes.Status400BadRequest, "不支援的圖片欄位", slotNotAllowed.Message),

            // ── v3.14 影片上傳共用元件（Videos）──────────────────────────────────────
            VideoProcessingException videoProcessing =>
                (StatusCodes.Status400BadRequest, "影片無法處理", videoProcessing.Message),

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

            // ── S1-7 新增：C1–C3 球隊／球員／教練（Features/AdminTeams、AdminPlayers、AdminStaff）──
            AdminTeamValidationException teamValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", teamValidation.Message),
            AdminTeamCodeConflictException teamCodeConflict =>
                (StatusCodes.Status409Conflict, "隊別代號重複", teamCodeConflict.Message),
            AdminTeamFirstTeamAlreadyExistsException firstTeamExists =>
                (StatusCodes.Status409Conflict, "一線隊已存在", firstTeamExists.Message),
            AdminPlayerValidationException playerValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", playerValidation.Message),
            AdminStaffValidationException staffValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", staffValidation.Message),
            SharedStaffReadOnlyException sharedStaffReadOnly =>
                (StatusCodes.Status403Forbidden, "共用內容唯讀", sharedStaffReadOnly.Message),

            // ── S1-6 新增：B3 首頁編排（Features/AdminBanners、AdminHomeSections）───────────
            AdminBannerValidationException bannerValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", bannerValidation.Message),
            AdminHomeSectionValidationException homeSectionValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", homeSectionValidation.Message),

            // ── S1-6 新增：B4 常見問題（Features/AdminFaqs）────────────────────────────
            AdminFaqValidationException faqValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", faqValidation.Message),
            FaqSlugConflictException faqSlugConflict =>
                (StatusCodes.Status409Conflict, "網址名稱重複", faqSlugConflict.Message),
            SharedFaqReadOnlyException sharedFaqReadOnly =>
                (StatusCodes.Status403Forbidden, "共用內容唯讀", sharedFaqReadOnly.Message),
            FaqCategorySlugConflictException faqCategorySlugConflict =>
                (StatusCodes.Status409Conflict, "分類網址名稱重複", faqCategorySlugConflict.Message),

            // ── S1-8 新增：C4 賽程與賽果／積分榜（Features/AdminMatches、AdminStandings）────
            AdminMatchValidationException matchValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", matchValidation.Message),
            AdminMatchNoConflictException matchNoConflict =>
                (StatusCodes.Status409Conflict, "場次編號重複", matchNoConflict.Message),
            AdminStandingValidationException standingValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", standingValidation.Message),

            // ── S1-9 新增：P1–P3 課程項目／梯次／報名（Features/AdminPrograms、AdminSessions、
            //    AdminRegistrations、Programs）─────────────────────────────────────
            AdminProgramValidationException programValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", programValidation.Message),
            ProgramSlugConflictException programSlugConflict =>
                (StatusCodes.Status409Conflict, "網址名稱重複", programSlugConflict.Message),
            AdminSessionValidationException sessionValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", sessionValidation.Message),
            ProgramNotFoundForSessionException programNotFoundForSession =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", programNotFoundForSession.Message),
            AdminRegistrationValidationException registrationValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", registrationValidation.Message),
            SessionNotFoundForRegistrationException sessionNotFoundForRegistration =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", sessionNotFoundForRegistration.Message),
            ProgramRegistrationValidationException publicRegistrationValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", publicRegistrationValidation.Message),
            ProgramSessionNotFoundException programSessionNotFound =>
                (StatusCodes.Status404NotFound, "找不到梯次", programSessionNotFound.Message),

            // ── S1-10 新增：G1 表單設計器／G2 詢問收件匣／10 表單中心公開端點 ─────────────
            AdminFormValidationException formValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", formValidation.Message),
            AdminFormFieldKeyConflictException formFieldKeyConflict =>
                (StatusCodes.Status409Conflict, "欄位代碼重複", formFieldKeyConflict.Message),
            AdminFormFieldInUseException formFieldInUse =>
                (StatusCodes.Status409Conflict, "欄位使用中", formFieldInUse.Message),
            AdminEnquiryValidationException enquiryValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", enquiryValidation.Message),
            PublicFormNotFoundException publicFormNotFound =>
                (StatusCodes.Status404NotFound, "找不到表單", publicFormNotFound.Message),
            PublicFormSubmissionValidationException publicFormSubmissionValidation =>
                (StatusCodes.Status400BadRequest, "輸入內容有誤", publicFormSubmissionValidation.Message),

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
