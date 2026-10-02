using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.CharityPlatform.Admin;

/// <summary>
/// 慈善後台的帳務與營運端點（CH-4／CH-5）：N4 回饋金結算、每日對帳、N5 憑證管理、N6 報表、N7 站台設定與稽核紀錄查詢。
/// 路徑前綴同 <see cref="CharityAdminEndpoints"/>（<c>/api/v1/donation-platform/admin/</c>）。🔴 <b>每支端點都先通過 <see cref="ICharityAdminAuthorizer"/></b>
/// （<c>CharityArchitectureTests</c> 逐支掃描）；沒有任何端點接受 <c>club_id</c>。
/// </summary>
public static class CharityAdminLedgerEndpoints
{
    private const long MaxImportBytes = 1 * 1024 * 1024;

    public static void MapCharityAdminLedgerEndpoints(this RouteGroupBuilder root)
    {
        MapSettlements(root.MapGroup("/settlements"));
        MapReconciliation(root.MapGroup("/reconciliation"));
        MapInvoices(root.MapGroup("/invoices"));
        MapReports(root.MapGroup("/reports"));
        MapSettings(root);
        MapAuditLogs(root.MapGroup("/audit-logs"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // N4 回饋金結算
    // ═══════════════════════════════════════════════════════════════════════

    private static void MapSettlements(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? status, string? payeeType, Guid? payeeId, DateOnly? from, DateOnly? to, int? page, int? pageSize,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettlementsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettlementView, ct);
            return Results.Ok(await service.ListAsync(scope, status, payeeType, payeeId, from, to, page, pageSize, ct));
        }).WithName("CharityAdminListSettlements").Produces<PagedResult<AdminSettlementListItemDto>>();

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettlementsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettlementView, ct);
            var detail = await service.GetAsync(scope, id, ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        }).WithName("CharityAdminGetSettlement").Produces<AdminSettlementDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/export", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettlementsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettlementExport, ct);
            var file = await service.ExportCsvAsync(scope, id, ct);
            return file is null ? Results.NotFound() : Results.File(file.Value.Bytes, "text/csv; charset=utf-8", file.Value.FileName);
        }).WithName("CharityAdminExportSettlement").Produces(StatusCodes.Status200OK, contentType: "text/csv").Produces(StatusCodes.Status404NotFound);

        group.MapPost("/run", async (
            RunSettlementRequest request, HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettlementsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettlementExecute, ct);
            return Results.Ok(await service.RunAsync(scope, request, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminRunSettlement").Produces<AdminSettlementRunResultDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).Produces(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/recalculate", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettlementsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettlementExecute, ct);
            return Results.Ok(await service.RecalculateAsync(scope, id, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminRecalculateSettlement").Produces<AdminSettlementRecalculationDto>()
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/settle", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettlementsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettlementExecute, ct);
            return Results.Ok(await service.SettleAsync(scope, id, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminSettleSettlement").Produces<AdminSettlementDetailDto>()
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        // 🔴 「已付款」登記用獨立權限碼：核對結算的人與登記匯款的人可以是不同的人（規劃書 §10）。
        group.MapPost("/{id:guid}/mark-paid", async (
            Guid id, MarkSettlementPaidRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharitySettlementsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettlementMarkPaid, ct);
            return Results.Ok(await service.MarkPaidAsync(scope, id, request, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminMarkSettlementPaid").Produces<AdminSettlementDetailDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettlementsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettlementExecute, ct);
            await service.DeleteDraftAsync(scope, id, ClientIpResolver.Resolve(http), ct);
            return Results.NoContent();
        }).WithName("CharityAdminDeleteSettlementDraft").Produces(StatusCodes.Status204NoContent)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 每日對帳（異常佇列的第三類來源）
    // ═══════════════════════════════════════════════════════════════════════

    private static void MapReconciliation(RouteGroupBuilder group)
    {
        group.MapGet("/runs", async (
            DateOnly? from, DateOnly? to, bool? onlyPending, int? page, int? pageSize,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityReconciliationAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationView, ct);
            return Results.Ok(await service.ListRunsAsync(scope, from, to, onlyPending, page, pageSize, ct));
        }).WithName("CharityAdminListReconciliationRuns").Produces<PagedResult<AdminReconciliationRunDto>>();

        group.MapGet("/runs/{id:guid}", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityReconciliationAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationView, ct);
            var detail = await service.GetRunAsync(scope, id, ct);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        }).WithName("CharityAdminGetReconciliationRun").Produces<AdminReconciliationRunDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/runs", async (
            RunReconciliationRequest? request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityReconciliationAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationRecheckPayment, ct);
            return Results.Ok(await service.RunAsync(scope, request?.Date, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminRunReconciliation").Produces<AdminReconciliationRunResultDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status409Conflict).Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/discrepancies/{id:guid}/resolve", async (
            Guid id, ResolveDiscrepancyRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityReconciliationAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationRecheckPayment, ct);
            return Results.Ok(await service.ResolveAsync(scope, id, request.Note, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminResolveDiscrepancy").Produces<AdminReconciliationDiscrepancyDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // N5 發票與收據管理
    // ═══════════════════════════════════════════════════════════════════════

    private static InvoiceFilter ToInvoiceFilter(DateOnly? from, DateOnly? to, string? issueStatus, string? voidStatus, string? invoiceType, Guid? projectId, string? keyword)
        => new(from, to, issueStatus, voidStatus, invoiceType, projectId, keyword);

    private static void MapInvoices(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            DateOnly? from, DateOnly? to, string? issueStatus, string? voidStatus, string? invoiceType, Guid? projectId, string? keyword,
            int? page, int? pageSize, HttpContext http, ICharityAdminAuthorizer authorizer, CharityInvoicesAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.InvoiceView, ct);
            return Results.Ok(await service.ListAsync(scope, ToInvoiceFilter(from, to, issueStatus, voidStatus, invoiceType, projectId, keyword), page, pageSize, ct));
        }).WithName("CharityAdminListInvoices").Produces<PagedResult<AdminInvoiceListItemDto>>();

        group.MapGet("/export", async (
            DateOnly? from, DateOnly? to, string? issueStatus, string? voidStatus, string? invoiceType, Guid? projectId, string? keyword,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityInvoicesAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.InvoiceView, ct);
            var bytes = await service.ExportAsync(scope, ToInvoiceFilter(from, to, issueStatus, voidStatus, invoiceType, projectId, keyword), ClientIpResolver.Resolve(http), ct);
            return Results.File(bytes, "text/csv; charset=utf-8", $"invoices-{DateTime.UtcNow.AddHours(8):yyyyMMdd-HHmmss}.csv");
        }).WithName("CharityAdminExportInvoices").Produces(StatusCodes.Status200OK, contentType: "text/csv");

        group.MapPost("/{id:guid}/manual-number", async (
            Guid id, ManualInvoiceNumberRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityInvoicesAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.InvoiceIssue, ct);
            return Results.Ok(await service.ManualNumberAsync(scope, id, request, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminManualInvoiceNumber").Produces<AdminInvoiceListItemDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/void", async (
            Guid id, InvoiceReasonRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityInvoicesAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.InvoiceVoid, ct);
            return Results.Ok(await service.VoidAsync(scope, id, request.Reason, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminVoidInvoice").Produces<AdminInvoiceListItemDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict).Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/{id:guid}/allowance", async (
            Guid id, InvoiceReasonRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityInvoicesAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.InvoiceVoid, ct);
            return Results.Ok(await service.AllowanceAsync(scope, id, request.Reason, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminAllowanceInvoice").Produces<AdminInvoiceListItemDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound)
          .Produces(StatusCodes.Status409Conflict).Produces(StatusCodes.Status503ServiceUnavailable);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // N6 捐款報表
    // ═══════════════════════════════════════════════════════════════════════

    private static async Task<bool> WantsCsvAsync(
        string? format, CharityAdminScope scope, ICharityAdminAuthorizer authorizer, CancellationToken ct)
    {
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // 匯出需要另一個權限碼：能看報表不代表能匯出（403，不會悄悄降級成 JSON）。
        if (!await authorizer.HasAdditionalPermissionAsync(scope, CharityPermissions.ReportExport, ct))
        {
            throw new AdminForbiddenException("匯出報表需要額外的授權，請洽系統管理員。");
        }

        return true;
    }

    private static string CsvName(string name) => $"report-{name}-{DateTime.UtcNow.AddHours(8):yyyyMMdd-HHmmss}.csv";

    private static void MapReports(RouteGroupBuilder group)
    {
        group.MapGet("/overview", async (
            DateOnly? from, DateOnly? to, Guid? storeId, bool? noStore, Guid? projectId, string? paymentStatus, string? invoiceType,
            int? amountMin, int? amountMax, string? granularity, string? format,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityReportsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ReportView, ct);
            var csv = await WantsCsvAsync(format, scope, authorizer, ct);
            var filter = CharityReportsAdminService.NormalizeFilter(from, to, storeId, noStore, projectId, paymentStatus, invoiceType, amountMin, amountMax);
            var data = await service.OverviewAsync(scope, filter, granularity, ct);
            return csv ? Results.File(CharityReportsAdminService.OverviewCsv(data), "text/csv; charset=utf-8", CsvName("overview")) : Results.Ok(data);
        }).WithName("CharityAdminReportOverview").Produces<AdminReportOverviewDto>();

        group.MapGet("/by-store", async (
            DateOnly? from, DateOnly? to, Guid? storeId, bool? noStore, Guid? projectId, string? paymentStatus, string? invoiceType,
            int? amountMin, int? amountMax, string? format,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityReportsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ReportView, ct);
            var csv = await WantsCsvAsync(format, scope, authorizer, ct);
            var filter = CharityReportsAdminService.NormalizeFilter(from, to, storeId, noStore, projectId, paymentStatus, invoiceType, amountMin, amountMax);
            var data = await service.ByStoreAsync(scope, filter, ct);
            return csv ? Results.File(CharityReportsAdminService.ByStoreCsv(data), "text/csv; charset=utf-8", CsvName("by-store")) : Results.Ok(data);
        }).WithName("CharityAdminReportByStore").Produces<IReadOnlyList<AdminReportStoreRowDto>>();

        group.MapGet("/by-project", async (
            DateOnly? from, DateOnly? to, Guid? storeId, bool? noStore, Guid? projectId, string? paymentStatus, string? invoiceType,
            int? amountMin, int? amountMax, string? format,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityReportsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ReportView, ct);
            var csv = await WantsCsvAsync(format, scope, authorizer, ct);
            var filter = CharityReportsAdminService.NormalizeFilter(from, to, storeId, noStore, projectId, paymentStatus, invoiceType, amountMin, amountMax);
            var data = await service.ByProjectAsync(scope, filter, ct);
            return csv ? Results.File(CharityReportsAdminService.ByProjectCsv(data), "text/csv; charset=utf-8", CsvName("by-project")) : Results.Ok(data);
        }).WithName("CharityAdminReportByProject").Produces<IReadOnlyList<AdminReportProjectRowDto>>();

        group.MapGet("/invoice-status", async (
            DateOnly? from, DateOnly? to, Guid? storeId, bool? noStore, Guid? projectId, string? paymentStatus, string? invoiceType,
            int? amountMin, int? amountMax, string? format,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityReportsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ReportView, ct);
            var csv = await WantsCsvAsync(format, scope, authorizer, ct);
            var filter = CharityReportsAdminService.NormalizeFilter(from, to, storeId, noStore, projectId, paymentStatus, invoiceType, amountMin, amountMax);
            var data = await service.InvoiceStatusAsync(scope, filter, ct);
            return csv ? Results.File(CharityReportsAdminService.InvoiceStatusCsv(data), "text/csv; charset=utf-8", CsvName("invoice-status")) : Results.Ok(data);
        }).WithName("CharityAdminReportInvoiceStatus").Produces<AdminReportInvoiceStatusDto>();

        group.MapGet("/details", async (
            DateOnly? from, DateOnly? to, Guid? storeId, bool? noStore, Guid? projectId, string? paymentStatus, string? invoiceType,
            int? amountMin, int? amountMax, string? format, int? page, int? pageSize,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityReportsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ReportView, ct);
            var csv = await WantsCsvAsync(format, scope, authorizer, ct);
            var filter = CharityReportsAdminService.NormalizeFilter(from, to, storeId, noStore, projectId, paymentStatus, invoiceType, amountMin, amountMax);
            if (csv)
            {
                return Results.File(CharityReportsAdminService.DetailsCsv(await service.AllDetailsAsync(scope, filter, ct)), "text/csv; charset=utf-8", CsvName("details"));
            }

            return Results.Ok(await service.DetailsAsync(scope, filter, page, pageSize, ct));
        }).WithName("CharityAdminReportDetails").Produces<PagedResult<AdminReportDetailRowDto>>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // N7 站台設定
    // ═══════════════════════════════════════════════════════════════════════

    private static void MapSettings(RouteGroupBuilder root)
    {
        root.MapGet("/settings", async (
            HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettingsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettingView, ct);
            return Results.Ok(await service.GetAsync(scope, ct));
        }).WithName("CharityAdminGetSettings").Produces<AdminSiteSettingsDto>();

        root.MapPut("/settings", async (
            UpdateSiteSettingsRequest request, HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettingsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettingManage, ct);
            return Results.Ok(await service.UpdateAsync(scope, request, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminUpdateSettings").Produces<AdminSiteSettingsDto>().Produces(StatusCodes.Status400BadRequest);

        root.MapGet("/email-templates", async (
            HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettingsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettingView, ct);
            return Results.Ok(await service.ListEmailTemplatesAsync(scope, ct));
        }).WithName("CharityAdminListEmailTemplates").Produces<IReadOnlyList<AdminEmailTemplateDto>>();

        root.MapPut("/email-templates/{code}", async (
            string code, UpdateEmailTemplateRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharitySettingsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.SettingManage, ct);
            return Results.Ok(await service.UpdateEmailTemplateAsync(scope, code, request, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminUpdateEmailTemplate").Produces<AdminEmailTemplateDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        // 🔴 金流與發票憑證：僅系統管理員（種子 sysadmin_only），只寫不讀（回應沒有任何憑證內容）。
        root.MapGet("/payment-channels", async (
            HttpContext http, ICharityAdminAuthorizer authorizer, CharitySettingsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.PaymentChannelManage, ct);
            return Results.Ok(await service.ListPaymentChannelsAsync(scope, ct));
        }).WithName("CharityAdminListPaymentChannels").Produces<IReadOnlyList<AdminPaymentChannelDto>>();

        root.MapPut("/payment-channels/{channelType}/credential", async (
            string channelType, SetPaymentCredentialRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharitySettingsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.PaymentChannelManage, ct);
            return Results.Ok(await service.SetCredentialAsync(scope, channelType, request, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminSetPaymentCredential").Produces<IReadOnlyList<AdminPaymentChannelDto>>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        root.MapPut("/payment-channels/{channelType}/environment", async (
            string channelType, SwitchPaymentEnvironmentRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharitySettingsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.PaymentChannelManage, ct);
            return Results.Ok(await service.SwitchEnvironmentAsync(scope, channelType, request, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminSwitchPaymentEnvironment").Produces<IReadOnlyList<AdminPaymentChannelDto>>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 稽核紀錄查詢
    // ═══════════════════════════════════════════════════════════════════════

    private static void MapAuditLogs(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            DateOnly? from, DateOnly? to, string? action, string? targetType, Guid? targetId, Guid? adminUserId, string? keyword, int? page, int? pageSize,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityAuditQueryService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AuditLogView, ct);
            return Results.Ok(await service.QueryAsync(scope, new AuditLogFilter(from, to, action, targetType, targetId, adminUserId, keyword), page, pageSize, ct));
        }).WithName("CharityAdminQueryAuditLogs").Produces<PagedResult<AdminAuditLogDto>>().Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/actions", async (
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityAuditQueryService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.AuditLogView, ct);
            return Results.Ok(await service.ListActionsAsync(scope, ct));
        }).WithName("CharityAdminListAuditActions").Produces<IReadOnlyList<AdminAuditActionOptionDto>>();
    }

    /// <summary>讀取請求本文為 UTF-8 文字（匯入 CSV 用），上限 <see cref="MaxImportBytes"/>。</summary>
    internal static async Task<string> ReadCsvBodyAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength is > MaxImportBytes)
        {
            throw new AdminValidationException("檔案太大，匯入檔不可超過 1 MB。");
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxImportBytes)
            {
                throw new AdminValidationException("檔案太大，匯入檔不可超過 1 MB。");
            }

            buffer.Write(chunk, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
