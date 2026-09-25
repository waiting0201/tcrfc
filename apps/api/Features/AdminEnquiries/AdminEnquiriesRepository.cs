using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Features.Forms;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminEnquiries;

/// <summary>
/// G2「詢問收件匣」——俱樂部範圍的統一收件匣（主站規劃書 §4.7 G2）。
///
/// ### 姓名／聯絡方式怎麼從動態欄位取出
/// <c>Enquiry</c> 本身沒有姓名／聯絡方式欄位——這兩項跟其餘表單內容一樣，全部存在
/// <c>EnquiryAnswer</c>（<c>(enquiry_id, form_field_id) → value</c>），因為 G1 是「表單設計器」，
/// 欄位是動態的。本檔採**慣例欄位鍵**：G1 建立表單欄位時若把姓名欄位的 <c>field_key</c> 取為
/// <c>"name"</c>、聯絡方式取為 <c>"contact"</c>，清單就能撈出來顯示；種子資料
/// （<c>db/seed/generate-club-seed-sql.py</c>）已依此慣例建立九個表單的預設欄位。**若後台把這
/// 兩個鍵改名或刪除，清單只會顯示 <c>null</c>，不是程式錯誤**——這是動態表單的必然取捨，沒有
/// 資料庫層的機制能保證「某個 <c>field_key</c> 一定存在」。
///
/// ### 「內容摘要」欄怎麼取出（審查回饋補做）
/// 跟姓名／聯絡方式同一種「慣例欄位鍵」機制，差別是不用字面 <c>field_key</c> 比對，改用
/// <c>form_fields.is_summary</c> 旗標——G1 表單設計器可以把任一欄位標記為「這是內容摘要」，
/// 同一張表單最多一個欄位可標記（<c>AdminFormsRepository</c> 應用層強制＋DB 層
/// <c>UQ_form_fields_one_summary_per_form</c> 過濾唯一索引兩道防線）。**沒有標記任何欄位的表單
/// （例如 <c>camp_registration</c>／<c>media_enquiry</c>／<c>proposal_download</c> 沒有合適的
/// 敘述性文字欄位）清單與匯出的內容摘要維持 <c>null</c>，是設計上的必然結果，不是缺陷**。
///
/// ### 依表單類別的列級授權（不是 <c>role_permissions.scope_type</c>）
/// 學院／課程管理、商務／贊助、公關／媒體三個角色只能看到自己類別的詢問（矩陣「課程類詢問」
/// 「合作／贊助類詢問」「媒體類詢問」）。這裡**不是**比照 <c>TeamRowScope</c> 用
/// <c>role_permissions.scope_type</c> 解析（那是給「同一權限碼、依逐人指派的關聯表決定範圍」的
/// 情境，例如 <c>own_teams</c> 靠 <c>AdminUserTeam</c>）——本模組的「類別」邊界是固定的 9 個
/// <c>form_code</c> 分組，不需要逐人指派的關聯表，直接拆成 <c>enquiry.inbox.*</c>／
/// <c>enquiry.course.*</c>／<c>enquiry.partnership.*</c>／<c>enquiry.media.*</c> 四組獨立權限碼，
/// 應用層依角色持有哪一組決定 <c>WHERE form_code IN (...)</c>，見
/// <see cref="ResolveViewFormCodeFilterAsync"/>／<see cref="ResolveUpdateFormCodeFilterAsync"/>。
/// 完整判斷理由見 docs/12b-database-tables.md §7.4「S1-10 新增」附註。
/// </summary>
public sealed class AdminEnquiriesRepository(ClubDbContext dbContext, IPermissionChecker permissionChecker)
{
    public const string PermissionInboxView = "enquiry.inbox.view";
    public const string PermissionInboxUpdate = "enquiry.inbox.update";
    public const string PermissionInboxExport = "enquiry.inbox.export";
    public const string PermissionCourseView = "enquiry.course.view";
    public const string PermissionCourseUpdate = "enquiry.course.update";
    public const string PermissionPartnershipView = "enquiry.partnership.view";
    public const string PermissionPartnershipUpdate = "enquiry.partnership.update";
    public const string PermissionMediaView = "enquiry.media.view";
    public const string PermissionMediaUpdate = "enquiry.media.update";

    public static readonly IReadOnlyList<string> ViewCandidateCodes =
        [PermissionInboxView, PermissionCourseView, PermissionPartnershipView, PermissionMediaView];
    public static readonly IReadOnlyList<string> UpdateCandidateCodes =
        [PermissionInboxUpdate, PermissionCourseUpdate, PermissionPartnershipUpdate, PermissionMediaUpdate];

    internal static readonly HashSet<string> AllowedStatuses =
        new(StringComparer.Ordinal) { "新進", "處理中", "已回覆", "已結案", "無效" };

    private const string NameFieldKey = "name";
    private const string ContactFieldKey = "contact";

    /// <summary>清單／詳情用的過濾條件。<c>null</c>＝不限（系統管理員或持有 <c>enquiry.inbox.view</c>）。</summary>
    public Task<IReadOnlySet<string>?> ResolveViewFormCodeFilterAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => ResolveFormCodeFilterAsync(scope, PermissionInboxView, PermissionCourseView, PermissionPartnershipView, PermissionMediaView, cancellationToken);

    /// <summary>更新（狀態／指派／備註／標籤）用的過濾條件——刻意跟檢視分開解析，因為一個角色可能
    /// 持有 <c>*.view</c> 卻沒有對應的 <c>*.update</c>（矩陣沒有這種組合，但權限碼本身各自獨立，
    /// 不假設兩者一定同進退）。</summary>
    public Task<IReadOnlySet<string>?> ResolveUpdateFormCodeFilterAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => ResolveFormCodeFilterAsync(scope, PermissionInboxUpdate, PermissionCourseUpdate, PermissionPartnershipUpdate, PermissionMediaUpdate, cancellationToken);

    private async Task<IReadOnlySet<string>?> ResolveFormCodeFilterAsync(
        AdminClubScope scope, string fullCode, string courseCode, string partnershipCode, string mediaCode, CancellationToken cancellationToken)
    {
        if (scope.Identity.IsSuperAdmin)
        {
            return null;
        }

        var candidates = new[] { fullCode, courseCode, partnershipCode, mediaCode };
        var held = await permissionChecker.GetHeldPermissionCodesAsync(
            scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, candidates, cancellationToken);

        if (held.Contains(fullCode))
        {
            return null;
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        if (held.Contains(courseCode))
        {
            allowed.UnionWith(FormCatalog.CourseCategoryCodes);
        }
        if (held.Contains(partnershipCode))
        {
            allowed.UnionWith(FormCatalog.PartnershipCategoryCodes);
        }
        if (held.Contains(mediaCode))
        {
            allowed.UnionWith(FormCatalog.MediaCategoryCodes);
        }

        // 空集合是合法結果（例如呼叫端只持有某個 view 碼，但這次是解析 update 用的過濾條件，
        // 而矩陣沒有指派任何一個 update 碼給這個角色）——查詢會用 WHERE form_code IN (空集合)，
        // 等同看不到任何一筆，fail-closed。
        return allowed;
    }

    public async Task<PagedResult<AdminEnquiryListItemDto>> ListAsync(
        AdminClubScope scope, IReadOnlySet<string>? allowedFormCodes, string? formCode, string? status,
        string? keyword, DateOnly? dateFrom, DateOnly? dateTo, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = ApplyFilters(dbContext.Enquiries.AsNoTracking().Where(e => e.ClubId == scope.ClubId),
            allowedFormCodes, formCode, status, keyword, dateFrom, dateTo);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.RowSeq)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AdminEnquiryListItemDto
            {
                Id = e.Id,
                FormCode = e.Form.FormCode,
                FormNameZh = string.Empty, // 下方逐筆補上（FormCatalog 是純 C# 字典，EF 無法轉譯進 SQL）。
                ApplicantName = e.EnquiryAnswers.Where(a => a.FormField.FieldKey == NameFieldKey).Select(a => a.Value).FirstOrDefault(),
                ContactInfo = e.EnquiryAnswers.Where(a => a.FormField.FieldKey == ContactFieldKey).Select(a => a.Value).FirstOrDefault(),
                ContentSummary = e.EnquiryAnswers.Where(a => a.FormField.IsSummary).Select(a => a.Value).FirstOrDefault(),
                SourcePath = e.SourcePath,
                UtmSource = e.UtmSource,
                UtmCampaign = e.UtmCampaign,
                Status = e.Status,
                AssigneeAdminUserId = e.AssigneeAdminUserId,
                CreatedAt = e.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var withNames = items.Select(i => i with { FormNameZh = FormCatalog.DisplayNameZh(i.FormCode) }).ToList();
        return new PagedResult<AdminEnquiryListItemDto> { Items = withNames, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    public async Task<AdminEnquiryDetailDto?> GetByIdAsync(
        AdminClubScope scope, IReadOnlySet<string>? allowedFormCodes, Guid id, CancellationToken cancellationToken)
    {
        var enquiry = await dbContext.Enquiries.AsNoTracking()
            .Include(e => e.Form)
            .Include(e => e.EnquiryAnswers).ThenInclude(a => a.FormField)
            .FirstOrDefaultAsync(e => e.Id == id && e.ClubId == scope.ClubId, cancellationToken);

        if (enquiry is null)
        {
            return null;
        }
        if (allowedFormCodes is not null && !allowedFormCodes.Contains(enquiry.Form.FormCode))
        {
            return null; // 類別越權：視同 404，不洩漏存在與否（比照跨俱樂部的既有慣例）。
        }

        return ToDetailDto(enquiry);
    }

    public async Task<AdminEnquiryDetailDto?> UpdateAsync(
        AdminClubScope scope, IReadOnlySet<string>? allowedFormCodes, Guid id, UpdateAdminEnquiryRequest request,
        CancellationToken cancellationToken)
    {
        if (!AllowedStatuses.Contains(request.Status))
        {
            throw new AdminEnquiryValidationException("狀態只能是「新進」「處理中」「已回覆」「已結案」或「無效」其中一種。");
        }

        var enquiry = await dbContext.Enquiries
            .Include(e => e.Form)
            .Include(e => e.EnquiryAnswers).ThenInclude(a => a.FormField)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (enquiry is null || enquiry.ClubId != scope.ClubId)
        {
            return null;
        }
        if (allowedFormCodes is not null && !allowedFormCodes.Contains(enquiry.Form.FormCode))
        {
            return null;
        }

        if (request.AssigneeAdminUserId is Guid assigneeId)
        {
            await ValidateAssigneeAsync(assigneeId, scope.ClubId, cancellationToken);
        }

        enquiry.Status = request.Status;
        enquiry.AssigneeAdminUserId = request.AssigneeAdminUserId;
        enquiry.InternalNote = request.InternalNote;
        enquiry.Tags = request.Tags;
        enquiry.UpdatedAt = DateTime.UtcNow;
        enquiry.UpdatedBy = scope.Identity.AdminUserId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDetailDto(enquiry);
    }

    /// <summary>名單匯出（規劃書 G2「匯出 CSV」）。⚠️ **表單類別欄一律輸出中文顯示名稱
    /// （<see cref="FormCatalog.DisplayNameZh"/>），不輸出 <c>form_code</c> 原始字面值**——
    /// docs/14-invariants.md 明文「CSV 匯出的欄位標題同此規則」不得出現英文技術詞，本輪判斷
    /// 這條約束及於欄位內容本身（表單類別是使用者看得到的分類，不是純技術識別碼）。</summary>
    public async Task<string> ExportCsvAsync(
        AdminClubScope scope, IReadOnlySet<string>? allowedFormCodes, string? formCode, string? status,
        string? keyword, DateOnly? dateFrom, DateOnly? dateTo, CancellationToken cancellationToken)
    {
        var query = ApplyFilters(dbContext.Enquiries.AsNoTracking().Where(e => e.ClubId == scope.ClubId),
            allowedFormCodes, formCode, status, keyword, dateFrom, dateTo);

        var rows = await query
            .OrderByDescending(e => e.RowSeq)
            .Select(e => new
            {
                e.Form.FormCode,
                ApplicantName = e.EnquiryAnswers.Where(a => a.FormField.FieldKey == NameFieldKey).Select(a => a.Value).FirstOrDefault(),
                ContactInfo = e.EnquiryAnswers.Where(a => a.FormField.FieldKey == ContactFieldKey).Select(a => a.Value).FirstOrDefault(),
                ContentSummary = e.EnquiryAnswers.Where(a => a.FormField.IsSummary).Select(a => a.Value).FirstOrDefault(),
                e.SourcePath,
                e.UtmSource,
                e.Status,
                e.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var lines = new List<IEnumerable<string?>>
        {
            new[] { "來源表單", "姓名", "聯絡方式", "內容摘要", "來源頁面", "UTM 來源", "狀態", "送出時間" },
        };

        lines.AddRange(rows.Select(r => new[]
        {
            FormCatalog.DisplayNameZh(r.FormCode),
            r.ApplicantName,
            r.ContactInfo,
            r.ContentSummary,
            r.SourcePath,
            r.UtmSource,
            r.Status,
            r.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
        }));

        return CsvUtils.BuildCsv(lines);
    }

    private static IQueryable<Data.EfEntities.Enquiry> ApplyFilters(
        IQueryable<Data.EfEntities.Enquiry> query, IReadOnlySet<string>? allowedFormCodes, string? formCode,
        string? status, string? keyword, DateOnly? dateFrom, DateOnly? dateTo)
    {
        if (allowedFormCodes is not null)
        {
            // 空集合＝這個角色對「更新」完全沒有任何類別的權限（見 ResolveFormCodeFilterAsync
            // 的說明），EF 對空集合的 Contains 會正確轉譯成一律不成立的條件，不需要另外特判。
            query = query.Where(e => allowedFormCodes.Contains(e.Form.FormCode));
        }
        if (formCode is not null)
        {
            query = query.Where(e => e.Form.FormCode == formCode);
        }
        if (status is not null)
        {
            query = query.Where(e => e.Status == status);
        }
        if (dateFrom is DateOnly df)
        {
            var fromUtc = df.ToDateTime(TimeOnly.MinValue);
            query = query.Where(e => e.CreatedAt >= fromUtc);
        }
        if (dateTo is DateOnly dt)
        {
            var toUtc = dt.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(e => e.CreatedAt <= toUtc);
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(e =>
                e.EnquiryAnswers.Any(a => a.Value != null && a.Value.Contains(keyword))
                || (e.SourcePath != null && e.SourcePath.Contains(keyword))
                || (e.Tags != null && e.Tags.Contains(keyword)));
        }
        return query;
    }

    private async Task ValidateAssigneeAsync(Guid assigneeId, Guid clubId, CancellationToken cancellationToken)
    {
        var assignee = await dbContext.AdminUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == assigneeId, cancellationToken);
        if (assignee is null)
        {
            throw new AdminEnquiryValidationException("找不到指定的負責人帳號。");
        }
        if (assignee.IsSuperAdmin)
        {
            return; // 系統管理員一律有效，跳過俱樂部授權檢查（同 AdminClubAuthorizer 的既有規則）。
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var hasClubGrant = await dbContext.AdminUserClubs.AsNoTracking()
            .AnyAsync(g => g.AdminUserId == assigneeId && g.ClubId == clubId && g.IsActive
                        && (g.ExpiresOn == null || g.ExpiresOn >= today), cancellationToken);
        if (!hasClubGrant)
        {
            throw new AdminEnquiryValidationException("指定的負責人帳號目前沒有這個俱樂部的授權，無法指派。");
        }
    }

    private static AdminEnquiryDetailDto ToDetailDto(Data.EfEntities.Enquiry enquiry) => new()
    {
        Id = enquiry.Id,
        FormCode = enquiry.Form.FormCode,
        FormNameZh = FormCatalog.DisplayNameZh(enquiry.Form.FormCode),
        SourcePath = enquiry.SourcePath,
        UtmSource = enquiry.UtmSource,
        UtmCampaign = enquiry.UtmCampaign,
        Status = enquiry.Status,
        InternalNote = enquiry.InternalNote,
        Tags = enquiry.Tags,
        AssigneeAdminUserId = enquiry.AssigneeAdminUserId,
        Answers = enquiry.EnquiryAnswers
            .OrderBy(a => a.FormField.SortOrder)
            .Select(a => new AdminEnquiryAnswerDto { FieldKey = a.FormField.FieldKey, FieldType = a.FormField.FieldType, Value = a.Value })
            .ToList(),
        CreatedAt = enquiry.CreatedAt,
        UpdatedAt = enquiry.UpdatedAt,
    };
}
