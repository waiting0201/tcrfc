using Tcrfc.Api.Common;
using Tcrfc.Api.Features.AdminCompetitions;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminMemberships;

/// <summary>K2 會籍與方案後台端點（主站規劃書 §4.11 K2）。權限碼（module=K、submodule=K2、domain=member）：
/// <c>member.plan.*</c>（方案）、<c>member.membership.view／create／update</c>（會籍與付款）、
/// <c>member.setting.view／update</c>（會員編號規則）、<c>member.export</c>（續會名單，is_restricted）。</summary>
public static class AdminMembershipsEndpoints
{
    private const string PlanView = "member.plan.view";
    private const string PlanCreate = "member.plan.create";
    private const string PlanUpdate = "member.plan.update";
    private const string PlanDelete = "member.plan.delete";
    private const string MembershipView = "member.membership.view";
    private const string MembershipCreate = "member.membership.create";
    private const string MembershipUpdate = "member.membership.update";
    private const string SettingView = "member.setting.view";
    private const string SettingUpdate = "member.setting.update";
    private const string Export = "member.export";

    public static void MapAdminMembershipsEndpoints(this IEndpointRouteBuilder app)
    {
        MapPlans(app);
        MapMemberships(app);
        MapSettings(app);
        MapSeasons(app);
    }

    /// <summary>GET /api/v1/admin/{club}/membership-seasons —— 會籍畫面（方案表單、開通、續會名單）的球季下拉選單。
    /// 權限碼 <c>member.membership.view</c>（客服／行政沒有 <c>team.competition.view</c>，不能借用 <c>/seasons</c>）。
    /// 回應形狀與 <c>GET …/seasons</c> 相同：<c>[{ id, code, startOn, endOn }]</c>，只含目前俱樂部的球季（新→舊）。</summary>
    private static void MapSeasons(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/{club}/membership-seasons", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminCompetitionsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MembershipView, cancellationToken);
            return Results.Ok(await repository.ListSeasonsAsync(scope, cancellationToken));
        })
        .WithTags("AdminMemberships")
        .WithName("AdminListMembershipSeasons")
        .Produces<IReadOnlyList<AdminSeasonListItemDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }

    private static void MapPlans(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/membership-plans")
            .WithTags("AdminMembershipPlans")
            .WithDescription("K2 會籍方案維護，需要登入與俱樂部授權。");

        group.MapGet("", async (
            string club, Guid? seasonId, string? status, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipPlansRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PlanView, cancellationToken);
            return Results.Ok(await repository.ListAsync(scope, seasonId, status, cancellationToken));
        })
        .WithName("AdminListMembershipPlans").Produces<IReadOnlyList<AdminPlanListItemDto>>();

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipPlansRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PlanView, cancellationToken);
            var plan = await repository.GetByIdAsync(scope, id, cancellationToken);
            return plan is null ? Results.NotFound() : Results.Ok(plan);
        })
        .WithName("AdminGetMembershipPlan").Produces<AdminPlanDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            string club, UpsertAdminPlanRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipPlansRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PlanCreate, cancellationToken);
            var created = await repository.CreateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/membership-plans/{created.Id}", created);
        })
        .WithName("AdminCreateMembershipPlan").Produces<AdminPlanDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            string club, Guid id, UpsertAdminPlanRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipPlansRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PlanUpdate, cancellationToken);
            var updated = await repository.UpdateAsync(scope, id, request, scope.Identity.AdminUserId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminUpdateMembershipPlan").Produces<AdminPlanDetailDto>().Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipPlansRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PlanDelete, cancellationToken);
            return await repository.DeleteAsync(scope, id, cancellationToken) ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteMembershipPlan").Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/order", async (
            string club, ReorderRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipPlansRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, PlanUpdate, cancellationToken);
            await repository.ReorderAsync(scope, request.Ids, scope.Identity.AdminUserId, cancellationToken);
            return Results.NoContent();
        })
        .WithName("AdminReorderMembershipPlans").Produces(StatusCodes.Status204NoContent).Produces(StatusCodes.Status400BadRequest);
    }

    private static void MapMemberships(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/memberships")
            .WithTags("AdminMemberships")
            .WithDescription("K2 會籍、開通、調整、會員卡張數、批次到期與續會名單，需要登入與俱樂部授權。");

        // GET /memberships?memberId=&keyword=&tier=&status=&seasonId=&planId=&expiringWithinDays=&page=&pageSize=
        group.MapGet("", async (
            string club, Guid? memberId, string? keyword, string? tier, string? status, Guid? seasonId, Guid? planId,
            int? expiringWithinDays, int? page, int? pageSize, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MembershipView, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            var filter = new AdminMembershipsRepository.ListFilter
            {
                MemberId = memberId, Keyword = keyword, Tier = tier, Status = status, SeasonId = seasonId, PlanId = planId,
                ExpiringWithinDays = expiringWithinDays,
            };
            return Results.Ok(await repository.ListAsync(scope, filter, p, ps, cancellationToken));
        })
        .WithName("AdminListMemberships").Produces<PagedResult<AdminMembershipListItemDto>>();

        // GET /memberships/renewal-export?kind=expiring|expired&days=30&seasonId=&purpose=… → CSV（member.export，受限）
        group.MapGet("/renewal-export", async (
            string club, string? kind, int? days, Guid? seasonId, string? purpose, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, Export, cancellationToken);
            var csv = await repository.RenewalExportCsvAsync(scope, kind, days, seasonId, purpose, cancellationToken);
            return Results.File(CsvUtils.ToUtf8BytesWithBom(csv), "text/csv; charset=utf-8", $"renewals-{club}-{DateTime.UtcNow:yyyyMMdd}.csv");
        })
        .WithName("AdminExportRenewals").Produces(StatusCodes.Status200OK).Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", async (
            string club, Guid id, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MembershipView, cancellationToken);
            var membership = await repository.GetByIdAsync(scope, id, cancellationToken);
            return membership is null ? Results.NotFound() : Results.Ok(membership);
        })
        .WithName("AdminGetMembership").Produces<AdminMembershipDetailDto>().Produces(StatusCodes.Status404NotFound);

        // POST /memberships/activate —— 手動開通與續會（受益俱樂部＝目前操作的俱樂部；收款法人由系統帶入）。
        group.MapPost("/activate", async (
            string club, ActivateMembershipRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MembershipCreate, cancellationToken);
            var result = await repository.ActivateAsync(scope, request, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("AdminActivateMembership").Produces<AdminMembershipDetailDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        // POST /memberships —— 建立免費（一般會員）會籍。
        group.MapPost("", async (
            string club, RegisterMembershipRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MembershipCreate, cancellationToken);
            var created = await repository.RegisterAsync(scope, request, cancellationToken);
            return Results.Created($"/api/v1/admin/{club}/memberships/{created.Membership.MembershipId}", created);
        })
        .WithName("AdminRegisterMembership").Produces<AdminMembershipDetailDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict);

        // PUT /memberships/{id}/adjust —— 手動調整層級／狀態／起訖日（原因必填）。
        group.MapPut("/{id:guid}/adjust", async (
            string club, Guid id, AdjustMembershipRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MembershipUpdate, cancellationToken);
            var updated = await repository.AdjustAsync(scope, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminAdjustMembership").Produces<AdminMembershipDetailDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/cards", async (
            string club, Guid id, AddMemberCardRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MembershipUpdate, cancellationToken);
            var updated = await repository.AddCardAsync(scope, id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminAddMemberCard").Produces<AdminMembershipDetailDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cards/{cardId:guid}/revoke", async (
            string club, Guid id, Guid cardId, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MembershipUpdate, cancellationToken);
            var updated = await repository.RevokeCardAsync(scope, id, cardId, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .WithName("AdminRevokeMemberCard").Produces<AdminMembershipDetailDto>().Produces(StatusCodes.Status404NotFound);

        // POST /memberships/expire-batch —— 球季末批次到期處理（依俱樂部各自執行，可先 dryRun 試算）。
        group.MapPost("/expire-batch", async (
            string club, ExpireBatchRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MembershipUpdate, cancellationToken);
            return Results.Ok(await repository.ExpireBatchAsync(scope, request, cancellationToken));
        })
        .WithName("AdminExpireMembershipsBatch").Produces<ExpireBatchResultDto>();

        // GET /membership-payments 在下面另一個群組。
        var payments = app.MapGroup("/api/v1/admin/{club}/membership-payments")
            .WithTags("AdminMembershipPayments")
            .WithDescription("K2 會籍付款紀錄（供對帳），需要登入與俱樂部授權。");
        payments.MapGet("", async (
            string club, Guid? membershipId, DateOnly? from, DateOnly? to, int? page, int? pageSize, HttpContext httpContext,
            IAdminClubAuthorizer authorizer, AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, MembershipView, cancellationToken);
            var (p, ps) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return Results.Ok(await repository.ListPaymentsAsync(scope, membershipId, from, to, p, ps, cancellationToken));
        })
        .WithName("AdminListMembershipPayments").Produces<PagedResult<AdminPaymentListItemDto>>();
    }

    private static void MapSettings(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/member-settings")
            .WithTags("AdminMemberSettings")
            .WithDescription("K2 會員編號產生規則。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingView, cancellationToken);
            return Results.Ok(await repository.GetSettingsAsync(scope, cancellationToken));
        })
        .WithName("AdminGetMemberSettings").Produces<AdminMemberSettingsDto>();

        group.MapPut("", async (
            string club, UpdateAdminMemberSettingsRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminMembershipsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, SettingUpdate, cancellationToken);
            return Results.Ok(await repository.UpdateSettingsAsync(scope, request, cancellationToken));
        })
        .WithName("AdminUpdateMemberSettings").Produces<AdminMemberSettingsDto>().Produces(StatusCodes.Status400BadRequest);
    }
}
