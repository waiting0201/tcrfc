using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminProposals;

/// <summary>
/// E3「Lead 名單」（規劃書 §4.5 E3：誰下載、公司、時間、來源頁面；可匯出 CSV 並標記跟進狀態）。
/// **Lead 不另建表**（docs/12 §12 第 26 點）——就是 <c>form_code = 'proposal_download'</c> 的 <c>enquiries</c>，
/// 本模組是它的商務視角（權限碼 <c>business.lead.*</c>，不需要 G2 的詢問收件匣權限）。公司／姓名／Email 取自
/// 動態欄位的慣例鍵 <c>company</c>／<c>name</c>／<c>contact</c>（種子表單已依此建立；若 G1 把這幾個鍵改名，
/// 該欄在清單上會是空的，機制同 G2）。狀態值與 G2 相同（新進／處理中／已回覆／已結案／無效）。
/// </summary>
public sealed class AdminLeadsRepository(ClubDbContext dbContext)
{
    private const string CompanyKey = "company";
    private const string NameKey = "name";
    private const string ContactKey = "contact";

    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal) { "新進", "處理中", "已回覆", "已結案", "無效" };

    public async Task<PagedResult<AdminLeadListItemDto>> ListAsync(
        AdminClubScope scope, Guid? proposalId, string? status, string? keyword, DateOnly? dateFrom, DateOnly? dateTo,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = Filter(scope, proposalId, status, keyword, dateFrom, dateTo);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(e => e.RowSeq).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new AdminLeadListItemDto
            {
                Id = e.Id,
                Company = e.EnquiryAnswers.Where(a => a.FormField.FieldKey == CompanyKey).Select(a => a.Value).FirstOrDefault(),
                Name = e.EnquiryAnswers.Where(a => a.FormField.FieldKey == NameKey).Select(a => a.Value).FirstOrDefault(),
                Email = e.EnquiryAnswers.Where(a => a.FormField.FieldKey == ContactKey).Select(a => a.Value).FirstOrDefault(),
                ProposalId = e.ProposalId,
                ProposalTitle = e.Proposal != null ? e.Proposal.ProposalsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Title).FirstOrDefault() : null,
                SourcePath = e.SourcePath, UtmSource = e.UtmSource, UtmCampaign = e.UtmCampaign, Status = e.Status,
                AssigneeAdminUserId = e.AssigneeAdminUserId, Tags = e.Tags, CreatedAt = e.CreatedAt,
            }).ToListAsync(cancellationToken);
        return new PagedResult<AdminLeadListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<AdminLeadDetailDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var enquiry = await Filter(scope, null, null, null, null, null)
            .Include(e => e.Proposal).ThenInclude(p => p!.ProposalsI18ns).Include(e => e.EnquiryAnswers).ThenInclude(a => a.FormField)
            .AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return enquiry is null ? null : ToDetail(enquiry);
    }

    public async Task<AdminLeadDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpdateAdminLeadRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Status, Statuses, "跟進狀態", "「新進」「處理中」「已回覆」「已結案」或「無效」", "status");
        var tags = AdminInput.OptionalText(request.Tags, "標籤", 255, "tags");
        var enquiry = await Filter(scope, null, null, null, null, null)
            .Include(e => e.Proposal).ThenInclude(p => p!.ProposalsI18ns).Include(e => e.EnquiryAnswers).ThenInclude(a => a.FormField)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (enquiry is null)
        {
            return null;
        }

        if (request.AssigneeAdminUserId is Guid assigneeId)
        {
            await ValidateAssigneeAsync(scope, assigneeId, cancellationToken);
        }

        enquiry.Status = request.Status;
        enquiry.AssigneeAdminUserId = request.AssigneeAdminUserId;
        enquiry.InternalNote = string.IsNullOrWhiteSpace(request.InternalNote) ? null : request.InternalNote;
        enquiry.Tags = tags;
        enquiry.UpdatedAt = DateTime.UtcNow;
        enquiry.UpdatedBy = scope.Identity.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetail(enquiry);
    }

    /// <summary>Lead 名單 CSV。欄位標題一律中文（規劃書 §4.0）；含個資（公司、姓名、Email），
    /// 呼叫端須持有 <c>business.lead.export</c>（is_restricted）。</summary>
    public async Task<string> ExportCsvAsync(
        AdminClubScope scope, Guid? proposalId, string? status, string? keyword, DateOnly? dateFrom, DateOnly? dateTo,
        CancellationToken cancellationToken)
    {
        var rows = await Filter(scope, proposalId, status, keyword, dateFrom, dateTo).OrderByDescending(e => e.RowSeq)
            .Select(e => new
            {
                Company = e.EnquiryAnswers.Where(a => a.FormField.FieldKey == CompanyKey).Select(a => a.Value).FirstOrDefault(),
                Name = e.EnquiryAnswers.Where(a => a.FormField.FieldKey == NameKey).Select(a => a.Value).FirstOrDefault(),
                Email = e.EnquiryAnswers.Where(a => a.FormField.FieldKey == ContactKey).Select(a => a.Value).FirstOrDefault(),
                ProposalTitle = e.Proposal != null ? e.Proposal.ProposalsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Title).FirstOrDefault() : null,
                e.SourcePath, e.UtmSource, e.Status, e.Tags, e.CreatedAt,
            }).ToListAsync(cancellationToken);

        var lines = new List<IEnumerable<string?>> { new[] { "公司", "姓名", "Email", "下載的提案", "來源頁面", "UTM 來源", "跟進狀態", "標籤", "下載時間（台灣時間）" } };
        lines.AddRange(rows.Select(r => new[]
        {
            r.Company, r.Name, r.Email, r.ProposalTitle, r.SourcePath, r.UtmSource, r.Status, r.Tags, TaiwanClock.ToText(r.CreatedAt),
        }));
        return CsvUtils.BuildCsv(lines);
    }

    /// <summary>「指派負責人」候選：這個俱樂部有效授權的帳號（含系統管理員），且持有 <c>business.lead.update</c>。
    /// 只回 id 與顯示名稱（理由同 <c>AdminEnquiriesRepository.ListAssignableUsersAsync</c>：不把帳號明細放寬給非系統管理員）。</summary>
    public async Task<IReadOnlyList<AdminLeadAssigneeDto>> ListAssignableUsersAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await dbContext.AdminUsers.AsNoTracking()
            .Where(u => u.Status == "active")
            .Where(u => u.IsSuperAdmin || (
                u.AdminUserClubAdminUsers.Any(g => g.ClubId == scope.ClubId && g.IsActive && (g.ExpiresOn == null || g.ExpiresOn >= today))
                && u.AdminRoles.Any(r => r.RolePermissions.Any(rp => rp.Permission.Code == "business.lead.update"))))
            .OrderBy(u => u.DisplayName)
            .Select(u => new AdminLeadAssigneeDto { Id = u.Id, DisplayName = u.DisplayName })
            .ToListAsync(cancellationToken);
    }

    private async Task ValidateAssigneeAsync(AdminClubScope scope, Guid assigneeId, CancellationToken cancellationToken)
    {
        var allowed = await ListAssignableUsersAsync(scope, cancellationToken);
        if (allowed.All(u => u.Id != assigneeId))
        {
            throw new AdminValidationException("指定的負責人帳號沒有處理提案下載名單的權限，或沒有這個俱樂部的授權，無法指派。", "assigneeAdminUserId");
        }
    }

    private IQueryable<Data.EfEntities.Enquiry> Filter(
        AdminClubScope scope, Guid? proposalId, string? status, string? keyword, DateOnly? dateFrom, DateOnly? dateTo)
    {
        var query = dbContext.Enquiries.Where(e => e.ClubId == scope.ClubId && e.Form.FormCode == FormCatalog.ProposalDownload);
        if (proposalId is Guid p)
        {
            query = query.Where(e => e.ProposalId == p);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, Statuses, "跟進狀態", "「新進」「處理中」「已回覆」「已結案」或「無效」");
            query = query.Where(e => e.Status == status);
        }

        if (dateFrom is DateOnly df)
        {
            var from = df.ToDateTime(TimeOnly.MinValue);
            query = query.Where(e => e.CreatedAt >= from);
        }

        if (dateTo is DateOnly dt)
        {
            var to = dt.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(e => e.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(e => e.EnquiryAnswers.Any(a => a.Value != null && a.Value.Contains(k))
                || (e.SourcePath != null && e.SourcePath.Contains(k)) || (e.Tags != null && e.Tags.Contains(k)));
        }

        return query;
    }

    private static AdminLeadDetailDto ToDetail(Data.EfEntities.Enquiry enquiry)
    {
        string? Answer(string key) => enquiry.EnquiryAnswers.FirstOrDefault(a => a.FormField.FieldKey == key)?.Value;
        return new AdminLeadDetailDto
        {
            Id = enquiry.Id,
            Company = Answer(CompanyKey), Name = Answer(NameKey), Email = Answer(ContactKey),
            ProposalId = enquiry.ProposalId, ProposalTitle = enquiry.Proposal?.ProposalsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Title, ProposalVersionNo = enquiry.Proposal?.VersionNo,
            SourcePath = enquiry.SourcePath, UtmSource = enquiry.UtmSource, UtmCampaign = enquiry.UtmCampaign,
            Status = enquiry.Status, AssigneeAdminUserId = enquiry.AssigneeAdminUserId, InternalNote = enquiry.InternalNote,
            Tags = enquiry.Tags, CreatedAt = enquiry.CreatedAt, UpdatedAt = enquiry.UpdatedAt,
        };
    }
}
