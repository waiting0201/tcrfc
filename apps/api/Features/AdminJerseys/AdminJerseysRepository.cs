using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminJerseys;

/// <summary>
/// K3 球衣發放（規劃書 §4.11 K3）：待處理清單、狀態標記、依尺寸統計備貨量、出貨清單 CSV。
/// <c>jersey_issues.club_id</c> 必填；一件一列。收件資訊（姓名、電話、地址）是個資：有 <c>member.pii.reveal</c> 才看得到完整值
/// （出貨作業需要完整地址，故客服／行政與系統管理員看得到），否則一律遮罩；匯出另需 <c>member.jersey.export</c> 並寫日誌。
/// 狀態變更即時反映在會員端（同一張表）。
/// </summary>
public sealed class AdminJerseysRepository(ClubDbContext db, IPermissionChecker permissions, SensitiveActionLogger audit)
{
    private static readonly string[] SizeOrder = ["XS", "S", "M", "L", "XL", "2XL", "3XL", "4XL"];

    private Task<bool> CanRevealAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => permissions.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, AdminMembersRepository.RevealPermission, cancellationToken);

    private IQueryable<JerseyIssue> Filter(AdminClubScope scope, string? status, string? size, string? deliveryMethod, Guid? memberId, Guid? membershipId, string? keyword, bool canReveal)
    {
        var query = db.JerseyIssues.AsNoTracking().Where(j => j.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, MemberLabels.Jersey.Keys.ToHashSet(), "狀態", "「待處理」「已寄出」或「已領取」");
            query = query.Where(j => j.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(size))
        {
            var s = size.Trim().ToUpperInvariant();
            query = query.Where(j => j.Size == s);
        }

        if (!string.IsNullOrWhiteSpace(deliveryMethod))
        {
            AdminInput.OneOf(deliveryMethod, MemberLabels.Delivery.Keys.ToHashSet(), "領取方式", "「寄送」或「到場領取」");
            query = query.Where(j => j.DeliveryMethod == deliveryMethod);
        }

        if (memberId is Guid m)
        {
            query = query.Where(j => j.MemberId == m);
        }

        if (membershipId is Guid ms)
        {
            query = query.Where(j => j.MembershipId == ms);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = canReveal
                ? query.Where(j => j.Member.MemberNo.Contains(k) || j.RecipientName.Contains(k) || (j.Phone != null && j.Phone.Contains(k)))
                : query.Where(j => j.Member.MemberNo.Contains(k));
        }

        return query;
    }

    public async Task<PagedResult<AdminJerseyDto>> ListAsync(
        AdminClubScope scope, string? status, string? size, string? deliveryMethod, Guid? memberId, Guid? membershipId, string? keyword,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var query = Filter(scope, status, size, deliveryMethod, memberId, membershipId, keyword, canReveal);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(j => j.Status == "pending" ? 0 : j.Status == "shipped" ? 1 : 2).ThenBy(j => j.RowSeq)
            .Skip((page - 1) * pageSize).Take(pageSize).Include(j => j.Member).ToListAsync(cancellationToken);
        if (canReveal && rows.Count > 0)
        {
            audit.Record(scope, "檢視球衣收件資訊（完整）", "球衣發放清單", rows.Count);
        }

        return new PagedResult<AdminJerseyDto> { Items = rows.Select(r => ToDto(r, canReveal)).ToList(), Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<AdminJerseyDto?> GetByIdAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var jersey = await db.JerseyIssues.AsNoTracking().Include(j => j.Member)
            .FirstOrDefaultAsync(j => j.Id == id && j.ClubId == scope.ClubId, cancellationToken);
        return jersey is null ? null : ToDto(jersey, canReveal);
    }

    /// <summary>依尺寸統計備貨量（預設只算待處理）。「未填尺寸」另列一組。</summary>
    public async Task<IReadOnlyList<AdminJerseySizeSummaryDto>> SizeSummaryAsync(AdminClubScope scope, string? status, CancellationToken cancellationToken)
    {
        var effective = string.IsNullOrWhiteSpace(status) ? "pending" : status;
        AdminInput.OneOf(effective, MemberLabels.Jersey.Keys.ToHashSet(), "狀態", "「待處理」「已寄出」或「已領取」");
        var rows = await db.JerseyIssues.AsNoTracking().Where(j => j.ClubId == scope.ClubId && j.Status == effective)
            .GroupBy(j => j.Size)
            .Select(g => new { Size = g.Key, Total = g.Count(), Ship = g.Count(x => x.DeliveryMethod == "ship"), Pickup = g.Count(x => x.DeliveryMethod == "pickup") })
            .ToListAsync(cancellationToken);
        return rows
            .OrderBy(r => r.Size is null ? int.MaxValue : Array.IndexOf(SizeOrder, r.Size) is var i and >= 0 ? i : 100)
            .ThenBy(r => r.Size, StringComparer.Ordinal)
            .Select(r => new AdminJerseySizeSummaryDto { Size = r.Size, SizeLabel = r.Size ?? "未填尺寸", Total = r.Total, Ship = r.Ship, Pickup = r.Pickup })
            .ToList();
    }

    public async Task<AdminJerseyDto> CreateAsync(AdminClubScope scope, CreateAdminJerseyRequest request, CancellationToken cancellationToken)
    {
        var recipient = AdminInput.RequireText(request.RecipientName, "領用人姓名", 64, "recipientName");
        var size = NormalizeSize(request.Size);
        AdminInput.OneOf(request.DeliveryMethod, MemberLabels.Delivery.Keys.ToHashSet(), "領取方式", "「寄送」或「到場領取」", "deliveryMethod");
        var phone = AdminInput.OptionalText(request.Phone, "電話", 32, "phone");
        var address = AdminInput.OptionalText(request.Address, "收件地址", 500, "address");
        RequireShippingInfo(request.DeliveryMethod, phone, address);

        var membership = await db.Memberships.Include(m => m.MembershipPlan)
            .FirstOrDefaultAsync(m => m.Id == request.MembershipId && m.ClubId == scope.ClubId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的會籍，請確認會籍屬於目前的俱樂部。", "membershipId");
        var quota = membership.MembershipPlan?.JerseyQuota ?? 0;
        var used = await db.JerseyIssues.AsNoTracking().CountAsync(j => j.MembershipId == membership.Id, cancellationToken);
        if (used >= quota)
        {
            throw new AdminConflictException("已達球衣件數上限", quota == 0
                ? "這份會籍的方案不含球衣。"
                : $"這份會籍的方案含 {quota} 件球衣，已經登記滿了。");
        }

        var now = DateTime.UtcNow;
        var jersey = new JerseyIssue
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, MemberId = membership.MemberId, MembershipId = membership.Id,
            RecipientName = recipient, Phone = phone, Size = size, DeliveryMethod = request.DeliveryMethod, Address = address,
            Status = "pending", CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        db.JerseyIssues.Add(jersey);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(scope, jersey.Id, cancellationToken))!;
    }

    public async Task<AdminJerseyDto?> UpdateAsync(AdminClubScope scope, Guid id, UpdateAdminJerseyRequest request, CancellationToken cancellationToken)
    {
        var jersey = await db.JerseyIssues.FirstOrDefaultAsync(j => j.Id == id && j.ClubId == scope.ClubId, cancellationToken);
        if (jersey is null)
        {
            return null;
        }

        var editsPii = request.RecipientName is not null || request.Phone is not null || request.Address is not null;
        if (editsPii && !await CanRevealAsync(scope, cancellationToken))
        {
            throw new AdminForbiddenException("你的角色不能修改收件人資料，請洽客服／行政或系統管理員。");
        }

        if (request.RecipientName is not null)
        {
            jersey.RecipientName = AdminInput.RequireText(request.RecipientName, "領用人姓名", 64, "recipientName");
        }

        if (request.Phone is not null)
        {
            jersey.Phone = AdminInput.OptionalText(request.Phone, "電話", 32, "phone");
        }

        if (request.Address is not null)
        {
            jersey.Address = AdminInput.OptionalText(request.Address, "收件地址", 500, "address");
        }

        if (request.Size is not null)
        {
            jersey.Size = NormalizeSize(request.Size);
        }

        if (request.DeliveryMethod is not null)
        {
            AdminInput.OneOf(request.DeliveryMethod, MemberLabels.Delivery.Keys.ToHashSet(), "領取方式", "「寄送」或「到場領取」", "deliveryMethod");
            jersey.DeliveryMethod = request.DeliveryMethod;
        }

        if (request.Status is not null)
        {
            ApplyStatus(jersey, request.Status);
        }
        else if (jersey.Status == "shipped")
        {
            RequireShippingInfo(jersey.DeliveryMethod, jersey.Phone, jersey.Address);
        }

        jersey.UpdatedAt = DateTime.UtcNow;
        jersey.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(scope, id, cancellationToken);
    }

    /// <summary>批次改狀態（能處理的處理、不能處理的列進 skipped）。</summary>
    public async Task<BatchOperationResultDto> BatchStatusAsync(AdminClubScope scope, BatchJerseyStatusRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Status, MemberLabels.Jersey.Keys.ToHashSet(), "狀態", "「待處理」「已寄出」或「已領取」");
        if (request.Ids.Count is 0 or > 200)
        {
            throw new AdminValidationException("一次最多處理 200 筆，至少選 1 筆。");
        }

        var ids = request.Ids.Distinct().ToList();
        var jerseys = await db.JerseyIssues.Where(j => j.ClubId == scope.ClubId && ids.Contains(j.Id)).ToListAsync(cancellationToken);
        var skipped = new List<BatchSkippedItemDto>();
        var updated = 0;
        var now = DateTime.UtcNow;
        foreach (var id in ids)
        {
            var jersey = jerseys.FirstOrDefault(j => j.Id == id);
            if (jersey is null)
            {
                skipped.Add(new BatchSkippedItemDto { Id = id, Reason = "找不到這筆球衣登記。" });
                continue;
            }

            try
            {
                ApplyStatus(jersey, request.Status);
                jersey.UpdatedAt = now;
                jersey.UpdatedBy = scope.Identity.AdminUserId;
                updated++;
            }
            catch (AdminValidationException ex)
            {
                skipped.Add(new BatchSkippedItemDto { Id = id, Reason = ex.Message });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new BatchOperationResultDto { UpdatedCount = updated, Skipped = skipped };
    }

    /// <summary>出貨清單 CSV（<c>member.jersey.export</c>，受限）。含完整收件資訊，須填用途並寫日誌。</summary>
    public async Task<string> ExportCsvAsync(AdminClubScope scope, string? status, string? purpose, CancellationToken cancellationToken)
    {
        var purposeText = AdminInput.RequireText(purpose, "匯出用途", 200);
        var rows = await Filter(scope, status, null, null, null, null, null, canReveal: true)
            .OrderBy(j => j.Size).ThenBy(j => j.RowSeq).Include(j => j.Member).ToListAsync(cancellationToken);
        var lines = new List<IEnumerable<string?>> { new[] { "會員編號", "領用人", "電話", "尺寸", "領取方式", "收件地址", "狀態", "寄出日期", "領取日期", "登記日期" } };
        lines.AddRange(rows.Select(j => new[]
        {
            j.Member.MemberNo, j.RecipientName, j.Phone, j.Size, MemberLabels.Of(MemberLabels.Delivery, j.DeliveryMethod), j.Address,
            MemberLabels.Of(MemberLabels.Jersey, j.Status), j.ShippedOn?.ToString("yyyy-MM-dd"), j.ReceivedOn?.ToString("yyyy-MM-dd"),
            TaiwanClock.ToDate(j.CreatedAt).ToString("yyyy-MM-dd"),
        }));
        audit.Record(scope, "匯出球衣出貨清單", $"共 {rows.Count} 件", rows.Count, purposeText);
        return CsvUtils.BuildCsv(lines);
    }

    // ── 規則 ───────────────────────────────────────────

    private static string NormalizeSize(string? size)
    {
        var s = AdminInput.RequireText(size, "尺寸", 16, "size").ToUpperInvariant();
        return s;
    }

    private static void RequireShippingInfo(string? deliveryMethod, string? phone, string? address)
    {
        if (deliveryMethod == "ship" && (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(address)))
        {
            throw new AdminValidationException("選「寄送」必須填寫電話與收件地址。", string.IsNullOrWhiteSpace(phone) ? "phone" : "address");
        }
    }

    /// <summary>狀態轉換（可往回改，方便更正誤按）：寄出時記寄出日期（寄送才能標「已寄出」）、領取時記領取日期，
    /// 回到待處理則清掉兩個日期。</summary>
    private static void ApplyStatus(JerseyIssue jersey, string status)
    {
        AdminInput.OneOf(status, MemberLabels.Jersey.Keys.ToHashSet(), "狀態", "「待處理」「已寄出」或「已領取」");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        switch (status)
        {
            case "shipped":
                if (jersey.DeliveryMethod != "ship")
                {
                    throw new AdminValidationException("只有「寄送」的球衣才能標記為已寄出，到場領取請直接標為已領取。");
                }

                RequireShippingInfo(jersey.DeliveryMethod, jersey.Phone, jersey.Address);
                jersey.ShippedOn ??= today;
                jersey.ReceivedOn = null;
                break;
            case "received":
                jersey.ReceivedOn ??= today;
                break;
            default:
                jersey.ShippedOn = null;
                jersey.ReceivedOn = null;
                break;
        }

        jersey.Status = status;
    }

    private static AdminJerseyDto ToDto(JerseyIssue j, bool reveal) => new()
    {
        Id = j.Id, MemberId = j.MemberId, MemberNo = j.Member.MemberNo, MembershipId = j.MembershipId,
        RecipientName = reveal ? j.RecipientName : PiiMasking.MaskName(j.RecipientName),
        Phone = reveal ? j.Phone : PiiMasking.MaskPhone(j.Phone), Size = j.Size, DeliveryMethod = j.DeliveryMethod,
        DeliveryMethodLabel = j.DeliveryMethod is null ? null : MemberLabels.Of(MemberLabels.Delivery, j.DeliveryMethod),
        Address = reveal ? j.Address : PiiMasking.MaskAddress(j.Address), Status = j.Status,
        StatusLabel = MemberLabels.Of(MemberLabels.Jersey, j.Status)!, ShippedOn = j.ShippedOn, ReceivedOn = j.ReceivedOn,
        CreatedAt = j.CreatedAt, UpdatedAt = j.UpdatedAt, IsMasked = !reveal,
    };
}
