using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.AdminApp;

/// <summary>
/// M2 App 內容編排與深連結（App 規劃書 §8.2）：首頁區塊的開關與排序（§3.1 的九個固定區塊）、快捷入口、「更多」分頁項目、
/// 深連結對照表（App 畫面 ↔ 官網網址，供推播與廣告素材選用）、App 專屬公告條。
/// <b>不做</b> App 內的內容 CRUD——新聞、賽事、球員、店家仍在既有模組維護，這裡只管呈現順序與開關。
/// </summary>
public sealed partial class AdminAppLayoutRepository(ClubDbContext dbContext)
{
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string>(["home_section", "quick_entry", "more_item"], StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, string> KindLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["home_section"] = "首頁區塊", ["quick_entry"] = "快捷入口", ["more_item"] = "「更多」分頁項目",
    };

    [GeneratedRegex(@"^[a-z0-9]+(_[a-z0-9]+)*$")]
    private static partial Regex KeyFormat();

    [GeneratedRegex(@"^[a-z][a-z0-9+.\-]*://\S+$")]
    private static partial Regex AppLinkFormat();

    // ═════════ 版面項目 ═════════

    public async Task<IReadOnlyList<AdminAppLayoutItemDto>> ListItemsAsync(string? kind, CancellationToken cancellationToken)
    {
        if (kind is not null)
        {
            AdminInput.OneOf(kind, Kinds, "項目類型", "「首頁區塊」「快捷入口」或「更多分頁項目」");
        }

        var q = dbContext.AppLayoutItems.AsNoTracking().Include(i => i.AppLayoutItemsI18ns).Include(i => i.DeepLink).AsSplitQuery().AsQueryable();
        if (kind is not null)
        {
            q = q.Where(i => i.Kind == kind);
        }

        var rows = await q.OrderBy(i => i.Kind).ThenBy(i => i.SortOrder).ThenBy(i => i.RowSeq).ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<AdminAppLayoutItemDto> CreateItemAsync(UpsertAdminAppLayoutItemRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var kind = AdminInput.OneOf(request.Kind, Kinds, "項目類型", "「快捷入口」或「更多分頁項目」", "kind");
        if (kind == "home_section")
        {
            throw new AdminValidationException("首頁區塊是固定的九個，不能新增；只能調整開關與排序。", "kind");
        }

        var key = AdminInput.RequireText(request.ItemKey, "識別代碼", 48, "itemKey");
        if (!KeyFormat().IsMatch(key))
        {
            throw new AdminValidationException("識別代碼只能使用小寫英文字母、數字與底線。", "itemKey");
        }

        if (await dbContext.AppLayoutItems.AnyAsync(i => i.Kind == kind && i.ItemKey == key, cancellationToken))
        {
            throw new AdminConflictException("識別代碼重複", $"這個類型已經有識別代碼「{key}」了。", "itemKey");
        }

        await ValidateLinkAsync(request.DeepLinkId, cancellationToken);
        var now = DateTime.UtcNow;
        var max = await dbContext.AppLayoutItems.Where(i => i.Kind == kind).Select(i => (int?)i.SortOrder).MaxAsync(cancellationToken) ?? -1;
        var item = new AppLayoutItem
        {
            Id = Guid.NewGuid(), Kind = kind, ItemKey = key, SortOrder = max + 1, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
        };
        Apply(item, request);
        SetLabel(item, request.Label);
        dbContext.AppLayoutItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await GetItemAsync(item.Id, cancellationToken))!;
    }

    public async Task<AdminAppLayoutItemDto?> UpdateItemAsync(Guid id, UpsertAdminAppLayoutItemRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var item = await dbContext.AppLayoutItems.Include(i => i.AppLayoutItemsI18ns).FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        await ValidateLinkAsync(request.DeepLinkId, cancellationToken);
        Apply(item, request);
        SetLabel(item, request.Label);
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetItemAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.AppLayoutItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        if (item.Kind == "home_section")
        {
            throw new AdminConflictException("首頁區塊不能刪除", "首頁的九個區塊是固定的；不想顯示請把它「關閉」。");
        }

        dbContext.AppLayoutItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReorderAsync(ReorderAppLayoutRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Kind, Kinds, "項目類型", "「首頁區塊」「快捷入口」或「更多分頁項目」");
        if (request.Ids.Count == 0 || request.Ids.Distinct().Count() != request.Ids.Count)
        {
            throw new AdminValidationException("排序清單不可為空，也不可重複。");
        }

        var items = await dbContext.AppLayoutItems.Where(i => i.Kind == request.Kind).OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).ToListAsync(cancellationToken);
        var byId = items.ToDictionary(i => i.Id);
        if (request.Ids.Any(i => !byId.ContainsKey(i)))
        {
            throw new AdminValidationException("排序清單含有不屬於這個類型的項目，請重新整理後再試。");
        }

        var order = request.Ids.Concat(items.Select(i => i.Id).Where(i => !request.Ids.Contains(i))).ToList();
        var now = DateTime.UtcNow;
        for (var n = 0; n < order.Count; n++)
        {
            var item = byId[order[n]];
            if (item.SortOrder != n)
            {
                item.SortOrder = n;
                item.UpdatedAt = now;
                item.UpdatedBy = operatorId;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AdminAppLayoutItemDto?> GetItemAsync(Guid id, CancellationToken cancellationToken)
    {
        var i = await dbContext.AppLayoutItems.AsNoTracking().Include(x => x.AppLayoutItemsI18ns).Include(x => x.DeepLink).AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return i is null ? null : ToDto(i);
    }

    private async Task ValidateLinkAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id is { } linkId && !await dbContext.AppDeepLinks.AnyAsync(l => l.Id == linkId, cancellationToken))
        {
            throw new AdminValidationException("找不到這個深連結，請重新挑選。", "deepLinkId");
        }
    }

    private static void Apply(AppLayoutItem item, UpsertAdminAppLayoutItemRequest r)
    {
        item.DeepLinkId = r.DeepLinkId;
        item.IconKey = AdminInput.OptionalText(r.IconKey, "圖示代碼", 48, "iconKey");
        item.IsEnabled = r.IsEnabled;
    }

    private static void SetLabel(AppLayoutItem item, AppLabelInput label)
    {
        Upsert(item, RequestLocale.DefaultDbLocale, AdminInput.RequireText(label.Zh, "名稱（繁中）", 120, "labelZh"));
        if (label.En is not null)
        {
            Upsert(item, "en", AdminInput.OptionalText(label.En, "名稱（英文）", 120, "labelEn"));
        }
    }

    private static void Upsert(AppLayoutItem item, string locale, string? label)
    {
        var row = item.AppLayoutItemsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (row is null)
        {
            row = new AppLayoutItemsI18n { AppLayoutItemId = item.Id, Locale = locale };
            item.AppLayoutItemsI18ns.Add(row);
        }

        row.Label = label;
    }

    private static AdminAppLayoutItemDto ToDto(AppLayoutItem i) => new()
    {
        Id = i.Id, Kind = i.Kind, KindLabel = KindLabels.GetValueOrDefault(i.Kind, i.Kind), ItemKey = i.ItemKey, DeepLinkId = i.DeepLinkId,
        DeepLinkCode = i.DeepLink?.Code, IconKey = i.IconKey, SortOrder = i.SortOrder, IsEnabled = i.IsEnabled, IsFixed = i.Kind == "home_section",
        LabelZh = i.AppLayoutItemsI18ns.FirstOrDefault(x => x.Locale == RequestLocale.DefaultDbLocale)?.Label,
        LabelEn = i.AppLayoutItemsI18ns.FirstOrDefault(x => x.Locale == "en")?.Label,
    };

    // ═════════ 深連結對照表 ═════════

    public async Task<IReadOnlyList<AdminAppDeepLinkDto>> ListDeepLinksAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.AppDeepLinks.AsNoTracking().Include(l => l.AppDeepLinksI18ns).OrderBy(l => l.SortOrder).ThenBy(l => l.RowSeq).ToListAsync(cancellationToken);
        var used = await dbContext.AppLayoutItems.AsNoTracking().Where(i => i.DeepLinkId != null).GroupBy(i => i.DeepLinkId!.Value)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        return rows.Select(l => ToDto(l, used.GetValueOrDefault(l.Id))).ToList();
    }

    public async Task<AdminAppDeepLinkDto> CreateDeepLinkAsync(UpsertAdminAppDeepLinkRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var code = ValidateLink(request);
        if (await dbContext.AppDeepLinks.AnyAsync(l => l.Code == code, cancellationToken))
        {
            throw new AdminConflictException("代碼重複", $"深連結代碼「{code}」已經存在。", "code");
        }

        var now = DateTime.UtcNow;
        var max = await dbContext.AppDeepLinks.Select(l => (int?)l.SortOrder).MaxAsync(cancellationToken) ?? -1;
        var link = new AppDeepLink { Id = Guid.NewGuid(), Code = code, SortOrder = max + 1, CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId };
        ApplyLink(link, request);
        dbContext.AppDeepLinks.Add(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(link, 0);
    }

    public async Task<AdminAppDeepLinkDto?> UpdateDeepLinkAsync(Guid id, UpsertAdminAppDeepLinkRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var code = ValidateLink(request);
        var link = await dbContext.AppDeepLinks.Include(l => l.AppDeepLinksI18ns).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (link is null)
        {
            return null;
        }

        if (!string.Equals(link.Code, code, StringComparison.Ordinal))
        {
            if (await dbContext.AppDeepLinks.AnyAsync(l => l.Code == code && l.Id != id, cancellationToken))
            {
                throw new AdminConflictException("代碼重複", $"深連結代碼「{code}」已經存在。", "code");
            }

            link.Code = code;
        }

        ApplyLink(link, request);
        link.UpdatedAt = DateTime.UtcNow;
        link.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(link, await dbContext.AppLayoutItems.CountAsync(i => i.DeepLinkId == id, cancellationToken));
    }

    public async Task<bool> DeleteDeepLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        var link = await dbContext.AppDeepLinks.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (link is null)
        {
            return false;
        }

        if (await dbContext.AppLayoutItems.AnyAsync(i => i.DeepLinkId == id, cancellationToken))
        {
            throw new AdminConflictException("深連結使用中", "有快捷入口或「更多」分頁項目正在使用這個深連結，請先改掉再刪除。");
        }

        dbContext.AppDeepLinks.Remove(link);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string ValidateLink(UpsertAdminAppDeepLinkRequest r)
    {
        var code = AdminInput.RequireText(r.Code, "代碼", 48, "code");
        if (!KeyFormat().IsMatch(code))
        {
            throw new AdminValidationException("代碼只能使用小寫英文字母、數字與底線。", "code");
        }

        var appLink = AdminInput.RequireText(r.AppLink, "App 深連結", 200, "appLink");
        if (!AppLinkFormat().IsMatch(appLink) || !appLink.StartsWith("tcrfc://", StringComparison.Ordinal))
        {
            throw new AdminValidationException("App 深連結必須以 tcrfc:// 開頭（例如 tcrfc://schedule/d1）。", "appLink");
        }

        AdminInput.OptionalText(r.WebUrl, "官網網址", 500, "webUrl");
        AdminInput.RequireText(r.Label.Zh, "名稱（繁中）", 120, "labelZh");
        return code;
    }

    private static void ApplyLink(AppDeepLink link, UpsertAdminAppDeepLinkRequest r)
    {
        link.AppLink = r.AppLink.Trim();
        link.WebUrl = AdminInput.OptionalText(r.WebUrl, "官網網址", 500, "webUrl");
        link.RequiresLogin = r.RequiresLogin;
        link.IsActive = r.IsActive;
        foreach (var (locale, text) in new[] { (RequestLocale.DefaultDbLocale, (string?)r.Label.Zh), ("en", r.Label.En) })
        {
            if (locale == "en" && text is null)
            {
                continue;
            }

            var row = link.AppDeepLinksI18ns.FirstOrDefault(x => x.Locale == locale);
            if (row is null)
            {
                row = new AppDeepLinksI18n { AppDeepLinkId = link.Id, Locale = locale };
                link.AppDeepLinksI18ns.Add(row);
            }

            row.Label = AdminInput.OptionalText(text, "名稱", 120);
        }
    }

    private static AdminAppDeepLinkDto ToDto(AppDeepLink l, int used) => new()
    {
        Id = l.Id, Code = l.Code, AppLink = l.AppLink, WebUrl = l.WebUrl, RequiresLogin = l.RequiresLogin, IsActive = l.IsActive, SortOrder = l.SortOrder,
        LabelZh = l.AppDeepLinksI18ns.FirstOrDefault(x => x.Locale == RequestLocale.DefaultDbLocale)?.Label,
        LabelEn = l.AppDeepLinksI18ns.FirstOrDefault(x => x.Locale == "en")?.Label, UsedByCount = used,
    };

    // ═════════ 公告條 ═════════

    public async Task<IReadOnlyList<AdminAppAnnouncementDto>> ListAnnouncementsAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.AppAnnouncements.AsNoTracking().Include(a => a.AppAnnouncementsI18ns).Include(a => a.AudienceClub)
            .OrderByDescending(a => a.RowSeq).ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<AdminAppAnnouncementDto> CreateAnnouncementAsync(UpsertAdminAppAnnouncementRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var clubId = await ValidateAnnouncementAsync(request, cancellationToken);
        var now = DateTime.UtcNow;
        var a = new AppAnnouncement { Id = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId };
        ApplyAnnouncement(a, request, clubId);
        dbContext.AppAnnouncements.Add(a);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(a.Id, cancellationToken);
    }

    public async Task<AdminAppAnnouncementDto?> UpdateAnnouncementAsync(Guid id, UpsertAdminAppAnnouncementRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var clubId = await ValidateAnnouncementAsync(request, cancellationToken);
        var a = await dbContext.AppAnnouncements.Include(x => x.AppAnnouncementsI18ns).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (a is null)
        {
            return null;
        }

        ApplyAnnouncement(a, request, clubId);
        a.UpdatedAt = DateTime.UtcNow;
        a.UpdatedBy = operatorId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAnnouncementAsync(Guid id, CancellationToken cancellationToken)
    {
        var a = await dbContext.AppAnnouncements.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (a is null)
        {
            return false;
        }

        dbContext.AppAnnouncements.Remove(a);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AdminAppAnnouncementDto> ReloadAsync(Guid id, CancellationToken cancellationToken)
        => ToDto(await dbContext.AppAnnouncements.AsNoTracking().Include(a => a.AppAnnouncementsI18ns).Include(a => a.AudienceClub).FirstAsync(a => a.Id == id, cancellationToken));

    private async Task<Guid?> ValidateAnnouncementAsync(UpsertAdminAppAnnouncementRequest r, CancellationToken cancellationToken)
    {
        AdminInput.RequireText(r.Message.Zh, "公告文案（繁中）", 200, "messageZh");
        AdminInput.OptionalText(r.Message.En, "公告文案（英文）", 200, "messageEn");
        var link = AdminInput.OptionalText(r.LinkUrl, "連結", 500, "linkUrl");
        if (link is not null && !link.StartsWith("tcrfc://", StringComparison.Ordinal))
        {
            AdminInput.OptionalHttpUrl(link, "連結", 500, "linkUrl");
        }

        AdminInput.OneOf(r.AudienceTier ?? "all", Tcrfc.Api.Features.AppPublic.PushAudienceSpec.Tiers, "目標對象", "「全部」「球迷會員」「一般會員」或「未登入」", "audienceTier");
        if (r.StartsAt is not null && r.EndsAt is not null && r.EndsAt <= r.StartsAt)
        {
            throw new AdminValidationException("顯示期間的結束時間必須晚於開始時間。", "endsAt");
        }

        if (string.IsNullOrWhiteSpace(r.AudienceClubCode))
        {
            return null;
        }

        return await dbContext.Clubs.AsNoTracking().Where(c => c.Code == r.AudienceClubCode).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken)
               ?? throw new AdminValidationException("找不到這個俱樂部代碼。", "audienceClubCode");
    }

    private static void ApplyAnnouncement(AppAnnouncement a, UpsertAdminAppAnnouncementRequest r, Guid? clubId)
    {
        a.LinkUrl = AdminInput.OptionalText(r.LinkUrl, "連結", 500);
        a.StartsAt = r.StartsAt?.UtcDateTime;
        a.EndsAt = r.EndsAt?.UtcDateTime;
        a.AudienceTier = r.AudienceTier ?? "all";
        a.AudienceClubId = clubId;
        a.IsEnabled = r.IsEnabled;
        foreach (var (locale, text) in new[] { (RequestLocale.DefaultDbLocale, (string?)r.Message.Zh), ("en", r.Message.En) })
        {
            if (locale == "en" && text is null)
            {
                continue;
            }

            var row = a.AppAnnouncementsI18ns.FirstOrDefault(x => x.Locale == locale);
            if (row is null)
            {
                row = new AppAnnouncementsI18n { AppAnnouncementId = a.Id, Locale = locale };
                a.AppAnnouncementsI18ns.Add(row);
            }

            row.Message = AdminInput.OptionalText(text, "公告文案", 200);
        }
    }

    private static AdminAppAnnouncementDto ToDto(AppAnnouncement a)
    {
        var now = DateTime.UtcNow;
        return new AdminAppAnnouncementDto
        {
            Id = a.Id, LinkUrl = a.LinkUrl, StartsAt = a.StartsAt, EndsAt = a.EndsAt, AudienceTier = a.AudienceTier,
            AudienceTierLabel = a.AudienceTier switch { "fan_club" => "球迷會員", "registered" => "一般會員", "anonymous" => "未登入", _ => "全部" },
            AudienceClubCode = a.AudienceClub?.Code, IsEnabled = a.IsEnabled,
            IsActiveNow = a.IsEnabled && (a.StartsAt is null || a.StartsAt <= now) && (a.EndsAt is null || a.EndsAt > now),
            MessageZh = a.AppAnnouncementsI18ns.FirstOrDefault(x => x.Locale == RequestLocale.DefaultDbLocale)?.Message,
            MessageEn = a.AppAnnouncementsI18ns.FirstOrDefault(x => x.Locale == "en")?.Message,
        };
    }
}
