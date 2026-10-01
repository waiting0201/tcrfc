using System.Text.Json;
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
/// N2 捐款項目管理的資料操作。🔴 分潤百分比同樣是需獨立授權、每次寫稽核的操作（見 <see cref="CharityStoresAdminService"/>）；
/// 🔴 儲存時驗證「店家分潤 + 項目分潤 ≤ 100%」（規劃書 §6.2：超過即擋下並提示）。
/// </summary>
public sealed class CharityProjectsAdminService(
    CharityDbContext db, ICharityAdminAuthorizer authorizer, CharityAuditLogger audit, ICharityImageStorage images)
{
    private const int MaxAmountOptions = 12;
    private const int MaxDescriptionBytes = 200_000;

    public async Task<IReadOnlyList<AdminProjectListItemDto>> ListAsync(CharityAdminScope scope, string? status, CancellationToken cancellationToken)
    {
        var query = db.DonationProjects.AsNoTracking().AsQueryable();
        if (status is "draft" or "published")
        {
            query = query.Where(p => p.Status == status);
        }

        var rows = await query
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Seq)
            .Select(p => new
            {
                p.Id,
                p.ProjectSlug,
                p.Status,
                p.SortOrder,
                p.InvoiceMode,
                p.ProjectSharePct,
                p.MinAmount,
                p.MaxAmount,
                p.CoverKey,
                p.CharityNameSnapshot,
                NameZh = p.DonationProjectsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                NameEn = p.DonationProjectsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new AdminProjectListItemDto
        {
            Id = r.Id,
            Slug = r.ProjectSlug,
            NameZh = r.NameZh,
            NameEn = r.NameEn,
            Status = r.Status,
            SortOrder = r.SortOrder,
            InvoiceMode = r.InvoiceMode,
            ProjectSharePct = r.ProjectSharePct,
            MinAmount = r.MinAmount,
            MaxAmount = r.MaxAmount,
            CoverUrl = images.Resolve(r.CoverKey),
            CharityName = r.CharityNameSnapshot,
        }).ToList();
    }

    public async Task<AdminProjectDetailDto?> GetAsync(CharityAdminScope scope, Guid id, CancellationToken cancellationToken)
    {
        var p = await db.DonationProjects.AsNoTracking()
            .Include(x => x.DonationProjectsI18ns)
            .Include(x => x.DonationAmountOptions)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return p is null ? null : ToDetail(p);
    }

    public async Task<AdminProjectDetailDto> CreateAsync(
        CharityAdminScope scope, UpsertProjectRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        var v = Validate(request);
        var pct = v.SharePct ?? 0m;
        if (pct != 0m)
        {
            await RequireSharePctPermissionAsync(scope, cancellationToken);
        }

        await EnsureShareCompatibleWithStoresAsync(pct, cancellationToken);
        var refs = await ResolveRefsAsync(request.CharityRefCode, request.CharityProgramRefCode, cancellationToken);

        var slug = v.Slug ?? AdminInput.GenerateSlug("project", v.NameEn);
        if (await db.DonationProjects.AnyAsync(p => p.ProjectSlug == slug, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經有其他項目使用，請換一個。");
        }

        var now = DateTime.UtcNow;
        var maxSort = await db.DonationProjects.AsNoTracking().Select(p => (int?)p.SortOrder).MaxAsync(cancellationToken) ?? 0;
        var project = new DonationProject
        {
            Id = Guid.NewGuid(),
            ProjectSlug = slug,
            InvoiceMode = v.InvoiceMode,
            ProjectSharePct = pct,
            MinAmount = v.MinAmount,
            MaxAmount = v.MaxAmount,
            SortOrder = request.SortOrder ?? maxSort + 1,
            Status = "draft",
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = scope.Identity.AdminUserId,
            UpdatedBy = scope.Identity.AdminUserId,
        };
        ApplyRefs(project, refs);
        db.DonationProjects.Add(project);
        ApplyI18n(project, v);
        ReplaceAmountOptions(project, v.AmountOptions ?? [], scope);

        if (pct != 0m)
        {
            audit.Stage(scope, CharityAuditActions.ProjectSharePctSet, CharityAuditTargets.Project, project.Id, $"建立項目，項目分潤 0% → {pct}%", null, sourceIp);
        }

        await SaveAsync(cancellationToken);
        return (await GetAsync(scope, project.Id, cancellationToken))!;
    }

    public async Task<AdminProjectDetailDto?> UpdateAsync(
        CharityAdminScope scope, Guid id, UpsertProjectRequest request, string sourceIp, CancellationToken cancellationToken)
    {
        var project = await db.DonationProjects
            .Include(p => p.DonationProjectsI18ns)
            .Include(p => p.DonationAmountOptions)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var v = Validate(request);
        var oldPct = project.ProjectSharePct;
        var newPct = v.SharePct ?? oldPct;
        if (newPct != oldPct)
        {
            await RequireSharePctPermissionAsync(scope, cancellationToken);
        }

        await EnsureShareCompatibleWithStoresAsync(newPct, cancellationToken);
        var refs = await ResolveRefsAsync(request.CharityRefCode, request.CharityProgramRefCode, cancellationToken);

        if (v.Slug is not null && v.Slug != project.ProjectSlug)
        {
            if (await db.DonationProjects.AnyAsync(p => p.ProjectSlug == v.Slug && p.Id != id, cancellationToken))
            {
                throw new AdminConflictException("網址名稱重複", $"網址名稱「{v.Slug}」已經有其他項目使用，請換一個。");
            }

            project.ProjectSlug = v.Slug;
        }

        project.InvoiceMode = v.InvoiceMode;
        project.ProjectSharePct = newPct;
        project.MinAmount = v.MinAmount;
        project.MaxAmount = v.MaxAmount;
        if (request.SortOrder is { } sort)
        {
            project.SortOrder = sort;
        }

        ApplyRefs(project, refs);
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = scope.Identity.AdminUserId;
        ApplyI18n(project, v);
        if (v.AmountOptions is not null)
        {
            ReplaceAmountOptions(project, v.AmountOptions, scope);
        }

        if (newPct != oldPct)
        {
            audit.Stage(scope, CharityAuditActions.ProjectSharePctSet, CharityAuditTargets.Project, project.Id, $"項目分潤 {oldPct}% → {newPct}%（只影響之後的新捐款）", null, sourceIp);
        }

        await SaveAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    /// <summary>上架／下架。上架前必須有繁中名稱（前台沒有名稱的項目不會輸出）。</summary>
    public async Task<AdminProjectDetailDto?> SetPublishedAsync(CharityAdminScope scope, Guid id, bool published, CancellationToken cancellationToken)
    {
        var project = await db.DonationProjects.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return null;
        }

        if (published && !await db.DonationProjectsI18ns.AnyAsync(i => i.DonationProjectId == id && i.Locale == RequestLocale.DefaultDbLocale && i.Name != "", cancellationToken))
        {
            throw new AdminValidationException("上架前請先填寫項目名稱（繁中）。");
        }

        project.Status = published ? "published" : "draft";
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<AdminProjectDetailDto?> SetCoverAsync(CharityAdminScope scope, Guid id, byte[] rawBytes, CancellationToken cancellationToken)
    {
        var project = await db.DonationProjects.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var uploaded = await images.UploadAsync(rawBytes, $"projects/{id}/cover", cancellationToken);
        var oldKey = project.CoverKey;
        try
        {
            project.CoverKey = uploaded.Key;
            project.CoverWidth = uploaded.Width;
            project.CoverHeight = uploaded.Height;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = scope.Identity.AdminUserId;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await images.DeleteAsync(uploaded.Key, CancellationToken.None);
            throw;
        }

        await images.DeleteAsync(oldKey, CancellationToken.None);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<bool> RemoveCoverAsync(CharityAdminScope scope, Guid id, CancellationToken cancellationToken)
    {
        var project = await db.DonationProjects.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return false;
        }

        var oldKey = project.CoverKey;
        project.CoverKey = null;
        project.CoverWidth = null;
        project.CoverHeight = null;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        await images.DeleteAsync(oldKey, CancellationToken.None);
        return true;
    }

    /// <summary>撥付對象與慈善計畫的候選清單（唯讀複本，N2 下拉選單用）。</summary>
    public async Task<AdminCharityRefOptionsDto> ListCharityRefsAsync(CharityAdminScope scope, CancellationToken cancellationToken)
    {
        var charities = await db.CharityRefs.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new AdminCharityRefDto(c.RefCode, c.Name)).ToListAsync(cancellationToken);
        var programs = await db.CharityProgramRefs.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new AdminCharityProgramRefDto(c.RefCode, c.Name, c.CharityRef.RefCode)).ToListAsync(cancellationToken);
        return new AdminCharityRefOptionsDto(charities, programs);
    }

    // ───────────────────────────────────────────────────────────────────────

    private async Task RequireSharePctPermissionAsync(CharityAdminScope scope, CancellationToken cancellationToken)
    {
        if (!await authorizer.HasAdditionalPermissionAsync(scope, CharityPermissions.ProjectSharePct, cancellationToken))
        {
            throw new AdminForbiddenException("設定項目分潤比例需要額外的授權，請洽系統管理員。");
        }
    }

    /// <summary>項目這邊儲存時：項目分潤加上<b>合作中店家中最高的店家分潤</b>不得超過 100%（規劃書 §6.2）。</summary>
    private async Task EnsureShareCompatibleWithStoresAsync(decimal projectPct, CancellationToken cancellationToken)
    {
        if (projectPct == 0m)
        {
            return;
        }

        var maxStorePct = await db.DonationStores.AsNoTracking().Where(s => s.Status == "active")
            .Select(s => (decimal?)s.StoreSharePct).MaxAsync(cancellationToken) ?? 0m;
        if (projectPct + maxStorePct > 100m)
        {
            throw new AdminValidationException(
                $"項目分潤 {projectPct}% 加上店家分潤（合作中店家目前最高 {maxStorePct}%）超過 100%，請調低其中一個。");
        }
    }

    private sealed record Validated(
        string? Slug, string NameZh, string? NameEn, string? OneLinerZh, string? OneLinerEn, string? DescZh, string? DescEn,
        string? FundZh, string? FundEn, string? AltZh, string? AltEn, int? MinAmount, int? MaxAmount,
        IReadOnlyList<int>? AmountOptions, decimal? SharePct, string InvoiceMode);

    private static Validated Validate(UpsertProjectRequest r)
    {
        var invoiceMode = r.InvoiceMode;
        if (invoiceMode is null || !InvoiceModes.All.Contains(invoiceMode))
        {
            throw new AdminValidationException("請選擇憑證模式（電子發票或捐贈收據）。");
        }

        if (r.MinAmount is <= 0 || r.MaxAmount is <= 0)
        {
            throw new AdminValidationException("單筆金額的下限與上限都必須大於 0。");
        }

        if (r.MinAmount is { } min && r.MaxAmount is { } max && min > max)
        {
            throw new AdminValidationException("單筆金額下限不可大於上限。");
        }

        if (r.ProjectSharePct is { } pct)
        {
            CharityStoresAdminService.ValidatePct(pct, "項目分潤");
        }

        IReadOnlyList<int>? options = null;
        if (r.AmountOptions is not null)
        {
            if (r.AmountOptions.Count > MaxAmountOptions)
            {
                throw new AdminValidationException($"金額選項最多 {MaxAmountOptions} 組。");
            }

            var distinct = r.AmountOptions.Distinct().OrderBy(a => a).ToList();
            if (distinct.Count != r.AmountOptions.Count)
            {
                throw new AdminValidationException("金額選項不可重複。");
            }

            foreach (var amount in distinct)
            {
                if (amount <= 0 || (r.MinAmount is { } mn && amount < mn) || (r.MaxAmount is { } mx && amount > mx))
                {
                    throw new AdminValidationException("每組金額選項都必須大於 0，且落在單筆金額的下限與上限之內。");
                }
            }

            options = distinct;
        }

        string? slug = null;
        if (!string.IsNullOrWhiteSpace(r.Slug))
        {
            slug = AdminInput.Slug(r.Slug.Trim());
        }

        return new Validated(
            slug,
            AdminInput.RequireText(r.NameZh, "項目名稱（繁中）", 128),
            AdminInput.OptionalText(r.NameEn, "項目名稱（英文）", 128),
            AdminInput.OptionalText(r.OneLinerZh, "一句話說明（繁中）", 255),
            AdminInput.OptionalText(r.OneLinerEn, "一句話說明（英文）", 255),
            JsonText(r.DescriptionZh, "說明內文（繁中）"),
            JsonText(r.DescriptionEn, "說明內文（英文）"),
            BlankToNull(r.FundUsageZh), BlankToNull(r.FundUsageEn),
            AdminInput.OptionalText(r.CoverAltZh, "封面替代文字（繁中）", 255),
            AdminInput.OptionalText(r.CoverAltEn, "封面替代文字（英文）", 255),
            r.MinAmount, r.MaxAmount, options, r.ProjectSharePct, invoiceMode);
    }

    private static string? BlankToNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? JsonText(JsonElement? element, string label)
    {
        if (element is null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            // 原生 json 欄位只收物件或陣列（docs/18 E-111）
            throw new AdminValidationException($"{label}不是合法的區塊內容格式，請確認編輯器的輸出。");
        }

        var text = element.Value.GetRawText();
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxDescriptionBytes)
        {
            throw new AdminValidationException($"{label}太長，請精簡內容。");
        }

        return text;
    }

    private void ApplyI18n(DonationProject project, Validated v)
    {
        Upsert(project, RequestLocale.DefaultDbLocale, v.NameZh, v.OneLinerZh, v.DescZh, v.FundZh, v.AltZh);

        var anyEn = v.NameEn is not null || v.OneLinerEn is not null || v.DescEn is not null || v.FundEn is not null || v.AltEn is not null;
        if (anyEn)
        {
            // name 欄位是必填：英文內容有任何一欄時，沒填英文名稱就以繁中名稱頂著（前台本來就會回退，這裡只是滿足欄位限制）。
            Upsert(project, "en", v.NameEn ?? v.NameZh, v.OneLinerEn, v.DescEn, v.FundEn, v.AltEn);
        }
        else
        {
            var en = project.DonationProjectsI18ns.FirstOrDefault(i => i.Locale == "en");
            if (en is not null)
            {
                db.DonationProjectsI18ns.Remove(en);
            }
        }
    }

    private void Upsert(DonationProject project, string locale, string name, string? oneLiner, string? description, string? fundUsage, string? coverAlt)
    {
        var row = project.DonationProjectsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            db.DonationProjectsI18ns.Add(new DonationProjectsI18n
            {
                DonationProjectId = project.Id, Locale = locale, Name = name, OneLiner = oneLiner,
                Description = description, FundUsage = fundUsage, CoverAlt = coverAlt,
            });
        }
        else
        {
            row.Name = name;
            row.OneLiner = oneLiner;
            row.Description = description;
            row.FundUsage = fundUsage;
            row.CoverAlt = coverAlt;
        }
    }

    private void ReplaceAmountOptions(DonationProject project, IReadOnlyList<int> amounts, CharityAdminScope scope)
    {
        foreach (var existing in project.DonationAmountOptions.ToList())
        {
            db.DonationAmountOptions.Remove(existing);
        }

        var order = 0;
        foreach (var amount in amounts)
        {
            // 🔴 一律明確 Add：子列只加進導覽集合會被 EF 當成既有列（Modified），UPDATE 不存在的列。
            db.DonationAmountOptions.Add(new DonationAmountOption
            {
                Id = Guid.NewGuid(),
                DonationProjectId = project.Id,
                Amount = amount,
                SortOrder = order++,
                CreatedBy = scope.Identity.AdminUserId,
                UpdatedBy = scope.Identity.AdminUserId,
            });
        }
    }

    private sealed record Refs(string? CharityCode, string? CharityName, string? ProgramCode, string? ProgramName);

    /// <summary>
    /// 撥付對象與慈善計畫：只收參照碼，名稱從唯讀複本<b>值複製</b>進快照欄位（規劃書 §9.3、docs/16 §1）。
    /// 只選慈善計畫時，撥付對象自動帶出該計畫所屬的公益團體；兩者都選時必須一致。
    /// </summary>
    private async Task<Refs> ResolveRefsAsync(string? charityCode, string? programCode, CancellationToken cancellationToken)
    {
        charityCode = string.IsNullOrWhiteSpace(charityCode) ? null : charityCode.Trim();
        programCode = string.IsNullOrWhiteSpace(programCode) ? null : programCode.Trim();

        string? charityName = null;
        string? programName = null;

        if (programCode is not null)
        {
            var program = await db.CharityProgramRefs.AsNoTracking()
                .Where(p => p.RefCode == programCode)
                .Select(p => new { p.Name, CharityCode = p.CharityRef.RefCode, CharityName = p.CharityRef.Name })
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new AdminValidationException("找不到所選的慈善計畫，請重新選擇。");

            if (charityCode is not null && charityCode != program.CharityCode)
            {
                throw new AdminValidationException("所選的慈善計畫不屬於所選的受贈公益團體。");
            }

            charityCode = program.CharityCode;
            charityName = program.CharityName;
            programName = program.Name;
        }
        else if (charityCode is not null)
        {
            charityName = await db.CharityRefs.AsNoTracking().Where(c => c.RefCode == charityCode).Select(c => c.Name).SingleOrDefaultAsync(cancellationToken)
                ?? throw new AdminValidationException("找不到所選的受贈公益團體，請重新選擇。");
        }

        return new Refs(charityCode, charityName, programCode, programName);
    }

    private static void ApplyRefs(DonationProject project, Refs refs)
    {
        project.CharityRefCode = refs.CharityCode;
        project.CharityNameSnapshot = refs.CharityName;
        project.CharityProgramRefCode = refs.ProgramCode;
        project.CharityProgramNameSnapshot = refs.ProgramName;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new AdminConflictException("網址名稱重複", "這個網址名稱剛剛被其他項目使用了，請換一個再儲存。");
        }
    }

    private AdminProjectDetailDto ToDetail(DonationProject p)
    {
        var zh = p.DonationProjectsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = p.DonationProjectsI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminProjectDetailDto
        {
            Id = p.Id,
            Slug = p.ProjectSlug,
            NameZh = zh?.Name,
            NameEn = en?.Name,
            OneLinerZh = zh?.OneLiner,
            OneLinerEn = en?.OneLiner,
            DescriptionZh = ParseJson(zh?.Description),
            DescriptionEn = ParseJson(en?.Description),
            FundUsageZh = zh?.FundUsage,
            FundUsageEn = en?.FundUsage,
            CoverAltZh = zh?.CoverAlt,
            CoverAltEn = en?.CoverAlt,
            CoverUrl = images.Resolve(p.CoverKey),
            Status = p.Status,
            SortOrder = p.SortOrder,
            InvoiceMode = p.InvoiceMode,
            ProjectSharePct = p.ProjectSharePct,
            MinAmount = p.MinAmount,
            MaxAmount = p.MaxAmount,
            AmountOptions = p.DonationAmountOptions.OrderBy(o => o.Amount).Select(o => o.Amount).ToList(),
            CharityRefCode = p.CharityRefCode,
            CharityName = p.CharityNameSnapshot,
            CharityProgramRefCode = p.CharityProgramRefCode,
            CharityProgramName = p.CharityProgramNameSnapshot,
        };
    }

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record AdminCharityRefDto(string RefCode, string Name);

public sealed record AdminCharityProgramRefDto(string RefCode, string Name, string CharityRefCode);

public sealed record AdminCharityRefOptionsDto(IReadOnlyList<AdminCharityRefDto> Charities, IReadOnlyList<AdminCharityProgramRefDto> Programs);
