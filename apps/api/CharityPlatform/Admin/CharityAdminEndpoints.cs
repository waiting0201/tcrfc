using System.IO.Compression;
using System.Text;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.CharityPlatform.Admin;

/// <summary>
/// 慈善獨立後台 API（CH-3：N1 店家與 QR Code、N2 捐款項目、N3 捐款紀錄與異常佇列）。路徑前綴
/// <c>/api/v1/donation-platform/admin/</c>。🔴 <b>每支端點都先通過 <see cref="ICharityAdminAuthorizer"/></b>，拿到
/// <see cref="CharityAdminScope"/> 才能呼叫 service（型別層強制，service 方法的第一個參數就是它）；這是慈善自己的帳號體系，
/// 主站後台的權杖在這裡一律當作沒登入。🔴 沒有任何端點接受 <c>club_id</c>——慈善是單一法人。
/// </summary>
public static class CharityAdminEndpoints
{
    public static void MapCharityAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var root = app.MapGroup("/api/v1/donation-platform/admin").WithTags("CharityAdmin");
        MapStores(root.MapGroup("/stores"));
        MapProjects(root.MapGroup("/projects"));
        MapDonations(root.MapGroup("/donations"));
        root.MapCharityAdminLedgerEndpoints();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // N1 店家管理與 QR Code
    // ═══════════════════════════════════════════════════════════════════════

    private static void MapStores(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? keyword, string? status, int? page, int? pageSize, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.StoreView, ct);
            return Results.Ok(await service.ListAsync(scope, keyword, status, page, pageSize, ct));
        }).WithName("CharityAdminListStores").Produces<PagedResult<AdminStoreListItemDto>>();

        // 批次匯入店家（CSV，規劃書 §6.1）。body 是 CSV 原始位元組（UTF-8，有無 BOM 都可）。整批驗證：任一列有錯整批不寫入，回 400 並列出所有問題。
        group.MapPost("/import", async (
            bool? skipDuplicates, HttpContext http, ICharityAdminAuthorizer authorizer, CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.StoreManage, ct);
            var csv = await CharityAdminLedgerEndpoints.ReadCsvBodyAsync(http.Request, ct);
            var result = await service.ImportCsvAsync(scope, csv, skipDuplicates == true, ClientIpResolver.Resolve(http), ct);
            return result.Errors.Count > 0 ? Results.BadRequest(result) : Results.Ok(result);
        }).WithName("CharityAdminImportStoresCsv").Produces<AdminStoreImportResultDto>()
          .Produces<AdminStoreImportResultDto>(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).DisableAntiforgery();

        group.MapGet("/import-template", async (
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.StoreManage, ct);
            return Results.File(service.BuildImportTemplate(scope), "text/csv; charset=utf-8", "store-import-template.csv");
        }).WithName("CharityAdminStoreImportTemplate").Produces(StatusCodes.Status200OK, contentType: "text/csv");

        // 批次匯出全部合作中店家的 QR（zip）。⚠️ 必須在 /{id:guid} 之前宣告（雖然 guid 約束已能區分，仍保持明確）。
        group.MapGet("/qr-export", async (
            string? format, int? size, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.StoreExport, ct);
            var kind = ParseQrFormat(format);
            var stores = await service.ListActiveQrSourcesAsync(scope, ct);

            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (_, slug, nameZh) in stores)
                {
                    var url = service.BuildQrTargetUrl(slug)
                        ?? throw new CharityServiceUnavailableException("前台網址尚未設定，無法產生 QR Code。");
                    var entry = zip.CreateEntry($"{SafeFileName(nameZh)}-{slug}.{kind}", CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    var bytes = kind == "png" ? CharityQrCodeGenerator.Png(url, size ?? CharityQrCodeGenerator.DefaultPixelsPerModule)
                                              : Encoding.UTF8.GetBytes(CharityQrCodeGenerator.Svg(url, size ?? CharityQrCodeGenerator.DefaultPixelsPerModule));
                    await entryStream.WriteAsync(bytes, ct);
                }
            }

            return Results.File(buffer.ToArray(), "application/zip", "store-qr-codes.zip");
        }).WithName("CharityAdminExportStoreQrCodes").Produces(StatusCodes.Status200OK, contentType: "application/zip");

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.StoreView, ct);
            var store = await service.GetAsync(scope, id, ct);
            return store is null ? Results.NotFound() : Results.Ok(store);
        }).WithName("CharityAdminGetStore").Produces<AdminStoreDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            UpsertStoreRequest request, HttpContext http, ICharityAdminAuthorizer authorizer, CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.StoreManage, ct);
            var created = await service.CreateAsync(scope, request, ClientIpResolver.Resolve(http), ct);
            return Results.Created($"/api/v1/donation-platform/admin/stores/{created.Id}", created);
        }).WithName("CharityAdminCreateStore").Produces<AdminStoreDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden);

        group.MapPut("/{id:guid}", async (
            Guid id, UpsertStoreRequest request, HttpContext http, ICharityAdminAuthorizer authorizer, CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.StoreManage, ct);
            var updated = await service.UpdateAsync(scope, id, request, ClientIpResolver.Resolve(http), ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("CharityAdminUpdateStore").Produces<AdminStoreDetailDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/regenerate-slug", async (
            Guid id, RegenerateStoreSlugRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.StoreManage, ct);
            var result = await service.RegenerateSlugAsync(scope, id, request.Confirm, ClientIpResolver.Resolve(http), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).WithName("CharityAdminRegenerateStoreSlug").Produces<AdminStoreSlugResponse>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/logo", async (
            Guid id, HttpRequest request, HttpContext http, ICharityAdminAuthorizer authorizer, CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.StoreManage, ct);
            var bytes = await ReadUploadAsync(request, ct);
            var updated = await service.SetLogoAsync(scope, id, bytes, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("CharityAdminSetStoreLogo").Produces<AdminStoreDetailDto>().DisableAntiforgery();

        group.MapDelete("/{id:guid}/logo", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.StoreManage, ct);
            return await service.RemoveLogoAsync(scope, id, ct) ? Results.NoContent() : Results.NotFound();
        }).WithName("CharityAdminRemoveStoreLogo").Produces(StatusCodes.Status204NoContent);

        // 單一店家 QR：PNG／SVG；PDF 尚未提供（協會標誌資產與字型未到位，見 CharityQrCodeGenerator）。
        group.MapGet("/{id:guid}/qr", async (
            Guid id, string? format, int? size, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityStoresAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAnyAsync(http, [CharityPermissions.StoreManage, CharityPermissions.StoreExport], ct);
            var kind = ParseQrFormat(format);
            var source = await service.GetQrSourceAsync(scope, id, ct);
            if (source is null)
            {
                return Results.NotFound();
            }

            var url = service.BuildQrTargetUrl(source.Value.Slug)
                ?? throw new CharityServiceUnavailableException("前台網址尚未設定，無法產生 QR Code。");
            var pixels = size ?? CharityQrCodeGenerator.DefaultPixelsPerModule;
            return kind == "png"
                ? Results.File(CharityQrCodeGenerator.Png(url, pixels), "image/png", $"{SafeFileName(source.Value.NameZh)}-{source.Value.Slug}.png")
                : Results.File(Encoding.UTF8.GetBytes(CharityQrCodeGenerator.Svg(url, pixels)), "image/svg+xml", $"{SafeFileName(source.Value.NameZh)}-{source.Value.Slug}.svg");
        }).WithName("CharityAdminGetStoreQr").Produces(StatusCodes.Status200OK, contentType: "image/png")
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status501NotImplemented);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // N2 捐款項目管理
    // ═══════════════════════════════════════════════════════════════════════

    private static void MapProjects(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            string? status, HttpContext http, ICharityAdminAuthorizer authorizer, CharityProjectsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ProjectView, ct);
            return Results.Ok(await service.ListAsync(scope, status, ct));
        }).WithName("CharityAdminListProjects").Produces<IReadOnlyList<AdminProjectListItemDto>>();

        // 撥付對象與慈善計畫的候選清單（唯讀複本，下拉選單用）。
        group.MapGet("/charity-refs", async (
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityProjectsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ProjectView, ct);
            return Results.Ok(await service.ListCharityRefsAsync(scope, ct));
        }).WithName("CharityAdminListCharityRefs").Produces<AdminCharityRefOptionsDto>();

        group.MapGet("/{id:guid}", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityProjectsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ProjectView, ct);
            var project = await service.GetAsync(scope, id, ct);
            return project is null ? Results.NotFound() : Results.Ok(project);
        }).WithName("CharityAdminGetProject").Produces<AdminProjectDetailDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("", async (
            UpsertProjectRequest request, HttpContext http, ICharityAdminAuthorizer authorizer, CharityProjectsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ProjectManage, ct);
            var created = await service.CreateAsync(scope, request, ClientIpResolver.Resolve(http), ct);
            return Results.Created($"/api/v1/donation-platform/admin/projects/{created.Id}", created);
        }).WithName("CharityAdminCreateProject").Produces<AdminProjectDetailDto>(StatusCodes.Status201Created)
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
            Guid id, UpsertProjectRequest request, HttpContext http, ICharityAdminAuthorizer authorizer, CharityProjectsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ProjectManage, ct);
            var updated = await service.UpdateAsync(scope, id, request, ClientIpResolver.Resolve(http), ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("CharityAdminUpdateProject").Produces<AdminProjectDetailDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict);

        // 只編輯內文（區塊編輯器儲存用）：省略的欄位不變，不會碰到分潤與金額設定。
        group.MapPut("/{id:guid}/content", async (
            Guid id, UpdateProjectContentRequest request, HttpContext http, ICharityAdminAuthorizer authorizer, CharityProjectsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ProjectManage, ct);
            var updated = await service.UpdateContentAsync(scope, id, request, ClientIpResolver.Resolve(http), ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("CharityAdminUpdateProjectContent").Produces<AdminProjectDetailDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/publish", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityProjectsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ProjectPublish, ct);
            var project = await service.SetPublishedAsync(scope, id, published: true, ct);
            return project is null ? Results.NotFound() : Results.Ok(project);
        }).WithName("CharityAdminPublishProject").Produces<AdminProjectDetailDto>();

        group.MapPost("/{id:guid}/unpublish", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityProjectsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ProjectPublish, ct);
            var project = await service.SetPublishedAsync(scope, id, published: false, ct);
            return project is null ? Results.NotFound() : Results.Ok(project);
        }).WithName("CharityAdminUnpublishProject").Produces<AdminProjectDetailDto>();

        group.MapPost("/{id:guid}/cover", async (
            Guid id, HttpRequest request, HttpContext http, ICharityAdminAuthorizer authorizer, CharityProjectsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ProjectManage, ct);
            var bytes = await ReadUploadAsync(request, ct);
            var updated = await service.SetCoverAsync(scope, id, bytes, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("CharityAdminSetProjectCover").Produces<AdminProjectDetailDto>().DisableAntiforgery();

        group.MapDelete("/{id:guid}/cover", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityProjectsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.ProjectManage, ct);
            return await service.RemoveCoverAsync(scope, id, ct) ? Results.NoContent() : Results.NotFound();
        }).WithName("CharityAdminRemoveProjectCover").Produces(StatusCodes.Status204NoContent);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // N3 捐款紀錄與異常佇列
    // ═══════════════════════════════════════════════════════════════════════

    private static DonationFilter ToFilter(
        DateOnly? from, DateOnly? to, string? status, Guid? projectId, Guid? storeId, bool? noStore,
        string? invoiceStatus, int? amountMin, int? amountMax, string? keyword)
        => new(from, to, status, projectId, storeId, noStore == true, invoiceStatus, amountMin, amountMax, keyword);

    private static void MapDonations(RouteGroupBuilder group)
    {
        group.MapGet("", async (
            DateOnly? from, DateOnly? to, string? status, Guid? projectId, Guid? storeId, bool? noStore, string? invoiceStatus,
            int? amountMin, int? amountMax, string? keyword, int? page, int? pageSize,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityDonationsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationView, ct);
            var filter = ToFilter(from, to, status, projectId, storeId, noStore, invoiceStatus, amountMin, amountMax, keyword);
            return Results.Ok(await service.ListAsync(scope, filter, page, pageSize, ct));
        }).WithName("CharityAdminListDonations").Produces<PagedResult<AdminDonationListItemDto>>();

        group.MapGet("/anomalies", async (
            string? kind, HttpContext http, ICharityAdminAuthorizer authorizer, CharityDonationsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationView, ct);
            return Results.Ok(await service.ListAnomaliesAsync(scope, kind, ct));
        }).WithName("CharityAdminListAnomalies").Produces<IReadOnlyList<AdminAnomalyDto>>();

        group.MapGet("/anomalies/counts", async (
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityDonationsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationView, ct);
            return Results.Ok(await service.CountAnomaliesAsync(scope, ct));
        }).WithName("CharityAdminCountAnomalies").Produces<AdminAnomalyCountsDto>();

        // 含個資的明細匯出：額外授權＋用途備註（purpose）必填＋稽核。
        group.MapGet("/export", async (
            DateOnly? from, DateOnly? to, string? status, Guid? projectId, Guid? storeId, bool? noStore, string? invoiceStatus,
            int? amountMin, int? amountMax, string? keyword, string? purpose,
            HttpContext http, ICharityAdminAuthorizer authorizer, CharityDonationsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationExport, ct);
            var filter = ToFilter(from, to, status, projectId, storeId, noStore, invoiceStatus, amountMin, amountMax, keyword);
            var bytes = await service.ExportAsync(scope, filter, purpose, ClientIpResolver.Resolve(http), ct);
            return Results.File(bytes, "text/csv; charset=utf-8", $"donations-{DateTime.UtcNow.AddHours(8):yyyyMMdd-HHmmss}.csv");
        }).WithName("CharityAdminExportDonations").Produces(StatusCodes.Status200OK, contentType: "text/csv");

        // reveal=true 解除個資遮罩：需要 reveal 權限並寫稽核（沒有權限回 403，不會悄悄降級成遮罩）。
        group.MapGet("/{id:guid}", async (
            Guid id, bool? reveal, HttpContext http, ICharityAdminAuthorizer authorizer, CharityDonationsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationView, ct);
            var donation = await service.GetAsync(scope, id, reveal == true, ClientIpResolver.Resolve(http), ct);
            return donation is null ? Results.NotFound() : Results.Ok(donation);
        }).WithName("CharityAdminGetDonation").Produces<AdminDonationDetailDto>()
          .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/refund", async (
            Guid id, RefundDonationRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityDonationsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationRefund, ct);
            return Results.Ok(await service.RefundAsync(scope, id, request.Reason, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminRefundDonation").Produces<AdminDonationDetailDto>()
          .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status403Forbidden)
          .Produces(StatusCodes.Status404NotFound).Produces(StatusCodes.Status409Conflict).Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/{id:guid}/resend-thanks", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityDonationsAdminService service, CancellationToken ct) =>
        {
            // 重寄信會寄到捐款人 Email，需要能看個資明文的角色（系統管理員、客服／行政）。
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationReveal, ct);
            var sent = await service.ResendThanksAsync(scope, id, ClientIpResolver.Resolve(http), ct);
            return Results.Ok(new { sent });
        }).WithName("CharityAdminResendThanks").Produces(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/invoice/reissue", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityDonationsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.InvoiceIssue, ct);
            return Results.Ok(await service.ReissueInvoiceAsync(scope, id, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminReissueInvoice").Produces<AdminDonationDetailDto>().Produces(StatusCodes.Status409Conflict);

        // 隱藏／恢復徵信名單顯示（規劃書 §6.3 N3 操作）。
        group.MapPost("/{id:guid}/credit-visibility", async (
            Guid id, SetCreditVisibilityRequest request, HttpContext http, ICharityAdminAuthorizer authorizer,
            CharityDonationsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationHideCredit, ct);
            return Results.Ok(await service.SetCreditVisibilityAsync(scope, id, request.Hidden, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminSetCreditVisibility").Produces<AdminCreditVisibilityDto>().Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/recheck-payment", async (
            Guid id, HttpContext http, ICharityAdminAuthorizer authorizer, CharityDonationsAdminService service, CancellationToken ct) =>
        {
            var scope = await authorizer.AuthorizeAsync(http, CharityPermissions.DonationRecheckPayment, ct);
            return Results.Ok(await service.RecheckPaymentAsync(scope, id, ClientIpResolver.Resolve(http), ct));
        }).WithName("CharityAdminRecheckPayment").Produces<AdminDonationDetailDto>().Produces(StatusCodes.Status409Conflict);
    }

    // ───────────────────────────────────────────────────────────────────────

    private static string ParseQrFormat(string? format)
    {
        var f = string.IsNullOrWhiteSpace(format) ? "png" : format.Trim().ToLowerInvariant();
        return f switch
        {
            "png" or "svg" => f,
            "pdf" => throw new CharityApiException(StatusCodes.Status501NotImplemented, "尚未提供",
                "含店名的印刷版 PDF 尚未提供：需要協會標誌資產與中文字型，兩者到位後才能產出。目前請先下載 PNG 或 SVG。"),
            _ => throw new AdminValidationException("格式只能是 png、svg（pdf 尚未提供）。"),
        };
    }

    /// <summary>讀取 multipart 的單一檔案欄位 <c>file</c> 為位元組（上限同全站圖片上傳通則 10 MB）。</summary>
    private static async Task<byte[]> ReadUploadAsync(HttpRequest request, CancellationToken ct)
    {
        var file = await Tcrfc.Api.Features.Uploads.AdminMultipartForm.ReadFileAsync(request, "file", ct);
        if (file.Length == 0)
        {
            throw new EmptyImageException();
        }

        if (file.Length > ImageUploadOptions.MaxUploadBytes)
        {
            throw new ImageTooLargeException();
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(memory, ct);
        return memory.ToArray();
    }

    private static string SafeFileName(string? name)
    {
        var raw = string.IsNullOrWhiteSpace(name) ? "store" : name.Trim();
        var cleaned = new string(raw.Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c != ' ').ToArray());
        return cleaned.Length == 0 ? "store" : cleaned.Length > 40 ? cleaned[..40] : cleaned;
    }
}
