using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>I4 多語系管理後台端點（語系、備援規則與格式、翻譯狀態總覽）。字串翻譯表見 <c>AdminUiStringsEndpoints</c>。
/// 權限碼 <c>site.locale.view／update</c>（sysadmin_only）；翻譯狀態總覽另允許字串翻譯表的檢視者（翻譯人員）。</summary>
public static class AdminI18nEndpoints
{
    private const string PermissionView = "site.locale.view";
    private const string PermissionUpdate = "site.locale.update";
    private static readonly string[] OverviewCodes = [PermissionView, "site.string.view", "site.string.translate"];

    public static void MapAdminI18nEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/i18n")
            .WithTags("AdminI18n")
            .WithDescription("I4 多語系管理（啟用語系、備援規則、日期數字格式、翻譯狀態總覽），需要登入。");

        group.MapGet("/locales", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminI18nRepository repository, CancellationToken cancellationToken) =>
        {
            await authorizer.AuthorizeAnyAsync(httpContext, club, OverviewCodes, cancellationToken);
            return Results.Ok(await repository.ListLocalesAsync(cancellationToken));
        })
        .WithName("AdminListLocales").Produces<IReadOnlyList<AdminLocaleDto>>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/locales/{code}", async (
            string club, string code, UpdateAdminLocaleRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminI18nRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateLocaleAsync(scope, code, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateLocale").Produces<AdminLocaleDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapGet("/settings", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminI18nRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.GetSettingsAsync(scope, cancellationToken));
        })
        .WithName("AdminGetI18nSettings").Produces<AdminI18nSettingsDto>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapPut("/settings", async (
            string club, UpdateAdminI18nSettingsRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminI18nRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            return Results.Ok(await repository.UpdateSettingsAsync(scope, request, scope.Identity.AdminUserId, cancellationToken));
        })
        .WithName("AdminUpdateI18nSettings").Produces<AdminI18nSettingsDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        // GET /i18n/overview?type=&missing=en&keyword=&page=1&pageSize=50
        group.MapGet("/overview", async (
            string club, string? type, string? missing, string? keyword, int? page, int? pageSize,
            HttpContext httpContext, IAdminClubAuthorizer authorizer, AdminI18nRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(httpContext, club, OverviewCodes, cancellationToken);
            return Results.Ok(await repository.OverviewAsync(scope, type, missing, keyword, page ?? 1, pageSize ?? 50, cancellationToken));
        })
        .WithName("AdminTranslationOverview").Produces<AdminTranslationOverviewDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);
    }
}
