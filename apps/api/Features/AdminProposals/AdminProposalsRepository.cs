using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Documents;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminProposals;

/// <summary>
/// E3「提案簡介與下載追蹤」的提案端（主站規劃書 §4.5 E3「上傳提案 PDF（可多版本／多語系）」），產出前台 9.4 CTA
/// 「下載提案簡介」的檔案。<c>proposals.club_id</c> 必填。檔案放**私有容器**（<see cref="DocumentBucket.Private"/>），
/// 只能經公開端點填寫表單、取得限時下載連結後串流（見 <c>Features/Proposals</c>）——沒有公開網址。
/// 「設定下載表單欄位」由 G1 表單設計器處理（表單代碼 <c>proposal_download</c>），不在本模組重複提供。
/// </summary>
public sealed class AdminProposalsRepository(ClubDbContext dbContext, Caching.IQueryCache cache)
{
    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "draft", "published" };
    private static readonly HashSet<string> Locales = new(StringComparer.Ordinal) { "zh", "en" };

    public async Task<IReadOnlyList<AdminProposalListItemDto>> ListAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Proposals.AsNoTracking().Where(p => p.ClubId == scope.ClubId)
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => new
            {
                p.Id, p.Title, p.VersionNo, p.Status, p.UpdatedAt,
                Locales = p.ProposalFiles.Select(f => f.Locale).Distinct().ToList(),
                FileCount = p.ProposalFiles.Count,
                LeadCount = dbContext.Enquiries.Count(e => e.ProposalId == p.Id),
            }).ToListAsync(cancellationToken);

        return rows.Select(r => new AdminProposalListItemDto
        {
            Id = r.Id, Title = r.Title, VersionNo = r.VersionNo, Status = r.Status,
            Locales = r.Locales.Select(ToExternalLocale).OrderBy(l => l).ToList(),
            FileCount = r.FileCount, LeadCount = r.LeadCount, UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminProposalDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var proposal = await dbContext.Proposals.AsNoTracking().Include(p => p.ProposalFiles)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (proposal is null)
        {
            return null;
        }

        var leadCount = await dbContext.Enquiries.CountAsync(e => e.ProposalId == id, cancellationToken);
        return ToDetail(proposal, leadCount);
    }

    public async Task<AdminProposalDetailDto> CreateAsync(
        AdminClubScope scope, UpsertAdminProposalRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        if (request.Status == "published")
        {
            throw new AdminValidationException("提案還沒有上傳任何檔案，不能直接發布。請先建立草稿並上傳檔案。", "status");
        }

        var now = DateTime.UtcNow;
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, Title = request.Title.Trim(), VersionNo = request.VersionNo,
            Status = request.Status, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        dbContext.Proposals.Add(proposal);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("proposals", scope.ClubCode, cancellationToken);
        return (await GetByIdAsync(scope, proposal.Id, cancellationToken))!;
    }

    public async Task<AdminProposalDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminProposalRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        Validate(request);
        var proposal = await dbContext.Proposals.Include(p => p.ProposalFiles)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (proposal is null)
        {
            return null;
        }

        if (request.Status == "published" && proposal.ProposalFiles.Count == 0)
        {
            throw new AdminValidationException("提案還沒有上傳任何檔案，不能發布。", "status");
        }

        proposal.Title = request.Title.Trim();
        proposal.VersionNo = request.VersionNo;
        proposal.Status = request.Status;
        proposal.UpdatedAt = DateTime.UtcNow;
        proposal.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("proposals", scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>刪除提案：檔案物件一併刪；已產生的 Lead 保留（<c>enquiries.proposal_id</c> 由資料庫設為空）。</summary>
    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var proposal = await dbContext.Proposals.Include(p => p.ProposalFiles)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (proposal is null)
        {
            return false;
        }

        foreach (var file in proposal.ProposalFiles)
        {
            orphans.Document(DocumentBucket.Private, file.FileKey);
        }

        dbContext.Proposals.Remove(proposal);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("proposals", scope.ClubCode, cancellationToken);
        return true;
    }

    public async Task<AdminProposalDetailDto?> AddFileAsync(
        AdminClubScope scope, Guid id, AddAdminProposalFileRequest request, UploadedDocumentInfo file, Guid? operatorId, CancellationToken cancellationToken)
    {
        var proposal = await dbContext.Proposals.Include(p => p.ProposalFiles)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (proposal is null)
        {
            return null;
        }

        var locale = ToDbLocale(request.Locale);
        var version = request.VersionNo ?? proposal.VersionNo;
        if (version < 1)
        {
            throw new AdminValidationException("版本號必須是 1 以上的整數。", "versionNo");
        }

        if (proposal.ProposalFiles.Any(f => f.Locale == locale && f.VersionNo == version))
        {
            throw new AdminConflictException("檔案已存在", "這份提案已經有相同語言與版本號的檔案，請先刪除舊檔，或改用其他版本號。", "versionNo");
        }

        var now = DateTime.UtcNow;
        // 🔴 用 DbSet.Add 明確標記為新增：Id 是 Guid 且資料庫有預設值（ValueGeneratedOnAdd），只加進導覽集合的話
        // EF 會因為 Id 已有值而當成既有列產生 UPDATE（連 row_seq 一起更新 → 「Cannot update identity column」）。
        dbContext.ProposalFiles.Add(new ProposalFile
        {
            Id = Guid.NewGuid(), ProposalId = id, Locale = locale, FileKey = file.Key,
            FileBytes = (int)Math.Min(file.SizeBytes, int.MaxValue), VersionNo = version,
            CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        });
        proposal.UpdatedAt = now;
        proposal.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("proposals", scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    public async Task<AdminProposalDetailDto?> DeleteFileAsync(
        AdminClubScope scope, Guid id, Guid fileId, OrphanedObjects orphans, Guid? operatorId, CancellationToken cancellationToken)
    {
        var proposal = await dbContext.Proposals.Include(p => p.ProposalFiles)
            .FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        var file = proposal?.ProposalFiles.FirstOrDefault(f => f.Id == fileId);
        if (proposal is null || file is null)
        {
            return null;
        }

        if (proposal.Status == "published" && proposal.ProposalFiles.Count == 1)
        {
            throw new AdminValidationException("這是已發布提案的最後一份檔案，請先把提案改回草稿，再刪除檔案。");
        }

        orphans.Document(DocumentBucket.Private, file.FileKey);
        dbContext.ProposalFiles.Remove(file);
        proposal.UpdatedAt = DateTime.UtcNow;
        proposal.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync("proposals", scope.ClubCode, cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>後台預覽下載用：取得檔案物件鍵，跨俱樂部或不存在回傳 <c>null</c>。</summary>
    public Task<ProposalFile?> FindFileAsync(AdminClubScope scope, Guid id, Guid fileId, CancellationToken cancellationToken)
        => dbContext.ProposalFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId && f.ProposalId == id && f.Proposal.ClubId == scope.ClubId, cancellationToken);

    private static void Validate(UpsertAdminProposalRequest request)
    {
        AdminInput.RequireText(request.Title, "提案名稱", 128, "title");
        if (request.VersionNo < 1)
        {
            throw new AdminValidationException("版本號必須是 1 以上的整數。", "versionNo");
        }

        AdminInput.OneOf(request.Status, Statuses, "狀態", "「草稿」或「發布」", "status");
    }

    internal static string ToDbLocale(string external)
    {
        AdminInput.OneOf(external, Locales, "語言", "「zh」（中文）或「en」（英文）");
        return RequestLocale.ToDbLocale(external);
    }

    internal static string ToExternalLocale(string dbLocale) => dbLocale == "en" ? "en" : "zh";

    private static AdminProposalDetailDto ToDetail(Proposal proposal, int leadCount) => new()
    {
        Id = proposal.Id,
        Title = proposal.Title,
        VersionNo = proposal.VersionNo,
        Status = proposal.Status,
        Files = proposal.ProposalFiles.OrderBy(f => f.Locale).ThenByDescending(f => f.VersionNo).Select(f => new AdminProposalFileDto
        {
            Id = f.Id, Locale = ToExternalLocale(f.Locale), VersionNo = f.VersionNo, FileBytes = f.FileBytes, CreatedAt = f.CreatedAt,
        }).ToList(),
        LeadCount = leadCount,
        CreatedAt = proposal.CreatedAt,
        UpdatedAt = proposal.UpdatedAt,
    };
}
