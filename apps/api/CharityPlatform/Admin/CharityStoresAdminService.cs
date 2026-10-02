using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Data;
using Tcrfc.Api.CharityPlatform.Data.Entities;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.CharityPlatform.Storage;
using Tcrfc.Api.Common;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.CharityPlatform.Admin;

/// <summary>
/// N1 店家管理與 QR Code 的資料操作。每個方法第一個參數都是 <see cref="CharityAdminScope"/>——呼叫端必須先通過
/// <see cref="ICharityAdminAuthorizer"/>（型別層強制授權）。🔴 分潤百分比的設定是需獨立授權、每次寫稽核的操作：
/// 建立或更新時只要百分比<b>實際發生變化</b>，就要求額外持有 <see cref="CharityPermissions.StoreSharePct"/>，並在同一次儲存寫入稽核。
/// </summary>
public sealed class CharityStoresAdminService(
    CharityDbContext db, ICharityAdminAuthorizer authorizer, CharityAuditLogger audit, ICharityImageStorage images,
    IConfiguration configuration, IHostEnvironment environment)
{
    public async Task<PagedResult<AdminStoreListItemDto>> ListAsync(
        CharityAdminScope scope, string? keyword, string? status, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var (p, size) = PagingQuery.Normalize(page, pageSize, defaultPageSize: 20, maxPageSize: 100);

        var query = db.DonationStores.AsNoTracking().AsQueryable();
        if (status is "active" or "inactive")
        {
            query = query.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(s =>
                s.StoreSlug.StartsWith(k)
                || (s.Category != null && s.Category.Contains(k))
                || (s.ContactName != null && s.ContactName.Contains(k))
                || s.DonationStoresI18ns.Any(i => i.Name.Contains(k)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(s => s.Status).ThenByDescending(s => s.CreatedAt).ThenBy(s => s.Seq)
            .Skip((p - 1) * size).Take(size)
            .Select(s => new StoreRow
            {
                Id = s.Id,
                Slug = s.StoreSlug,
                Category = s.Category,
                Address = s.Address,
                ContactName = s.ContactName,
                ContactPhone = s.ContactPhone,
                Status = s.Status,
                Pct = s.StoreSharePct,
                StartOn = s.StartOn,
                EndOn = s.EndOn,
                LogoKey = s.LogoKey,
                NameZh = s.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = s.DonationStoresI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                LogoAltZh = s.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.LogoAlt).FirstOrDefault(),
                LogoAltEn = s.DonationStoresI18ns.Where(i => i.Locale == "en").Select(i => i.LogoAlt).FirstOrDefault(),
                PaidCount = s.Donations.Count(d => d.Status == DonationStatus.Paid),
                PaidTotal = s.Donations.Where(d => d.Status == DonationStatus.Paid).Sum(d => (long?)d.Amount) ?? 0,
                ShareAccrued = s.Donations.Where(d => d.Status == DonationStatus.Paid).Sum(d => (long?)d.StoreAmount) ?? 0,
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminStoreListItemDto>
        {
            Items = rows.Select(ToListItem).ToList(),
            Page = p,
            PageSize = size,
            TotalCount = total,
        };
    }

    public async Task<AdminStoreDetailDto?> GetAsync(CharityAdminScope scope, Guid id, CancellationToken cancellationToken)
    {
        var row = await db.DonationStores.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StoreRow
            {
                Id = s.Id,
                Slug = s.StoreSlug,
                Category = s.Category,
                Address = s.Address,
                ContactName = s.ContactName,
                ContactPhone = s.ContactPhone,
                Status = s.Status,
                Pct = s.StoreSharePct,
                StartOn = s.StartOn,
                EndOn = s.EndOn,
                LogoKey = s.LogoKey,
                NameZh = s.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = s.DonationStoresI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                LogoAltZh = s.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.LogoAlt).FirstOrDefault(),
                LogoAltEn = s.DonationStoresI18ns.Where(i => i.Locale == "en").Select(i => i.LogoAlt).FirstOrDefault(),
                PaidCount = s.Donations.Count(d => d.Status == DonationStatus.Paid),
                PaidTotal = s.Donations.Where(d => d.Status == DonationStatus.Paid).Sum(d => (long?)d.Amount) ?? 0,
                ShareAccrued = s.Donations.Where(d => d.Status == DonationStatus.Paid).Sum(d => (long?)d.StoreAmount) ?? 0,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : ToDetail(row);
    }

    public async Task<AdminStoreDetailDto> CreateAsync(
        CharityAdminScope scope, UpsertStoreRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        var v = Validate(request);
        var pct = v.SharePct ?? 0m;
        if (pct != 0m)
        {
            await RequireSharePctPermissionAsync(scope, cancellationToken);
        }

        await EnsureShareCompatibleWithProjectsAsync(v.Status, pct, cancellationToken);

        var now = DateTime.UtcNow;
        var store = new DonationStore
        {
            Id = Guid.NewGuid(),
            Category = v.Category,
            Address = v.Address,
            ContactName = v.ContactName,
            ContactPhone = v.ContactPhone,
            StoreSharePct = pct,
            StartOn = v.StartOn,
            EndOn = v.EndOn,
            Status = v.Status,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId,
            UpdatedBy = scope.Identity.AdminUserId,
        };

        // slug 由系統產生；極小機率撞唯一鍵時重試（QR 網址不可由店家編號推導，也不接受外部指定）。
        for (var attempt = 0; ; attempt++)
        {
            store.StoreSlug = NewSlug();
            db.DonationStores.Add(store);
            ApplyI18n(store, v);
            if (pct != 0m)
            {
                audit.Stage(scope, CharityAuditActions.StoreSharePctSet, CharityAuditTargets.Store, store.Id, $"建立店家，店家分潤 0% → {pct}%", null, sourceIp);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt < 4)
            {
                db.ChangeTracker.Clear();
                store = CloneForRetry(store);
            }
        }

        return (await GetAsync(scope, store.Id, cancellationToken))!;
    }

    public async Task<AdminStoreDetailDto?> UpdateAsync(
        CharityAdminScope scope, Guid id, UpsertStoreRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        var store = await db.DonationStores.Include(s => s.DonationStoresI18ns).SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (store is null)
        {
            return null;
        }

        var v = Validate(request);
        var oldPct = store.StoreSharePct;
        var newPct = v.SharePct ?? oldPct;
        var newStatus = request.Status is null ? store.Status : v.Status;

        if (newPct != oldPct)
        {
            await RequireSharePctPermissionAsync(scope, cancellationToken);
        }

        await EnsureShareCompatibleWithProjectsAsync(newStatus, newPct, cancellationToken);

        store.Category = v.Category;
        store.Address = v.Address;
        store.ContactName = v.ContactName;
        store.ContactPhone = v.ContactPhone;
        store.StartOn = v.StartOn;
        store.EndOn = v.EndOn;
        store.Status = newStatus;
        store.StoreSharePct = newPct;
        store.UpdatedAt = DateTime.UtcNow;
        store.UpdatedBy = scope.Identity.AdminUserId;
        ApplyI18n(store, v);

        if (newPct != oldPct)
        {
            audit.Stage(scope, CharityAuditActions.StoreSharePctSet, CharityAuditTargets.Store, store.Id, $"店家分潤 {oldPct}% → {newPct}%（只影響之後的新捐款）", null, sourceIp);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    /// <summary>
    /// 重新產生 <c>store_slug</c>（規劃書 §2.3）：<b>舊 QR 立即失效</b>，須二次確認並記錄操作者（稽核）。
    /// 前台對「對不到有效店家」的處理是視同無店家歸屬，所以已印出的舊 QR 仍可開啟、仍可捐款，只是不再歸屬於這家店。
    /// </summary>
    public async Task<AdminStoreSlugResponse?> RegenerateSlugAsync(
        CharityAdminScope scope, Guid id, bool confirm, string sourceIp, CancellationToken cancellationToken)
    {
        if (!confirm)
        {
            throw new AdminValidationException("重新產生網址後，已經印出去的舊 QR Code 會立即失效，請先確認後再送出。");
        }

        var store = await db.DonationStores.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (store is null)
        {
            return null;
        }

        var oldSlug = store.StoreSlug;
        for (var attempt = 0; ; attempt++)
        {
            store.StoreSlug = NewSlug();
            store.UpdatedAt = DateTime.UtcNow;
            store.UpdatedBy = scope.Identity.AdminUserId;
            audit.Stage(scope, CharityAuditActions.StoreSlugRegenerate, CharityAuditTargets.Store, store.Id, $"重新產生店家網址：{oldSlug} → {store.StoreSlug}（舊 QR 立即失效）", null, sourceIp);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex) && attempt < 4)
            {
                // 撞唯一鍵：撤掉這次暫存的稽核與變更，換一個新的再試。
                db.ChangeTracker.Clear();
                store = await db.DonationStores.SingleAsync(s => s.Id == id, cancellationToken);
            }
        }

        return new AdminStoreSlugResponse(store.Id, store.StoreSlug, BuildQrTargetUrl(store.StoreSlug));
    }

    /// <summary>上傳店家 Logo 或照片（欄位直傳，規劃書 §4.0 通則）。新圖寫入成功才刪舊物件。</summary>
    public async Task<AdminStoreDetailDto?> SetLogoAsync(
        CharityAdminScope scope, Guid id, byte[] rawBytes, CancellationToken cancellationToken)
    {
        var store = await db.DonationStores.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (store is null)
        {
            return null;
        }

        var uploaded = await images.UploadAsync(rawBytes, $"stores/{id}/logo", cancellationToken);
        var oldKey = store.LogoKey;
        try
        {
            store.LogoKey = uploaded.Key;
            store.LogoWidth = uploaded.Width;
            store.LogoHeight = uploaded.Height;
            store.UpdatedAt = DateTime.UtcNow;
            store.UpdatedBy = scope.Identity.AdminUserId;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await images.DeleteAsync(uploaded.Key, CancellationToken.None); // 資料列寫入失敗：補償刪除剛上傳的物件
            throw;
        }

        await images.DeleteAsync(oldKey, CancellationToken.None);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<bool> RemoveLogoAsync(CharityAdminScope scope, Guid id, CancellationToken cancellationToken)
    {
        var store = await db.DonationStores.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (store is null)
        {
            return false;
        }

        var oldKey = store.LogoKey;
        store.LogoKey = null;
        store.LogoWidth = null;
        store.LogoHeight = null;
        store.UpdatedAt = DateTime.UtcNow;
        store.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        await images.DeleteAsync(oldKey, CancellationToken.None);
        return true;
    }

    /// <summary>QR 編碼用：店家 slug（找不到回傳 <c>null</c>）。</summary>
    public async Task<(string Slug, string? NameZh)?> GetQrSourceAsync(CharityAdminScope scope, Guid id, CancellationToken cancellationToken)
    {
        var row = await db.DonationStores.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.StoreSlug,
                NameZh = s.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.StoreSlug, row.NameZh);
    }

    /// <summary>批次匯出用：全部「合作中」店家的 slug 與店名。</summary>
    public async Task<IReadOnlyList<(Guid Id, string Slug, string? NameZh)>> ListActiveQrSourcesAsync(CharityAdminScope scope, CancellationToken cancellationToken)
    {
        var rows = await db.DonationStores.AsNoTracking()
            .Where(s => s.Status == "active")
            .OrderBy(s => s.Seq)
            .Select(s => new
            {
                s.Id,
                s.StoreSlug,
                NameZh = s.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return rows.Select(r => (r.Id, r.StoreSlug, r.NameZh)).ToList();
    }

    /// <summary>QR 編碼的目標網址（規劃書 §2.3：一律指向中文版）。前台網址尚未設定（非本機且沒設環境變數）時回傳 <c>null</c>。</summary>
    public string? BuildQrTargetUrl(string slug)
    {
        var baseUrl = CharityOptions.ResolvePublicBaseUrl(configuration, environment);
        return baseUrl is null ? null : $"{baseUrl}/zh/s/{slug}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 批次匯入店家（規劃書 §6.1「批次匯入店家資料（CSV）」）
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>匯入檔的表頭（依序，完全相符）。欄位順序與名稱是契約，後台「下載範本」給的就是這一列。</summary>
    public static readonly IReadOnlyList<string> ImportHeader =
    [
        "店家名稱（繁中）", "店家名稱（英文）", "類別", "地址", "聯絡人", "電話", "合作開始日", "合作結束日", "狀態", "店家分潤（%）",
    ];

    public const int MaxImportRows = 500;

    /// <summary>範本 CSV（UTF-8 BOM）：表頭＋一列示範。</summary>
    public byte[] BuildImportTemplate(CharityAdminScope scope)
        => CsvUtils.ToUtf8BytesWithBom(CsvUtils.BuildCsv(new List<IEnumerable<string?>>
        {
            ImportHeader,
            new string?[] { "範例咖啡店", "Example Cafe", "餐飲", "台中市西區範例路 1 號", "王小明", "04-2222-3333", "2026-10-01", "2027-09-30", "合作中", "5" },
        }));

    /// <summary>
    /// 批次匯入店家。🔴 規則（規劃書只寫「可批次匯入（CSV）」，下面是執行層決定，寫在 README 與 docs/16）：
    /// <list type="bullet">
    /// <item><b>整批驗證、任一列有錯整批不寫入</b>（比照主站 FAQ 匯入）；錯誤逐列回報，列號與 Excel 一致。</item>
    /// <item><c>store_slug</c> 一律由系統產生，檔案裡沒有這一欄（QR 網址不可由外部指定，規劃書 §2.3）。</item>
    /// <item><b>重複判定</b>＝繁中店名與地址（去空白、不分大小寫）都相同，對照<b>既有全部店家（含已停止）</b>與<b>檔案裡較前面的列</b>。
    /// 因為 slug 是系統產生的，沒有可靠的自然鍵能做「更新」，所以匯入只做<b>新增</b>；同一份檔案重複匯入會被重複判定擋下，不會產生重複店家（冪等）。
    /// 預設重複視為錯誤；<c>skipDuplicates=true</c> 時重複的列改為略過並回報。</item>
    /// <item>分潤欄位非 0 的列，操作者需持有「設定店家分潤」權限（否則整批 403），並為每家店各寫一筆分潤稽核；另受「店家＋項目 ≤ 100%」限制。</item>
    /// <item>一次最多 <see cref="MaxImportRows"/> 家。</item>
    /// </list>
    /// </summary>
    public async Task<AdminStoreImportResultDto> ImportCsvAsync(
        CharityAdminScope scope, string csvContent, bool skipDuplicates, string sourceIp, CancellationToken cancellationToken)
    {
        var rows = CsvUtils.Parse(csvContent);
        if (rows.Count == 0)
        {
            throw new AdminValidationException("檔案是空的，找不到任何資料列。");
        }

        if (rows[0].Count != ImportHeader.Count || !rows[0].Select(h => h.Trim()).SequenceEqual(ImportHeader, StringComparer.Ordinal))
        {
            throw new AdminValidationException($"檔案格式不正確，表頭必須依序是「{string.Join("、", ImportHeader)}」（可下載範本）。");
        }

        if (rows.Count - 1 > MaxImportRows)
        {
            throw new AdminValidationException($"一次最多匯入 {MaxImportRows} 家店家，這份檔案有 {rows.Count - 1} 列，請分批匯入。");
        }

        if (rows.Count == 1)
        {
            throw new AdminValidationException("檔案裡只有表頭，沒有任何店家資料。");
        }

        var errors = new List<AdminStoreImportRowIssueDto>();
        var skipped = new List<AdminStoreImportRowIssueDto>();
        var parsed = new List<(int RowNumber, Validated Value)>();
        var maxProjectPct = await db.DonationProjects.AsNoTracking().Select(p => (decimal?)p.ProjectSharePct).MaxAsync(cancellationToken) ?? 0m;

        // 既有店家的（繁中店名＋地址）指紋，含已停止的。
        var existing = await db.DonationStores.AsNoTracking()
            .Select(s => new { s.Address, Name = s.DonationStoresI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault() })
            .ToListAsync(cancellationToken);
        var seen = new Dictionary<string, int?>(StringComparer.Ordinal);
        foreach (var e in existing.Where(e => e.Name is not null))
        {
            seen.TryAdd(Fingerprint(e.Name!, e.Address), null);
        }

        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var cells = rows[i];
            if (cells.Count != ImportHeader.Count)
            {
                errors.Add(new(rowNumber, $"欄位數不正確，應為 {ImportHeader.Count} 欄，實際 {cells.Count} 欄。"));
                continue;
            }

            var problems = new List<string>();
            string? Text(int col, string label, int max, bool required = false)
            {
                try
                {
                    return required ? AdminInput.RequireText(cells[col], label, max) : AdminInput.OptionalText(cells[col], label, max);
                }
                catch (AdminValidationException ex)
                {
                    problems.Add(ex.Message);
                    return null;
                }
            }

            var nameZh = Text(0, "店家名稱（繁中）", 128, required: true);
            var nameEn = Text(1, "店家名稱（英文）", 128);
            var category = Text(2, "類別", 64);
            var address = Text(3, "地址", 500);
            var contact = Text(4, "聯絡人", 64);
            var phone = Text(5, "電話", 32);
            var startOn = ParseImportDate(cells[6], "合作開始日", problems);
            var endOn = ParseImportDate(cells[7], "合作結束日", problems);
            if (startOn is not null && endOn is not null && endOn < startOn)
            {
                problems.Add("合作結束日不可早於開始日。");
            }

            var statusText = cells[8].Trim();
            string? status = statusText switch
            {
                "" or "合作中" or "active" => "active",
                "已停止" or "inactive" => "inactive",
                _ => null,
            };
            if (status is null)
            {
                problems.Add("狀態只能填「合作中」或「已停止」（留空視為合作中）。");
            }

            decimal? pct = 0m;
            var pctText = cells[9].Trim().TrimEnd('%').Trim();
            if (pctText.Length > 0)
            {
                if (!decimal.TryParse(pctText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsedPct)
                    || parsedPct < 0m || parsedPct > 100m || decimal.Round(parsedPct, 2) != parsedPct)
                {
                    problems.Add("店家分潤須介於 0 到 100 之間，最多兩位小數。");
                    pct = null;
                }
                else
                {
                    pct = parsedPct;
                }
            }

            if (status == "active" && pct is { } p && p > 0m && p + maxProjectPct > 100m)
            {
                problems.Add($"店家分潤 {p}% 加上項目分潤（目前最高 {maxProjectPct}%）超過 100%。");
            }

            if (problems.Count > 0 || nameZh is null || status is null || pct is null)
            {
                errors.Add(new(rowNumber, string.Join("；", problems)));
                continue;
            }

            var key = Fingerprint(nameZh, address);
            if (seen.TryGetValue(key, out var firstRow))
            {
                var reason = firstRow is { } earlier ? $"與檔案第 {earlier} 列重複（店名與地址相同）。" : "與既有店家重複（店名與地址相同）。";
                (skipDuplicates ? skipped : errors).Add(new(rowNumber, reason));
                continue;
            }

            seen[key] = rowNumber;
            parsed.Add((rowNumber, new Validated(nameZh, nameEn, null, null, category, address, contact, phone, startOn, endOn, status, pct)));
        }

        if (errors.Count > 0)
        {
            return new AdminStoreImportResultDto { ImportedCount = 0, Errors = errors, Skipped = skipped };
        }

        if (parsed.Any(r => r.Value.SharePct is > 0m))
        {
            await RequireSharePctPermissionAsync(scope, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var usedSlugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, v) in parsed)
        {
            var store = new DonationStore
            {
                Id = Guid.NewGuid(),
                Category = v.Category,
                Address = v.Address,
                ContactName = v.ContactName,
                ContactPhone = v.ContactPhone,
                StoreSharePct = v.SharePct ?? 0m,
                StartOn = v.StartOn,
                EndOn = v.EndOn,
                Status = v.Status,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = scope.Identity.AdminUserId,
                UpdatedBy = scope.Identity.AdminUserId,
            };
            do
            {
                store.StoreSlug = NewSlug();
            }
            while (!usedSlugs.Add(store.StoreSlug));

            db.DonationStores.Add(store);
            db.DonationStoresI18ns.Add(new DonationStoresI18n { DonationStoreId = store.Id, Locale = RequestLocale.DefaultDbLocale, Name = v.NameZh });
            if (v.NameEn is not null)
            {
                db.DonationStoresI18ns.Add(new DonationStoresI18n { DonationStoreId = store.Id, Locale = "en", Name = v.NameEn });
            }

            if (store.StoreSharePct != 0m)
            {
                audit.Stage(scope, CharityAuditActions.StoreSharePctSet, CharityAuditTargets.Store, store.Id, $"批次匯入建立店家，店家分潤 0% → {store.StoreSharePct}%", null, sourceIp);
            }
        }

        // 極小機率：新產生的 slug 撞到既有店家（80 bits 亂數，實務上不會發生；撞了就整批失敗，由唯一鍵擋下，不靜默吞掉）。
        audit.Stage(scope, CharityAuditActions.StoreImport, CharityAuditTargets.Store, null,
            $"批次匯入店家 {parsed.Count} 家（略過重複 {skipped.Count} 列）", null, sourceIp);
        await db.SaveChangesAsync(cancellationToken);

        return new AdminStoreImportResultDto { ImportedCount = parsed.Count, Errors = [], Skipped = skipped };
    }

    private static string Fingerprint(string nameZh, string? address)
        => $"{nameZh.Trim().ToLowerInvariant()}|{(address ?? string.Empty).Trim().ToLowerInvariant()}";

    private static DateOnly? ParseImportDate(string raw, string label, List<string> problems)
    {
        var text = raw.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (DateOnly.TryParseExact(text, ["yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd", "yyyy/M/d"], System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }

        problems.Add($"{label}格式不正確，請填 2026-10-01 這樣的日期。");
        return null;
    }

    // ───────────────────────────────────────────────────────────────────────

    private async Task RequireSharePctPermissionAsync(CharityAdminScope scope, CancellationToken cancellationToken)
    {
        if (!await authorizer.HasAdditionalPermissionAsync(scope, CharityPermissions.StoreSharePct, cancellationToken))
        {
            throw new AdminForbiddenException("設定店家分潤比例需要額外的授權，請洽系統管理員。");
        }
    }

    /// <summary>
    /// 規劃書 §6.2：<c>store_share_pct + project_share_pct &lt;= 100%</c>。捐款是「某家店 × 某個項目」的組合，所以店家這邊儲存時，
    /// 要確認店家分潤加上<b>所有項目中最高的項目分潤</b>不超過 100%（只擋「合作中」的店家——已停止的店家不會有新捐款歸屬；
    /// 重新啟用時 <paramref name="status"/> 是 <c>active</c>，同樣會被檢查）。
    /// </summary>
    private async Task EnsureShareCompatibleWithProjectsAsync(string status, decimal storePct, CancellationToken cancellationToken)
    {
        if (status != "active" || storePct == 0m)
        {
            return;
        }

        var maxProjectPct = await db.DonationProjects.AsNoTracking().Select(p => (decimal?)p.ProjectSharePct).MaxAsync(cancellationToken) ?? 0m;
        if (storePct + maxProjectPct > 100m)
        {
            throw new AdminValidationException(
                $"店家分潤 {storePct}% 加上項目分潤（目前最高 {maxProjectPct}%）超過 100%，請調低其中一個。");
        }
    }

    private sealed record Validated(
        string NameZh, string? NameEn, string? LogoAltZh, string? LogoAltEn, string? Category, string? Address,
        string? ContactName, string? ContactPhone, DateOnly? StartOn, DateOnly? EndOn, string Status, decimal? SharePct);

    private static Validated Validate(UpsertStoreRequest r)
    {
        var status = r.Status ?? "active";
        if (status is not ("active" or "inactive"))
        {
            throw new AdminValidationException("狀態只能是「合作中」或「已停止」。");
        }

        AdminInput.DateRange(r.StartOn, r.EndOn, "合作期間");

        if (r.StoreSharePct is { } pct)
        {
            ValidatePct(pct, "店家分潤");
        }

        return new Validated(
            AdminInput.RequireText(r.NameZh, "店家名稱（繁中）", 128),
            AdminInput.OptionalText(r.NameEn, "店家名稱（英文）", 128),
            AdminInput.OptionalText(r.LogoAltZh, "Logo 替代文字（繁中）", 255),
            AdminInput.OptionalText(r.LogoAltEn, "Logo 替代文字（英文）", 255),
            AdminInput.OptionalText(r.Category, "類別", 64),
            AdminInput.OptionalText(r.Address, "地址", 500),
            AdminInput.OptionalText(r.ContactName, "聯絡人", 64),
            AdminInput.OptionalText(r.ContactPhone, "電話", 32),
            r.StartOn, r.EndOn, status, r.StoreSharePct);
    }

    internal static void ValidatePct(decimal pct, string label)
    {
        if (pct < 0m || pct > 100m || decimal.Round(pct, 2) != pct)
        {
            throw new AdminValidationException($"{label}須介於 0 到 100 之間，最多兩位小數。");
        }
    }

    private void ApplyI18n(DonationStore store, Validated v)
    {
        Upsert(store, RequestLocale.DefaultDbLocale, v.NameZh, v.LogoAltZh);
        if (v.NameEn is not null)
        {
            Upsert(store, "en", v.NameEn, v.LogoAltEn);
        }
        else
        {
            // 英文名稱清空＝刪掉英文列（欄位必須存在但可空：缺漏時前台回退繁中，規劃書 §2.4）。
            var en = store.DonationStoresI18ns.FirstOrDefault(i => i.Locale == "en");
            if (en is not null)
            {
                db.DonationStoresI18ns.Remove(en);
            }
        }
    }

    private void Upsert(DonationStore store, string locale, string name, string? logoAlt)
    {
        var row = store.DonationStoresI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            // 🔴 一律明確 Add（不只加進導覽集合）：避免子列被 EF 當成 Modified 而更新不存在的列。
            db.DonationStoresI18ns.Add(new DonationStoresI18n { DonationStoreId = store.Id, Locale = locale, Name = name, LogoAlt = logoAlt });
        }
        else
        {
            row.Name = name;
            row.LogoAlt = logoAlt;
        }
    }

    private static DonationStore CloneForRetry(DonationStore s) => new()
    {
        Id = s.Id,
        Category = s.Category,
        Address = s.Address,
        ContactName = s.ContactName,
        ContactPhone = s.ContactPhone,
        StoreSharePct = s.StoreSharePct,
        StartOn = s.StartOn,
        EndOn = s.EndOn,
        Status = s.Status,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
        CreatedBy = s.CreatedBy,
        UpdatedBy = s.UpdatedBy,
    };

    /// <summary>
    /// 不可推導的店家網址識別字串：80 bits 密碼學亂數（<see cref="RandomNumberGenerator"/>）轉 16 個小寫英數字元
    /// （去掉易混淆的字元）。與店家編號、建立時間完全無關，猜不到其他店家的網址（規劃書 §2.3）。
    /// </summary>
    internal static string NewSlug()
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789"; // 31 字元
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        var chars = new char[16];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length]; // 256 % 31 ≠ 0 的取餘偏差極小（每字元 < 1%），對「不可猜測」沒有實質影響
        }

        return new string(chars);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };

    private sealed class StoreRow
    {
        public Guid Id { get; init; }
        public string Slug { get; init; } = null!;
        public string? Category { get; init; }
        public string? Address { get; init; }
        public string? ContactName { get; init; }
        public string? ContactPhone { get; init; }
        public string Status { get; init; } = null!;
        public decimal Pct { get; init; }
        public DateOnly? StartOn { get; init; }
        public DateOnly? EndOn { get; init; }
        public string? LogoKey { get; init; }
        public string? NameZh { get; init; }
        public string? NameEn { get; init; }
        public string? LogoAltZh { get; init; }
        public string? LogoAltEn { get; init; }
        public int PaidCount { get; init; }
        public long PaidTotal { get; init; }
        public long ShareAccrued { get; init; }
    }

    private AdminStoreListItemDto ToListItem(StoreRow r) => new()
    {
        Id = r.Id, Slug = r.Slug, NameZh = r.NameZh, NameEn = r.NameEn, Category = r.Category, Status = r.Status,
        StoreSharePct = r.Pct, StartOn = r.StartOn, EndOn = r.EndOn, LogoUrl = images.Resolve(r.LogoKey),
        PaidCount = r.PaidCount, PaidTotal = r.PaidTotal, StoreShareAccrued = r.ShareAccrued,
    };

    private AdminStoreDetailDto ToDetail(StoreRow r) => new()
    {
        Id = r.Id, Slug = r.Slug, NameZh = r.NameZh, NameEn = r.NameEn, LogoAltZh = r.LogoAltZh, LogoAltEn = r.LogoAltEn,
        Category = r.Category, Address = r.Address, ContactName = r.ContactName, ContactPhone = r.ContactPhone,
        Status = r.Status, StoreSharePct = r.Pct, StartOn = r.StartOn, EndOn = r.EndOn, LogoUrl = images.Resolve(r.LogoKey),
        PaidCount = r.PaidCount, PaidTotal = r.PaidTotal, StoreShareAccrued = r.ShareAccrued,
        QrTargetUrl = BuildQrTargetUrl(r.Slug),
    };
}
