using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.AdminAds;

/// <summary>
/// E4 廣告主與合約（App 規劃書 §7.3）。<c>sponsor_id</c> 可為空，指向既有贊助商：只用來避免重複維護聯絡窗口，
/// 「五種商業對象」不合併——贊助商 Logo 牆不計曝光、不入廣告報表（規劃書 §10.3）。v1 不做廣告主自助帳號，報表由業務匯出後寄送。
/// </summary>
public sealed class AdminAdvertisersRepository(ClubDbContext dbContext)
{
    public async Task<IReadOnlyList<AdminAdvertiserDto>> ListAsync(string? status, string? keyword, CancellationToken cancellationToken)
    {
        if (status is not null && !AdLabels.AdvertiserStatus.ContainsKey(status))
        {
            throw new AdminValidationException("狀態篩選只能是「洽談中」「合作中」或「已結束」。");
        }

        var q = dbContext.Advertisers.AsNoTracking().Include(a => a.AdvertisersI18ns).Include(a => a.Sponsor).ThenInclude(s => s!.SponsorsI18ns)
            .AsSplitQuery().AsQueryable();
        if (status is not null)
        {
            q = q.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(a => (a.TaxId != null && a.TaxId.Contains(k)) || a.AdvertisersI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        var rows = await q.OrderBy(a => a.RowSeq).ToListAsync(cancellationToken);
        var counts = await dbContext.AdCampaigns.AsNoTracking().GroupBy(c => c.AdvertiserId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return rows.Select(a => ToDto(a, counts.GetValueOrDefault(a.Id))).ToList();
    }

    public async Task<AdminAdvertiserDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var a = await dbContext.Advertisers.AsNoTracking().Include(x => x.AdvertisersI18ns).Include(x => x.Sponsor).ThenInclude(s => s!.SponsorsI18ns)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (a is null)
        {
            return null;
        }

        return ToDto(a, await dbContext.AdCampaigns.CountAsync(c => c.AdvertiserId == id, cancellationToken));
    }

    public async Task<AdminAdvertiserDto> CreateAsync(UpsertAdminAdvertiserRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        await ValidateAsync(request, cancellationToken);
        var now = DateTime.UtcNow;
        var a = new Advertiser { Id = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId };
        Apply(a, request);
        SetI18n(a, request.Content);
        dbContext.Advertisers.Add(a);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(a.Id, cancellationToken))!;
    }

    public async Task<AdminAdvertiserDto?> UpdateAsync(Guid id, UpsertAdminAdvertiserRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        await ValidateAsync(request, cancellationToken);
        var a = await dbContext.Advertisers.Include(x => x.AdvertisersI18ns).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (a is null)
        {
            return null;
        }

        Apply(a, request);
        a.UpdatedAt = DateTime.UtcNow;
        a.UpdatedBy = operatorId;
        SetI18n(a, request.Content);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var a = await dbContext.Advertisers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (a is null)
        {
            return false;
        }

        if (await dbContext.AdCampaigns.AnyAsync(c => c.AdvertiserId == id, cancellationToken))
        {
            throw new AdminConflictException("廣告主已有檔期", "這個廣告主已經有投放檔期，不能刪除；合作結束請把狀態改成「已結束」。");
        }

        dbContext.Advertisers.Remove(a);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>可關聯的贊助商挑選清單（跨俱樂部；名稱取繁中，沒有就取英文）。</summary>
    public async Task<IReadOnlyList<AdminSponsorOptionDto>> SponsorOptionsAsync(string? keyword, CancellationToken cancellationToken)
    {
        var q = dbContext.Sponsors.AsNoTracking().Include(s => s.SponsorsI18ns).Include(s => s.Club).AsSplitQuery().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(s => s.SponsorsI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        var rows = await q.OrderBy(s => s.Club.Code).ThenBy(s => s.SortOrder).Take(200).ToListAsync(cancellationToken);
        return rows.Select(s => new AdminSponsorOptionDto
        {
            Id = s.Id, ClubCode = s.Club.Code,
            Name = s.SponsorsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name
                   ?? s.SponsorsI18ns.FirstOrDefault(i => i.Locale == "en")?.Name,
        }).ToList();
    }

    private async Task ValidateAsync(UpsertAdminAdvertiserRequest r, CancellationToken cancellationToken)
    {
        AdminInput.RequireText(r.Content.Zh.Name, "廣告主名稱（繁中）", 128);
        AdminInput.OptionalText(r.Content.En?.Name, "廣告主名稱（英文）", 128);
        AdminInput.OptionalText(r.TaxId, "統一編號", 16);
        AdminInput.OptionalText(r.ContactName, "聯絡窗口", 100);
        AdminInput.OptionalText(r.ContactPhone, "電話", 40);
        AdminInput.OptionalEmail(r.ContactEmail, "聯絡 Email");
        AdminInput.OptionalText(r.ContractNote, "合約備註", 1000);
        AdminInput.DateRange(r.CooperationStartOn, r.CooperationEndOn, "合作期間");
        AdminInput.OneOf(r.Status ?? "negotiating", AdLabels.AdvertiserStatus.Keys.ToHashSet(StringComparer.Ordinal), "狀態", "「洽談中」「合作中」或「已結束」");
        if (r.SponsorId is { } sid && !await dbContext.Sponsors.AnyAsync(s => s.Id == sid, cancellationToken))
        {
            throw new AdminValidationException("找不到要關聯的贊助商，請重新挑選。");
        }
    }

    private static void Apply(Advertiser a, UpsertAdminAdvertiserRequest r)
    {
        a.TaxId = AdminInput.OptionalText(r.TaxId, "統一編號", 16);
        a.ContactName = AdminInput.OptionalText(r.ContactName, "聯絡窗口", 100);
        a.ContactPhone = AdminInput.OptionalText(r.ContactPhone, "電話", 40);
        a.ContactEmail = AdminInput.OptionalEmail(r.ContactEmail, "聯絡 Email");
        a.ContractNote = AdminInput.OptionalText(r.ContractNote, "合約備註", 1000);
        a.CooperationStartOn = r.CooperationStartOn;
        a.CooperationEndOn = r.CooperationEndOn;
        a.SponsorId = r.SponsorId;
        a.Status = r.Status ?? "negotiating";
    }

    private static void SetI18n(Advertiser a, AdvertiserContentInput c)
    {
        Upsert(a, RequestLocale.DefaultDbLocale, c.Zh.Name);
        if (c.En is not null)
        {
            Upsert(a, "en", c.En.Name);
        }
    }

    private static void Upsert(Advertiser a, string locale, string? name)
    {
        var row = a.AdvertisersI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new AdvertisersI18n { AdvertiserId = a.Id, Locale = locale };
            a.AdvertisersI18ns.Add(row);
        }

        row.Name = AdminInput.OptionalText(name, "廣告主名稱", 128);
    }

    private static AdminAdvertiserDto ToDto(Advertiser a, int campaignCount)
    {
        string? Name(string locale) => a.AdvertisersI18ns.FirstOrDefault(i => i.Locale == locale)?.Name;
        var sponsorName = a.Sponsor?.SponsorsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale)?.Name
                          ?? a.Sponsor?.SponsorsI18ns.FirstOrDefault(i => i.Locale == "en")?.Name;
        return new AdminAdvertiserDto
        {
            Id = a.Id, NameZh = Name(RequestLocale.DefaultDbLocale), NameEn = Name("en"), TaxId = a.TaxId, ContactName = a.ContactName,
            ContactPhone = a.ContactPhone, ContactEmail = a.ContactEmail, ContractNote = a.ContractNote,
            CooperationStartOn = a.CooperationStartOn, CooperationEndOn = a.CooperationEndOn, SponsorId = a.SponsorId, SponsorName = sponsorName,
            Status = a.Status, StatusLabel = AdLabels.Of(AdLabels.AdvertiserStatus, a.Status), CampaignCount = campaignCount, UpdatedAt = a.UpdatedAt,
        };
    }
}
