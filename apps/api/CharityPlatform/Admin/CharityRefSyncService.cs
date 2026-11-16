using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.CharityPlatform.Admin;

/// <summary>
/// N2「從官網更新清單」（K-2，2026-11-13 使用者拍板）：由管理員手動觸發，慈善平台以 <b>HTTP</b> 呼叫主站的唯讀公開清單
/// （<c>GET /api/v1/tcrfc/charity/directory</c>，只含已公開的公益團體與慈善計畫的識別與中英名稱），再把<b>整份</b>對齊到本庫的
/// 唯讀複本 <c>charity_refs</c>／<c>charity_program_refs</c>：新增、更新名稱、主站已下架或刪除者標為停用（<b>不刪列</b>）、重新公開者恢復啟用。
///
/// 🔴 邊界（慈善規劃書 §9.3）：本平台是第二個資料庫，<b>不得持有主站資料庫連線、不得即時 join</b>——即使兩者目前在同一個 api 行程內，
/// 這裡也只注入 <see cref="IHttpClientFactory"/>，不注入任何主站 DbContext／連線工廠／Repository（<c>CharityArchitectureTests</c> 有掃描）。
/// 兩邊不同步時一律以主站為準；複本只是「選撥付對象的候選清單」，<c>donation_projects</c> 的名稱快照與已開立憑證不受影響。
///
/// 🔴 對照鍵是主站主鍵（<c>external_id</c>），不是 slug：主站後台允許改網址名稱（slug）。新建複本列的 <c>ref_code</c> 固定為主站 id 的
/// 32 碼十六進位、建立後永不變。<c>external_id</c> 為空的列（同步機制之前的本機開發種子）<b>不屬於同步範圍</b>，不會被更新或停用。
///
/// 並發：同一把交易層級 <c>sp_getapplock</c> 把同步串行化（先取鎖再抓清單，後取得鎖的人拿到的一定是較新的清單）；
/// 主站連不上、逾時、回應不合法時整個交易回復，複本一個欄位都不動。
/// </summary>
public sealed class CharityRefSyncService(
    CharityDbContext db,
    CharityAuditLogger audit,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<CharityRefSyncService> logger)
{
    private const string LockResource = "charity-ref-sync";
    private const int LockWaitMilliseconds = 15_000;
    private const int MaxResponseBytes = 2 * 1024 * 1024;
    private const int MaxItems = 2_000;
    private const int MaxNameLength = 128;
    public const string SourceName = "main_site_api";

    /// <summary>主站公開清單的路徑。慈善單元只屬於磐石（藍鯨不設），所以俱樂部代碼固定 <c>tcrfc</c>。</summary>
    public const string DirectoryPath = "/api/v1/tcrfc/charity/directory";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdminCharityRefSyncResultDto> SyncAsync(CharityAdminScope scope, string sourceIp, CancellationToken cancellationToken)
    {
        var baseUrl = CharityOptions.ResolveMainSiteApiBaseUrl(configuration, environment)
            ?? throw new CharityServiceUnavailableException("尚未設定官網清單的來源，請洽系統管理員。");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await CharitySqlLocks.TryAcquireAsync(db, transaction, LockResource, LockWaitMilliseconds, cancellationToken))
        {
            throw new CharityConflictException("清單更新中", "另一位同仁正在更新清單，請稍候幾秒後重新整理頁面再試。");
        }

        var directory = await FetchAsync(baseUrl, cancellationToken);
        var now = DateTime.UtcNow;

        var existingCharities = await db.CharityRefs.Where(c => c.ExternalId != null).ToDictionaryAsync(c => c.ExternalId!.Value, cancellationToken);
        var existingPrograms = await db.CharityProgramRefs.Where(p => p.ExternalId != null).ToDictionaryAsync(p => p.ExternalId!.Value, cancellationToken);

        var charityCounts = new Counter();
        var programCounts = new Counter();
        var charityEntities = new Dictionary<Guid, CharityRef>();

        foreach (var incoming in directory.Charities)
        {
            if (!existingCharities.TryGetValue(incoming.Id, out var row))
            {
                row = new CharityRef
                {
                    Id = Guid.NewGuid(),
                    RefCode = incoming.Id.ToString("N"),
                    ExternalId = incoming.Id,
                    Source = SourceName,
                    ImportedAt = now,
                    CreatedAt = now,
                    CreatedBy = scope.Identity.AdminUserId,
                    UpdatedAt = now,
                    UpdatedBy = scope.Identity.AdminUserId,
                    Name = incoming.NameZh,
                    NameEn = incoming.NameEn,
                    IsActive = true,
                    LastSyncedAt = now,
                };
                db.CharityRefs.Add(row);
                charityCounts.Created++;
            }
            else
            {
                var changed = false;
                if (!string.Equals(row.Name, incoming.NameZh, StringComparison.Ordinal))
                {
                    row.Name = incoming.NameZh;
                    changed = true;
                }

                if (!string.Equals(row.NameEn, incoming.NameEn, StringComparison.Ordinal))
                {
                    row.NameEn = incoming.NameEn;
                    changed = true;
                }

                if (!row.IsActive)
                {
                    row.IsActive = true;
                    row.DeactivatedAt = null;
                    charityCounts.Reactivated++;
                    changed = true;
                }
                else if (changed)
                {
                    charityCounts.Updated++;
                }
                else
                {
                    charityCounts.Unchanged++;
                }

                row.LastSyncedAt = now;
                if (changed)
                {
                    row.UpdatedAt = now;
                    row.UpdatedBy = scope.Identity.AdminUserId;
                }
            }

            charityEntities[incoming.Id] = row;
        }

        foreach (var (externalId, row) in existingCharities)
        {
            if (!charityEntities.ContainsKey(externalId) && row.IsActive)
            {
                Deactivate(row, now, scope);
                charityCounts.Deactivated++;
            }
        }

        var seenPrograms = new HashSet<Guid>();
        foreach (var incoming in directory.Programs)
        {
            var parent = charityEntities[incoming.CharityId]; // FetchAsync 已驗證：每個計畫的 charityId 都在同一份清單的公益團體裡
            seenPrograms.Add(incoming.Id);
            if (!existingPrograms.TryGetValue(incoming.Id, out var row))
            {
                db.CharityProgramRefs.Add(new CharityProgramRef
                {
                    Id = Guid.NewGuid(),
                    RefCode = incoming.Id.ToString("N"),
                    ExternalId = incoming.Id,
                    CharityRef = parent,
                    ImportedAt = now,
                    CreatedAt = now,
                    CreatedBy = scope.Identity.AdminUserId,
                    UpdatedAt = now,
                    UpdatedBy = scope.Identity.AdminUserId,
                    Name = incoming.NameZh,
                    NameEn = incoming.NameEn,
                    IsActive = true,
                    LastSyncedAt = now,
                });
                programCounts.Created++;
                continue;
            }

            var changed = false;
            if (!string.Equals(row.Name, incoming.NameZh, StringComparison.Ordinal))
            {
                row.Name = incoming.NameZh;
                changed = true;
            }

            if (!string.Equals(row.NameEn, incoming.NameEn, StringComparison.Ordinal))
            {
                row.NameEn = incoming.NameEn;
                changed = true;
            }

            if (row.CharityRefId != parent.Id)
            {
                // 主站把計畫改掛到另一個公益團體：跟著主站。（parent 若是新建列，Id 已在上面指定，這裡直接用導覽屬性。）
                row.CharityRef = parent;
                changed = true;
            }

            if (!row.IsActive)
            {
                row.IsActive = true;
                row.DeactivatedAt = null;
                programCounts.Reactivated++;
                changed = true;
            }
            else if (changed)
            {
                programCounts.Updated++;
            }
            else
            {
                programCounts.Unchanged++;
            }

            row.LastSyncedAt = now;
            if (changed)
            {
                row.UpdatedAt = now;
                row.UpdatedBy = scope.Identity.AdminUserId;
            }
        }

        foreach (var (externalId, row) in existingPrograms)
        {
            if (!seenPrograms.Contains(externalId) && row.IsActive)
            {
                Deactivate(row, now, scope);
                programCounts.Deactivated++;
            }
        }

        var activeCharities = existingCharities.Values.Count(c => c.IsActive) + charityCounts.Created;
        var activePrograms = existingPrograms.Values.Count(p => p.IsActive) + programCounts.Created;

        audit.Stage(scope, CharityAuditActions.CharityRefSync, CharityAuditTargets.CharityRef, null,
            $"從官網更新清單：公益團體 新增 {charityCounts.Created}、更新 {charityCounts.Updated}、停用 {charityCounts.Deactivated}、恢復 {charityCounts.Reactivated}；"
            + $"慈善計畫 新增 {programCounts.Created}、更新 {programCounts.Updated}、停用 {programCounts.Deactivated}、恢復 {programCounts.Reactivated}",
            null, sourceIp);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminCharityRefSyncResultDto(
            now,
            charityCounts.ToDto(activeCharities),
            programCounts.ToDto(activePrograms));
    }

    private static void Deactivate(CharityRef row, DateTime now, CharityAdminScope scope)
    {
        row.IsActive = false;
        row.DeactivatedAt = now;
        row.LastSyncedAt = now;
        row.UpdatedAt = now;
        row.UpdatedBy = scope.Identity.AdminUserId;
    }

    private static void Deactivate(CharityProgramRef row, DateTime now, CharityAdminScope scope)
    {
        row.IsActive = false;
        row.DeactivatedAt = now;
        row.LastSyncedAt = now;
        row.UpdatedAt = now;
        row.UpdatedBy = scope.Identity.AdminUserId;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 抓取與驗證
    // ═══════════════════════════════════════════════════════════════════════

    private sealed record DirectoryResponse(List<IncomingCharity>? Charities, List<IncomingProgram>? Programs);

    private sealed record IncomingCharity(Guid Id, string? Slug, string? Status, string? NameZh, string? NameEn);

    private sealed record IncomingProgram(Guid Id, string? Slug, Guid CharityId, string? Status, string? NameZh, string? NameEn);

    /// <summary>驗證後的清單：只含已公開者，名稱已去除前後空白、英文空白為 <c>null</c>。</summary>
    private sealed record ValidDirectory(List<ValidCharity> Charities, List<ValidProgram> Programs);

    private sealed record ValidCharity(Guid Id, string NameZh, string? NameEn);

    private sealed record ValidProgram(Guid Id, Guid CharityId, string NameZh, string? NameEn);

    private async Task<ValidDirectory> FetchAsync(string baseUrl, CancellationToken cancellationToken)
    {
        DirectoryResponse? parsed;
        try
        {
            using var client = httpClientFactory.CreateClient(ExternalHttp.CharityMainSite);
            using var request = new HttpRequestMessage(HttpMethod.Get, baseUrl + DirectoryPath);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning("慈善複本同步：主站公開清單回應 {Status}", (int)response.StatusCode);
                throw new CharityBadGatewayException("官網目前無法提供清單（回應異常），複本沒有任何變動，請稍後再試。");
            }

            var bytes = await ReadCappedAsync(response, cancellationToken);
            parsed = JsonSerializer.Deserialize<DirectoryResponse>(bytes, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "慈善複本同步：連不上主站公開清單");
            throw new CharityBadGatewayException("連不上官網，複本沒有任何變動，請稍後再試。");
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "慈善複本同步：主站公開清單逾時");
            throw new CharityBadGatewayException("官網回應逾時，複本沒有任何變動，請稍後再試。");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "慈善複本同步：主站公開清單不是合法的 JSON");
            throw new CharityBadGatewayException("官網回傳的清單格式不正確，複本沒有任何變動，請洽系統管理員。");
        }

        return Validate(parsed);
    }

    private static async Task<byte[]> ReadCappedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
        {
            throw new CharityBadGatewayException("官網回傳的清單過大，複本沒有任何變動，請洽系統管理員。");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxResponseBytes)
            {
                throw new CharityBadGatewayException("官網回傳的清單過大，複本沒有任何變動，請洽系統管理員。");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static CharityBadGatewayException Invalid(string detail)
        => new($"官網回傳的清單內容不正確（{detail}），複本沒有任何變動，請洽系統管理員。");

    /// <summary>
    /// 清單合法性：兩個陣列都必須存在（缺欄位代表打到錯的端點或版本，不可當成「全部下架」）、識別碼非空且不重複、繁中名稱必填且不超過欄位長度、
    /// 每個計畫的所屬公益團體必須在同一份清單裡。狀態不是 <c>published</c> 的項目視為「不公開」略過（契約目前只輸出已公開者）。
    /// 🔴 <b>清單為空是合法的</b>（主站把所有計畫下架了）——這時複本全部標為停用；因為只是標記、下次同步可恢復，不另設保險絲。
    /// </summary>
    private static ValidDirectory Validate(DirectoryResponse? parsed)
    {
        if (parsed?.Charities is null || parsed.Programs is null)
        {
            throw Invalid("缺少公益團體或慈善計畫清單");
        }

        if (parsed.Charities.Count > MaxItems || parsed.Programs.Count > MaxItems)
        {
            throw Invalid("筆數異常");
        }

        string? Name(string? raw, string label, bool required)
        {
            var value = raw?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                return required ? throw Invalid($"{label}缺少名稱") : null;
            }

            return value.Length > MaxNameLength ? throw Invalid($"{label}名稱過長") : value;
        }

        var charities = new List<ValidCharity>();
        var allCharityIds = new HashSet<Guid>();
        foreach (var c in parsed.Charities)
        {
            if (c is null || c.Id == Guid.Empty || !allCharityIds.Add(c.Id))
            {
                throw Invalid("公益團體識別碼缺漏或重複");
            }

            if (!string.Equals(c.Status, "published", StringComparison.Ordinal))
            {
                continue;
            }

            charities.Add(new ValidCharity(c.Id, Name(c.NameZh, "公益團體", true)!, Name(c.NameEn, "公益團體", false)));
        }

        var publicCharityIds = charities.Select(c => c.Id).ToHashSet();
        var programs = new List<ValidProgram>();
        var programIds = new HashSet<Guid>();
        foreach (var p in parsed.Programs)
        {
            if (p is null || p.Id == Guid.Empty || !programIds.Add(p.Id))
            {
                throw Invalid("慈善計畫識別碼缺漏或重複");
            }

            if (!allCharityIds.Contains(p.CharityId))
            {
                throw Invalid("慈善計畫找不到所屬的公益團體");
            }

            // 所屬公益團體不公開（或計畫本身不公開）→ 視為不公開，略過；不算清單錯誤。
            if (!string.Equals(p.Status, "published", StringComparison.Ordinal) || !publicCharityIds.Contains(p.CharityId))
            {
                continue;
            }

            programs.Add(new ValidProgram(p.Id, p.CharityId, Name(p.NameZh, "慈善計畫", true)!, Name(p.NameEn, "慈善計畫", false)));
        }

        return new ValidDirectory(charities, programs);
    }

    private sealed class Counter
    {
        public int Created;
        public int Updated;
        public int Deactivated;
        public int Reactivated;
        public int Unchanged;

        public AdminCharityRefSyncCountsDto ToDto(int activeTotal) => new(Created, Updated, Deactivated, Reactivated, Unchanged, activeTotal);
    }
}

/// <summary>N2「從官網更新清單」的結果。<c>SyncedAt</c> 是本次同步時間（UTC）。</summary>
public sealed record AdminCharityRefSyncResultDto(
    DateTime SyncedAt, AdminCharityRefSyncCountsDto Charities, AdminCharityRefSyncCountsDto Programs);

/// <summary>單一種類的異動筆數。<c>ActiveTotal</c> 是同步後<b>由官網同步而來</b>且啟用中的總數（不含同步機制之前的本機開發列）。</summary>
public sealed record AdminCharityRefSyncCountsDto(int Created, int Updated, int Deactivated, int Reactivated, int Unchanged, int ActiveTotal);
