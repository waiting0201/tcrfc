using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.MemberCenter;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.MembershipPublic;

/// <summary>
/// 會員方案、權益對照表與特約店家的公開讀取（主站規劃書 §3.14「權益對照表：未登入即可檢視，不得要求先註冊才能看」、§3.8 8.4「公開頁面，未登入即可瀏覽」）。
/// 只回 <c>published</c>（上架）的資料；雙語依 <c>?lang</c> 挑欄位，英文缺漏回退繁中（<see cref="RequestLocale.Pick"/>，與全站其他公開端點同一規則）。
/// <b>不快取</b>：後台 K2／K4 寫入端尚未接公開快取失效（本批不改既有後台程式），讓客服改了權益、店家馬上看得到；資料量極小（兩隊各數筆到數十筆）。
/// 方案與店家都帶 <c>club_id</c>（店家可為空＝兩隊共同）：方案一律限定本俱樂部；店家取本俱樂部專屬與共同兩者（<see cref="ClubOrSharedSql"/> 的同一個讀法）。
/// </summary>
public sealed class MembershipPublicRepository(ClubDbContext db, IImagePublicUrlResolver imageUrls)
{
    private static readonly string[] GroupOrder = ["member_card", "store_discount", "jersey", "event"];

    private static readonly IReadOnlyDictionary<string, (string Zh, string En)> GroupLabels = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["member_card"] = ("會員卡", "Membership card"),
        ["store_discount"] = ("店家折扣", "Partner discounts"),
        ["jersey"] = ("球衣", "Jersey"),
        ["event"] = ("活動", "Events"),
    };

    // ═══════════════════════════ 方案 ═══════════════════════════

    /// <summary>目前可購買的方案（已上架、球季尚未結束）。排序：球季起始日、排序值。</summary>
    public async Task<IReadOnlyList<MembershipPlanPublicDto>> ListPlansAsync(ClubScope club, string dbLocale, CancellationToken cancellationToken)
    {
        var today = TaiwanClock.Today;
        var plans = await db.MembershipPlans.AsNoTracking().Include(p => p.Season).Include(p => p.MembershipPlansI18ns)
            .Where(p => p.ClubId == club.ClubId && p.Status == "published" && p.Season.EndOn >= today && (p.EndsOn == null || p.EndsOn >= today))
            .OrderBy(p => p.Season.StartOn).ThenBy(p => p.SortOrder).ThenBy(p => p.RowSeq)
            .AsSplitQuery().ToListAsync(cancellationToken);
        return plans.Select(p =>
        {
            var requested = p.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == dbLocale);
            var fallback = p.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
            return new MembershipPlanPublicDto
            {
                Code = p.Code, Name = RequestLocale.Pick(requested?.Name, fallback?.Name), IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requested?.Name), BenefitNote = RequestLocale.Pick(requested?.BenefitNote, fallback?.BenefitNote),
                Fee = p.Fee, CardQuota = p.CardQuota, JerseyQuota = p.JerseyQuota, MidSeasonRule = RequestLocale.Pick(requested?.MidSeasonRule, fallback?.MidSeasonRule), SeasonCode = p.Season.Code,
                StartsOn = p.StartsOn ?? p.Season.StartOn, EndsOn = p.EndsOn ?? p.Season.EndOn,
            };
        }).ToList();
    }

    // ═══════════════════════════ 權益對照表 ═══════════════════════════

    /// <summary>權益對照表。<paramref name="planCode"/> 省略＝取目前最早的上架方案。方案不存在回 null（呼叫端 404）。</summary>
    public async Task<BenefitTablePublicDto?> GetBenefitsAsync(ClubScope club, string? planCode, string dbLocale, CancellationToken cancellationToken)
    {
        var today = TaiwanClock.Today;
        var planQuery = db.MembershipPlans.AsNoTracking().Include(p => p.Season).Include(p => p.MembershipPlansI18ns)
            .Where(p => p.ClubId == club.ClubId && p.Status == "published");
        var plan = string.IsNullOrWhiteSpace(planCode)
            ? await planQuery.Where(p => p.Season.EndOn >= today).OrderBy(p => p.Season.StartOn).ThenBy(p => p.SortOrder).ThenBy(p => p.RowSeq).AsSplitQuery().FirstOrDefaultAsync(cancellationToken)
            : await planQuery.AsSplitQuery().FirstOrDefaultAsync(p => p.Code == planCode, cancellationToken);
        if (plan is null)
        {
            return string.IsNullOrWhiteSpace(planCode) ? new BenefitTablePublicDto { Groups = [], IsFallbackLocale = false } : null;
        }

        var benefits = await db.MembershipBenefits.AsNoTracking().Include(b => b.MembershipBenefitsI18ns)
            .Where(b => b.MembershipPlanId == plan.Id && b.Status == "published").OrderBy(b => b.SortOrder).ThenBy(b => b.RowSeq)
            .AsSplitQuery().ToListAsync(cancellationToken);
        var groups = benefits.GroupBy(b => b.BenefitGroup)
            .OrderBy(g => Array.IndexOf(GroupOrder, g.Key) is var i and >= 0 ? i : int.MaxValue)
            .Select(g =>
            {
                var items = g.Select(b =>
                {
                    var requested = b.MembershipBenefitsI18ns.FirstOrDefault(i => i.Locale == dbLocale);
                    var fallback = b.MembershipBenefitsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
                    return new BenefitItemPublicDto
                    {
                        Name = RequestLocale.Pick(requested?.Name, fallback?.Name), IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requested?.Name), Description = RequestLocale.Pick(requested?.Description, fallback?.Description),
                        FreeValue = RequestLocale.Pick(requested?.FreeValue, fallback?.FreeValue), PaidValue = RequestLocale.Pick(requested?.PaidValue, fallback?.PaidValue),
                    };
                }).ToList();
                var en = dbLocale == "en";
                var label = GroupLabels.TryGetValue(g.Key, out var l) ? (en ? l.En : l.Zh) : g.Key;
                return new BenefitGroupPublicDto { Group = g.Key, GroupLabel = label, Items = items };
            }).ToList();

        var planRequested = plan.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == dbLocale);
        var planFallback = plan.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        return new BenefitTablePublicDto { PlanCode = plan.Code, PlanName = RequestLocale.Pick(planRequested?.Name, planFallback?.Name), IsFallbackLocale = RequestLocale.IsFallback(dbLocale, planRequested?.Name), Groups = groups };
    }

    // ═══════════════════════════ 特約店家 ═══════════════════════════

    private IQueryable<PartnerStore> VisibleStores(ClubScope club)
    {
        var today = TaiwanClock.Today;
        return db.PartnerStores.AsNoTracking()
            .Where(s => (s.ClubId == club.ClubId || s.ClubId == null) && s.Status == "published"
                        && (s.StartOn == null || s.StartOn <= today) && (s.EndOn == null || s.EndOn >= today));
    }

    public async Task<IReadOnlyList<PartnerStorePublicDto>> ListStoresAsync(
        ClubScope club, string? category, string? region, string? tier, string dbLocale, CancellationToken cancellationToken)
    {
        var query = VisibleStores(club);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(s => s.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(region))
        {
            query = query.Where(s => s.Region == region);
        }

        if (tier is "all" or "fan_club")
        {
            query = query.Where(s => s.ApplicableTier == tier);
        }

        var rows = await query.Include(s => s.PartnerStoresI18ns).OrderBy(s => s.SortOrder).ThenBy(s => s.RowSeq).AsSplitQuery().ToListAsync(cancellationToken);
        return rows.Select(s => ToDto(s, dbLocale)).ToList();
    }

    public async Task<PartnerStorePublicDto?> GetStoreAsync(ClubScope club, string slug, string dbLocale, CancellationToken cancellationToken)
    {
        var row = await VisibleStores(club).Include(s => s.PartnerStoresI18ns).AsSplitQuery().FirstOrDefaultAsync(s => s.Slug == slug, cancellationToken);
        return row is null ? null : ToDto(row, dbLocale);
    }

    /// <summary>8.4 清單頁的類別與地區篩選項目（只列目前有上架店家的）。</summary>
    public async Task<PartnerStoreFiltersDto> GetStoreFiltersAsync(ClubScope club, CancellationToken cancellationToken)
    {
        var rows = await VisibleStores(club).Select(s => new { s.Category, s.Region }).ToListAsync(cancellationToken);
        return new PartnerStoreFiltersDto
        {
            Categories = rows.Where(r => !string.IsNullOrWhiteSpace(r.Category)).Select(r => r.Category!).Distinct().Order(StringComparer.Ordinal).ToList(),
            Regions = rows.Where(r => !string.IsNullOrWhiteSpace(r.Region)).Select(r => r.Region!).Distinct().Order(StringComparer.Ordinal).ToList(),
        };
    }

    private PartnerStorePublicDto ToDto(PartnerStore s, string dbLocale)
    {
        var requested = s.PartnerStoresI18ns.FirstOrDefault(i => i.Locale == dbLocale);
        var fallback = s.PartnerStoresI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        // 地址：中文地址在主檔，英文地址在英文側表；英文缺漏回退中文。
        var address = dbLocale == "en" ? RequestLocale.Pick(requested?.Address, s.Address) : s.Address;
        return new PartnerStorePublicDto
        {
            Slug = s.Slug, Name = RequestLocale.Pick(requested?.Name, fallback?.Name), IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requested?.Name), Category = s.Category, Region = s.Region, Address = address,
            Lat = s.Lat, Lng = s.Lng, Phone = s.Phone, BusinessHours = ReadHours(s.BusinessHours),
            OfferContent = RequestLocale.Pick(requested?.OfferContent, fallback?.OfferContent), ApplicableTier = s.ApplicableTier,
            ApplicableTierLabel = dbLocale == "en" ? (s.ApplicableTier == "fan_club" ? "Paid Fan Club members only" : "All members") : (s.ApplicableTier == "fan_club" ? "限付費會員" : "全會員適用"),
            MapUrl = s.MapUrl, WebsiteUrl = s.WebsiteUrl, ImageUrl = imageUrls.Resolve(s.ImageKey), IsShared = s.ClubId is null,
        };
    }

    /// <summary>營業時間欄位是 json：後台存 <c>{"text":"…"}</c>（自由文字），這裡還原成純文字；相容舊的 JSON 字串值。</summary>
    private static string? ReadHours(string? stored) => JsonColumn.UnwrapText(stored);
}
