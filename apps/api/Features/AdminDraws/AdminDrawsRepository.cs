using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminMembers;
using Tcrfc.Api.Features.AdminNews;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminDraws;

/// <summary>
/// K5 抽獎名單管理（主站規劃書 §4.11 K5、§3.14「球迷會員抽獎」）。<b>定位</b>：付費會籍的權益之一。系統<b>只負責</b>產生合格名單、凍結快照、
/// 配發序號、匯出清單；<b>實體抽獎於現場或直播由人工進行，系統不做任何隨機抽出演算法、不產生任何隨機結果</b>，中獎人由後台以序號回填。
/// 各俱樂部各自舉辦（<c>member_draws.club_id</c> 必填，不合辦）。
/// <list type="bullet">
/// <item><b>資格</b>：條件固定為「基準時間當下持有<b>本活動主辦俱樂部</b>的球迷會員（<c>fan_club</c>）會籍、該會籍有效、帳號狀態為啟用」，<b>不可由後台自訂</b>；
/// 會員完全不需要操作。同時持有兩隊會籍者在兩份名單各佔一號（活動辦法須明示「可分別參加兩隊抽獎」）。</item>
/// <item><b>快照</b>：依會員編號升冪配發連號序號 1…N，一人一號，複製當下的姓名、會員編號、層級與到期日；鎖定後<b>不得逐列新增或刪除</b>，有誤只能整份作廢重產
/// （<c>roster_version</c>＋1，舊版保留供稽核）。記錄基準時間、合格人數、名單雜湊與執行人。</item>
/// <item><b>個資</b>：名單視同會員個資。名單與中獎人姓名預設遮罩（王○明），完整值需 <c>member.pii.reveal</c>；受限版中獎人清單匯出另需 <c>member.draw.export</c>
/// 並寫敏感操作日誌；公關／媒體撰寫公布稿只取得遮罩版名單。</item>
/// <item><b>不做</b>：中獎通知信、站內信、LINE 推播（全系統沒有寄信通路）；前台任何抽獎頁；點數、權重、加碼機制；獎品含門票或現金。</item>
/// </list>
/// 稽核：本庫不建日誌表，比照 K1–K4 寫敏感操作日誌（<see cref="SensitiveActionLogger"/>）。
/// </summary>
public sealed partial class AdminDrawsRepository(
    ClubDbContext db, IImagePublicUrlResolver imageUrls, IPermissionChecker permissions, SensitiveActionLogger audit,
    ClubSettingsStore settings, AdminArticlesRepository articles)
{
    public const string NoticeKey = "member.draw_notice_confirmed";
    public const string RevealPermission = AdminMembersRepository.RevealPermission;
    public const string MemberDrawTagSlug = "member-draw";

    public static readonly IReadOnlyDictionary<string, string> StatusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["draft"] = "草稿",
        ["roster_locked"] = "名單已鎖定",
        ["drawn"] = "已抽出",
        ["announced"] = "已公布",
        ["closed"] = "已結案",
        ["voided"] = "作廢",
    };

    public static readonly IReadOnlyDictionary<string, string> OccasionLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["home_match"] = "主場賽事日",
        ["livestream"] = "直播",
        ["other"] = "其他",
    };

    public static readonly IReadOnlyDictionary<string, string> FulfilmentLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pending"] = "待處理",
        ["shipped"] = "已寄出",
        ["claimed"] = "已領取",
        ["overdue"] = "逾期",
    };

    public static readonly IReadOnlyDictionary<string, string> ClaimLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ship"] = "寄送",
        ["pickup"] = "現場領取",
    };

    private static readonly IReadOnlyDictionary<string, string> ArticleStatusLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["draft"] = "草稿",
        ["published"] = "已發布",
        ["scheduled"] = "排程發布中",
    };

    /// <summary>公布稿內文的 JSON：不把中文轉成 \uXXXX（維持可讀，編輯器與資料庫檢視都看得到原文）。</summary>
    private static readonly JsonSerializerOptions BodyJson = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9-]{0,31}$")]
    private static partial Regex DrawCodeFormat();

    private Task<bool> CanRevealAsync(AdminClubScope scope, CancellationToken cancellationToken)
        => permissions.HasPermissionAsync(scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin, RevealPermission, cancellationToken);

    // ═════════════ 蒐集告知（未完成前不得舉辦抽獎）═════════════

    public async Task<AdminDrawNoticeDto> GetNoticeAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var map = await settings.GetManyAsync(scope.ClubId, [NoticeKey], cancellationToken);
        var raw = map.GetValueOrDefault(NoticeKey);
        return new AdminDrawNoticeDto
        {
            Confirmed = !string.IsNullOrWhiteSpace(raw), ConfirmedAt = DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var at) ? at : null,
        };
    }

    public async Task<AdminDrawNoticeDto> UpdateNoticeAsync(AdminClubScope scope, UpdateAdminDrawNoticeRequest request, CancellationToken cancellationToken)
    {
        await settings.UpsertAsync(scope.ClubId, NoticeKey, request.Confirmed ? DateTime.UtcNow.ToString("o") : null, "member", scope.Identity.AdminUserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, request.Confirmed ? "確認抽獎蒐集告知已增列" : "取消抽獎蒐集告知確認", null, 1);
        return await GetNoticeAsync(scope, cancellationToken);
    }

    // ═════════════ 活動 ═════════════

    public async Task<PagedResult<AdminDrawListItemDto>> ListAsync(AdminClubScope scope, string? status, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.MemberDraws.AsNoTracking().Where(d => d.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, StatusLabels.Keys.ToHashSet(), "狀態", "草稿、名單已鎖定、已抽出、已公布、已結案或作廢");
            query = query.Where(d => d.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(d => d.DrawCode.Contains(k) || d.MemberDrawsI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.RowSeq).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new
            {
                Draw = d,
                Zh = d.MemberDrawsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                En = d.MemberDrawsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                Winners = db.DrawRosters.Count(r => r.MemberDrawId == d.Id && r.RosterVersion == d.RosterVersion && r.IsWinner),
                Backups = db.DrawRosters.Count(r => r.MemberDrawId == d.Id && r.RosterVersion == d.RosterVersion && r.IsBackup),
                Fulfilled = db.DrawRosters.Count(r => r.MemberDrawId == d.Id && r.RosterVersion == d.RosterVersion && r.IsWinner && (r.FulfilmentStatus == "shipped" || r.FulfilmentStatus == "claimed")),
                ArticleStatus = d.AnnouncementArticle == null ? null : d.AnnouncementArticle.Status,
                Creator = d.CreatedByNavigation == null ? null : d.CreatedByNavigation.DisplayName,
            }).ToListAsync(cancellationToken);
        return new PagedResult<AdminDrawListItemDto>
        {
            Items = rows.Select(r => new AdminDrawListItemDto
            {
                Id = r.Draw.Id, DrawCode = r.Draw.DrawCode, NameZh = r.Zh, NameEn = r.En, SnapshotAt = r.Draw.SnapshotAt, DrawnAt = r.Draw.DrawnAt,
                DrawOccasion = r.Draw.DrawOccasion, DrawOccasionLabel = r.Draw.DrawOccasion is null ? null : ShopOrDefault(OccasionLabels, r.Draw.DrawOccasion),
                ClaimDeadlineOn = r.Draw.ClaimDeadlineOn, Status = r.Draw.Status, StatusLabel = StatusLabels[r.Draw.Status], RosterVersion = r.Draw.RosterVersion,
                TotalCount = r.Draw.TotalCount, WinnerCount = r.Winners, BackupCount = r.Backups, FulfilledCount = r.Fulfilled,
                AnnouncementArticleId = r.Draw.AnnouncementArticleId, AnnouncementStatus = r.ArticleStatus,
                AnnouncementStatusLabel = r.ArticleStatus is null ? null : ShopOrDefault(ArticleStatusLabels, r.ArticleStatus),
                CreatedByName = r.Creator, CreatedAt = r.Draw.CreatedAt, UpdatedAt = r.Draw.UpdatedAt,
            }).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    public async Task<AdminDrawDetailDto?> GetAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var draw = await LoadAsync(scope, id, tracking: false, cancellationToken);
        return draw is null ? null : await ToDetailAsync(draw, cancellationToken);
    }

    public async Task<AdminDrawDetailDto> CreateAsync(AdminClubScope scope, Guid id, UpsertAdminDrawRequest request, ImageFieldUpdate cover, CancellationToken cancellationToken)
    {
        var code = Validate(request);
        code ??= $"D{TaiwanClock.Today:yyyyMMdd}-{RandomNumberGenerator.GetInt32(0x10000):X4}";
        await EnsureCodeFreeAsync(scope, code, null, cancellationToken);
        var now = DateTime.UtcNow;
        var draw = new MemberDraw
        {
            Id = id, ClubId = scope.ClubId, DrawCode = code, Status = "draft", RosterVersion = 1, CoverKey = cover.Change ? cover.Key : null,
            CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        Apply(draw, request);
        db.MemberDraws.Add(draw);
        SetI18n(draw, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(scope, id, cancellationToken))!;
    }

    public async Task<AdminDrawDetailDto?> UpdateAsync(
        AdminClubScope scope, Guid id, UpsertAdminDrawRequest request, ImageFieldUpdate cover, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var newCode = Validate(request);
        var draw = await LoadAsync(scope, id, tracking: true, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        if (draw.Status is "closed" or "voided")
        {
            throw new AdminConflictException("活動已結束", "這個抽獎活動已結案或作廢，不能再編輯。");
        }

        if (draw.Status != "draft")
        {
            if (newCode is not null && !string.Equals(newCode, draw.DrawCode, StringComparison.Ordinal))
            {
                throw new AdminConflictException("名單已鎖定", "名單鎖定後不能變更活動代碼（它會出現在匯出檔名與稽核紀錄）。");
            }

            if (request.SnapshotAt is DateTime s && (draw.SnapshotAt is null || Math.Abs((Utc(s) - draw.SnapshotAt.Value).TotalMilliseconds) >= 1))
            {
                throw new AdminConflictException("名單已鎖定", "名單鎖定後不能變更資格基準時間；名單有誤請整份作廢重產。");
            }
        }
        else if (newCode is not null && !string.Equals(newCode, draw.DrawCode, StringComparison.Ordinal))
        {
            await EnsureCodeFreeAsync(scope, newCode, id, cancellationToken);
            draw.DrawCode = newCode;
        }

        if (cover.Change)
        {
            orphans.Image(draw.CoverKey);
            draw.CoverKey = cover.Key;
        }

        var keepSnapshot = draw.Status != "draft" ? draw.SnapshotAt : null;
        Apply(draw, request);
        if (draw.Status != "draft")
        {
            draw.SnapshotAt = keepSnapshot;
        }

        draw.UpdatedAt = DateTime.UtcNow;
        draw.UpdatedBy = scope.Identity.AdminUserId;
        SetI18n(draw, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var draw = await db.MemberDraws.FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return false;
        }

        if (draw.Status != "draft" || await db.DrawRosterVersions.AsNoTracking().AnyAsync(v => v.MemberDrawId == id, cancellationToken))
        {
            throw new AdminConflictException("活動不能刪除", "已經產生過名單的抽獎活動要保留供稽核，不能刪除；不辦了請按「作廢」。");
        }

        orphans.Image(draw.CoverKey);
        db.MemberDraws.Remove(draw);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ═════════════ 合格名單（快照）═════════════

    private sealed record Eligible(Guid MemberId, string MemberNo, string Name, string Tier, DateOnly? EndOn);

    private async Task<List<Eligible>> QueryEligibleAsync(Guid clubId, DateTime asOf, CancellationToken cancellationToken)
    {
        var asOfDate = TaiwanClock.ToDate(asOf);
        var rows = await db.Memberships.AsNoTracking()
            .Where(ms => ms.ClubId == clubId && ms.Tier == "fan_club" && (ms.Status == "active" || ms.Status == "expired")
                && (ms.MembershipStartOn == null || ms.MembershipStartOn <= asOfDate) && (ms.MembershipEndOn == null || ms.MembershipEndOn >= asOfDate)
                && ms.Member.Status == "active")
            .Select(ms => new Eligible(ms.MemberId, ms.Member.MemberNo, ms.Member.Name, ms.Tier, ms.MembershipEndOn)).ToListAsync(cancellationToken);
        // 狀態 expired（批次到期處理後）只要基準時間落在會籍期間內仍算合格——基準時間可能早於到期處理；cancelled／pending 一律不算。
        // 一人一號：同一位會員在這個俱樂部有多份有效會籍時，取到期日最晚的那一份。
        return rows.GroupBy(r => r.MemberId).Select(g => g.OrderByDescending(x => x.EndOn ?? DateOnly.MaxValue).First())
            .OrderBy(r => r.MemberNo, StringComparer.Ordinal).ToList();
    }

    public async Task<AdminRosterPreviewDto?> PreviewRosterAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var draw = await db.MemberDraws.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        var asOf = draw.SnapshotAt ?? throw new AdminValidationException("請先設定資格基準時間，才能試算合格人數。");
        return new AdminRosterPreviewDto { AsOf = asOf, EligibleCount = (await QueryEligibleAsync(scope.ClubId, asOf, cancellationToken)).Count };
    }

    public async Task<AdminDrawDetailDto?> GenerateRosterAsync(AdminClubScope scope, Guid id, GenerateAdminRosterRequest request, CancellationToken cancellationToken)
    {
        var draw = await LoadAsync(scope, id, tracking: true, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        var regenerate = draw.Status == "roster_locked";
        if (draw.Status != "draft" && !regenerate)
        {
            throw new AdminConflictException("不能產生名單", draw.Status is "drawn" or "announced"
                ? "已經進入抽出階段，名單不能重產；如果整個活動要重來，請按「作廢」後建立新的活動。"
                : "這個活動已結案或作廢，不能產生名單。");
        }

        var voidReason = regenerate ? AdminInput.RequireText(request.VoidReason, "作廢原因", 255) : null;
        if (!(await GetNoticeAsync(scope, cancellationToken)).Confirmed)
        {
            throw new AdminConflictException("尚未完成蒐集告知", "會員條款與註冊同意事項還沒有增列抽獎的蒐集告知（「會籍有效期間將自動列入球迷會員抽獎合格名單；中獎時，姓名將以遮罩方式於最新消息公布」），完成前不能舉辦抽獎。");
        }

        var asOf = draw.SnapshotAt ?? throw new AdminValidationException("請先設定資格基準時間。");
        var rules = draw.MemberDrawsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Rules;
        if (string.IsNullOrWhiteSpace(rules))
        {
            throw new AdminValidationException("活動辦法為必填：請先填寫活動辦法（獎品內容、名額、資格條件、基準時間、開獎時間與場合、領獎期限、主辦單位保留變更權利之範圍）。");
        }

        var eligible = await QueryEligibleAsync(scope.ClubId, asOf, cancellationToken);
        if (eligible.Count == 0)
        {
            throw new AdminConflictException("沒有合格會員", "基準時間當下這個俱樂部沒有任何合格的球迷會員，無法產生名單。");
        }

        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        if (regenerate)
        {
            var current = await db.DrawRosterVersions.FirstAsync(v => v.MemberDrawId == id && v.RosterVersion == draw.RosterVersion, cancellationToken);
            current.VoidedAt = now;
            current.VoidedBy = scope.Identity.AdminUserId;
            current.VoidReason = voidReason;
            current.UpdatedAt = now;
            draw.RosterVersion += 1;
        }

        var version = draw.RosterVersion;
        var hash = ComputeHash(draw.DrawCode, version, eligible);
        for (var i = 0; i < eligible.Count; i++)
        {
            var e = eligible[i];
            db.DrawRosters.Add(new DrawRoster
            {
                Id = Guid.NewGuid(), ClubId = scope.ClubId, MemberDrawId = id, RosterVersion = version, SerialNo = i + 1, MemberNoSnapshot = e.MemberNo,
                NameSnapshot = e.Name, TierSnapshot = e.Tier, MembershipEndOnSnapshot = e.EndOn, CreatedAt = now, UpdatedAt = now,
                CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
            });
        }

        db.DrawRosterVersions.Add(new DrawRosterVersion
        {
            Id = Guid.NewGuid(), MemberDrawId = id, RosterVersion = version, SnapshotAt = asOf, TotalCount = eligible.Count, RosterHash = hash,
            GeneratedBy = scope.Identity.AdminUserId, GeneratedAt = now, CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        });
        draw.TotalCount = eligible.Count;
        draw.RosterHash = hash;
        draw.LockedBy = scope.Identity.AdminUserId;
        draw.LockedAt = now;
        draw.Status = "roster_locked";
        draw.UpdatedAt = now;
        draw.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        audit.Record(scope, regenerate ? "作廢並重產抽獎合格名單" : "產生並鎖定抽獎合格名單", $"活動 {draw.DrawCode}／版本 {version}／雜湊 {hash}", eligible.Count, voidReason);
        return await GetAsync(scope, id, cancellationToken);
    }

    /// <summary>名單雜湊：對「活動代碼、版本、每列的序號｜會員編號｜姓名｜層級｜到期日」逐行取 SHA-256，供日後稽核名單有沒有被換過。</summary>
    public static string ComputeHash(string drawCode, int version, IReadOnlyList<(int Serial, string MemberNo, string? Name, string? Tier, DateOnly? EndOn)> rows)
    {
        var sb = new StringBuilder();
        sb.Append(drawCode).Append('|').Append(version).Append('\n');
        foreach (var r in rows)
        {
            sb.Append(r.Serial).Append('|').Append(r.MemberNo).Append('|').Append(r.Name).Append('|').Append(r.Tier).Append('|').Append(r.EndOn?.ToString("yyyy-MM-dd")).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    private static string ComputeHash(string drawCode, int version, IReadOnlyList<Eligible> eligible)
        => ComputeHash(drawCode, version, eligible.Select((e, i) => (i + 1, e.MemberNo, (string?)e.Name, (string?)e.Tier, e.EndOn)).ToList());

    public async Task<PagedResult<AdminRosterEntryDto>?> ListRosterAsync(
        AdminClubScope scope, Guid id, int? version, string? keyword, bool? winnersOnly, int page, int pageSize, CancellationToken cancellationToken, bool reveal = false)
    {
        var draw = await db.MemberDraws.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        // 與 K1 會員名單一致：名單預設遮罩，要 reveal=true 才解除，且需要 member.pii.reveal、每次解除寫敏感操作日誌。
        // （有權限卻自動顯示完整姓名，會讓現場投影或截圖直接洩漏；也會每翻一頁就寫一筆日誌。）
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        if (reveal && !canReveal)
        {
            throw new AdminForbiddenException("你的角色不能檢視會員的完整個資，請洽系統管理員。");
        }

        var v = version ?? draw.RosterVersion;
        var query = db.DrawRosters.AsNoTracking().Where(r => r.MemberDrawId == id && r.RosterVersion == v);
        if (winnersOnly == true)
        {
            query = query.Where(r => r.IsWinner || r.IsBackup);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = int.TryParse(k, out var serial)
                ? query.Where(r => r.SerialNo == serial || r.MemberNoSnapshot.Contains(k))
                : reveal ? query.Where(r => r.MemberNoSnapshot.Contains(k) || (r.NameSnapshot != null && r.NameSnapshot.Contains(k))) : query.Where(r => r.MemberNoSnapshot.Contains(k));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(r => r.SerialNo).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        if (reveal && rows.Count > 0)
        {
            audit.Record(scope, "檢視抽獎名單（完整姓名）", $"活動 {draw.DrawCode}／版本 {v}", rows.Count);
        }

        return new PagedResult<AdminRosterEntryDto>
        {
            Items = rows.Select(r => new AdminRosterEntryDto
            {
                SerialNo = r.SerialNo, MemberNo = r.MemberNoSnapshot, Name = reveal ? r.NameSnapshot : PiiMasking.MaskName(r.NameSnapshot), Tier = r.TierSnapshot,
                TierLabel = r.TierSnapshot is null ? null : MemberLabels.Of(MemberLabels.Tier, r.TierSnapshot), MembershipEndOn = r.MembershipEndOnSnapshot,
                IsWinner = r.IsWinner, IsBackup = r.IsBackup, PrizeName = r.PrizeName, IsMasked = !reveal,
            }).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    // ═════════════ 中獎人回填（序號為準，系統不抽出）═════════════

    public async Task<AdminWinnerResultDto?> RecordWinnersAsync(AdminClubScope scope, Guid id, RecordAdminWinnersRequest request, CancellationToken cancellationToken)
    {
        var draw = await db.MemberDraws.FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        RequireWinnerPhase(draw, request.Reason);
        if (request.Winners.Count is 0 or > 200)
        {
            throw new AdminValidationException("一次最多回填 200 筆，至少 1 筆。");
        }

        var serials = request.Winners.Select(w => w.SerialNo).ToList();
        if (serials.Distinct().Count() != serials.Count)
        {
            throw new AdminValidationException("回填清單裡有重複的序號。");
        }

        var rows = await db.DrawRosters.Where(r => r.MemberDrawId == id && r.RosterVersion == draw.RosterVersion && serials.Contains(r.SerialNo)).ToListAsync(cancellationToken);
        var missing = serials.Where(s => rows.All(r => r.SerialNo != s)).ToList();
        if (missing.Count > 0)
        {
            throw new AdminValidationException($"序號 {string.Join("、", missing)} 不在這一版名單裡，請確認序號。");
        }

        var now = DateTime.UtcNow;
        foreach (var w in request.Winners)
        {
            var row = rows.First(r => r.SerialNo == w.SerialNo);
            var prize = AdminInput.OptionalText(w.PrizeName, "獎項名稱", 128);
            if (w.IsBackup)
            {
                if (row.IsWinner)
                {
                    throw new AdminConflictException("已經是中獎人", $"序號 {row.SerialNo} 已經是中獎人，不能改成備取；請先取消中獎標記。");
                }

                row.IsBackup = true;
                row.PrizeName = prize;
            }
            else
            {
                row.IsWinner = true;
                row.IsBackup = false;
                row.PrizeName = prize ?? throw new AdminValidationException($"序號 {row.SerialNo} 中獎，請填寫獎項名稱。");
                row.FulfilmentStatus ??= "pending";
            }

            row.UpdatedAt = now;
            row.UpdatedBy = scope.Identity.AdminUserId;
        }

        if (draw.Status == "roster_locked" && request.Winners.Any(w => !w.IsBackup))
        {
            draw.Status = "drawn";
        }

        draw.UpdatedAt = now;
        draw.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "回填抽獎中獎人", $"活動 {draw.DrawCode}／序號 {string.Join(",", request.Winners.Select(w => $"{w.SerialNo}{(w.IsBackup ? "(備取)" : "")}"))}", request.Winners.Count, request.Reason);
        return new AdminWinnerResultDto { UpdatedCount = request.Winners.Count, Draw = (await GetAsync(scope, id, cancellationToken))! };
    }

    public async Task<AdminWinnerResultDto?> RemoveWinnersAsync(AdminClubScope scope, Guid id, RemoveAdminWinnersRequest request, CancellationToken cancellationToken)
    {
        var draw = await db.MemberDraws.FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        RequireWinnerPhase(draw, request.Reason);
        var serials = request.SerialNos.Distinct().ToList();
        if (serials.Count is 0 or > 200)
        {
            throw new AdminValidationException("一次最多處理 200 筆，至少 1 筆。");
        }

        var rows = await db.DrawRosters.Where(r => r.MemberDrawId == id && r.RosterVersion == draw.RosterVersion && serials.Contains(r.SerialNo) && (r.IsWinner || r.IsBackup)).ToListAsync(cancellationToken);
        if (rows.Count != serials.Count)
        {
            throw new AdminValidationException("清單裡有不是中獎人或備取的序號。");
        }

        foreach (var row in rows)
        {
            row.IsWinner = false;
            row.IsBackup = false;
            row.PrizeName = null;
            row.ClaimMethod = null;
            row.FulfilmentStatus = null;
            row.RecipientName = row.RecipientPhone = row.RecipientAddress = null;
            row.ClaimedAt = row.ShippedAt = null;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = scope.Identity.AdminUserId;
        }

        if (draw.Status == "drawn" && !await db.DrawRosters.AnyAsync(r => r.MemberDrawId == id && r.RosterVersion == draw.RosterVersion && (r.IsWinner || r.IsBackup) && !serials.Contains(r.SerialNo), cancellationToken))
        {
            draw.Status = "roster_locked";
        }

        draw.UpdatedAt = DateTime.UtcNow;
        draw.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "取消抽獎中獎標記", $"活動 {draw.DrawCode}／序號 {string.Join(",", serials)}", serials.Count, request.Reason);
        return new AdminWinnerResultDto { UpdatedCount = rows.Count, Draw = (await GetAsync(scope, id, cancellationToken))! };
    }

    private static void RequireWinnerPhase(MemberDraw draw, string? reason)
    {
        if (draw.Status is not ("roster_locked" or "drawn" or "announced"))
        {
            throw new AdminConflictException("現在不能回填中獎人", draw.Status == "draft" ? "還沒有產生合格名單，請先產生名單。" : "這個活動已結案或作廢，不能再修改中獎人。");
        }

        if (draw.Status == "announced" && string.IsNullOrWhiteSpace(reason))
        {
            throw new AdminValidationException("名單已經公布，再修改中獎人必須填寫異動原因。");
        }
    }

    // ═════════════ 獎品發放（比照 K3）═════════════

    private static string EffectiveStatus(DrawRoster r, DateOnly? deadline, DateOnly today)
        => r.FulfilmentStatus == "pending" && deadline is DateOnly d && d < today ? "overdue" : r.FulfilmentStatus ?? "pending";

    public async Task<PagedResult<AdminFulfilmentDto>?> ListFulfilmentAsync(
        AdminClubScope scope, Guid id, string? status, string? claimMethod, int page, int pageSize, CancellationToken cancellationToken)
    {
        var draw = await db.MemberDraws.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, FulfilmentLabels.Keys.ToHashSet(), "發放狀態", "待處理、已寄出、已領取或逾期");
        }

        if (!string.IsNullOrWhiteSpace(claimMethod))
        {
            AdminInput.OneOf(claimMethod, ClaimLabels.Keys.ToHashSet(), "領獎方式", "寄送或現場領取");
        }

        var canReveal = await CanRevealAsync(scope, cancellationToken);
        var winners = await db.DrawRosters.AsNoTracking().Where(r => r.MemberDrawId == id && r.RosterVersion == draw.RosterVersion && r.IsWinner)
            .OrderBy(r => r.SerialNo).ToListAsync(cancellationToken);
        var today = TaiwanClock.Today;
        var filtered = winners.Where(r => (string.IsNullOrWhiteSpace(status) || EffectiveStatus(r, draw.ClaimDeadlineOn, today) == status)
            && (string.IsNullOrWhiteSpace(claimMethod) || r.ClaimMethod == claimMethod)).ToList();
        var pageRows = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        if (canReveal && pageRows.Count > 0)
        {
            audit.Record(scope, "檢視抽獎獎品發放清單（完整收件資訊）", $"活動 {draw.DrawCode}", pageRows.Count);
        }

        return new PagedResult<AdminFulfilmentDto>
        {
            Items = pageRows.Select(r => ToFulfilment(r, draw.ClaimDeadlineOn, today, canReveal)).ToList(), Page = page, PageSize = pageSize, TotalCount = filtered.Count,
        };
    }

    public async Task<AdminFulfilmentDto?> UpdateFulfilmentAsync(AdminClubScope scope, Guid id, int serialNo, UpdateAdminFulfilmentRequest request, CancellationToken cancellationToken)
    {
        var draw = await db.MemberDraws.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        var row = await db.DrawRosters.FirstOrDefaultAsync(r => r.MemberDrawId == id && r.RosterVersion == draw.RosterVersion && r.SerialNo == serialNo && r.IsWinner, cancellationToken);
        if (row is null)
        {
            return null;
        }

        RequireFulfilmentPhase(draw);
        var canReveal = await CanRevealAsync(scope, cancellationToken);
        ApplyFulfilment(row, request, canReveal);
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return ToFulfilment(row, draw.ClaimDeadlineOn, TaiwanClock.Today, canReveal);
    }

    public async Task<AdminBatchFulfilmentResultDto?> BatchFulfilmentAsync(AdminClubScope scope, Guid id, BatchAdminFulfilmentRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Status, new HashSet<string>(["pending", "shipped", "claimed"]), "發放狀態", "待處理、已寄出或已領取");
        if (request.SerialNos.Count is 0 or > 200)
        {
            throw new AdminValidationException("一次最多處理 200 筆，至少選 1 筆。");
        }

        var draw = await db.MemberDraws.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        RequireFulfilmentPhase(draw);
        var serials = request.SerialNos.Distinct().ToList();
        var rows = await db.DrawRosters.Where(r => r.MemberDrawId == id && r.RosterVersion == draw.RosterVersion && r.IsWinner && serials.Contains(r.SerialNo)).ToListAsync(cancellationToken);
        var skipped = new List<AdminBatchFulfilmentSkippedDto>();
        var updated = 0;
        foreach (var serial in serials)
        {
            var row = rows.FirstOrDefault(r => r.SerialNo == serial);
            if (row is null)
            {
                skipped.Add(new AdminBatchFulfilmentSkippedDto { SerialNo = serial, Reason = "這個序號不是中獎人。" });
                continue;
            }

            try
            {
                ApplyFulfilment(row, new UpdateAdminFulfilmentRequest { Status = request.Status }, canReveal: false);
                row.UpdatedAt = DateTime.UtcNow;
                row.UpdatedBy = scope.Identity.AdminUserId;
                updated++;
            }
            catch (AdminValidationException ex)
            {
                skipped.Add(new AdminBatchFulfilmentSkippedDto { SerialNo = serial, Reason = ex.Message });
            }
        }

        await db.SaveChangesAsync();
        return new AdminBatchFulfilmentResultDto { UpdatedCount = updated, Skipped = skipped };
    }

    private static void RequireFulfilmentPhase(MemberDraw draw)
    {
        if (draw.Status is not ("drawn" or "announced"))
        {
            throw new AdminConflictException("現在不能處理發放", draw.Status is "closed" or "voided" ? "這個活動已結案或作廢，發放紀錄不能再修改。" : "還沒有回填中獎人。");
        }
    }

    private static void ApplyFulfilment(DrawRoster row, UpdateAdminFulfilmentRequest request, bool canReveal)
    {
        var editsPii = request.RecipientName is not null || request.RecipientPhone is not null || request.RecipientAddress is not null;
        if (editsPii && !canReveal)
        {
            throw new AdminForbiddenException("你的角色不能修改收件人資料，請洽客服／行政或系統管理員。");
        }

        if (request.ClaimMethod is not null)
        {
            row.ClaimMethod = AdminInput.OneOf(request.ClaimMethod, ClaimLabels.Keys.ToHashSet(), "領獎方式", "「寄送」或「現場領取」");
        }

        if (request.RecipientName is not null)
        {
            row.RecipientName = AdminInput.OptionalText(request.RecipientName, "收件人姓名", 64);
        }

        if (request.RecipientPhone is not null)
        {
            row.RecipientPhone = AdminInput.OptionalText(request.RecipientPhone, "收件人電話", 32);
        }

        if (request.RecipientAddress is not null)
        {
            row.RecipientAddress = AdminInput.OptionalText(request.RecipientAddress, "收件地址", 500);
        }

        if (request.Note is not null)
        {
            row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        }

        if (request.Status is null)
        {
            return;
        }

        AdminInput.OneOf(request.Status, new HashSet<string>(["pending", "shipped", "claimed"]), "發放狀態", "待處理、已寄出或已領取");
        var now = DateTime.UtcNow;
        switch (request.Status)
        {
            case "shipped":
                if (row.ClaimMethod != "ship")
                {
                    throw new AdminValidationException($"序號 {row.SerialNo}：只有領獎方式為「寄送」的才能標記已寄出，現場領取請直接標為已領取。");
                }

                if (string.IsNullOrWhiteSpace(row.RecipientName) || string.IsNullOrWhiteSpace(row.RecipientPhone) || string.IsNullOrWhiteSpace(row.RecipientAddress))
                {
                    throw new AdminValidationException($"序號 {row.SerialNo}：寄送必須有收件人姓名、電話與地址。");
                }

                row.ShippedAt ??= now;
                row.ClaimedAt = null;
                break;
            case "claimed":
                row.ClaimedAt ??= now;
                break;
            default:
                row.ShippedAt = null;
                row.ClaimedAt = null;
                break;
        }

        row.FulfilmentStatus = request.Status;
    }

    private static AdminFulfilmentDto ToFulfilment(DrawRoster r, DateOnly? deadline, DateOnly today, bool reveal)
    {
        var effective = EffectiveStatus(r, deadline, today);
        return new AdminFulfilmentDto
        {
            SerialNo = r.SerialNo, MemberNo = r.MemberNoSnapshot, MemberName = reveal ? r.NameSnapshot : PiiMasking.MaskName(r.NameSnapshot), IsBackup = r.IsBackup,
            PrizeName = r.PrizeName, ClaimMethod = r.ClaimMethod, ClaimMethodLabel = r.ClaimMethod is null ? null : ShopOrDefault(ClaimLabels, r.ClaimMethod),
            RecipientName = reveal ? r.RecipientName : PiiMasking.MaskName(r.RecipientName), RecipientPhone = reveal ? r.RecipientPhone : PiiMasking.MaskPhone(r.RecipientPhone),
            RecipientAddress = reveal ? r.RecipientAddress : PiiMasking.MaskAddress(r.RecipientAddress), FulfilmentStatus = r.FulfilmentStatus,
            EffectiveStatus = effective, EffectiveStatusLabel = ShopOrDefault(FulfilmentLabels, effective), ShippedAt = r.ShippedAt, ClaimedAt = r.ClaimedAt, Note = r.Note, IsMasked = !reveal,
        };
    }

    // ═════════════ 匯出（兩種名單 CSV＋出貨清單，權限不同）═════════════

    /// <summary>現場抽獎／可公開投影版：抽獎序號、會員編號、遮罩姓名、活動代碼、名單版本、基準時間（與 K1 檢視同級，須填用途）。</summary>
    public async Task<(string Csv, string FileName)?> ExportPublicAsync(AdminClubScope scope, Guid id, string? purpose, CancellationToken cancellationToken)
    {
        var purposeText = AdminInput.RequireText(purpose, "匯出用途", 200);
        var draw = await db.MemberDraws.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        RequireRoster(draw);
        var rows = await db.DrawRosters.AsNoTracking().Where(r => r.MemberDrawId == id && r.RosterVersion == draw.RosterVersion).OrderBy(r => r.SerialNo).ToListAsync(cancellationToken);
        var asOfText = TaiwanText(draw.SnapshotAt);
        var lines = new List<IEnumerable<string?>> { new[] { "抽獎序號", "會員編號", "姓名", "活動代碼", "名單版本", "基準時間" } };
        lines.AddRange(rows.Select(r => new[]
        {
            r.SerialNo.ToString(), r.MemberNoSnapshot, PiiMasking.MaskName(r.NameSnapshot), draw.DrawCode, draw.RosterVersion.ToString(), asOfText,
        }));
        audit.Record(scope, "匯出抽獎名單（遮罩公開版）", $"活動 {draw.DrawCode}／版本 {draw.RosterVersion}", rows.Count, purposeText);
        return (CsvUtils.BuildCsv(lines), $"draw-{draw.DrawCode}-v{draw.RosterVersion}-public.csv");
    }

    /// <summary>中獎人聯絡用（受限版）：<b>僅已回填的中獎人</b>，不得匯出全體合格名單的完整個資（資料最小化）。手機與 Email 取會員主檔目前的值。</summary>
    public async Task<(string Csv, string FileName)?> ExportWinnersAsync(AdminClubScope scope, Guid id, string? purpose, CancellationToken cancellationToken)
    {
        var purposeText = AdminInput.RequireText(purpose, "匯出用途", 200);
        var draw = await db.MemberDraws.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        RequireRoster(draw);
        var rows = await db.DrawRosters.AsNoTracking().Where(r => r.MemberDrawId == id && r.RosterVersion == draw.RosterVersion && r.IsWinner).OrderBy(r => r.SerialNo).ToListAsync(cancellationToken);
        var memberNos = rows.Select(r => r.MemberNoSnapshot).ToList();
        var members = await db.Members.AsNoTracking().Where(m => memberNos.Contains(m.MemberNo)).ToDictionaryAsync(m => m.MemberNo, cancellationToken);
        var lines = new List<IEnumerable<string?>> { new[] { "抽獎序號", "會員編號", "姓名", "手機", "Email", "領獎方式", "收件人", "收件電話", "收件地址", "獎項" } };
        lines.AddRange(rows.Select(r =>
        {
            members.TryGetValue(r.MemberNoSnapshot, out var m);
            return new[]
            {
                r.SerialNo.ToString(), r.MemberNoSnapshot, r.NameSnapshot, m?.Phone, m?.Email, r.ClaimMethod is null ? null : ShopOrDefault(ClaimLabels, r.ClaimMethod),
                r.RecipientName, r.RecipientPhone, r.RecipientAddress, r.PrizeName,
            };
        }));
        audit.Record(scope, "匯出抽獎中獎人聯絡名單（受限）", $"活動 {draw.DrawCode}／版本 {draw.RosterVersion}", rows.Count, purposeText);
        return (CsvUtils.BuildCsv(lines), $"draw-{draw.DrawCode}-v{draw.RosterVersion}-winners.csv");
    }

    /// <summary>獎品出貨清單（比照 K3 球衣出貨清單）：中獎人、獎項、領獎方式、收件資訊與發放狀態。</summary>
    public async Task<(string Csv, string FileName)?> ExportShippingAsync(AdminClubScope scope, Guid id, string? purpose, CancellationToken cancellationToken)
    {
        var purposeText = AdminInput.RequireText(purpose, "匯出用途", 200);
        var draw = await db.MemberDraws.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        RequireRoster(draw);
        var today = TaiwanClock.Today;
        var rows = await db.DrawRosters.AsNoTracking().Where(r => r.MemberDrawId == id && r.RosterVersion == draw.RosterVersion && r.IsWinner).OrderBy(r => r.SerialNo).ToListAsync(cancellationToken);
        var lines = new List<IEnumerable<string?>> { new[] { "抽獎序號", "會員編號", "獎項", "領獎方式", "收件人", "電話", "地址", "發放狀態", "備註" } };
        lines.AddRange(rows.Select(r => new[]
        {
            r.SerialNo.ToString(), r.MemberNoSnapshot, r.PrizeName, r.ClaimMethod is null ? null : ShopOrDefault(ClaimLabels, r.ClaimMethod), r.RecipientName, r.RecipientPhone,
            r.RecipientAddress, ShopOrDefault(FulfilmentLabels, EffectiveStatus(r, draw.ClaimDeadlineOn, today)), r.Note,
        }));
        audit.Record(scope, "匯出抽獎獎品出貨清單（受限）", $"活動 {draw.DrawCode}／版本 {draw.RosterVersion}", rows.Count, purposeText);
        return (CsvUtils.BuildCsv(lines), $"draw-{draw.DrawCode}-v{draw.RosterVersion}-shipping.csv");
    }

    private static void RequireRoster(MemberDraw draw)
    {
        if (draw.Status == "draft" || draw.TotalCount is null)
        {
            throw new AdminConflictException("還沒有名單", "這個活動還沒有產生合格名單，沒有可以匯出的資料。");
        }
    }

    // ═════════════ 公布（只走 B2 新聞）═════════════

    public async Task<AdminAnnouncementPreviewDto?> AnnouncementPreviewAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var draw = await LoadAsync(scope, id, tracking: false, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        var winners = await MaskedWinnersAsync(draw, cancellationToken);
        return new AdminAnnouncementPreviewDto
        {
            DrawName = draw.MemberDrawsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name ?? draw.DrawCode,
            PrizeDescription = draw.MemberDrawsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.PrizeDescription,
            SnapshotAt = draw.SnapshotAt, EligibleCount = draw.TotalCount ?? 0, Winners = winners,
        };
    }

    private async Task<List<AdminAnnouncementWinnerDto>> MaskedWinnersAsync(MemberDraw draw, CancellationToken cancellationToken)
        => (await db.DrawRosters.AsNoTracking().Where(r => r.MemberDrawId == draw.Id && r.RosterVersion == draw.RosterVersion && r.IsWinner).OrderBy(r => r.SerialNo).ToListAsync(cancellationToken))
            .Select(r => new AdminAnnouncementWinnerDto { SerialNo = r.SerialNo, MemberNo = r.MemberNoSnapshot, MaskedName = PiiMasking.MaskName(r.NameSnapshot), PrizeName = r.PrizeName }).ToList();

    /// <summary>產生公布稿草稿並交接 B2：分類 7.1 Club News（代碼 <c>club</c>）、加掛標籤「球迷會員抽獎／Member Draw」，一律是<b>草稿</b>，由公關潤稿後發布。
    /// 內容只含遮罩後的中獎名單（抽獎序號＋會員編號＋姓名遮罩），不含手機、Email、地址、生日或完整姓名。</summary>
    public async Task<AdminAnnouncementDraftDto?> CreateAnnouncementDraftAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var draw = await LoadAsync(scope, id, tracking: true, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        if (draw.Status is not ("drawn" or "announced"))
        {
            throw new AdminConflictException("還不能產生公布稿", "要先回填中獎人（狀態為「已抽出」）才能產生公布稿。");
        }

        if (draw.AnnouncementArticleId is not null)
        {
            throw new AdminConflictException("已有公布稿", "這個活動已經連結了公布文章；要換文章請用「連結既有文章」，或直接到最新消息編輯。");
        }

        var winners = await MaskedWinnersAsync(draw, cancellationToken);
        if (winners.Count == 0)
        {
            throw new AdminConflictException("還沒有中獎人", "還沒有回填任何中獎人，沒有名單可以公布。");
        }

        var zh = draw.MemberDrawsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var name = zh?.Name ?? draw.DrawCode;
        var blocks = new List<object> { new { type = "paragraph", text = $"「{name}」中獎名單公布。" } };
        if (!string.IsNullOrWhiteSpace(zh?.PrizeDescription))
        {
            blocks.Add(new { type = "paragraph", text = $"獎品：{zh!.PrizeDescription}" });
        }

        blocks.Add(new { type = "paragraph", text = $"資格基準時間：{TaiwanText(draw.SnapshotAt)}，合格人數 {draw.TotalCount} 人。" });
        blocks.AddRange(winners.Select(w => (object)new { type = "paragraph", text = $"序號 {w.SerialNo}｜會員編號 {w.MemberNo}｜{w.MaskedName}｜{w.PrizeName}" }));
        var articleId = Guid.NewGuid();
        var slug = $"member-draw-{draw.DrawCode.ToLowerInvariant()}";
        var request = new CreateArticleRequest
        {
            Slug = slug, CategoryCode = "club",
            Content = new AdminArticleContentInput
            {
                Zh = new AdminArticleLocaleContent { Title = BuildAnnouncementTitle(name), Summary = $"「{name}」抽獎結果，名單依規定遮罩。", Body = JsonSerializer.Serialize(new { blocks }, BodyJson) },
            },
            Tags = [new AdminArticleTagInput { Slug = MemberDrawTagSlug, NameZh = "球迷會員抽獎", NameEn = "Member Draw" }],
        };
        await articles.CreateAsync(scope, articleId, request, null, null, null, ImageFieldUpdate.Keep, scope.Identity.AdminUserId, cancellationToken);
        draw.AnnouncementArticleId = articleId;
        draw.UpdatedAt = DateTime.UtcNow;
        draw.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "產生抽獎公布稿草稿", $"活動 {draw.DrawCode}／文章 {slug}", winners.Count);
        return new AdminAnnouncementDraftDto { ArticleId = articleId, ArticleSlug = slug, Draw = (await GetAsync(scope, id, cancellationToken))! };
    }

    /// <summary>
    /// 公布稿標題。活動名稱內含任何全形括號【或】（前綴「【測試】…」、整段「【…】」、中間夾括號皆是）就不再外包【】，
    /// 否則會出現「【【測試】…】」雙層括號（E-149 同一類：後端產生的文字要對齊 docs/06 §1）。
    /// </summary>
    public static string BuildAnnouncementTitle(string name)
        => name.AsSpan().IndexOfAny('【', '】') >= 0 ? $"{name} 中獎名單公布" : $"【{name}】中獎名單公布";

    public async Task<AdminDrawDetailDto?> LinkAnnouncementAsync(AdminClubScope scope, Guid id, LinkAdminAnnouncementRequest request, CancellationToken cancellationToken)
    {
        var draw = await db.MemberDraws.FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        if (draw.Status is "closed" or "voided")
        {
            throw new AdminConflictException("活動已結束", "這個抽獎活動已結案或作廢。");
        }

        if (!await db.Articles.AsNoTracking().AnyAsync(a => a.Id == request.ArticleId && (a.ClubId == scope.ClubId || a.ClubId == null), cancellationToken))
        {
            throw new AdminValidationException("找不到指定的文章，請確認文章屬於目前的俱樂部。");
        }

        draw.AnnouncementArticleId = request.ArticleId;
        draw.UpdatedAt = DateTime.UtcNow;
        draw.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    // ═════════════ 狀態 ═════════════

    /// <summary>已抽出 → 已公布：連結的文章必須已發布。</summary>
    public async Task<AdminDrawDetailDto?> MarkAnnouncedAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var draw = await db.MemberDraws.Include(d => d.AnnouncementArticle).FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        if (draw.Status != "drawn")
        {
            throw new AdminConflictException("狀態不允許", $"只有「已抽出」的活動可以標記為已公布（目前是「{StatusLabels[draw.Status]}」）。");
        }

        if (draw.AnnouncementArticle is null)
        {
            throw new AdminConflictException("還沒有公布文章", "請先產生公布稿或連結最新消息的文章。");
        }

        if (draw.AnnouncementArticle.Status != "published")
        {
            throw new AdminConflictException("文章還沒發布", "連結的文章還沒有發布，請公關潤稿發布後再標記為已公布。");
        }

        draw.Status = "announced";
        draw.UpdatedAt = DateTime.UtcNow;
        draw.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<AdminDrawDetailDto?> CloseAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var draw = await db.MemberDraws.FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        if (draw.Status != "announced")
        {
            throw new AdminConflictException("狀態不允許", $"只有「已公布」的活動可以結案（目前是「{StatusLabels[draw.Status]}」）。");
        }

        draw.Status = "closed";
        draw.UpdatedAt = DateTime.UtcNow;
        draw.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "抽獎活動結案", $"活動 {draw.DrawCode}", 1);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<AdminDrawDetailDto?> VoidAsync(AdminClubScope scope, Guid id, VoidAdminDrawRequest request, CancellationToken cancellationToken)
    {
        var reason = AdminInput.RequireText(request.Reason, "作廢原因", 255);
        var draw = await db.MemberDraws.FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
        if (draw is null)
        {
            return null;
        }

        if (draw.Status is "closed" or "voided")
        {
            throw new AdminConflictException("狀態不允許", "這個活動已經結案或作廢了。");
        }

        draw.Status = "voided";
        draw.InternalNote = string.IsNullOrWhiteSpace(draw.InternalNote) ? $"作廢：{reason}" : $"{draw.InternalNote}\n作廢：{reason}";
        draw.UpdatedAt = DateTime.UtcNow;
        draw.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        audit.Record(scope, "抽獎活動作廢", $"活動 {draw.DrawCode}", 1, reason);
        return await GetAsync(scope, id, cancellationToken);
    }

    // ═════════════ 內部 ═════════════

    private static string? Validate(UpsertAdminDrawRequest request)
    {
        AdminInput.RequireText(request.Content.Zh.Name, "中文活動名稱", 128);
        AdminInput.OptionalText(request.Content.Zh.Rules, "中文活動辦法", 20000);
        AdminInput.OptionalText(request.Content.Zh.PrizeDescription, "中文獎品內容", 20000);
        AdminInput.OptionalText(request.Content.Zh.Notes, "中文注意事項", 20000);
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文活動名稱", 128);
            AdminInput.OptionalText(request.Content.En.Rules, "英文活動辦法", 20000);
            AdminInput.OptionalText(request.Content.En.PrizeDescription, "英文獎品內容", 20000);
            AdminInput.OptionalText(request.Content.En.Notes, "英文注意事項", 20000);
        }

        if (request.DrawOccasion is not null)
        {
            AdminInput.OneOf(request.DrawOccasion, OccasionLabels.Keys.ToHashSet(), "開獎場合", "「主場賽事日」「直播」或「其他」");
        }

        AdminInput.OptionalText(request.InternalNote, "內部備註", 4000);
        if (string.IsNullOrWhiteSpace(request.DrawCode))
        {
            return null;
        }

        var code = request.DrawCode.Trim();
        return DrawCodeFormat().IsMatch(code) ? code : throw new AdminValidationException("活動代碼只能使用英文字母、數字與連字號（-），開頭必須是英數字，長度不可超過 32 字。");
    }

    /// <summary>API 的時間戳一律是 UTC，回傳的 JSON 不帶時區記號（EF 讀出的 Kind 是 Unspecified）；送回來的無時區時間因此視為 UTC，只有明確標示本地時區的才換算。</summary>
    private static DateTime Utc(DateTime v) => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc);

    private static void Apply(MemberDraw draw, UpsertAdminDrawRequest request)
    {
        draw.DrawnAt = request.DrawnAt is DateTime d ? Utc(d) : null;
        draw.SnapshotAt = request.SnapshotAt is DateTime s ? Utc(s) : draw.DrawnAt is DateTime drawn ? TaiwanClock.StartOfDayUtc(TaiwanClock.ToDate(drawn)) : null;
        draw.DrawOccasion = request.DrawOccasion;
        draw.ClaimDeadlineOn = request.ClaimDeadlineOn;
        draw.InternalNote = string.IsNullOrWhiteSpace(request.InternalNote) ? null : request.InternalNote.Trim();
    }

    private async Task EnsureCodeFreeAsync(AdminClubScope scope, string code, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await db.MemberDraws.AsNoTracking().AnyAsync(d => d.ClubId == scope.ClubId && d.DrawCode == code && d.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("活動代碼重複", $"活動代碼「{code}」已經被這個俱樂部的另一個抽獎活動使用，請換一個。");
        }
    }

    private void SetI18n(MemberDraw draw, AdminDrawContentInput content)
    {
        Upsert(draw, RequestLocale.DefaultDbLocale, content.Zh);
        var en = draw.MemberDrawsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(draw, "en", content.En);
        }
        else if (en is not null)
        {
            draw.MemberDrawsI18ns.Remove(en);
            db.MemberDrawsI18ns.Remove(en);
        }
    }

    private void Upsert(MemberDraw draw, string locale, AdminDrawLocaleContent content)
    {
        var i18n = draw.MemberDrawsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (i18n is null)
        {
            i18n = new MemberDrawsI18n { MemberDrawId = draw.Id, Locale = locale };
            draw.MemberDrawsI18ns.Add(i18n);
            db.MemberDrawsI18ns.Add(i18n);
        }

        i18n.Name = content.Name.Trim();
        i18n.PrizeDescription = string.IsNullOrWhiteSpace(content.PrizeDescription) ? null : content.PrizeDescription;
        i18n.Rules = string.IsNullOrWhiteSpace(content.Rules) ? null : content.Rules;
        i18n.Notes = string.IsNullOrWhiteSpace(content.Notes) ? null : content.Notes;
    }

    private Task<MemberDraw?> LoadAsync(AdminClubScope scope, Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var query = db.MemberDraws.Include(d => d.MemberDrawsI18ns).AsQueryable();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(d => d.Id == id && d.ClubId == scope.ClubId, cancellationToken);
    }

    private static string? TaiwanText(DateTime? utc)
        => utc is DateTime v ? DateTime.SpecifyKind(v, DateTimeKind.Utc).AddHours(8).ToString("yyyy-MM-dd HH:mm") : null;

    private static string ShopOrDefault(IReadOnlyDictionary<string, string> map, string code) => map.TryGetValue(code, out var label) ? label : code;

    private async Task<AdminDrawDetailDto> ToDetailAsync(MemberDraw d, CancellationToken cancellationToken)
    {
        var versions = await db.DrawRosterVersions.AsNoTracking().Where(v => v.MemberDrawId == d.Id).OrderByDescending(v => v.RosterVersion).ToListAsync(cancellationToken);
        var adminIds = versions.Select(v => v.GeneratedBy).Append(d.LockedBy).Append(d.CreatedBy).OfType<Guid>().Distinct().ToList();
        var names = await db.AdminUsers.AsNoTracking().Where(a => adminIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.DisplayName, cancellationToken);
        string? Name(Guid? id) => id is Guid g && names.TryGetValue(g, out var n) ? n : null;
        var counts = await db.DrawRosters.AsNoTracking().Where(r => r.MemberDrawId == d.Id && r.RosterVersion == d.RosterVersion && (r.IsWinner || r.IsBackup))
            .Select(r => new { r.IsWinner, r.IsBackup, r.FulfilmentStatus }).ToListAsync(cancellationToken);
        string? articleStatus = null;
        if (d.AnnouncementArticleId is Guid articleId)
        {
            articleStatus = await db.Articles.AsNoTracking().Where(a => a.Id == articleId).Select(a => a.Status).FirstOrDefaultAsync(cancellationToken);
        }

        var zh = d.MemberDrawsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = d.MemberDrawsI18ns.FirstOrDefault(i => i.Locale == "en");
        var winners = counts.Count(c => c.IsWinner);
        var actions = new List<string>();
        switch (d.Status)
        {
            case "draft":
                actions.AddRange(["edit", "generate_roster", "void", "delete"]);
                break;
            case "roster_locked":
                actions.AddRange(["edit", "regenerate_roster", "record_winners", "void"]);
                break;
            case "drawn":
                actions.AddRange(["edit", "record_winners", "announce", "mark_announced", "void"]);
                break;
            case "announced":
                actions.AddRange(["edit", "record_winners", "close", "void"]);
                break;
        }

        return new AdminDrawDetailDto
        {
            Id = d.Id, DrawCode = d.DrawCode, SnapshotAt = d.SnapshotAt, DrawnAt = d.DrawnAt, DrawOccasion = d.DrawOccasion,
            DrawOccasionLabel = d.DrawOccasion is null ? null : ShopOrDefault(OccasionLabels, d.DrawOccasion), ClaimDeadlineOn = d.ClaimDeadlineOn, Status = d.Status,
            StatusLabel = StatusLabels[d.Status], RosterVersion = d.RosterVersion, TotalCount = d.TotalCount, RosterHash = d.RosterHash, LockedAt = d.LockedAt,
            LockedByName = Name(d.LockedBy), CoverKey = d.CoverKey, CoverUrl = d.CoverKey is null ? null : imageUrls.Resolve(d.CoverKey),
            CoverThumbUrl = d.CoverKey is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(d.CoverKey)), InternalNote = d.InternalNote,
            Zh = new AdminDrawLocaleContent { Name = zh?.Name ?? "", PrizeDescription = zh?.PrizeDescription, Rules = zh?.Rules, Notes = zh?.Notes },
            En = en is null ? null : new AdminDrawLocaleContent { Name = en.Name ?? "", PrizeDescription = en.PrizeDescription, Rules = en.Rules, Notes = en.Notes },
            WinnerCount = winners, BackupCount = counts.Count(c => c.IsBackup), FulfilledCount = counts.Count(c => c.IsWinner && c.FulfilmentStatus is "shipped" or "claimed"),
            AnnouncementArticleId = d.AnnouncementArticleId, AnnouncementStatus = articleStatus,
            AnnouncementStatusLabel = articleStatus is null ? null : ShopOrDefault(ArticleStatusLabels, articleStatus),
            Versions = versions.Select(v => new AdminDrawVersionDto
            {
                Version = v.RosterVersion, SnapshotAt = v.SnapshotAt, TotalCount = v.TotalCount, RosterHash = v.RosterHash, GeneratedAt = v.GeneratedAt,
                GeneratedByName = Name(v.GeneratedBy), VoidedAt = v.VoidedAt, VoidReason = v.VoidReason, IsCurrent = v.RosterVersion == d.RosterVersion && v.VoidedAt is null,
            }).ToList(),
            AvailableActions = actions, CreatedByName = Name(d.CreatedBy), CreatedAt = d.CreatedAt, UpdatedAt = d.UpdatedAt,
        };
    }
}
