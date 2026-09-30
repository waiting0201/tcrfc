using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>
/// S1 商品系列（Collection：俱樂部／學院／球迷…）與系列介紹文（規劃書 §4.13 S1）。<c>collections.club_id</c> 必填，
/// 跨俱樂部 id 一律 404。刻意<b>不注入快取服務</b>（商品可購買狀態屬「不得讀快取」五類，見 <see cref="InventoryService"/>）。
/// </summary>
public sealed class AdminShopCollectionsRepository(ClubDbContext db)
{
    public async Task<IReadOnlyList<AdminCollectionListItemDto>> ListAsync(AdminClubScope scope, string? status, CancellationToken cancellationToken)
    {
        var query = db.Collections.AsNoTracking().Where(c => c.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, ShopLabels.CollectionStatus.Keys.ToHashSet(), "狀態", "「草稿」或「已發布」");
            query = query.Where(c => c.Status == status);
        }

        var rows = await query.OrderBy(c => c.SortOrder).ThenBy(c => c.RowSeq)
            .Select(c => new
            {
                Collection = c,
                Products = c.Products.Count,
                Zh = c.CollectionsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                En = c.CollectionsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        return rows.Select(r => new AdminCollectionListItemDto
        {
            Id = r.Collection.Id, Slug = r.Collection.Slug, SortOrder = r.Collection.SortOrder, Status = r.Collection.Status,
            StatusLabel = ShopLabels.Of(ShopLabels.CollectionStatus, r.Collection.Status), NameZh = r.Zh, NameEn = r.En,
            ProductCount = r.Products, UpdatedAt = r.Collection.UpdatedAt,
        }).ToList();
    }

    public async Task<AdminCollectionDetailDto?> GetAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Collections.AsNoTracking().Include(c => c.CollectionsI18ns)
            .FirstOrDefaultAsync(c => c.Id == id && c.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var count = await db.Products.AsNoTracking().CountAsync(p => p.CollectionId == id, cancellationToken);
        return ToDto(row, count);
    }

    public async Task<AdminCollectionDetailDto> CreateAsync(AdminClubScope scope, UpsertAdminCollectionRequest request, CancellationToken cancellationToken)
    {
        var slug = Validate(request) ?? AdminInput.GenerateSlug("collection", request.Content.En?.Name);
        await EnsureSlugFreeAsync(scope, slug, null, cancellationToken);
        var maxOrder = await db.Collections.Where(c => c.ClubId == scope.ClubId).Select(c => (int?)c.SortOrder).MaxAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var row = new Collection
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, Slug = slug, SortOrder = request.SortOrder ?? (maxOrder ?? -1) + 1, Status = request.Status,
            CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        db.Collections.Add(row);
        SetI18n(row, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(scope, row.Id, cancellationToken))!;
    }

    public async Task<AdminCollectionDetailDto?> UpdateAsync(AdminClubScope scope, Guid id, UpsertAdminCollectionRequest request, CancellationToken cancellationToken)
    {
        var newSlug = Validate(request);
        var row = await db.Collections.Include(c => c.CollectionsI18ns).FirstOrDefaultAsync(c => c.Id == id && c.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        if (newSlug is not null && !string.Equals(newSlug, row.Slug, StringComparison.Ordinal))
        {
            await EnsureSlugFreeAsync(scope, newSlug, id, cancellationToken);
            row.Slug = newSlug;
        }

        row.Status = request.Status;
        if (request.SortOrder is int order)
        {
            row.SortOrder = order;
        }

        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = scope.Identity.AdminUserId;
        SetI18n(row, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Collections.FirstOrDefaultAsync(c => c.Id == id && c.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        var count = await db.Products.AsNoTracking().CountAsync(p => p.CollectionId == id, cancellationToken);
        if (count > 0)
        {
            throw new AdminConflictException("系列仍有商品", $"這個系列底下還有 {count} 件商品，請先把商品移到其他系列，或將系列改為草稿。");
        }

        db.Collections.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReorderAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var rows = await db.Collections.Where(c => c.ClubId == scope.ClubId).OrderBy(c => c.SortOrder).ThenBy(c => c.RowSeq).ToListAsync(cancellationToken);
        var order = AdminReorder.Compute(rows.Select(r => r.Id).ToList(), ids, "系列");
        var byId = rows.ToDictionary(r => r.Id);
        for (var i = 0; i < order.Count; i++)
        {
            if (byId[order[i]].SortOrder != i)
            {
                byId[order[i]].SortOrder = i;
                byId[order[i]].UpdatedAt = DateTime.UtcNow;
                byId[order[i]].UpdatedBy = scope.Identity.AdminUserId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Validate(UpsertAdminCollectionRequest request)
    {
        AdminInput.OneOf(request.Status, ShopLabels.CollectionStatus.Keys.ToHashSet(), "狀態", "「草稿」或「已發布」");
        AdminInput.RequireText(request.Content.Zh.Name, "中文系列名稱", 64);
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文系列名稱", 64);
        }

        AdminInput.OptionalNonNegative(request.SortOrder, "排序");
        return string.IsNullOrWhiteSpace(request.Slug) ? null : AdminInput.Slug(request.Slug.Trim());
    }

    private async Task EnsureSlugFreeAsync(AdminClubScope scope, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await db.Collections.AsNoTracking().AnyAsync(c => c.ClubId == scope.ClubId && c.Slug == slug && c.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經被這個俱樂部的另一個系列使用，請換一個。");
        }
    }

    private void SetI18n(Collection row, AdminCollectionContentInput content)
    {
        Upsert(row, RequestLocale.DefaultDbLocale, content.Zh);
        var en = row.CollectionsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(row, "en", content.En);
        }
        else if (en is not null)
        {
            row.CollectionsI18ns.Remove(en);
            db.CollectionsI18ns.Remove(en);
        }
    }

    private void Upsert(Collection row, string locale, AdminCollectionLocaleContent content)
    {
        var i18n = row.CollectionsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (i18n is null)
        {
            i18n = new CollectionsI18n { CollectionId = row.Id, Locale = locale };
            row.CollectionsI18ns.Add(i18n);
            db.CollectionsI18ns.Add(i18n);
        }

        i18n.Name = content.Name.Trim();
        i18n.Narrative = string.IsNullOrWhiteSpace(content.Narrative) ? null : content.Narrative;
    }

    private static AdminCollectionDetailDto ToDto(Collection row, int productCount)
    {
        var zh = row.CollectionsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = row.CollectionsI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminCollectionDetailDto
        {
            Id = row.Id, Slug = row.Slug, SortOrder = row.SortOrder, Status = row.Status, StatusLabel = ShopLabels.Of(ShopLabels.CollectionStatus, row.Status),
            Zh = new AdminCollectionLocaleContent { Name = zh?.Name ?? "", Narrative = zh?.Narrative },
            En = en is null ? null : new AdminCollectionLocaleContent { Name = en.Name ?? "", Narrative = en.Narrative },
            ProductCount = productCount, CreatedAt = row.CreatedAt, UpdatedAt = row.UpdatedAt,
        };
    }
}
