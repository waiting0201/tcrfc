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
