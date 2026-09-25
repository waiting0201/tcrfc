using System.Text;
using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// 301 轉址管理（S1-12）。權限碼 <c>sysadmin_only</c>，理由同
/// <see cref="AdminSeoSettingsEndpoints"/> 檔頭說明。
/// </summary>
public static class AdminRedirectsEndpoints
{
    private const string PermissionView = "seo.redirect.view";
    private const string PermissionCreate = "seo.redirect.create";
    private const string PermissionUpdate = "seo.redirect.update";
    private const string PermissionDelete = "seo.redirect.delete";
    private const string PermissionImport = "seo.redirect.import";

    public static void MapAdminRedirectsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/seo/redirects")
            .WithTags("AdminRedirects")
            .WithDescription("後台 301 轉址管理（H 模組），需要登入與系統管理員權限。");

        // GET /api/v1/admin/{club}/seo/redirects?keyword=&page=&pageSize=
        group.MapGet("", async (
            string club, string? keyword, int? page, int? pageSize,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminRedirectsRepository repository,
            CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (normalizedPage, normalizedPageSize) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 200);
            var result = await repository.ListAsync(scope, keyword, normalizedPage, normalizedPageSize, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminListRedirects")
        .Produces<PagedResult<AdminRedirectDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateRedirectRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminRedirectsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var result = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/seo/redirects/{result.Id}", result);
        })
        .WithName("AdminCreateRedirect")
        .Produces<AdminRedirectDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpdateRedirectRequest request, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminRedirectsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var result = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithName("AdminUpdateRedirect")
        .Produces<AdminRedirectDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminRedirectsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionDelete, cancellationToken);
            var deleted = await repository.DeleteAsync(scope, id, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteRedirect")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/v1/admin/{club}/seo/redirects/export → 下載這個俱樂部自己的轉址對照 CSV。
        group.MapGet("/export", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminRedirectsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var csvText = await repository.ExportCsvAsync(scope, cancellationToken);
            var bytes = CsvUtils.ToUtf8BytesWithBom(csvText);
            return Results.File(bytes, "text/csv; charset=utf-8", $"redirects-{club}.csv");
        })
        .WithName("AdminExportRedirectsCsv")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv")
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/{club}/seo/redirects/import  body：CSV 檔案原始位元組（比照既有
        // Features/AdminFaqs／AdminMatches 的既有匯入端點形狀：不是 multipart/form-data，
        // 直接讀 HTTP 請求主體，一律以 UTF-8 解碼，開頭若有 BOM 由 CsvUtils.Parse 自動去除）。
        group.MapPost("/import", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminRedirectsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionImport, cancellationToken);

            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8);
            var csvContent = await reader.ReadToEndAsync(cancellationToken);

            var result = await repository.ImportCsvAsync(scope, csvContent, scope.Identity.AdminUserId, cancellationToken);
            return result.Errors.Count > 0 ? Results.BadRequest(result) : Results.Ok(result);
        })
        .WithName("AdminImportRedirectsCsv")
        .Produces<RedirectCsvImportResultDto>()
        .Produces<RedirectCsvImportResultDto>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .DisableAntiforgery();
    }
}
