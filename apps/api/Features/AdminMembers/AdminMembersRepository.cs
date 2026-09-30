using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminMembers;

/// <summary>
/// K1「會員名單與檢視」（主站規劃書 §4.11 K1）。
///
/// <b>兩層資料範圍</b>（規劃書 §4.11 前言、§6）：<c>Member</c> 是帳號層、不分俱樂部；<c>Membership</c> 才分俱樂部。
/// 名單預設只列「在目前操作的俱樂部有會籍」的會員（站台切換器）；<c>crossClub=true</c> 改為「你有授權的所有俱樂部」
/// （系統管理員為全部，且含尚無任何會籍的帳號）。<b>無論哪一種，回應裡的會籍列都只含你有授權的俱樂部</b>——
/// 絕對看不到對方俱樂部的任何會籍列。範圍在資料存取層強制（查詢條件），不靠畫面隱藏。
///
/// <b>個資遮罩</b>：名單一律遮罩（姓名、Email、電話）；詳情要 <c>reveal=true</c> 才回完整值，且需要
/// <c>member.pii.reveal</c>（客服／行政、系統管理員），每次解除遮罩寫敏感操作日誌。搜尋關鍵字沒有解除遮罩權限時只比對會員編號
/// （否則遮罩會被搜尋結果當成探測工具繞過）。<b>LINE 綁定識別碼永不回傳</b>。
/// </summary>
public sealed class AdminMembersRepository(
    ClubDbContext db,
    IPermissionChecker permissions,
    SensitiveActionLogger audit,
    MemberNumberGenerator numbers)
{
    public const string RevealPermission = "member.pii.reveal";

    public sealed record ListFilter
    {
        public bool CrossClub { get; init; }
        public string? Keyword { get; init; }
        public string? ClubCode { get; init; }
        public string? Tier { get; init; }
        public string? MembershipStatus { get; init; }
        public string? Status { get; init; }
        public string? SignupSource { get; init; }
        public bool? LineBound { get; init; }
        public DateOnly? RegisteredFrom { get; init; }
        public DateOnly? RegisteredTo { get; init; }
        public Guid? SeasonId { get; init; }
        public int? ExpiringWithinDays { get; init; }
        public string? JerseyStatus { get; init; }
        public string? Locale { get; init; }
    }

    private sealed record Visibility(List<Guid>? ClubIds, bool IncludeMemberless);

    private sealed record DupRow(Guid Id, string MemberNo, string Name, string Email, string? Phone, DateTime CreatedAt, int MembershipCount);

    // ═══════════════════════════ 名單 ═══════════════════════════

    public async Task<PagedResult<AdminMemberListItemDto>> ListAsync(
        AdminClubScope scope, ListFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (query, visibility) = await BuildQueryAsync(scope, filter, cancellationToken);
        var total = await query.CountAsync(cancellationToken);
        var members = await query.OrderByDescending(m => m.RowSeq).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await ToListItemsAsync(members, visibility, cancellationToken);
        return new PagedResult<AdminMemberListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    /// <summary>名單條件的共用建構（清單與 CSV 匯出共用同一套範圍與篩選，匯出不會看到清單看不到的人）。</summary>
    private async Task<(IQueryable<Member> Query, Visibility Visibility)> BuildQueryAsync(
        AdminClubScope scope, ListFilter filter, CancellationToken cancellationToken)
    {
        var visibility = await ResolveVisibilityAsync(scope, filter.CrossClub, cancellationToken);
        var canReveal = await CanRevealAsync(scope, cancellationToken);

        var query = db.Members.AsNoTracking().AsQueryable();

        // 會籍相關條件都套在「同一份會籍」上（層級、狀態、球季、即將到期要同時成立於同一列，不是分散在兩份會籍）。
        var ms = db.Memberships.AsNoTracking().AsQueryable();
        var membershipRestricted = false;
        if (visibility.ClubIds is { } clubIds)
        {
            ms = ms.Where(x => clubIds.Contains(x.ClubId));
            membershipRestricted = true;
        }

        if (!string.IsNullOrWhiteSpace(filter.ClubCode))
        {
            var code = filter.ClubCode.Trim();
            var allowed = visibility.ClubIds;
            var clubId = await db.Clubs.AsNoTracking().Where(c => c.Code == code).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);
            if (clubId is null || (allowed is not null && !allowed.Contains(clubId.Value)))
            {
                throw new AdminValidationException("找不到指定的俱樂部，或你沒有這個俱樂部的授權。");
            }

            ms = ms.Where(x => x.ClubId == clubId.Value);
            membershipRestricted = true;
        }

        if (!string.IsNullOrWhiteSpace(filter.Tier))
        {
            AdminInput.OneOf(filter.Tier, MemberLabels.Tier.Keys.ToHashSet(), "會員層級", "「一般會員」或「球迷會員」");
            ms = ms.Where(x => x.Tier == filter.Tier);
            membershipRestricted = true;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(filter.MembershipStatus))
        {
            AdminInput.OneOf(filter.MembershipStatus, MemberLabels.MembershipStatus.Keys.ToHashSet(), "會籍狀態", "「待確認」「有效」「已到期」或「已取消」");
            ms = ApplyEffectiveStatus(ms, filter.MembershipStatus, today);
            membershipRestricted = true;
        }

        if (filter.SeasonId is Guid seasonId)
        {
            ms = ms.Where(x => x.SeasonId == seasonId);
            membershipRestricted = true;
        }

        if (filter.ExpiringWithinDays is int days)
        {
            if (days is < 1 or > 366)
            {
                throw new AdminValidationException("即將到期的天數請填 1 到 366。");
            }

            var limit = today.AddDays(days);
            ms = ms.Where(x => x.Status == "active" && x.MembershipEndOn != null && x.MembershipEndOn >= today && x.MembershipEndOn <= limit);
            membershipRestricted = true;
        }

        if (membershipRestricted)
        {
            query = query.Where(m => ms.Any(x => x.MemberId == m.Id));
        }
        else if (!visibility.IncludeMemberless)
        {
            // 不會發生（沒有範圍限制時 ClubIds 為 null 且 IncludeMemberless 為 true），保留為保守防線。
            query = query.Where(m => false);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var k = filter.Keyword.Trim();
            query = canReveal
                ? query.Where(m => m.MemberNo.Contains(k) || m.Name.Contains(k) || m.Email.Contains(k) || (m.Phone != null && m.Phone.Contains(k)))
                : query.Where(m => m.MemberNo.Contains(k));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = filter.Status switch
            {
                "active" => query.Where(m => m.Status == "active" && m.EmailVerifiedAt != null),
                "unverified" => query.Where(m => m.Status == "active" && m.EmailVerifiedAt == null),
                "suspended" => query.Where(m => m.Status == "suspended"),
                "deleted" => query.Where(m => m.Status == "deleted"),
                _ => throw new AdminValidationException("帳號狀態只能是「啟用」「停用」「未驗證」或「已刪除」。"),
            };
        }
        else
        {
            query = query.Where(m => m.Status != "deleted"); // 被合併或刪除的帳號預設不列出
        }

        if (!string.IsNullOrWhiteSpace(filter.SignupSource))
        {
            AdminInput.OneOf(filter.SignupSource, MemberLabels.SignupSource.Keys.ToHashSet(), "註冊來源", "「官網註冊」「LINE」「現場入會」或「行動 App」");
            query = query.Where(m => m.SignupSource == filter.SignupSource);
        }

        if (filter.LineBound is bool bound)
        {
            query = bound ? query.Where(m => m.LineUserIdEncrypted != null) : query.Where(m => m.LineUserIdEncrypted == null);
        }

        if (filter.RegisteredFrom is DateOnly from)
        {
            var fromTs = from.ToDateTime(TimeOnly.MinValue);
            query = query.Where(m => m.CreatedAt >= fromTs);
        }

        if (filter.RegisteredTo is DateOnly to)
        {
            var toTs = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(m => m.CreatedAt < toTs);
        }

        if (!string.IsNullOrWhiteSpace(filter.Locale))
        {
            AdminInput.OneOf(filter.Locale, MemberLabels.Locale.Keys.ToHashSet(), "語系偏好", "「繁體中文」或「English」");
            query = query.Where(m => m.Locale == filter.Locale);
        }

        if (!string.IsNullOrWhiteSpace(filter.JerseyStatus))
        {
            AdminInput.OneOf(filter.JerseyStatus, MemberLabels.Jersey.Keys.ToHashSet(), "球衣狀態", "「待處理」「已寄出」或「已領取」");
            var js = filter.JerseyStatus;
            var restrict = visibility.ClubIds is not null;
            var clubList = visibility.ClubIds ?? [];
            query = query.Where(m => db.JerseyIssues.Any(j => j.MemberId == m.Id && j.Status == js && (!restrict || clubList.Contains(j.ClubId))));
        }

        return (query, visibility);
    }

    private static IQueryable<Membership> ApplyEffectiveStatus(IQueryable<Membership> ms, string status, DateOnly today)
        => status switch
        {
            "active" => ms.Where(x => x.Status == "active" && (x.MembershipEndOn == null || x.MembershipEndOn >= today)),
            "expired" => ms.Where(x => x.Status == "expired" || (x.Status == "active" && x.MembershipEndOn != null && x.MembershipEndOn < today)),
            _ => ms.Where(x => x.Status == status),
        };

    private async Task<Visibility> ResolveVisibilityAsync(AdminClubScope scope, bool crossClub, CancellationToken cancellationToken)
    {
        if (!crossClub)
        {
            return new Visibility([scope.ClubId], false);
        }

        var reach = await AdminReach.GetAuthorizedClubIdsAsync(db, scope, cancellationToken);
        return reach is null ? new Visibility(null, true) : new Visibility(reach.ToList(), false);
    }

    private Task<bool> CanRevealAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => permissions.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, RevealPermission, cancellationToken);

    private async Task<IReadOnlyList<AdminMemberListItemDto>> ToListItemsAsync(
        IReadOnlyList<Member> members, Visibility visibility, CancellationToken cancellationToken)
    {
        if (members.Count == 0)
        {
            return [];
        }

        var ids = members.Select(m => m.Id).ToList();
        var summaries = await LoadMembershipSummariesAsync(ids, visibility.ClubIds, cancellationToken);
        var restrictJersey = visibility.ClubIds is not null;
        var jerseyClubs = visibility.ClubIds ?? [];
        var jerseyRows = await db.JerseyIssues.AsNoTracking()
            .Where(j => ids.Contains(j.MemberId) && (!restrictJersey || jerseyClubs.Contains(j.ClubId)))
            .Select(j => new { j.MemberId, j.Status })
            .ToListAsync(cancellationToken);
        var jerseyByMember = jerseyRows.GroupBy(j => j.MemberId).ToDictionary(g => g.Key, g => AggregateJersey(g.Select(x => x.Status)));

        return members.Select(m =>
        {
            var display = DisplayStatus(m);
            var jersey = jerseyByMember.GetValueOrDefault(m.Id);
            return new AdminMemberListItemDto
            {
                Id = m.Id,
                MemberNo = m.MemberNo,
                Name = PiiMasking.MaskName(m.Name),
                Email = PiiMasking.MaskEmail(m.Email),
                Phone = PiiMasking.MaskPhone(m.Phone),
                SignupSource = m.SignupSource,
                SignupSourceLabel = MemberLabels.Of(MemberLabels.SignupSource, m.SignupSource)!,
                LineBound = m.LineUserIdEncrypted != null,
                Status = m.Status,
                DisplayStatus = display,
                DisplayStatusLabel = MemberLabels.Of(MemberLabels.AccountStatus, display)!,
                Locale = m.Locale,
                LocaleLabel = m.Locale is null ? null : MemberLabels.Of(MemberLabels.Locale, m.Locale),
                CreatedAt = m.CreatedAt,
                LastLoginAt = m.LastLoginAt,
                JerseyStatus = jersey,
                JerseyStatusLabel = jersey is null ? null : MemberLabels.Of(MemberLabels.Jersey, jersey),
                Memberships = summaries.GetValueOrDefault(m.Id) ?? [],
                IsMasked = true,
            };
        }).ToList();
    }

    internal static string DisplayStatus(Member m)
        => m.Status == "active" && m.EmailVerifiedAt is null ? "unverified" : m.Status;

    private static string? AggregateJersey(IEnumerable<string> statuses)
    {
        var set = statuses.ToHashSet(StringComparer.Ordinal);
        if (set.Count == 0)
        {
            return null;
        }

        return set.Contains("pending") ? "pending" : set.Contains("shipped") ? "shipped" : "received";
    }

    internal async Task<Dictionary<Guid, IReadOnlyList<AdminMemberMembershipSummaryDto>>> LoadMembershipSummariesAsync(
        IReadOnlyCollection<Guid> memberIds, List<Guid>? clubIds, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = db.Memberships.AsNoTracking().Where(x => memberIds.Contains(x.MemberId));
        if (clubIds is not null)
        {
            query = query.Where(x => clubIds.Contains(x.ClubId));
        }

        var rows = await query
            .OrderBy(x => x.Club.SortOrder).ThenByDescending(x => x.Season.StartOn)
            .Select(x => new
            {
                x.MemberId, x.Id, x.ClubId, ClubCode = x.Club.Code, x.SeasonId, SeasonCode = x.Season.Code,
                x.Tier, x.Status, x.MembershipStartOn, x.MembershipEndOn, x.MembershipPlanId,
                ClubName = x.Club.ClubsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                PlanName = x.MembershipPlan == null ? null
                    : x.MembershipPlan.MembershipPlansI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(r => r.MemberId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<AdminMemberMembershipSummaryDto>)g.Select(r =>
            {
                var effective = MemberLabels.EffectiveMembershipStatus(r.Status, r.MembershipEndOn, today);
                return new AdminMemberMembershipSummaryDto
                {
                    MembershipId = r.Id,
                    ClubId = r.ClubId,
                    ClubCode = r.ClubCode,
                    ClubName = r.ClubName,
                    SeasonId = r.SeasonId,
                    SeasonCode = r.SeasonCode,
                    Tier = r.Tier,
                    TierLabel = MemberLabels.Of(MemberLabels.Tier, r.Tier)!,
                    Status = r.Status,
                    EffectiveStatus = effective,
                    EffectiveStatusLabel = MemberLabels.Of(MemberLabels.MembershipStatus, effective)!,
                    StartOn = r.MembershipStartOn,
                    EndOn = r.MembershipEndOn,
                    DaysToExpire = r.MembershipEndOn is DateOnly end ? end.DayNumber - today.DayNumber : null,
                    PlanId = r.MembershipPlanId,
                    PlanName = r.PlanName,
                };
            }).ToList());
    }

    // ═══════════════════════════ 詳情 ═══════════════════════════

    public async Task<AdminMemberDetailDto?> GetByIdAsync(
        AdminClubScope scope, Guid id, bool reveal, CancellationToken cancellationToken)
    {
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        if (reveal && !canReveal)
        {
            throw new AdminForbiddenException("你的角色不能檢視會員的完整個資，請洽系統管理員。");
        }

        var reach = await AdminReach.GetAuthorizedClubIdsAsync(db, scope, cancellationToken);
        var clubIds = reach?.ToList();

        var member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (member is null)
        {
            return null;
        }

        // 資料範圍：受限帳號只看得到「在自家俱樂部有會籍」的會員；看不到就當作不存在（404，不洩漏存在與否）。
        if (clubIds is not null && !await db.Memberships.AsNoTracking().AnyAsync(x => x.MemberId == id && clubIds.Contains(x.ClubId), cancellationToken))
        {
            return null;
        }

        if (reveal)
        {
            audit.Record(scope, "解除會員個資遮罩", $"會員 {member.MemberNo}");
        }

        return await BuildDetailAsync(member, clubIds, reveal, canReveal, cancellationToken);
    }

    private async Task<AdminMemberDetailDto> BuildDetailAsync(
        Member member, List<Guid>? clubIds, bool reveal, bool canReveal, CancellationToken cancellationToken)
    {
        var summaries = (await LoadMembershipSummariesAsync([member.Id], clubIds, cancellationToken)).GetValueOrDefault(member.Id) ?? [];
        var membershipIds = summaries.Select(s => s.MembershipId).ToList();

        var payments = await db.MembershipPayments.AsNoTracking()
            .Where(p => membershipIds.Contains(p.MembershipId))
            .OrderByDescending(p => p.RowSeq)
            .Select(p => new
            {
                p.Id, p.MembershipId, p.MembershipPlanId, p.Method, p.Amount, p.PaidOn, p.Note, p.ActivatedStartOn, p.ActivatedEndOn, p.CreatedAt,
                CollectingClubCode = p.CollectingClub.Code, BeneficiaryClubCode = p.Club.Code,
                HandledByName = p.HandledByNavigation == null ? null : p.HandledByNavigation.DisplayName,
                PlanName = p.MembershipPlan.MembershipPlansI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        var cards = await db.MemberCards.AsNoTracking()
            .Where(c => membershipIds.Contains(c.MembershipId))
            .OrderBy(c => c.RowSeq)
            .ToListAsync(cancellationToken);
        var adjust = await db.Memberships.AsNoTracking()
            .Where(x => membershipIds.Contains(x.Id))
            .Select(x => new { x.Id, x.LastAdjustReason, x.LastAdjustedAt })
            .ToListAsync(cancellationToken);
        var restrictClubs = clubIds is not null;
        var clubList = clubIds ?? [];
        var jerseys = await db.JerseyIssues.AsNoTracking()
            .Where(j => j.MemberId == member.Id && (!restrictClubs || clubList.Contains(j.ClubId)))
            .OrderBy(j => j.RowSeq)
            .Select(j => new { j.Id, j.ClubId, ClubCode = j.Club.Code, j.RecipientName, j.Size, j.DeliveryMethod, j.Status, j.ShippedOn, j.ReceivedOn })
            .ToListAsync(cancellationToken);
        var mergedNo = member.MergedIntoMemberId is Guid mergedId
            ? await db.Members.AsNoTracking().Where(m => m.Id == mergedId).Select(m => m.MemberNo).FirstOrDefaultAsync(cancellationToken)
            : null;

        var display = DisplayStatus(member);
        string? Name(string? v) => reveal ? v : PiiMasking.MaskName(v);

        return new AdminMemberDetailDto
        {
            Id = member.Id,
            MemberNo = member.MemberNo,
            Name = reveal ? member.Name : PiiMasking.MaskName(member.Name),
            Email = reveal ? member.Email : PiiMasking.MaskEmail(member.Email),
            Phone = reveal ? member.Phone : PiiMasking.MaskPhone(member.Phone),
            BirthOn = member.BirthOn is null ? null : reveal ? member.BirthOn.Value.ToString("yyyy-MM-dd") : PiiMasking.MaskedBirthOn,
            SignupSource = member.SignupSource,
            SignupSourceLabel = MemberLabels.Of(MemberLabels.SignupSource, member.SignupSource)!,
            LineBound = member.LineUserIdEncrypted != null,
            Status = member.Status,
            DisplayStatus = display,
            DisplayStatusLabel = MemberLabels.Of(MemberLabels.AccountStatus, display)!,
            Locale = member.Locale,
            LocaleLabel = member.Locale is null ? null : MemberLabels.Of(MemberLabels.Locale, member.Locale),
            EmailVerifiedAt = member.EmailVerifiedAt,
            CreatedAt = member.CreatedAt,
            LastLoginAt = member.LastLoginAt,
            InternalNote = member.InternalNote,
            MergedIntoMemberNo = mergedNo,
            Memberships = summaries.Select(s => new AdminMemberMembershipDetailDto
            {
                Membership = s,
                LastAdjustReason = adjust.First(a => a.Id == s.MembershipId).LastAdjustReason,
                LastAdjustedAt = adjust.First(a => a.Id == s.MembershipId).LastAdjustedAt,
                Payments = payments.Where(p => p.MembershipId == s.MembershipId).Select(p => new AdminMemberPaymentDto
                {
                    Id = p.Id,
                    PlanId = p.MembershipPlanId,
                    PlanName = p.PlanName,
                    Method = p.Method,
                    MethodLabel = p.Method is null ? null : MemberLabels.Of(MemberLabels.PaymentMethod, p.Method),
                    Amount = p.Amount,
                    PaidOn = p.PaidOn,
                    CollectingClubCode = p.CollectingClubCode,
                    BeneficiaryClubCode = p.BeneficiaryClubCode,
                    Note = p.Note,
                    HandledByName = p.HandledByName,
                    ActivatedStartOn = p.ActivatedStartOn,
                    ActivatedEndOn = p.ActivatedEndOn,
                    CreatedAt = p.CreatedAt,
                }).ToList(),
                Cards = cards.Where(c => c.MembershipId == s.MembershipId).Select(c => new AdminMemberCardDto
                {
                    Id = c.Id,
                    MembershipId = c.MembershipId,
                    HolderName = Name(c.HolderName),
                    Status = c.Status,
                    StatusLabel = c.Status == "active" ? "使用中" : "已停用",
                    IssuedAt = c.IssuedAt,
                    RevokedAt = c.RevokedAt,
                    ReissueCount = c.ReissueCount,
                }).ToList(),
            }).ToList(),
            JerseyIssues = jerseys.Select(j => new AdminMemberJerseyDto
            {
                Id = j.Id,
                ClubId = j.ClubId,
                ClubCode = j.ClubCode,
                RecipientName = Name(j.RecipientName),
                Size = j.Size,
                DeliveryMethod = j.DeliveryMethod,
                DeliveryMethodLabel = j.DeliveryMethod is null ? null : MemberLabels.Of(MemberLabels.Delivery, j.DeliveryMethod),
                Status = j.Status,
                StatusLabel = MemberLabels.Of(MemberLabels.Jersey, j.Status)!,
                ShippedOn = j.ShippedOn,
                ReceivedOn = j.ReceivedOn,
            }).ToList(),
            IsMasked = !reveal,
            CanReveal = canReveal,
        };
    }

    // ═══════════════════════════ 帳號操作 ═══════════════════════════

    /// <summary>後台建立會員（現場入會，<c>signup_source = admin</c>）。沒有設定登入密碼（存一個不可能比對成功的雜湊），
    /// 會員日後要用「忘記密碼」自行設定；Email 視為尚未驗證。</summary>
    public async Task<AdminMemberDetailDto> CreateAsync(
        AdminClubScope scope, CreateAdminMemberRequest request, CancellationToken cancellationToken)
    {
        var name = AdminInput.RequireText(request.Name, "姓名", 64);
        var email = AdminInput.OptionalEmail(request.Email, "Email") ?? throw new AdminValidationException("Email 為必填欄位。");
        var phone = AdminInput.OptionalText(request.Phone, "電話", 32);
        var note = AdminInput.OptionalText(request.InternalNote, "內部備註", 2000);
        var locale = string.IsNullOrWhiteSpace(request.Locale) ? RequestLocale.DefaultDbLocale : request.Locale;
        AdminInput.OneOf(locale, MemberLabels.Locale.Keys.ToHashSet(), "語系偏好", "「繁體中文」或「English」");
        if (request.BirthOn is DateOnly birth && birth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new AdminValidationException("生日不可晚於今天。");
        }

        if (await db.Members.AsNoTracking().AnyAsync(m => m.Email == email, cancellationToken))
        {
            // 🔴 會員帳號全站唯一（一人一帳號，跨俱樂部）：撞號的可能是對方俱樂部的會員，訊息不得確認「這個人存在」或暗示去搜尋。
            throw new AdminConflictException("Email 無法使用", "這個 Email 目前無法用來建立新的會員帳號，請確認輸入是否正確；若這位會員已經加入，請直接在會員名單裡搜尋。");
        }

        Member? created = null;
        for (var attempt = 0; attempt < 3 && created is null; attempt++)
        {
            var now = DateTime.UtcNow;
            var candidate = new Member
            {
                Id = Guid.NewGuid(),
                MemberNo = await numbers.NextAsync(scope.ClubId, cancellationToken),
                Name = name,
                Email = email,
                Phone = phone,
                BirthOn = request.BirthOn,
                Locale = locale,
                InternalNote = note,
                SignupSource = "admin",
                Status = "active",
                // 不可能比對成功的雜湊：登入端一律先驗雜湊格式，這個值不是任何合法的雜湊。
                PasswordHash = "!unset-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = scope.Identity.AdminUserId,
                UpdatedBy = scope.Identity.AdminUserId,
            };
            db.Members.Add(candidate);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                created = candidate;
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                // 並發之下撞到同一個會員編號：放掉這一筆、重新取號再試。
                db.Entry(candidate).State = EntityState.Detached;
            }
        }

        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var reach = await AdminReach.GetAuthorizedClubIdsAsync(db, scope, cancellationToken);
        return await BuildDetailAsync(created!, reach?.ToList(), canReveal, canReveal, cancellationToken);
    }

    public async Task<AdminMemberDetailDto?> UpdateStatusAsync(
        AdminClubScope scope, Guid id, UpdateAdminMemberStatusRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Status, new HashSet<string> { "active", "suspended" }, "帳號狀態", "「啟用」或「停用」");
        var reason = AdminInput.OptionalText(request.Reason, "原因", 255);
        var member = await FindWritableAsync(scope, id, cancellationToken);
        if (member is null)
        {
            return null;
        }

        if (member.Status == "deleted")
        {
            throw new AdminValidationException("這個帳號已刪除或已合併，無法變更狀態。");
        }

        if (member.Status != request.Status)
        {
            member.Status = request.Status;
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedBy = scope.Identity.AdminUserId;
            await db.SaveChangesAsync(cancellationToken);
            audit.Record(scope, request.Status == "suspended" ? "停用會員帳號" : "啟用會員帳號", $"會員 {member.MemberNo}", purpose: reason);
        }

        return await GetByIdAsync(scope, id, reveal: false, cancellationToken);
    }

    public async Task<AdminMemberDetailDto?> UpdateNoteAsync(
        AdminClubScope scope, Guid id, UpdateAdminMemberNoteRequest request, CancellationToken cancellationToken)
    {
        var note = AdminInput.OptionalText(request.InternalNote, "內部備註", 2000);
        var member = await FindWritableAsync(scope, id, cancellationToken);
        if (member is null)
        {
            return null;
        }

        member.InternalNote = note;
        member.UpdatedAt = DateTime.UtcNow;
        member.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(scope, id, reveal: false, cancellationToken);
    }

    /// <summary>重新產生會員卡 QR（卡片外流時自保，規劃書 §3.14 token 安全）：舊的憑證立即失效，同一張卡換一組新的。
    /// 只能處理「目前操作的俱樂部」的卡。</summary>
    public async Task<AdminMemberCardDto?> ReissueCardAsync(
        AdminClubScope scope, Guid memberId, Guid cardId, CancellationToken cancellationToken)
    {
        var card = await db.MemberCards
            .Include(c => c.Membership)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ClubId == scope.ClubId && c.Membership.MemberId == memberId, cancellationToken);
        if (card is null)
        {
            return null;
        }

        if (card.Status != "active")
        {
            throw new AdminValidationException("這張會員卡已停用，無法重新產生 QR。");
        }

        card.Token = SecureToken.Generate();
        card.ReissueCount += 1;
        card.IssuedAt = DateTime.UtcNow;
        card.UpdatedAt = DateTime.UtcNow;
        card.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "重新產生會員卡 QR", $"會員卡 {card.Id}");

        return new AdminMemberCardDto
        {
            Id = card.Id,
            MembershipId = card.MembershipId,
            HolderName = PiiMasking.MaskName(card.HolderName),
            Status = card.Status,
            StatusLabel = "使用中",
            IssuedAt = card.IssuedAt,
            RevokedAt = card.RevokedAt,
            ReissueCount = card.ReissueCount,
        };
    }

    /// <summary>處理帳號類操作的可寫查詢：帳號必須在你有授權的俱樂部有會籍（沒有會籍的帳號只有系統管理員能處理）。</summary>
    private async Task<Member?> FindWritableAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var reach = await AdminReach.GetAuthorizedClubIdsAsync(db, scope, cancellationToken);
        var query = db.Members.Where(m => m.Id == id);
        if (reach is not null)
        {
            var clubIds = reach.ToList();
            query = query.Where(m => db.Memberships.Any(x => x.MemberId == m.Id && clubIds.Contains(x.ClubId)));
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    // ═══════════════════════════ 重複帳號 ═══════════════════════════

    public async Task<IReadOnlyList<AdminMemberDuplicateGroupDto>> FindDuplicatesAsync(
        AdminClubScope scope, bool crossClub, CancellationToken cancellationToken)
    {
        var visibility = await ResolveVisibilityAsync(scope, crossClub, cancellationToken);
        var query = db.Members.AsNoTracking().Where(m => m.Status != "deleted");
        if (visibility.ClubIds is { } clubIds)
        {
            query = query.Where(m => db.Memberships.Any(x => x.MemberId == m.Id && clubIds.Contains(x.ClubId)));
        }

        var restrictClubs = visibility.ClubIds is not null;
        var clubList = visibility.ClubIds ?? [];
        var rows = await query
            .Select(m => new DupRow(
                m.Id, m.MemberNo, m.Name, m.Email, m.Phone, m.CreatedAt,
                m.Memberships.Count(x => !restrictClubs || clubList.Contains(x.ClubId))))
            .ToListAsync(cancellationToken);

        static AdminMemberDuplicateMemberDto Map(DupRow r) => new()
        {
            Id = r.Id,
            MemberNo = r.MemberNo,
            Name = PiiMasking.MaskName(r.Name),
            Email = PiiMasking.MaskEmail(r.Email),
            Phone = PiiMasking.MaskPhone(r.Phone),
            CreatedAt = r.CreatedAt,
            MembershipCount = r.MembershipCount,
        };

        var groups = new List<AdminMemberDuplicateGroupDto>();
        foreach (var g in rows.Where(r => NormalizePhone(r.Phone) is not null).GroupBy(r => NormalizePhone(r.Phone)!).Where(g => g.Count() > 1))
        {
            groups.Add(new AdminMemberDuplicateGroupDto
            {
                MatchKind = "phone",
                MatchKindLabel = "同一支電話",
                Members = g.OrderBy(r => r.CreatedAt).Select(r => Map(r)).ToList(),
            });
        }

        foreach (var g in rows.GroupBy(r => NormalizeEmail(r.Email)).Where(g => g.Count() > 1))
        {
            groups.Add(new AdminMemberDuplicateGroupDto
            {
                MatchKind = "email",
                MatchKindLabel = "同一個 Email（寫法不同）",
                Members = g.OrderBy(r => r.CreatedAt).Select(r => Map(r)).ToList(),
            });
        }

        return groups;
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("886", StringComparison.Ordinal) && digits.Length > 9)
        {
            digits = "0" + digits[3..];
        }

        return digits.Length >= 6 ? digits : null;
    }

    /// <summary>Email 正規化：小寫；Gmail 忽略本地部分的「.」與「+標籤」。</summary>
    private static string NormalizeEmail(string email)
    {
        var lowered = email.Trim().ToLowerInvariant();
        var at = lowered.IndexOf('@');
        if (at <= 0)
        {
            return lowered;
        }

        var local = lowered[..at];
        var domain = lowered[(at + 1)..];
        if (domain is "gmail.com" or "googlemail.com")
        {
            var plus = local.IndexOf('+');
            if (plus >= 0)
            {
                local = local[..plus];
            }

            local = local.Replace(".", "", StringComparison.Ordinal);
            domain = "gmail.com";
        }

        return local + "@" + domain;
    }

    // ═══════════════════════════ 合併帳號 ═══════════════════════════

    /// <summary>
    /// 合併重複帳號（規劃書 §4.11 K1：合併後會籍與付款紀錄一併轉移）。<b>不可逆</b>，只有系統管理員（<c>member.account.merge</c>）。
    /// 兩個帳號在同一俱樂部同一球季都有會籍時無法自動決定留哪一份，整批拒絕（409），請先處理掉其中一份。
    /// 被合併的帳號保留會員編號並標為「已刪除」，指向保留的帳號（<c>merged_into_member_id</c>），其餘個資清除
    /// （比照「刪帳號＝欄位清除不是刪列」，docs/12b §6.3）；保留帳號若沒有 LINE 綁定而被合併帳號有，則承接該綁定。
    /// 全程一個交易。
    /// </summary>
    public async Task<MergeAdminMembersResultDto> MergeAsync(
        AdminClubScope scope, MergeAdminMembersRequest request, CancellationToken cancellationToken)
    {
        if (request.TargetMemberId == request.SourceMemberId)
        {
            throw new AdminValidationException("保留的帳號與被合併的帳號不能是同一個。");
        }

        var target = await db.Members.FirstOrDefaultAsync(m => m.Id == request.TargetMemberId, cancellationToken);
        var source = await db.Members.FirstOrDefaultAsync(m => m.Id == request.SourceMemberId, cancellationToken);
        if (target is null || source is null)
        {
            throw new AdminValidationException("找不到指定的會員帳號，請重新整理後再試。");
        }

        if (target.Status == "deleted" || source.Status == "deleted")
        {
            throw new AdminValidationException("已刪除或已合併的帳號不能再參與合併。");
        }

        var sourceMemberships = await db.Memberships.Where(x => x.MemberId == source.Id).ToListAsync(cancellationToken);
        var targetKeys = await db.Memberships.AsNoTracking().Where(x => x.MemberId == target.Id)
            .Select(x => new { x.ClubId, x.SeasonId }).ToListAsync(cancellationToken);
        if (sourceMemberships.Any(s => targetKeys.Any(t => t.ClubId == s.ClubId && t.SeasonId == s.SeasonId)))
        {
            throw new AdminConflictException(
                "無法自動合併",
                "兩個帳號在同一個俱樂部的同一個球季都有會籍，系統無法決定要保留哪一份。請先到會籍管理處理掉其中一份，再重新合併。");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;

        foreach (var membership in sourceMemberships)
        {
            membership.MemberId = target.Id;
            membership.UpdatedAt = now;
            membership.UpdatedBy = scope.Identity.AdminUserId;
        }

        // 其餘依附資料用集合式 UPDATE 轉移（列數可能很多，不逐筆載入）。
        var registrations = await db.Registrations.Where(r => r.MemberId == source.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.MemberId, target.Id), cancellationToken);
        var orders = await db.Orders.Where(o => o.MemberId == source.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.MemberId, target.Id), cancellationToken);
        var jerseys = await db.JerseyIssues.Where(j => j.MemberId == source.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.MemberId, target.Id), cancellationToken);
        await db.EmailLogs.Where(e => e.MemberId == source.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.MemberId, target.Id), cancellationToken);
        await db.FanEventRegistrations.Where(e => e.MemberId == source.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.MemberId, target.Id), cancellationToken);
        await db.Carts.Where(c => c.MemberId == source.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.MemberId, target.Id), cancellationToken);

        if (target.LineUserIdEncrypted is null && source.LineUserIdEncrypted is not null)
        {
            target.LineUserIdEncrypted = source.LineUserIdEncrypted;
        }

        target.Phone ??= source.Phone;
        target.BirthOn ??= source.BirthOn;
        target.UpdatedAt = now;
        target.UpdatedBy = scope.Identity.AdminUserId;
        var mergedNote = $"（{now:yyyy-MM-dd} 已合併帳號 {source.MemberNo}）";
        target.InternalNote = string.IsNullOrEmpty(target.InternalNote) ? mergedNote : target.InternalNote + Environment.NewLine + mergedNote;

        // 被合併的帳號：保留編號與遮罩姓名，其餘個資清除，並讓它無法再登入。
        source.Status = "deleted";
        source.MergedIntoMemberId = target.Id;
        source.Name = PiiMasking.MaskName(source.Name) ?? "○";
        source.Email = $"merged-{source.MemberNo.ToLowerInvariant()}@merged.invalid";
        source.Phone = null;
        source.BirthOn = null;
        source.LineUserIdEncrypted = null;
        source.PasswordHash = "!merged-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        source.InternalNote = $"（{now:yyyy-MM-dd} 已合併至 {target.MemberNo}）";
        source.UpdatedAt = now;
        source.UpdatedBy = scope.Identity.AdminUserId;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        audit.Record(scope, "合併會員帳號", $"{source.MemberNo} → {target.MemberNo}", count: sourceMemberships.Count);
        return new MergeAdminMembersResultDto
        {
            TargetMemberId = target.Id,
            TargetMemberNo = target.MemberNo,
            SourceMemberNo = source.MemberNo,
            MovedMemberships = sourceMemberships.Count,
            MovedRegistrations = registrations,
            MovedOrders = orders,
            MovedJerseys = jerseys,
        };
    }

    // ═══════════════════════════ 匯出 ═══════════════════════════

    /// <summary>名單 CSV（<c>member.export</c>，受限）。範圍與篩選與名單完全相同。<b>不含</b>生日與 LINE 識別碼；
    /// 每份會籍一列（沒有會籍的會員一列空白會籍）。呼叫端必須填「用途」，連同筆數寫入敏感操作日誌。</summary>
    public async Task<string> ExportCsvAsync(
        AdminClubScope scope, ListFilter filter, string? purpose, CancellationToken cancellationToken)
    {
        var purposeText = AdminInput.RequireText(purpose, "匯出用途", 200);
        var (query, visibility) = await BuildQueryAsync(scope, filter, cancellationToken);
        var members = await query.OrderBy(m => m.MemberNo).ToListAsync(cancellationToken);
        var summaries = members.Count == 0
            ? new Dictionary<Guid, IReadOnlyList<AdminMemberMembershipSummaryDto>>()
            : await LoadMembershipSummariesAsync(members.Select(m => m.Id).ToList(), visibility.ClubIds, cancellationToken);

        var lines = new List<IEnumerable<string?>>
        {
            new[] { "會員編號", "姓名", "Email", "電話", "俱樂部", "球季", "會員層級", "會籍狀態", "到期日", "註冊來源", "帳號狀態", "語系偏好", "註冊日期" },
        };
        foreach (var m in members)
        {
            var display = DisplayStatus(m);
            var rows = summaries.GetValueOrDefault(m.Id) ?? [];
            string?[] Base(AdminMemberMembershipSummaryDto? s) =>
            [
                m.MemberNo, m.Name, m.Email, m.Phone,
                s?.ClubName ?? s?.ClubCode, s?.SeasonCode, s?.TierLabel, s?.EffectiveStatusLabel, s?.EndOn?.ToString("yyyy-MM-dd"),
                MemberLabels.Of(MemberLabels.SignupSource, m.SignupSource), MemberLabels.Of(MemberLabels.AccountStatus, display),
                m.Locale is null ? null : MemberLabels.Of(MemberLabels.Locale, m.Locale), m.CreatedAt.ToString("yyyy-MM-dd"),
            ];
            if (rows.Count == 0)
            {
                lines.Add(Base(null));
            }
            else
            {
                lines.AddRange(rows.Select(Base));
            }
        }

        audit.Record(scope, "匯出會員名單", $"共 {members.Count} 位會員", members.Count, purposeText);
        return CsvUtils.BuildCsv(lines);
    }
}
