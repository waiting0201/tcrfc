using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.SiteSettings;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminSiteSettings;

/// <summary>
/// I2 選單管理（規劃書 §4.9：主選單／Mega Menu／Footer 選單，拖曳排序、多層級、外部連結、雙語）。
/// 資料在 <c>menu_items</c>／<c>menu_items_i18n</c>（<c>club_id</c> 必填，兩個俱樂部各自一份；<c>menu_location</c> 區分三種選單）。
///
/// ### 儲存語意：整棵樹取代（不做逐筆 CRUD）
/// 拖曳排序與換層級一次會動到很多列，逐筆端點會產生中間狀態（排序衝突、孤兒子項目）。所以一次儲存「一個位置的整棵樹」：
/// 請求裡有 <c>id</c> 的項目沿用（更新標籤／連結／父層／順序），沒有 <c>id</c> 的新增，既有項目不在請求裡的刪除。同層順序＝陣列順序（儲存時重新編號）。
/// 兩個人同時編輯同一個位置時後儲存者覆蓋先儲存者（沒有版本檢查——選單是低頻編輯的小資料；若日後需要，加 <c>updated_at</c> 比對）。
///
/// ### 規則（執行層決定）
/// 最多 3 層、每個位置最多 100 項；標籤繁中必填（≤64 字）、英文選填；有子項目的項目可以沒有連結（群組標題），葉節點必須有連結；
/// 內部連結是 <c>/</c> 開頭、不含語系前綴的路徑（前台自己接 <c>/zh</c>／<c>/en</c>），外部連結必須是完整 http(s) 網址（<c>is_external</c> 由請求指定，不由網址猜測）。
/// </summary>
public sealed partial class AdminMenusRepository(ClubDbContext db, IQueryCache cache)
{
    public const int MaxDepth = 3;
    public const int MaxItemsPerLocation = 100;

    public static readonly IReadOnlyList<(string Location, string Label)> Locations =
    [
        ("main", "主選單"),
        ("mega", "Mega Menu"),
        ("footer", "頁尾選單"),
    ];

    [GeneratedRegex(@"^/[A-Za-z0-9\-._~!$&'()*+,;=:@%/?#\[\]]*$")]
    private static partial Regex InternalPath();

    public async Task<AdminMenusDto> GetAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var items = await db.MenuItems.AsNoTracking().Include(i => i.MenuItemsI18ns)
            .Where(i => i.ClubId == scope.ClubId).ToListAsync(cancellationToken);
        return new AdminMenusDto
        {
            Locations = Locations.Select(l => new AdminMenuLocationDto
            {
                Location = l.Location, Label = l.Label, Items = BuildTree(items.Where(i => i.MenuLocation == l.Location).ToList()),
            }).ToList(),
        };
    }

    public async Task<AdminMenuLocationDto> ReplaceAsync(
        AdminClubScope scope, string location, UpdateAdminMenuRequest request, Guid? operatorId, CancellationToken cancellationToken)
    {
        var definition = Locations.FirstOrDefault(l => l.Location == location);
        if (definition.Location is null)
        {
            throw new AdminValidationException("選單位置只能是主選單、Mega Menu 或頁尾選單。");
        }

        var flat = new List<FlatNode>();
        Flatten(request.Items ?? [], parentIndex: null, depth: 1, flat, "items");
        if (flat.Count > MaxItemsPerLocation)
        {
            throw new AdminValidationException($"每個選單最多 {MaxItemsPerLocation} 個項目。", "items");
        }

        var requestedIds = flat.Where(n => n.Source.Id is not null).Select(n => n.Source.Id!.Value).ToList();
        if (requestedIds.Count != requestedIds.Distinct().Count())
        {
            throw new AdminValidationException("同一個選單項目不能出現兩次。", "items");
        }

        var existing = await db.MenuItems.Include(i => i.MenuItemsI18ns)
            .Where(i => i.ClubId == scope.ClubId && i.MenuLocation == location).ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(i => i.Id);
        if (requestedIds.Any(id => !existingById.ContainsKey(id)))
        {
            throw new AdminValidationException("有選單項目已不存在或不屬於這個選單，請重新整理後再編輯。");
        }

        var now = DateTime.UtcNow;
        var entities = new MenuItem[flat.Count];
        for (var i = 0; i < flat.Count; i++)
        {
            var node = flat[i];
            MenuItem item;
            if (node.Source.Id is { } id)
            {
                item = existingById[id];
                item.UpdatedAt = now;
                item.UpdatedBy = operatorId;
            }
            else
            {
                item = new MenuItem
                {
                    Id = Guid.NewGuid(), ClubId = scope.ClubId, MenuLocation = location,
                    CreatedAt = now, UpdatedAt = now, CreatedBy = operatorId, UpdatedBy = operatorId,
                };
                db.MenuItems.Add(item);
            }

            entities[i] = item;
            item.ParentId = node.ParentIndex is { } p ? entities[p].Id : null;
            item.Url = node.Url;
            item.IsExternal = node.Source.IsExternal;
            item.SortOrder = node.Order;
            SetLabel(item, RequestLocale.DefaultDbLocale, node.LabelZh);
            SetLabel(item, "en", node.LabelEn);
        }

        var keep = entities.Select(e => e.Id).ToHashSet();
        foreach (var removed in existing.Where(e => !keep.Contains(e.Id)))
        {
            db.MenuItemsI18ns.RemoveRange(removed.MenuItemsI18ns.ToList());
            db.MenuItems.Remove(removed);
        }

        await db.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(SiteSettingsRepository.CacheEntities.Menus, scope.ClubCode, cancellationToken);

        var saved = await db.MenuItems.AsNoTracking().Include(i => i.MenuItemsI18ns)
            .Where(i => i.ClubId == scope.ClubId && i.MenuLocation == location).ToListAsync(cancellationToken);
        return new AdminMenuLocationDto { Location = definition.Location, Label = definition.Label, Items = BuildTree(saved) };
    }

    private sealed record FlatNode(UpsertAdminMenuItemRequest Source, int? ParentIndex, int Order, string LabelZh, string? LabelEn, string? Url);

    private static void Flatten(IReadOnlyList<UpsertAdminMenuItemRequest> siblings, int? parentIndex, int depth, List<FlatNode> flat, string path)
    {
        if (depth > MaxDepth)
        {
            throw new AdminValidationException($"選單最多 {MaxDepth} 層。", path);
        }

        for (var order = 0; order < siblings.Count; order++)
        {
            var source = siblings[order];
            var nodePath = $"{path}[{order}]";
            var labelZh = AdminInput.RequireText(source.LabelZh, "選單名稱（繁中）", 64, nodePath + ".labelZh");
            var labelEn = AdminInput.OptionalText(source.LabelEn, "選單名稱（英文）", 64, nodePath + ".labelEn");
            var hasChildren = source.Children is { Count: > 0 };
            var url = NormalizeUrl(source.Url, source.IsExternal, labelZh, nodePath + ".url");
            if (url is null && !hasChildren)
            {
                throw new AdminValidationException($"選單「{labelZh}」沒有子項目，必須設定連結。", nodePath + ".url");
            }

            var index = flat.Count;
            flat.Add(new FlatNode(source, parentIndex, order, labelZh, labelEn, url));
            if (hasChildren)
            {
                Flatten(source.Children!, index, depth + 1, flat, nodePath + ".children");
            }
        }
    }

    private static string? NormalizeUrl(string? raw, bool isExternal, string labelForMessage, string field)
    {
        var url = raw?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (url.Length > 500)
        {
            throw new AdminValidationException($"選單「{labelForMessage}」的連結不可超過 500 個字。", field);
        }

        if (isExternal)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) || string.IsNullOrEmpty(uri.Host))
            {
                throw new AdminValidationException($"選單「{labelForMessage}」是外部連結，必須是以 http:// 或 https:// 開頭的完整網址。", field);
            }

            return url;
        }

        if (url.StartsWith("//", StringComparison.Ordinal) || !InternalPath().IsMatch(url))
        {
            throw new AdminValidationException($"選單「{labelForMessage}」的內部連結必須以 / 開頭（例如 /about/），不能含空白或網域；外部網址請勾選「外部連結」。", field);
        }

        return url;
    }

    private void SetLabel(MenuItem item, string locale, string? label)
    {
        var row = item.MenuItemsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (!string.IsNullOrWhiteSpace(label))
        {
            if (row is null)
            {
                row = new MenuItemsI18n { MenuItemId = item.Id, Locale = locale };
                item.MenuItemsI18ns.Add(row);
                db.MenuItemsI18ns.Add(row);
            }

            row.Label = label;
        }
        else if (row is not null)
        {
            db.MenuItemsI18ns.Remove(row);
            item.MenuItemsI18ns.Remove(row);
        }
    }

    private static IReadOnlyList<AdminMenuItemDto> BuildTree(IReadOnlyList<MenuItem> inLocation)
    {
        var byParent = inLocation.Where(i => i.ParentId != null).GroupBy(i => i.ParentId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        IReadOnlyList<AdminMenuItemDto> Build(IEnumerable<MenuItem> siblings)
            => siblings.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).Select(i => new AdminMenuItemDto
            {
                Id = i.Id,
                LabelZh = i.MenuItemsI18ns.FirstOrDefault(x => x.Locale == RequestLocale.DefaultDbLocale)?.Label ?? string.Empty,
                LabelEn = i.MenuItemsI18ns.FirstOrDefault(x => x.Locale == "en")?.Label,
                Url = i.Url,
                IsExternal = i.IsExternal,
                Children = byParent.TryGetValue(i.Id, out var children) ? Build(children) : [],
            }).ToList();

        return Build(inLocation.Where(i => i.ParentId == null));
    }
}
