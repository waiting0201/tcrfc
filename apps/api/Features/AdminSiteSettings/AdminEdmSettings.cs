using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.AdminNewsletter;
using Tcrfc.Api.Features.SiteSettings;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>I6 EDM 平台設定（規劃書 §4.9 外部服務連結「EDM 平台設定」）。</summary>
public sealed record AdminEdmSettingsDto
{
    public required bool Enabled { get; init; }

    /// <summary>平台名稱（自由文字，例如供應商名稱）。供應商尚未確定，不做固定選項。</summary>
    public string? Provider { get; init; }

    /// <summary>平台端的名單／受眾識別。</summary>
    public string? ListId { get; init; }

    public string? SenderEmail { get; init; }

    /// <summary>是否已設定 API 金鑰。<b>金鑰本身只寫不讀</b>：任何端點都不回傳，也不回傳部分字元。</summary>
    public required bool ApiKeyConfigured { get; init; }

    /// <summary>目前系統有沒有串接任何 EDM 供應商的實作（<c>INewsletterEdmSync</c>）。<c>false</c> 時這些設定只是先存起來，同步仍回「尚未串接」。</summary>
    public required bool IntegrationAvailable { get; init; }

    public DateTime? UpdatedAt { get; init; }
}

public sealed record UpdateAdminEdmSettingsRequest
{
    public bool Enabled { get; init; }

    public string? Provider { get; init; }

    public string? ListId { get; init; }

    public string? SenderEmail { get; init; }

    /// <summary>新的 API 金鑰；留空＝維持原金鑰。</summary>
    public string? ApiKey { get; init; }

    /// <summary>true＝清除已儲存的金鑰（與 <see cref="ApiKey"/> 不能同時使用）。</summary>
    public bool ClearApiKey { get; init; }
}

/// <summary>
/// I6 EDM 平台設定的讀寫。資料存 <c>settings</c>（群組 <c>edm</c>，鍵見 <see cref="SiteSettingKeys"/>），每俱樂部一份（兩站名單各自獨立，同 G3）。
/// 🔴 <b>API 金鑰以 Data Protection 加密後存放，只寫不讀</b>：回應只有 <c>apiKeyConfigured</c>；設定或清除金鑰寫敏感操作日誌（不記金鑰內容）。
/// 日後串接真正的 EDM 供應商時，由 <c>INewsletterEdmSync</c> 的新實作透過 <see cref="TryGetApiKeyAsync"/> 取回金鑰（purpose 見 <see cref="Purpose"/>）。
/// 本次<b>不</b>改 <c>INewsletterEdmSync</c>（供應商未定）：同步仍回「尚未串接」，這份設定只是把將來需要的欄位先備好。
/// </summary>
public sealed class AdminEdmSettingsRepository(
    ClubDbContext db, ClubSettingsEditor settingsEditor, IDataProtectionProvider protection, INewsletterEdmSync edmSync, SensitiveActionLogger audit)
{
    public const string Purpose = "Tcrfc.Edm.ApiKey.v1";

    private static readonly string[] Keys =
        [SiteSettingKeys.EdmEnabled, SiteSettingKeys.EdmProvider, SiteSettingKeys.EdmListId, SiteSettingKeys.EdmSenderEmail, SiteSettingKeys.EdmApiKeyEncrypted];

    public async Task<AdminEdmSettingsDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => Build(await settingsEditor.LoadAsync(scope.ClubId, Keys, cancellationToken));

    public async Task<AdminEdmSettingsDto> UpdateAsync(
        AdminClubScope scope, UpdateAdminEdmSettingsRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var provider = AdminInput.OptionalText(request.Provider, "平台名稱", 64);
        var listId = AdminInput.OptionalText(request.ListId, "名單識別", 128);
        var sender = AdminInput.OptionalEmail(request.SenderEmail, "寄件者信箱")?.ToLowerInvariant();
        var newKey = request.ApiKey?.Trim();
        if (!string.IsNullOrEmpty(newKey) && request.ClearApiKey)
        {
            throw new AdminValidationException("不能同時輸入新的 API 金鑰與清除金鑰，請擇一。");
        }

        if (!string.IsNullOrEmpty(newKey) && (newKey.Length is < 8 or > 512 || newKey.Any(char.IsWhiteSpace)))
        {
            throw new AdminValidationException("API 金鑰長度須為 8 到 512 個字元，且不能含空白。");
        }

        var settings = await settingsEditor.LoadAsync(scope.ClubId, Keys, cancellationToken);
        var hadKey = !string.IsNullOrEmpty(ClubSettingsEditor.Value(settings, SiteSettingKeys.EdmApiKeyEncrypted));
        var willHaveKey = !string.IsNullOrEmpty(newKey) || (hadKey && !request.ClearApiKey);
        if (request.Enabled && (provider is null || !willHaveKey))
        {
            throw new AdminValidationException("啟用前請先填寫平台名稱與 API 金鑰。");
        }

        settingsEditor.SetValue(settings, scope.ClubId, SiteSettingKeys.EdmEnabled, SiteSettingKeys.GroupEdm, request.Enabled ? "1" : "0", operatorId);
        settingsEditor.SetValue(settings, scope.ClubId, SiteSettingKeys.EdmProvider, SiteSettingKeys.GroupEdm, provider, operatorId);
        settingsEditor.SetValue(settings, scope.ClubId, SiteSettingKeys.EdmListId, SiteSettingKeys.GroupEdm, listId, operatorId);
        settingsEditor.SetValue(settings, scope.ClubId, SiteSettingKeys.EdmSenderEmail, SiteSettingKeys.GroupEdm, sender, operatorId);
        if (!string.IsNullOrEmpty(newKey))
        {
            settingsEditor.SetValue(settings, scope.ClubId, SiteSettingKeys.EdmApiKeyEncrypted, SiteSettingKeys.GroupEdm,
                protection.CreateProtector(Purpose).Protect(newKey), operatorId);
        }
        else if (request.ClearApiKey)
        {
            settingsEditor.SetValue(settings, scope.ClubId, SiteSettingKeys.EdmApiKeyEncrypted, SiteSettingKeys.GroupEdm, null, operatorId);
        }

        await db.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrEmpty(newKey) || request.ClearApiKey)
        {
            audit.Record(scope, !string.IsNullOrEmpty(newKey) ? "設定 EDM 平台金鑰" : "清除 EDM 平台金鑰", "EDM 平台設定", 1, "憑證變更");
        }

        return Build(settings);
    }

    /// <summary>給未來的 EDM 同步實作取回金鑰；沒設定或金鑰環遺失回 <c>null</c>。目前沒有呼叫端。</summary>
    public async Task<string?> TryGetApiKeyAsync(Guid clubId, CancellationToken cancellationToken)
    {
        var cipher = await db.Settings.AsNoTracking()
            .Where(s => s.ClubId == clubId && s.SettingKey == SiteSettingKeys.EdmApiKeyEncrypted)
            .Select(s => s.SettingValue).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrEmpty(cipher))
        {
            return null;
        }

        try
        {
            return protection.CreateProtector(Purpose).Unprotect(cipher);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private AdminEdmSettingsDto Build(IReadOnlyDictionary<string, Data.EfEntities.Setting> settings) => new()
    {
        Enabled = ClubSettingsEditor.Value(settings, SiteSettingKeys.EdmEnabled) == "1",
        Provider = ClubSettingsEditor.Value(settings, SiteSettingKeys.EdmProvider),
        ListId = ClubSettingsEditor.Value(settings, SiteSettingKeys.EdmListId),
        SenderEmail = ClubSettingsEditor.Value(settings, SiteSettingKeys.EdmSenderEmail),
        ApiKeyConfigured = !string.IsNullOrEmpty(ClubSettingsEditor.Value(settings, SiteSettingKeys.EdmApiKeyEncrypted)),
        IntegrationAvailable = edmSync.ProviderName is not null,
        UpdatedAt = settings.Values.Any() ? settings.Values.Max(s => s.UpdatedAt) : null,
    };
}

/// <summary>I6 EDM 平台設定後台端點。權限碼 <c>site.edm.view／update</c>（sysadmin_only、受限）。</summary>
public static class AdminEdmSettingsEndpoints
{
    public static void MapAdminEdmSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/{club}/edm-settings")
            .WithTags("AdminEdmSettings")
            .WithDescription("I6 EDM 平台設定（平台名稱、名單識別、寄件者、API 金鑰只寫不讀），需要登入與系統管理員權限。");

        group.MapGet("", async (
            string club, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminEdmSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "site.edm.view", cancellationToken);
            return Results.Ok(await repository.GetAsync(scope, cancellationToken));
        })
        .WithName("AdminGetEdmSettings").Produces<AdminEdmSettingsDto>()
        .Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);

        group.MapPut("", async (
            string club, UpdateAdminEdmSettingsRequest request, HttpContext httpContext, IAdminClubAuthorizer authorizer,
            AdminEdmSettingsRepository repository, CancellationToken cancellationToken) =>
        {
            var scope = await authorizer.AuthorizeAsync(httpContext, club, "site.edm.update", cancellationToken);
            return Results.Ok(await repository.UpdateAsync(scope, request, scope.Identity.AdminUserId, cancellationToken));
        })
        .WithName("AdminUpdateEdmSettings").Produces<AdminEdmSettingsDto>()
        .Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status404NotFound);
    }
}
