using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.AdminMembers;
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
    private static readonly string[] GroupOrder = ["member_card", "discount", "gift", "event", "content"];

    private static readonly IReadOnlyDictionary<string, (string Zh, string En)> GroupLabels = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["member_card"] = ("會員卡", "Membership card"),
        ["discount"] = ("折扣與優惠", "Discounts & offers"),
        ["gift"] = ("禮品", "Gifts"),
        ["event"] = ("活動", "Events"),
        ["content"] = ("內容與溝通", "Content & community"),
    };

    // ═══════════════════════════ 方案 ═══════════════════════════

    /// <summary>目前可購買的方案（已上架、球季尚未結束）。排序：球季起始日、排序值。</summary>
    private async Task<List<MembershipPlan>> LoadCurrentPlansAsync(ClubScope club, CancellationToken cancellationToken)
    {
        var today = TaiwanClock.Today;
        return await db.MembershipPlans.AsNoTracking().Include(p => p.Season).Include(p => p.MembershipPlansI18ns)
            .Where(p => p.ClubId == club.ClubId && p.Status == "published" && p.Season.EndOn >= today && (p.EndsOn == null || p.EndsOn >= today))
            .OrderBy(p => p.Season.StartOn).ThenBy(p => p.SortOrder).ThenBy(p => p.RowSeq)
            .AsSplitQuery().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MembershipPlanPublicDto>> ListPlansAsync(ClubScope club, string dbLocale, CancellationToken cancellationToken)
    {
        var plans = await LoadCurrentPlansAsync(club, cancellationToken);
        var en = dbLocale == "en";
        return plans.Select(p =>
        {
            var requested = p.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == dbLocale);
            var fallback = p.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
            var name = RequestLocale.Pick(requested?.Name, fallback?.Name);
            return new MembershipPlanPublicDto
            {
                Code = p.Code, Tier = p.Tier, TierLabel = MemberLabels.TierName(p.Tier, en, name), ShopDiscountPercent = p.ShopDiscountPercent,
                Name = name, IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requested?.Name), BenefitNote = RequestLocale.Pick(requested?.BenefitNote, fallback?.BenefitNote),
                Fee = p.Fee, CardQuota = p.CardQuota, JerseyQuota = p.JerseyQuota, MidSeasonRule = RequestLocale.Pick(requested?.MidSeasonRule, fallback?.MidSeasonRule), SeasonCode = p.Season.Code,
                StartsOn = p.StartsOn ?? p.Season.StartOn, EndsOn = p.EndsOn ?? p.Season.EndOn,
            };
        }).ToList();
    }

    // ═══════════════════════════ 權益對照表 ═══════════════════════════

    /// <summary>
    /// 權益對照表（以方案為欄，v3.29）：欄＝目前可購買的方案；列＝以 <c>benefit_code</c> 對齊各方案的已上架條目。
    /// 列的顯示名稱／說明／分組取「排序最前的方案」的那一筆（各方案的同代碼條目文案通常相同，值才不同）；排序以該條目在各方案中最小的 <c>sort_order</c> 為準。
    /// 沒有可購買方案回傳空表（不是 404）。
    /// </summary>
    public async Task<BenefitTablePublicDto> GetBenefitsAsync(ClubScope club, string dbLocale, CancellationToken cancellationToken)
    {
        var plans = await LoadCurrentPlansAsync(club, cancellationToken);
        if (plans.Count == 0)
        {
            return new BenefitTablePublicDto { Plans = [], Groups = [] };
        }

        var en = dbLocale == "en";
        var planIds = plans.Select(p => p.Id).ToList();
        var benefits = await db.MembershipBenefits.AsNoTracking().Include(b => b.MembershipBenefitsI18ns)
            .Where(b => planIds.Contains(b.MembershipPlanId) && b.Status == "published")
            .AsSplitQuery().ToListAsync(cancellationToken);
        var planIndex = plans.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i);

        var columns = plans.Select(p =>
        {
            var requested = p.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == dbLocale);
            var fallback = p.MembershipPlansI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
            var name = RequestLocale.Pick(requested?.Name, fallback?.Name);
            return new BenefitPlanColumnDto
            {
                Code = p.Code, Tier = p.Tier, TierLabel = MemberLabels.TierName(p.Tier, en, name), Name = name,
                IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requested?.Name), Fee = p.Fee,
            };
        }).ToList();

        var rows = benefits.GroupBy(b => b.BenefitCode, StringComparer.Ordinal).Select(g =>
        {
            var lead = g.OrderBy(b => planIndex[b.MembershipPlanId]).ThenBy(b => b.SortOrder).ThenBy(b => b.RowSeq).First();
            var requested = lead.MembershipBenefitsI18ns.FirstOrDefault(i => i.Locale == dbLocale);
            var fallback = lead.MembershipBenefitsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
            var values = plans.Select(p =>
            {
                var own = g.FirstOrDefault(b => b.MembershipPlanId == p.Id);
                var ownRequested = own?.MembershipBenefitsI18ns.FirstOrDefault(i => i.Locale == dbLocale);
                var ownFallback = own?.MembershipBenefitsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
                return new BenefitPlanValueDto { PlanCode = p.Code, Value = own is null ? null : RequestLocale.Pick(ownRequested?.Value, ownFallback?.Value) };
            }).ToList();
            return new
            {
                Group = lead.BenefitGroup, Order = g.Min(b => b.SortOrder), Seq = g.Min(b => b.RowSeq),
                Item = new BenefitItemPublicDto
                {
                    Code = g.Key, Name = RequestLocale.Pick(requested?.Name, fallback?.Name), IsFallbackLocale = RequestLocale.IsFallback(dbLocale, requested?.Name),
                    Description = RequestLocale.Pick(requested?.Description, fallback?.Description), Values = values,
                },
            };
        }).ToList();

        var groups = rows.GroupBy(r => r.Group)
            .OrderBy(g => Array.IndexOf(GroupOrder, g.Key) is var i and >= 0 ? i : int.MaxValue)
            .Select(g => new BenefitGroupPublicDto
            {
                Group = g.Key,
                GroupLabel = GroupLabels.TryGetValue(g.Key, out var l) ? (en ? l.En : l.Zh) : g.Key,
                Items = g.OrderBy(r => r.Order).ThenBy(r => r.Seq).Select(r => r.Item).ToList(),
            }).ToList();

        return new BenefitTablePublicDto { Plans = columns, Groups = groups };
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

        if (tier is "all" or "premium")
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
            ApplicableTierLabel = AdminPartnerStores.AdminPartnerStoresRepository.TierLabel(s.ApplicableTier, dbLocale == "en"),
            MapUrl = s.MapUrl, WebsiteUrl = s.WebsiteUrl, ImageUrl = imageUrls.Resolve(s.ImageKey),
            ImageWidth = s.ImageKey is null ? null : s.ImageWidth, ImageHeight = s.ImageKey is null ? null : s.ImageHeight,
            ImageAlt = s.ImageKey is null ? null : RequestLocale.Pick(requested?.ImageAlt, fallback?.ImageAlt), IsShared = s.ClubId is null,
        };
    }

    /// <summary>營業時間欄位是 json：後台存 <c>{"text":"…"}</c>（自由文字），這裡還原成純文字；相容舊的 JSON 字串值。</summary>
    private static string? ReadHours(string? stored) => JsonColumn.UnwrapText(stored);
}
