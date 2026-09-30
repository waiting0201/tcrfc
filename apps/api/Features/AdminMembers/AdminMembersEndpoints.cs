using Microsoft.AspNetCore.Mvc;
using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminMembers;

/// <summary>名單查詢參數（清單與匯出共用同一組）。</summary>
public sealed record MemberListQuery(
    bool? CrossClub, string? Keyword, string? ClubCode, string? Tier, string? MembershipStatus, string? Status,
    string? SignupSource, bool? LineBound, DateOnly? RegisteredFrom, DateOnly? RegisteredTo, Guid? SeasonId,
    int? ExpiringWithinDays, string? JerseyStatus, string? Locale, int? Page, int? PageSize, string? Purpose)
{
    public AdminMembersRepository.ListFilter ToFilter() => new()
    {
        CrossClub = CrossClub ?? false,
        Keyword = Keyword,
        ClubCode = ClubCode,
        Tier = Tier,
        MembershipStatus = MembershipStatus,
        Status = Status,
        SignupSource = SignupSource,
        LineBound = LineBound,
        RegisteredFrom = RegisteredFrom,
        RegisteredTo = RegisteredTo,
        SeasonId = SeasonId,
        ExpiringWithinDays = ExpiringWithinDays,
        JerseyStatus = JerseyStatus,
        Locale = Locale,
    };
}

/// <summary>K1 會員名單與檢視後台端點（主站規劃書 §4.11 K1）。權限碼：<c>member.account.view／create／update</c>、
/// <c>member.account.merge</c>（sysadmin_only）、<c>member.pii.reveal</c>（解除遮罩，於 repository 內判斷）、
/// <c>member.export</c>（is_restricted）。module=K、submodule=K1、domain=member。</summary>
public static class AdminMembersEndpoints
{
    private const string PermissionView = "member.account.view";
    private const string PermissionCreate = "member.account.create";
    private const string PermissionUpdate = "member.account.update";
    private const string PermissionMerge = "member.account.merge";
    private const string PermissionExport = "member.export";

    public static void MapAdminMembersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/members")
            .WithTags("AdminMembers")
            .WithDescription("K1 會員名單與檢視，需要登入與俱樂部授權。名單一律遮罩個資；完整個資需要解除遮罩權限。");

        // GET /members?crossClub=&keyword=&clubCode=&tier=&membershipStatus=&status=&signupSource=&lineBound=&registeredFrom=&registeredTo=
        //            &seasonId=&expiringWithinDays=&jerseyStatus=&locale=&page=&pageSize=
        group.MapGet("", async (
            string club, [AsParameters] MemberListQuery query, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var (page, pageSize) = PagingQuery.Normalize(query.Page, query.PageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListAsync(scope, query.ToFilter(), page, pageSize, cancellationToken));
        })
        .WithName("AdminListMembers")
        .Produces<PagedResult<AdminMemberListItemDto>>();

        // GET /members/export?purpose=…（同名單的篩選參數）→ CSV。需要 member.export，並須填用途。
        group.MapGet("/export", async (
            string club, [AsParameters] MemberListQuery query, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionExport, cancellationToken);
            var csv = await repository.ExportCsvAsync(scope, query.ToFilter(), query.Purpose, cancellationToken);
            return Results.File(CsvUtils.ToUtf8BytesWithBom(csv), "text/csv; charset=utf-8", $"members-{club}-{DateTime.UtcNow:yyyyMMdd}.csv");
        })
        .WithName("AdminExportMembers")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/duplicates", async (
            string club, bool? crossClub, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            return Results.Ok(await repository.FindDuplicatesAsync(scope, crossClub ?? false, cancellationToken));
        })
        .WithName("AdminListDuplicateMembers")
        .Produces<IReadOnlyList<AdminMemberDuplicateGroupDto>>();

        group.MapPost("/merge", async (
            string club, MergeAdminMembersRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionMerge, cancellationToken);
            return Results.Ok(await repository.MergeAsync(scope, request, cancellationToken));
        })
        .WithName("AdminMergeMembers")
        .Produces<MergeAdminMembersResultDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        // GET /members/{id}?reveal=true —— reveal=true 需要 member.pii.reveal，並寫敏感操作日誌。
        group.MapGet("/{id:guid}", async (
            string club, Guid id, bool? reveal, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionView, cancellationToken);
            var member = await repository.GetByIdAsync(scope, id, reveal ?? false, cancellationToken);
            return member is null ? Results.NotFound() : Results.Ok(member);
        })
        .WithName("AdminGetMember")
        .Produces<AdminMemberDetailDto>()
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, CreateAdminMemberRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/members/{created.Id}", created);
        })
        .WithName("AdminCreateMember")
        .Produces<AdminMemberDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/status", async (
            string club, Guid id, UpdateAdminMemberStatusRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateStatusAsync(scope, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateMemberStatus")
        .Produces<AdminMemberDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/note", async (
            string club, Guid id, UpdateAdminMemberNoteRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var updated = await repository.UpdateNoteAsync(scope, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateMemberNote")
        .Produces<AdminMemberDetailDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // POST /members/{id}/cards/{cardId}/reissue —— 重新產生會員卡 QR（舊憑證立即失效）。
        group.MapPost("/{id:guid}/cards/{cardId:guid}/reissue", async (
            string club, Guid id, Guid cardId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembersRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PermissionUpdate, cancellationToken);
            var card = await repository.ReissueCardAsync(scope, id, cardId, cancellationToken);
            return card is null ? Results.NotFound() : Results.Ok(card);
        })
        .WithName("AdminReissueMemberCard")
        .Produces<AdminMemberCardDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);
    }
}
