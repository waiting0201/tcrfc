using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// 301 轉址管理（<c>redirects</c>，S1-12，主站規劃書 §4.8 H）。資料表與唯一鍵
/// （<c>UQ_redirects_club_path</c> = <c>(club_id, from_path)</c>）已於既有 <c>db/club-schema.sql</c>
/// 完整存在，本輪只補後端 CRUD 與 CSV 批次匯入／匯出。
/// </summary>
public sealed class AdminRedirectsRepository(ClubDbContext dbContext)
{
    /// <summary>CSV 表頭（中文，比照 <c>AdminFaqsRepository</c> 既有慣例：後台介面與匯出檔一律
    /// 中文欄名，CLAUDE.md「CSV 匯出的欄位標題同此規則」）。</summary>
    private static readonly string[] CsvHeader = ["來源網址", "目的網址", "啟用狀態"];

    public async Task<PagedResult<AdminRedirectDto>> ListAsync(
        AdminClubScope scope, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Redirects.AsNoTracking().Where(r => r.ClubId == scope.ClubId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(r => r.FromPath.Contains(keyword) || r.ToPath.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(r => r.FromPath)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminRedirectDto>
        {
            Items = rows.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<AdminRedirectDto> CreateAsync(
        AdminClubScope scope, CreateRedirectRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        RedirectPathPolicy.Validate(request.FromPath, "來源網址");
        RedirectPathPolicy.Validate(request.ToPath, "目的網址");

        if (string.Equals(request.FromPath, request.ToPath, StringComparison.Ordinal))
        {
            throw new AdminSeoValidationException("來源網址與目的網址不能相同，那不是一筆有意義的轉址。");
        }

        if (await dbContext.Redirects.AsNoTracking()
            .AnyAsync(r => r.ClubId == scope.ClubId && r.FromPath == request.FromPath, cancellationToken))
        {
            throw new RedirectFromPathConflictException(request.FromPath);
        }

        var now = DateTime.UtcNow;
        var redirect = new Redirect
        {
            Id = Guid.NewGuid(),
            ClubId = scope.ClubId,
            FromPath = request.FromPath,
            ToPath = request.ToPath,
            IsActive = request.IsActive,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        dbContext.Redirects.Add(redirect);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(redirect);
    }

    public async Task<AdminRedirectDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateRedirectRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        RedirectPathPolicy.Validate(request.ToPath, "目的網址");

        var redirect = await dbContext.Redirects.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (redirect is null || redirect.ClubId != scope.ClubId)
        {
            return null;
        }

        if (string.Equals(redirect.FromPath, request.ToPath, StringComparison.Ordinal))
        {
            throw new AdminSeoValidationException("來源網址與目的網址不能相同，那不是一筆有意義的轉址。");
        }

        redirect.ToPath = request.ToPath;
        redirect.IsActive = request.IsActive;
        redirect.UpdatedAt = DateTime.UtcNow;
        redirect.UpdatedBy = operatorId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(redirect);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var redirect = await dbContext.Redirects.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (redirect is null || redirect.ClubId != scope.ClubId)
        {
            return false;
        }

        dbContext.Redirects.Remove(redirect);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>匯出目前這個俱樂部的全部轉址規則（含停用的）——下載回去編輯後可以直接整份
    /// 重新匯入，見 <see cref="ImportCsvAsync"/>。回傳純文字，BOM 編碼比照
    /// <c>AdminFaqsRepository.ExportCsvAsync</c> 既有慣例交給呼叫端（<c>AdminRedirectsEndpoints</c>）
    /// 處理，不在 repository 層做位元組編碼。</summary>
    public async Task<string> ExportCsvAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var redirects = await dbContext.Redirects.AsNoTracking()
            .Where(r => r.ClubId == scope.ClubId)
            .OrderBy(r => r.FromPath)
            .ToListAsync(cancellationToken);

        var rows = new List<IEnumerable<string?>> { CsvHeader };
        rows.AddRange(redirects.Select(r => new[] { r.FromPath, r.ToPath, r.IsActive ? "啟用" : "停用" }));

        return CsvUtils.BuildCsv(rows);
    }

    /// <summary>
    /// CSV 批次匯入（規劃書 §4.8 H「301 轉址管理…含批次匯入」；<c>Common/CsvUtils.cs</c> 檔頭
    /// 本來就預告本項會重用它）。**Upsert 鍵是 <c>(club_id, from_path)</c>**（跟
    /// <c>UQ_redirects_club_path</c> 一致）：CSV 裡的來源網址若已存在就整列覆寫
    /// <c>to_path</c>／<c>is_active</c>，否則新增——比照 <c>AdminFaqsRepository.ImportCsvAsync</c>
    /// 的既有語意（整批驗證、任一列有誤就整批不寫入），不是比照 <c>AdminMatchesRepository</c>
    /// 那種純建立式匯入，理由：轉址表本來就是「拿現況修一修再整批回貼」的使用情境（先
    /// <see cref="ExportCsvAsync"/> 匯出、編輯、再匯入），upsert 才符合這個工作流程。
    /// </summary>
    public async Task<RedirectCsvImportResultDto> ImportCsvAsync(
        AdminClubScope scope, string csvContent, Guid? operatorId, CancellationToken cancellationToken)
    {
        var rows = CsvUtils.Parse(csvContent);
        if (rows.Count == 0)
        {
            throw new AdminSeoValidationException("檔案是空的，找不到任何資料列。");
        }

        var header = rows[0];
        if (header.Count != CsvHeader.Length || !header.SequenceEqual(CsvHeader, StringComparer.Ordinal))
        {
            throw new AdminSeoValidationException($"檔案格式不正確，表頭必須依序是「{string.Join("、", CsvHeader)}」。");
        }

        var errors = new List<RedirectCsvImportRowErrorDto>();
        var parsedRows = new List<(string FromPath, string ToPath, bool IsActive)>();
        var seenFromPaths = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1; // 表頭是第 1 行，第一筆資料是第 2 行。
            var row = rows[i];

            if (row.Count != CsvHeader.Length)
            {
                errors.Add(new RedirectCsvImportRowErrorDto
                {
                    RowNumber = rowNumber,
                    Reason = $"欄位數不正確，應為 {CsvHeader.Length} 欄，實際 {row.Count} 欄。",
                });
                continue;
            }

            var fromPath = row[0].Trim();
            var toPath = row[1].Trim();
            var activeText = row[2].Trim();

            var rowErrors = new List<string>();

            try
            {
                RedirectPathPolicy.Validate(fromPath, "來源網址");
            }
            catch (AdminSeoValidationException ex)
            {
                rowErrors.Add(ex.Message);
            }

            try
            {
                RedirectPathPolicy.Validate(toPath, "目的網址");
            }
            catch (AdminSeoValidationException ex)
            {
                rowErrors.Add(ex.Message);
            }

            if (fromPath.Length > 0 && !seenFromPaths.Add(fromPath))
            {
                rowErrors.Add($"來源網址「{fromPath}」在檔案中重複出現，同一份檔案裡的來源網址不能重複。");
            }

            if (fromPath.Length > 0 && string.Equals(fromPath, toPath, StringComparison.Ordinal))
            {
                rowErrors.Add("來源網址與目的網址不能相同。");
            }

            var isActive = activeText switch
            {
                "啟用" => true,
                "停用" => false,
                _ => (bool?)null,
            };
            if (isActive is null)
            {
                rowErrors.Add("啟用狀態欄位必須是「啟用」或「停用」。");
            }

            if (rowErrors.Count > 0)
            {
                errors.Add(new RedirectCsvImportRowErrorDto { RowNumber = rowNumber, Reason = string.Join("；", rowErrors) });
                continue;
            }

            parsedRows.Add((fromPath, toPath, isActive!.Value));
        }

        if (errors.Count > 0)
        {
            // 🔴 任一列有錯就整批不寫入：這裡完全沒有呼叫過任何寫入方法，直接回傳即可。
            return new RedirectCsvImportResultDto { ImportedCount = 0, Errors = errors };
        }

        var now = DateTime.UtcNow;
        foreach (var parsed in parsedRows)
        {
            var existing = await dbContext.Redirects
                .FirstOrDefaultAsync(r => r.ClubId == scope.ClubId && r.FromPath == parsed.FromPath, cancellationToken);

            var redirect = existing ?? new Redirect
            {
                Id = Guid.NewGuid(),
                ClubId = scope.ClubId,
                FromPath = parsed.FromPath,
                CreatedAt = now,
                CreatedBy = operatorId,
            };

            if (existing is null)
            {
                dbContext.Redirects.Add(redirect);
            }

            redirect.ToPath = parsed.ToPath;
            redirect.IsActive = parsed.IsActive;
            redirect.UpdatedAt = now;
            redirect.UpdatedBy = operatorId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RedirectCsvImportResultDto { ImportedCount = parsedRows.Count, Errors = [] };
    }

    private static AdminRedirectDto ToDto(Redirect redirect) => new()
    {
        Id = redirect.Id,
        FromPath = redirect.FromPath,
        ToPath = redirect.ToPath,
        IsActive = redirect.IsActive,
        UpdatedAt = redirect.UpdatedAt,
    };
}
