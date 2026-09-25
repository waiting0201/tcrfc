using Tcrfc.Api.Common;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminEnquiries;

/// <summary>G2「詢問收件匣」後台端點。權限碼命名照 docs/12b §7.3：module_code=G、submodule_code=G2、
/// domain=enquiry。四組權限碼（<c>enquiry.inbox.*</c>／<c>enquiry.course.*</c>／
/// <c>enquiry.partnership.*</c>／<c>enquiry.media.*</c>）與依類別過濾的完整說明見
/// <c>AdminEnquiriesRepository</c> 檔頭。<c>enquiry.inbox.export</c> 是 <c>is_restricted=1</c>——
/// 本檔只做基本權限檢查，「執行當下二次驗證」全系統目前都還沒有任何模組實作，比照 S1-9 既有先例
/// 不另外發明，見任務回報。</summary>
public static class AdminEnquiriesEndpoints
{
    public static void MapAdminEnquiriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/enquiries")
            .WithTags("AdminEnquiries")
            .WithDescription("G2 俱樂部範圍的詢問收件匣，需要登入與俱樂部授權。");

        // GET /api/v1/admin/{club}/enquiries?formCode=&status=&keyword=&dateFrom=&dateTo=&page=&pageSize=
        group.MapGet("", async (
            string club, string? formCode, string? status, string? keyword, DateOnly? dateFrom, DateOnly? dateTo,
            int? page, int? pageSize, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminEnquiriesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, AdminEnquiriesRepository.ViewCandidateCodes, cancellationToken);
            var allowedFormCodes = await repository.ResolveViewFormCodeFilterAsync(scope, cancellationToken);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);

            var result = await repository.ListAsync(
                scope, allowedFormCodes, formCode, status, keyword, dateFrom, dateTo, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListEnquiries")
        .Produces<PagedResult<AdminEnquiryListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminEnquiriesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, AdminEnquiriesRepository.ViewCandidateCodes, cancellationToken);
            var allowedFormCodes = await repository.ResolveViewFormCodeFilterAsync(scope, cancellationToken);
            var enquiry = await repository.GetByIdAsync(scope, allowedFormCodes, id, cancellationToken);
            return enquiry is null ? Results.NotFound() : Results.Ok(enquiry);
        })
        .WithName("AdminGetEnquiry")
        .Produces<AdminEnquiryDetailDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/{club}/enquiries/export?... → CSV（Excel 可直接開啟，比照既有慣例）。
        group.MapGet("/export", async (
            string club, string? formCode, string? status, string? keyword, DateOnly? dateFrom, DateOnly? dateTo,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminEnquiriesRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, AdminEnquiriesRepository.PermissionInboxExport, cancellationToken);
            var csv = await repository.ExportCsvAsync(scope, null, formCode, status, keyword, dateFrom, dateTo, cancellationToken);
            var bytes = CsvUtils.ToUtf8BytesWithBom(csv);
            return Results.File(bytes, "text/csv; charset=utf-8", $"enquiries-{club}-{DateTime.UtcNow:yyyyMMdd}.csv");
        })
        .WithName("AdminExportEnquiries")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/{club}/enquiries/assignable-users?formCode=... —— S1-10 修正
        // （2026-09-25）新增：G2「指派負責人」姓名選單，只有系統管理員能查得到姓名的既有缺口。
        // 權限碼與 PUT 同一組（UpdateCandidateCodes）——能處理詢問的人才能查「能指派給誰」；
        // 再依 formCode 是否落在呼叫端持有的類別範圍內二次檢查，越權一律視同 404（不洩漏存在與否）。
        group.MapGet("/assignable-users", async (
            string club, string formCode, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminEnquiriesRepository repository, CancellationToken cancellationToken) =>
        {
            if (!FormCatalog.IsKnownCode(formCode))
            {
                return Results.NotFound();
            }

            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, AdminEnquiriesRepository.UpdateCandidateCodes, cancellationToken);
            var allowedFormCodes = await repository.ResolveUpdateFormCodeFilterAsync(scope, cancellationToken);
            if (allowedFormCodes is not null && !allowedFormCodes.Contains(formCode))
            {
                return Results.NotFound(); // 越權查詢別的類別：視同 404，比照既有跨類別慣例。
            }

            var users = await repository.ListAssignableUsersAsync(scope, formCode, cancellationToken);
            return Results.Ok(users);
        })
        .WithName("AdminListAssignableEnquiryUsers")
        .Produces<IReadOnlyList<AssignableAdminUserDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateAdminEnquiryRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminEnquiriesRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, AdminEnquiriesRepository.UpdateCandidateCodes, cancellationToken);
            var allowedFormCodes = await repository.ResolveUpdateFormCodeFilterAsync(scope, cancellationToken);
            var updated = await repository.UpdateAsync(scope, allowedFormCodes, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateEnquiry")
        .Produces<AdminEnquiryDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
