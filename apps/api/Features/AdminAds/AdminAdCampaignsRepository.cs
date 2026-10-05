using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminAds;

/// <summary>
/// E5 投放檔期（App 規劃書 §7.4、§8.8）：檔期 CRUD、狀態機操作、衝突檢視、緊急暫停與 pacing 進度。
/// 一個檔期綁一個版位（不做多對多）；<b>合約金額</b>只有「檢視／編輯合約金額」權限才看得到與寫得進（規劃書 §11 補充規則 4）。
/// </summary>
public sealed class AdminAdCampaignsRepository(ClubDbContext dbContext, AdCreativeMapper creativeMapper, SensitiveActionLogger audit)
{
    private static readonly IReadOnlySet<string> GoalTypes = new HashSet<string>(["guaranteed", "traffic"], StringComparer.Ordinal);

    public async Task<IReadOnlyList<AdminAdCampaignListItemDto>> ListAsync(
        string? status, Guid? slotId, Guid? advertiserId, string? keyword, CancellationToken cancellationToken)
    {
        await AdCampaignLifecycle.AdvanceAsync(dbContext, DateTime.UtcNow, cancellationToken);
        if (status is not null && !AdLabels.CampaignStatus.ContainsKey(status))
        {
            throw new AdminValidationException("狀態篩選不正確。");
        }

        var q = dbContext.AdCampaigns.AsNoTracking().AsQueryable();
        if (status is not null) { q = q.Where(c => c.Status == status); }
        if (slotId is not null) { q = q.Where(c => c.SlotId == slotId); }
        if (advertiserId is not null) { q = q.Where(c => c.AdvertiserId == advertiserId); }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(c => c.Name.Contains(k));
        }

        var rows = await q.OrderByDescending(c => c.StartsAt).ThenBy(c => c.RowSeq)
            .Select(c => new
            {
                c.Id, c.Name, c.AdvertiserId, c.SlotId, SlotCode = c.Slot.SlotCode, c.StartsAt, c.EndsAt, c.Weight, c.GoalType,
                c.GoalImpressions, c.DeliveredTotal, c.Status, c.UpdatedAt,
                AdvertiserName = c.Advertiser.AdvertisersI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                SlotName = c.Slot.AdSlotsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                CreativeCount = c.AdCreatives.Count,
                ApprovedCount = c.AdCreatives.Count(x => x.ReviewStatus == "approved"),
            }).Take(500).ToListAsync(cancellationToken);
        return rows.Select(r => new AdminAdCampaignListItemDto
        {
            Id = r.Id, Name = r.Name, AdvertiserId = r.AdvertiserId, AdvertiserName = r.AdvertiserName, SlotId = r.SlotId, SlotCode = r.SlotCode,
            SlotName = r.SlotName, StartsAt = r.StartsAt, EndsAt = r.EndsAt, Weight = r.Weight, GoalType = r.GoalType,
            GoalTypeLabel = AdLabels.Of(AdLabels.GoalType, r.GoalType), GoalImpressions = r.GoalImpressions, DeliveredTotal = r.DeliveredTotal,
            Status = r.Status, StatusLabel = AdLabels.Of(AdLabels.CampaignStatus, r.Status), CreativeCount = r.CreativeCount,
            ApprovedCreativeCount = r.ApprovedCount, UpdatedAt = r.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminAdCampaignDetailDto?> GetAsync(Guid id, AdCaller caller, CancellationToken cancellationToken)
    {
        await AdCampaignLifecycle.AdvanceAsync(dbContext, DateTime.UtcNow, cancellationToken);
        var c = await dbContext.AdCampaigns.AsNoTracking().Include(x => x.AdCreatives)
            .Include(x => x.Advertiser).ThenInclude(a => a.AdvertisersI18ns).Include(x => x.Slot).ThenInclude(s => s.AdSlotsI18ns).Include(x => x.ReviewedByNavigation)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return c is null ? null : ToDetail(c, caller);
    }

    public async Task<AdminAdCampaignDetailDto> CreateAsync(UpsertAdminAdCampaignRequest request, AdCaller caller, Guid? operatorId, CancellationToken cancellationToken)
    {
        var (starts, ends) = await ValidateAsync(request, cancellationToken);
        var now = DateTime.UtcNow;
        var c = new AdCampaign { Id = Guid.NewGuid(), Status = AdCampaignLifecycle.Draft, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId };
        ApplyAll(c, request, starts, ends, caller, isCreate: true);
        dbContext.AdCampaigns.Add(c);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(c.Id, caller, cancellationToken))!;
    }

    public async Task<AdminAdCampaignDetailDto?> UpdateAsync(
        Guid id, UpsertAdminAdCampaignRequest request, AdCaller caller, Guid? operatorId, CancellationToken cancellationToken)
    {
        var c = await dbContext.AdCampaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null)
        {
            return null;
        }

        var (starts, ends) = await ValidateAsync(request, cancellationToken);
        switch (c.Status)
        {
            case AdCampaignLifecycle.Draft:
                ApplyAll(c, request, starts, ends, caller, isCreate: false);
                break;
            case AdCampaignLifecycle.PendingReview:
                throw new AdminConflictException("檔期正在審核", "檔期已送出審核，請先退回草稿再修改。");
            case AdCampaignLifecycle.Scheduled or AdCampaignLifecycle.Running or AdCampaignLifecycle.Paused:
                if (request.AdvertiserId != c.AdvertiserId || request.SlotId != c.SlotId || starts != c.StartsAt || ends != c.EndsAt
                    || (request.GoalType ?? "traffic") != c.GoalType || request.GoalImpressions != c.GoalImpressions)
                {
                    throw new AdminConflictException("檔期已排程",
                        "檔期已經排程或投放中，只能調整名稱、權重與曝光上限；要改廣告主、版位、期間或目標，請先作廢再重新建立。");
                }

                c.Name = request.Name.Trim();
                c.Weight = request.Weight ?? c.Weight;
                c.DailyImpressionCap = request.DailyImpressionCap;
                c.PerDeviceDailyCap = request.PerDeviceDailyCap;
                ApplyAmount(c, request, caller);
                break;
            default:
                throw new AdminConflictException("檔期已結束", "已結束、已結案或已作廢的檔期不能再修改。");
        }

        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, caller, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var c = await dbContext.AdCampaigns.Include(x => x.AdCreatives).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null)
        {
            return false;
        }

        if (c.Status != AdCampaignLifecycle.Draft)
        {
            throw new AdminConflictException("檔期不是草稿", "只有草稿檔期可以刪除；已送審或投放過的檔期請改用「作廢」。");
        }

        dbContext.AdCampaigns.Remove(c);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ═════════ 狀態操作 ═════════

    public Task<AdminAdCampaignDetailDto?> SubmitAsync(Guid id, AdCaller caller, AdminIdentity actor, CancellationToken ct)
        => TransitionAsync(id, caller, actor, async c =>
        {
            Require(c, [AdCampaignLifecycle.Draft], "只有草稿檔期可以送審。");
            if (!await dbContext.AdCreatives.AnyAsync(x => x.CampaignId == c.Id, ct))
            {
                throw new AdminConflictException("還沒有素材", "檔期還沒有任何素材，請先上傳素材再送審。");
            }

            c.Status = AdCampaignLifecycle.PendingReview;
        }, ct);

    public Task<AdminAdCampaignDetailDto?> ApproveAsync(Guid id, AdCaller caller, AdminIdentity actor, CancellationToken ct)
        => TransitionAsync(id, caller, actor, async c =>
        {
            Require(c, [AdCampaignLifecycle.PendingReview], "只有待審核的檔期可以核可。");
            if (!await dbContext.AdCreatives.AnyAsync(x => x.CampaignId == c.Id && x.ReviewStatus == "approved", ct))
            {
                throw new AdminConflictException("素材尚未通過審核", "檔期至少要有一個通過審核的素材才能排程；沒審過的素材不能上線。");
            }

            c.Status = AdCampaignLifecycle.Scheduled;
            c.ReviewedBy = actor.AdminUserId;
            c.ReviewedAt = DateTime.UtcNow;
        }, ct);

    public Task<AdminAdCampaignDetailDto?> ReturnAsync(Guid id, AdCaller caller, AdminIdentity actor, CancellationToken ct)
        => TransitionAsync(id, caller, actor, c =>
        {
            Require(c, [AdCampaignLifecycle.PendingReview], "只有待審核的檔期可以退回。");
            c.Status = AdCampaignLifecycle.Draft;
            return Task.CompletedTask;
        }, ct);

    /// <summary>緊急暫停：單一檔期立即停止投放（規劃書 §7.9 第 3 點，違規或申訴時的即時下架機制）。原因必填。</summary>
    public Task<AdminAdCampaignDetailDto?> PauseAsync(Guid id, string? reason, AdCaller caller, AdminIdentity actor, CancellationToken ct)
        => TransitionAsync(id, caller, actor, c =>
        {
            Require(c, [AdCampaignLifecycle.Scheduled, AdCampaignLifecycle.Running], "只有已排程或投放中的檔期可以暫停。");
            c.PausedFrom = c.Status;
            c.Status = AdCampaignLifecycle.Paused;
            c.PauseReason = AdminInput.RequireText(reason, "暫停原因", 255);
            return Task.CompletedTask;
        }, ct);

    public Task<AdminAdCampaignDetailDto?> ResumeAsync(Guid id, AdCaller caller, AdminIdentity actor, CancellationToken ct)
        => TransitionAsync(id, caller, actor, async c =>
        {
            Require(c, [AdCampaignLifecycle.Paused], "只有已暫停的檔期可以恢復。");
            var now = DateTime.UtcNow;
            if (c.EndsAt <= now)
            {
                c.Status = AdCampaignLifecycle.Ended;
            }
            else if (c.StartsAt > now)
            {
                c.Status = AdCampaignLifecycle.Scheduled;
            }
            else
            {
                if (!await dbContext.AdCreatives.AnyAsync(x => x.CampaignId == c.Id && x.ReviewStatus == "approved" && !x.IsPaused, ct))
                {
                    throw new AdminConflictException("沒有可投放的素材", "檔期目前沒有「通過審核且未暫停」的素材，不能恢復投放。");
                }

                c.Status = AdCampaignLifecycle.Running;
            }

            c.PausedFrom = null;
            c.PauseReason = null;
        }, ct);

    public Task<AdminAdCampaignDetailDto?> CloseAsync(Guid id, AdCaller caller, AdminIdentity actor, CancellationToken ct)
        => TransitionAsync(id, caller, actor, c =>
        {
            Require(c, [AdCampaignLifecycle.Ended], "只有已結束的檔期可以結案。");
            c.Status = AdCampaignLifecycle.Closed;
            return Task.CompletedTask;
        }, ct);

    /// <summary>作廢：任一狀態可轉入，不可逆。原因必填。</summary>
    public Task<AdminAdCampaignDetailDto?> VoidAsync(Guid id, string? reason, AdCaller caller, AdminIdentity actor, CancellationToken ct)
        => TransitionAsync(id, caller, actor, c =>
        {
            if (c.Status == AdCampaignLifecycle.Voided)
            {
                throw new AdminConflictException("已經作廢", "這個檔期已經作廢了。");
            }

            c.PauseReason = AdminInput.RequireText(reason, "作廢原因", 255);
            c.PausedFrom = null;
            c.Status = AdCampaignLifecycle.Voided;
            return Task.CompletedTask;
        }, ct);

    private async Task<AdminAdCampaignDetailDto?> TransitionAsync(
        Guid id, AdCaller caller, AdminIdentity actor, Func<AdCampaign, Task> mutate, CancellationToken cancellationToken)
    {
        await AdCampaignLifecycle.AdvanceAsync(dbContext, DateTime.UtcNow, cancellationToken);
        var c = await dbContext.AdCampaigns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (c is null)
        {
            return null;
        }

        var before = c.Status;
        await mutate(c);
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = actor.AdminUserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        if (c.Status != before)
        {
            audit.Record(actor, $"廣告檔期狀態 {before}→{c.Status}", $"檔期 {c.Id}", 1, c.PauseReason);
        }

        return await GetAsync(id, caller, cancellationToken);
    }

    private static void Require(AdCampaign c, string[] allowed, string message)
    {
        if (!allowed.Contains(c.Status))
        {
            throw new AdminConflictException("目前狀態不能這樣操作", message);
        }
    }

    // ═════════ 衝突檢視 ═════════

    /// <summary>同版位同時段的檔期清單與權重佔比預覽（規劃書 §8.8「檔期衝突檢視」）。
    /// 只納入仍會投放的狀態（待審核、已排程、投放中、已暫停）；佔比是「期間內同時在輪播的檔期」之間的相對權重。</summary>
    public async Task<AdminAdScheduleDto?> ScheduleAsync(Guid slotId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        var slot = await dbContext.AdSlots.AsNoTracking().FirstOrDefaultAsync(s => s.Id == slotId, cancellationToken);
        if (slot is null)
        {
            return null;
        }

        var windowFrom = from ?? DateTime.UtcNow;
        var windowTo = to ?? windowFrom.AddDays(30);
        if (windowTo <= windowFrom)
        {
            throw new AdminValidationException("結束時間必須晚於開始時間。");
        }

        string[] live = [AdCampaignLifecycle.PendingReview, AdCampaignLifecycle.Scheduled, AdCampaignLifecycle.Running, AdCampaignLifecycle.Paused];
        var rows = await dbContext.AdCampaigns.AsNoTracking()
            .Where(c => c.SlotId == slotId && live.Contains(c.Status) && c.StartsAt < windowTo && c.EndsAt > windowFrom)
            .Select(c => new
            {
                c.Id, c.Name, c.StartsAt, c.EndsAt, c.Weight, c.Status,
                Advertiser = c.Advertiser.AdvertisersI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            }).OrderBy(c => c.StartsAt).ToListAsync(cancellationToken);

        var total = rows.Sum(r => r.Weight);
        var items = rows.Select(r => new AdminAdScheduleItemDto
        {
            CampaignId = r.Id, Name = r.Name, AdvertiserName = r.Advertiser, StartsAt = r.StartsAt, EndsAt = r.EndsAt, Weight = r.Weight,
            Status = r.Status, StatusLabel = AdLabels.Of(AdLabels.CampaignStatus, r.Status),
            WeightSharePercent = total == 0 ? 0 : (int)Math.Round(100.0 * r.Weight / total),
        }).ToList();

        // 期間內同時最多幾個檔期重疊（掃描起訖事件點）。
        var points = rows.SelectMany(r => new[] { (Time: r.StartsAt, Delta: 1), (Time: r.EndsAt, Delta: -1) })
            .OrderBy(p => p.Time).ThenBy(p => p.Delta).ToList();
        int current = 0, max = 0;
        foreach (var p in points)
        {
            current += p.Delta;
            max = Math.Max(max, current);
        }

        return new AdminAdScheduleDto
        {
            SlotId = slot.Id, SlotCode = slot.SlotCode, RotationCap = slot.RotationCap, MaxConcurrent = max,
            ExceedsRotationCap = max > slot.RotationCap, Items = items,
        };
    }

    // ═════════ 驗證與套用 ═════════

    private async Task<(DateTime Starts, DateTime Ends)> ValidateAsync(UpsertAdminAdCampaignRequest r, CancellationToken cancellationToken)
    {
        AdminInput.RequireText(r.Name, "檔期名稱", 160);
        var starts = r.StartsAt.UtcDateTime;
        var ends = r.EndsAt.UtcDateTime;
        if (ends <= starts)
        {
            throw new AdminValidationException("檔期的結束時間必須晚於開始時間。");
        }

        if (r.Weight is < 1 or > 100)
        {
            throw new AdminValidationException("輪播權重只能是 1 到 100。");
        }

        AdminInput.OptionalNonNegative(r.DailyImpressionCap, "每日曝光上限");
        AdminInput.OptionalNonNegative(r.PerDeviceDailyCap, "每人頻次上限");
        var goalType = r.GoalType ?? "traffic";
        AdminInput.OneOf(goalType, GoalTypes, "目標類型", "「曝光保證」或「導流」");
        if (goalType == "guaranteed" && r.GoalImpressions is null or <= 0)
        {
            throw new AdminValidationException("曝光保證型的檔期必須填目標曝光次數。");
        }

        AdminInput.OptionalNonNegative(r.GoalImpressions, "目標曝光次數");
        AdminInput.OptionalNonNegative(r.ContractAmount, "合約金額");

        var advertiser = await dbContext.Advertisers.AsNoTracking().FirstOrDefaultAsync(a => a.Id == r.AdvertiserId, cancellationToken)
            ?? throw new AdminValidationException("找不到這個廣告主，請重新挑選。");
        if (advertiser.Status == "ended")
        {
            throw new AdminValidationException("這個廣告主的合作已經結束，不能再建立新檔期。");
        }

        var slot = await dbContext.AdSlots.AsNoTracking().FirstOrDefaultAsync(s => s.Id == r.SlotId, cancellationToken)
            ?? throw new AdminValidationException("找不到這個版位，請重新挑選。");
        if (!slot.IsActive)
        {
            throw new AdminValidationException("這個版位已停用，不能排入新的檔期。");
        }

        return (starts, ends);
    }

    private static void ApplyAll(AdCampaign c, UpsertAdminAdCampaignRequest r, DateTime starts, DateTime ends, AdCaller caller, bool isCreate)
    {
        c.AdvertiserId = r.AdvertiserId;
        c.SlotId = r.SlotId;
        c.Name = r.Name.Trim();
        c.StartsAt = starts;
        c.EndsAt = ends;
        c.Weight = r.Weight ?? 1;
        c.DailyImpressionCap = r.DailyImpressionCap;
        c.PerDeviceDailyCap = r.PerDeviceDailyCap;
        c.GoalType = r.GoalType ?? "traffic";
        c.GoalImpressions = c.GoalType == "guaranteed" ? r.GoalImpressions : r.GoalImpressions;
        ApplyAmount(c, r, caller, isCreate);
    }

    private static void ApplyAmount(AdCampaign c, UpsertAdminAdCampaignRequest r, AdCaller caller, bool isCreate = false)
    {
        if (caller.CanEditAmount)
        {
            c.ContractAmount = r.ContractAmount;
            c.IsAmountHidden = r.IsAmountHidden ?? c.IsAmountHidden;
            return;
        }

        if (r.ContractAmount is not null || r.IsAmountHidden is not null)
        {
            throw new AdminForbiddenException("你的角色沒有編輯合約金額的權限。");
        }

        if (isCreate)
        {
            c.IsAmountHidden = true;
        }
    }

    private AdminAdCampaignDetailDto ToDetail(AdCampaign c, AdCaller caller)
    {
        string? Adv() => c.Advertiser.AdvertisersI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name;
        string? SlotName() => c.Slot.AdSlotsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name;
        return new AdminAdCampaignDetailDto
        {
            Id = c.Id, Name = c.Name, AdvertiserId = c.AdvertiserId, AdvertiserName = Adv(), SlotId = c.SlotId, SlotCode = c.Slot.SlotCode,
            SlotName = SlotName(), StartsAt = c.StartsAt, EndsAt = c.EndsAt, Weight = c.Weight, DailyImpressionCap = c.DailyImpressionCap,
            PerDeviceDailyCap = c.PerDeviceDailyCap, GoalType = c.GoalType, GoalTypeLabel = AdLabels.Of(AdLabels.GoalType, c.GoalType),
            GoalImpressions = c.GoalImpressions,
            DeliveredToday = c.DeliveredOn == DateOnly.FromDateTime(DateTime.UtcNow) ? c.DeliveredToday : 0,
            DeliveredTotal = c.DeliveredTotal,
            ContractAmount = caller.CanViewAmount ? c.ContractAmount : null,
            ContractAmountLabel = !caller.CanViewAmount ? "不公開" : c.ContractAmount is null ? "尚未填寫" : $"NT$ {c.ContractAmount:N0}",
            IsAmountHidden = caller.CanViewAmount ? c.IsAmountHidden : null,
            Status = c.Status, StatusLabel = AdLabels.Of(AdLabels.CampaignStatus, c.Status), PauseReason = c.PauseReason,
            ReviewedBy = c.ReviewedBy, ReviewedByName = c.ReviewedByNavigation?.DisplayName, ReviewedAt = c.ReviewedAt,
            AvailableActions = AdCampaignLifecycle.AvailableActions(c.Status, caller.CanUpdate, caller.CanReview, caller.CanPause),
            Pacing = ComputePacing(c, DateTime.UtcNow),
            Creatives = c.AdCreatives.OrderBy(x => x.SortOrder).ThenBy(x => x.RowSeq).Select(creativeMapper.ToDto).ToList(),
            UpdatedAt = c.UpdatedAt,
        };
    }

    /// <summary>曝光保證型的 pacing：目標平均分配到檔期天數；已達成 vs 依天數應達成，±10% 內算正常。</summary>
    public static AdminAdPacingDto? ComputePacing(AdCampaign c, DateTime utcNow)
    {
        if (c.GoalType != "guaranteed" || c.GoalImpressions is not > 0)
        {
            return null;
        }

        var goal = c.GoalImpressions.Value;
        var totalDays = Math.Max(1, (int)Math.Ceiling((c.EndsAt - c.StartsAt).TotalDays));
        var elapsed = utcNow <= c.StartsAt ? 0 : Math.Min(totalDays, (int)Math.Ceiling((utcNow - c.StartsAt).TotalDays));
        var expected = (int)Math.Round((double)goal * elapsed / totalDays);
        var status = c.DeliveredTotal >= expected * 1.1 ? "ahead" : c.DeliveredTotal >= expected * 0.9 ? "on_track" : "behind";
        return new AdminAdPacingDto
        {
            GoalImpressions = goal, Delivered = c.DeliveredTotal, ExpectedByNow = expected, DailyTarget = (int)Math.Ceiling((double)goal / totalDays),
            Status = status, StatusLabel = status switch { "ahead" => "超前", "on_track" => "正常", _ => "落後" },
        };
    }
}
