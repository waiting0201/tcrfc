using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;
using Tcrfc.Api.Features.Uploads;
using Tcrfc.Api.Images;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>商店權限碼（<c>db/seed/generate-club-seed-sql.py</c> 的權限表，module=S、domain=shop）。</summary>
public static class ShopPermissions
{
    public const string VariantView = "shop.variant.view";
    public const string CostView = "shop.cost.view";
    public const string CostUpdate = "shop.cost.update";
    public const string OrderReveal = "shop.order.reveal";
}

/// <summary>
/// S1 商品與規格（規劃書 §4.13 S1）：商品（雙語名稱與敘事、系列、標籤、尺碼表、上下架、SEO）、圖集、規格（SKU：貨號、尺寸×顏色、
/// 售價、促銷價、成本、低庫存門檻）。<b>不做會員價欄位</b>（本期不做會員折扣）。
/// 🔴 <b>庫存量不在這裡改</b>：新增規格時的「初始庫存」記成一筆進貨異動，之後一律走 S2（<see cref="InventoryService"/>）。
/// 🔴 <b>成本欄位受限</b>：只有 <c>shop.cost.view</c> 的角色看得到、<c>shop.cost.update</c> 才能改；
/// 「檢視者」角色<b>看不到規格與售價</b>（<c>shop.variant.view</c>，規劃書 §6「唯讀（不含金額）」）。
/// 商品狀態在資料庫只有 <c>draft</c>／<c>published</c>（S1-8 收斂）；「缺貨」由庫存自動判定（<c>displayStatus</c>）。
/// 刻意不注入快取服務（商品可購買狀態屬「不得讀快取」五類）。
/// </summary>
public sealed class AdminShopProductsRepository(
    ClubDbContext db, IImagePublicUrlResolver imageUrls, IPermissionChecker permissions, InventoryService inventory, ShopSettingsReader shopSettings)
{
    private sealed record Access(bool CanViewVariants, bool CanViewCost, bool CanUpdateCost);

    private async Task<Access> AccessAsync(AdminClubScope scope, CancellationToken cancellationToken)
    {
        var held = await permissions.GetHeldPermissionCodesAsync(
            scope.Identity.AdminUserId, scope.Identity.IsSuperAdmin,
            [ShopPermissions.VariantView, ShopPermissions.CostView, ShopPermissions.CostUpdate], cancellationToken);
        return new Access(held.Contains(ShopPermissions.VariantView), held.Contains(ShopPermissions.CostView), held.Contains(ShopPermissions.CostUpdate));
    }

    // ═════════════ 商品 ═════════════

    public async Task<PagedResult<AdminProductListItemDto>> ListAsync(
        AdminClubScope scope, string? status, Guid? collectionId, string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = await AccessAsync(scope, cancellationToken);
        var query = db.Products.AsNoTracking().Where(p => p.ClubId == scope.ClubId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, new HashSet<string>(["draft", "published"]), "狀態", "「下架」或「上架」");
            query = query.Where(p => p.Status == status);
        }

        if (collectionId is Guid c)
        {
            query = query.Where(p => p.CollectionId == c);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(p => p.Slug.Contains(k) || p.ProductsI18ns.Any(i => i.Name != null && i.Name.Contains(k))
                || p.ProductVariants.Any(v => v.Sku.Contains(k)));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(p => p.SortOrder).ThenByDescending(p => p.RowSeq).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                Product = p,
                CollectionName = p.Collection == null ? null : p.Collection.CollectionsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                Zh = p.ProductsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
                En = p.ProductsI18ns.Where(i => i.Locale == "en").Select(i => i.Name).FirstOrDefault(),
                Cover = p.ProductImages.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).Select(i => i.ImageKey).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Product.Id).ToList();
        var variants = await db.ProductVariants.AsNoTracking().Where(v => ids.Contains(v.ProductId))
            .Select(v => new { v.ProductId, v.Price, v.SalePrice, v.Status, Available = v.StockQty - v.ReservedQty }).ToListAsync(cancellationToken);
        var items = rows.Select(r =>
        {
            var vs = variants.Where(v => v.ProductId == r.Product.Id).ToList();
            var active = vs.Where(v => v.Status == "active").ToList();
            var display = DisplayStatus(r.Product.Status, active.Select(v => v.Available));
            return new AdminProductListItemDto
            {
                Id = r.Product.Id, Slug = r.Product.Slug, CollectionId = r.Product.CollectionId, CollectionName = r.CollectionName,
                IsNewArrival = r.Product.IsNewArrival, SortOrder = r.Product.SortOrder, Status = r.Product.Status, DisplayStatus = display,
                DisplayStatusLabel = ShopLabels.Of(ShopLabels.ProductStatus, display), OutOfStockBehavior = r.Product.OutOfStockBehavior,
                OutOfStockBehaviorLabel = ShopLabels.Of(ShopLabels.OutOfStock, r.Product.OutOfStockBehavior), CoverThumbUrl = ThumbUrl(r.Cover),
                NameZh = r.Zh, NameEn = r.En, VariantCount = vs.Count,
                PriceMin = access.CanViewVariants && vs.Count > 0 ? vs.Min(v => v.SalePrice ?? v.Price) : null,
                PriceMax = access.CanViewVariants && vs.Count > 0 ? vs.Max(v => v.SalePrice ?? v.Price) : null,
                AvailableTotal = access.CanViewVariants ? active.Sum(v => v.Available) : null, UpdatedAt = r.Product.UpdatedAt,
            };
        }).ToList();
        return new PagedResult<AdminProductListItemDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<AdminProductDetailDto?> GetAsync(AdminClubScope scope, Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking().Include(p => p.ProductsI18ns).Include(p => p.ProductImages)
            .AsSplitQuery().FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var access = await AccessAsync(scope, cancellationToken);
        var variants = await db.ProductVariants.AsNoTracking().Where(v => v.ProductId == id).OrderBy(v => v.SortOrder).ThenBy(v => v.RowSeq).ToListAsync(cancellationToken);
        var threshold = await shopSettings.GetLowStockThresholdAsync(scope.ClubId, cancellationToken);
        string? collectionName = null;
        if (product.CollectionId is Guid cid)
        {
            collectionName = await db.CollectionsI18ns.AsNoTracking().Where(i => i.CollectionId == cid && i.Locale == RequestLocale.DefaultDbLocale)
                .Select(i => i.Name).FirstOrDefaultAsync(cancellationToken);
        }

        var display = DisplayStatus(product.Status, variants.Where(v => v.Status == "active").Select(v => v.StockQty - v.ReservedQty));
        var zh = product.ProductsI18ns.FirstOrDefault(i => i.Locale == RequestLocale.DefaultDbLocale);
        var en = product.ProductsI18ns.FirstOrDefault(i => i.Locale == "en");
        return new AdminProductDetailDto
        {
            Id = product.Id, Slug = product.Slug, CollectionId = product.CollectionId, CollectionName = collectionName, IsNewArrival = product.IsNewArrival,
            SortOrder = product.SortOrder, Status = product.Status, DisplayStatus = display, DisplayStatusLabel = ShopLabels.Of(ShopLabels.ProductStatus, display),
            OutOfStockBehavior = product.OutOfStockBehavior, OutOfStockBehaviorLabel = ShopLabels.Of(ShopLabels.OutOfStock, product.OutOfStockBehavior),
            SizeChart = ParseSizeChart(product.SizeChart),
            Zh = ToLocale(zh), En = en is null ? null : ToLocale(en),
            Images = product.ProductImages.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).Select(i => new AdminProductImageDto
            {
                Id = i.Id, ImageKey = i.ImageKey, ImageUrl = imageUrls.Resolve(i.ImageKey), ImageThumbUrl = ThumbUrl(i.ImageKey), Width = i.Width, Height = i.Height, SortOrder = i.SortOrder,
            }).ToList(),
            Variants = access.CanViewVariants ? variants.Select(v => ToVariantDto(v, access.CanViewCost, threshold)).ToList() : [],
            CanViewVariants = access.CanViewVariants, CanViewCost = access.CanViewCost, CreatedAt = product.CreatedAt, UpdatedAt = product.UpdatedAt,
        };
    }

    public async Task<AdminProductDetailDto> CreateAsync(AdminClubScope scope, UpsertAdminProductRequest request, CancellationToken cancellationToken)
    {
        var slug = Validate(request) ?? AdminInput.GenerateSlug("product", request.Content.En?.Name);
        if (request.Status == "published")
        {
            throw new AdminValidationException("新商品還沒有規格，請先存成下架、新增規格後再上架。", "status");
        }

        await EnsureSlugFreeAsync(scope, slug, null, cancellationToken);
        await EnsureCollectionAsync(scope, request.CollectionId, cancellationToken);
        var maxOrder = await db.Products.Where(p => p.ClubId == scope.ClubId).Select(p => (int?)p.SortOrder).MaxAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var row = new Product
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, Slug = slug, CollectionId = request.CollectionId, IsNewArrival = request.IsNewArrival,
            SortOrder = request.SortOrder ?? (maxOrder ?? -1) + 1, Status = request.Status,
            OutOfStockBehavior = request.OutOfStockBehavior ?? "show_unavailable", SizeChart = SerializeSizeChart(request.SizeChart),
            CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        db.Products.Add(row);
        SetI18n(row, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(scope, row.Id, cancellationToken))!;
    }

    public async Task<AdminProductDetailDto?> UpdateAsync(AdminClubScope scope, Guid id, UpsertAdminProductRequest request, CancellationToken cancellationToken)
    {
        var newSlug = Validate(request);
        var row = await db.Products.Include(p => p.ProductsI18ns).FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        if (request.Status == "published"
            && !await db.ProductVariants.AsNoTracking().AnyAsync(v => v.ProductId == id && v.Status == "active", cancellationToken))
        {
            throw new AdminValidationException("上架前請先新增至少一個販售中的規格。", "status");
        }

        if (newSlug is not null && !string.Equals(newSlug, row.Slug, StringComparison.Ordinal))
        {
            await EnsureSlugFreeAsync(scope, newSlug, id, cancellationToken);
            row.Slug = newSlug;
        }

        await EnsureCollectionAsync(scope, request.CollectionId, cancellationToken);
        row.CollectionId = request.CollectionId;
        row.IsNewArrival = request.IsNewArrival;
        row.Status = request.Status;
        if (request.SortOrder is int order)
        {
            row.SortOrder = order;
        }

        if (request.OutOfStockBehavior is not null)
        {
            row.OutOfStockBehavior = request.OutOfStockBehavior;
        }

        row.SizeChart = SerializeSizeChart(request.SizeChart);
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = scope.Identity.AdminUserId;
        SetI18n(row, request.Content);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(AdminClubScope scope, Guid id, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var row = await db.Products.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        if (await db.OrderItems.AsNoTracking().AnyAsync(i => i.ProductVariant.ProductId == id, cancellationToken))
        {
            throw new AdminConflictException("商品已有訂單", "這件商品已經有訂單紀錄，不能刪除；請改為下架，並把規格改為停售。");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var variantIds = await db.ProductVariants.Where(v => v.ProductId == id).Select(v => v.Id).ToListAsync(cancellationToken);
        await db.CartItems.Where(c => variantIds.Contains(c.ProductVariantId)).ExecuteDeleteAsync(cancellationToken);
        await db.InventoryMovements.Where(m => variantIds.Contains(m.ProductVariantId)).ExecuteDeleteAsync(cancellationToken);
        await db.ProductVariants.Where(v => v.ProductId == id).ExecuteDeleteAsync(cancellationToken);
        foreach (var image in row.ProductImages)
        {
            orphans.Image(image.ImageKey);
        }

        db.ProductImages.RemoveRange(row.ProductImages);
        db.Products.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task ReorderAsync(AdminClubScope scope, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var rows = await db.Products.Where(p => p.ClubId == scope.ClubId).OrderBy(p => p.SortOrder).ThenByDescending(p => p.RowSeq).ToListAsync(cancellationToken);
        var order = AdminReorder.Compute(rows.Select(r => r.Id).ToList(), ids, "商品");
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

    // ═════════════ 圖集 ═════════════

    public async Task<AdminProductDetailDto?> AddImagesAsync(AdminClubScope scope, Guid id, IReadOnlyList<UploadedImageInfo> images, CancellationToken cancellationToken)
    {
        var row = await db.Products.FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var maxOrder = await db.ProductImages.Where(i => i.ProductId == id).Select(i => (int?)i.SortOrder).MaxAsync(cancellationToken);
        var next = (maxOrder ?? -1) + 1;
        var now = DateTime.UtcNow;
        foreach (var image in images)
        {
            db.ProductImages.Add(new ProductImage
            {
                Id = Guid.NewGuid(), ProductId = id, ImageKey = image.Key, Width = image.Width, Height = image.Height, SortOrder = next++,
                CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
            });
        }

        row.UpdatedAt = now;
        row.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    public async Task<bool> DeleteImageAsync(AdminClubScope scope, Guid id, Guid imageId, OrphanedObjects orphans, CancellationToken cancellationToken)
    {
        var row = await db.Products.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        var image = row?.ProductImages.FirstOrDefault(i => i.Id == imageId);
        if (row is null || image is null)
        {
            return false;
        }

        orphans.Image(image.ImageKey);
        db.ProductImages.Remove(image);
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminProductDetailDto?> ReorderImagesAsync(AdminClubScope scope, Guid id, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var row = await db.Products.Include(p => p.ProductImages).FirstOrDefaultAsync(p => p.Id == id && p.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var ordered = row.ProductImages.OrderBy(i => i.SortOrder).ThenBy(i => i.RowSeq).ToList();
        var order = AdminReorder.Compute(ordered.Select(i => i.Id).ToList(), ids, "圖片");
        var byId = ordered.ToDictionary(i => i.Id);
        for (var i = 0; i < order.Count; i++)
        {
            byId[order[i]].SortOrder = i;
        }

        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(scope, id, cancellationToken);
    }

    // ═════════════ 規格（SKU）═════════════

    public async Task<IReadOnlyList<AdminVariantDto>?> ListVariantsAsync(AdminClubScope scope, Guid productId, CancellationToken cancellationToken)
    {
        if (!await db.Products.AsNoTracking().AnyAsync(p => p.Id == productId && p.ClubId == scope.ClubId, cancellationToken))
        {
            return null;
        }

        var access = await AccessAsync(scope, cancellationToken);
        var threshold = await shopSettings.GetLowStockThresholdAsync(scope.ClubId, cancellationToken);
        var rows = await db.ProductVariants.AsNoTracking().Where(v => v.ProductId == productId).OrderBy(v => v.SortOrder).ThenBy(v => v.RowSeq).ToListAsync(cancellationToken);
        return rows.Select(v => ToVariantDto(v, access.CanViewCost, threshold)).ToList();
    }

    public async Task<AdminVariantDto?> GetVariantAsync(AdminClubScope scope, Guid productId, Guid id, CancellationToken cancellationToken)
    {
        var row = await db.ProductVariants.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id && v.ProductId == productId && v.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var access = await AccessAsync(scope, cancellationToken);
        var threshold = await shopSettings.GetLowStockThresholdAsync(scope.ClubId, cancellationToken);
        return ToVariantDto(row, access.CanViewCost, threshold);
    }

    public async Task<AdminVariantDto?> CreateVariantAsync(AdminClubScope scope, Guid productId, UpsertAdminVariantRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Products.AsNoTracking().AnyAsync(p => p.Id == productId && p.ClubId == scope.ClubId, cancellationToken))
        {
            return null;
        }

        var access = await AccessAsync(scope, cancellationToken);
        var sku = ValidateVariant(request);
        RequireCostRight(request, access);
        if (request.InitialStock is < 0)
        {
            throw new AdminValidationException("初始庫存不可為負數。", "initialStock");
        }

        await EnsureSkuFreeAsync(scope, sku, null, cancellationToken);
        var maxOrder = await db.ProductVariants.Where(v => v.ProductId == productId).Select(v => (int?)v.SortOrder).MaxAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var row = new ProductVariant
        {
            Id = Guid.NewGuid(), ClubId = scope.ClubId, ProductId = productId, Sku = sku, Size = Clean(request.Size), Colour = Clean(request.Colour),
            Price = request.Price, SalePrice = request.SalePrice, Cost = request.ClearCost ? null : request.Cost, StockQty = 0, ReservedQty = 0,
            Status = request.Status ?? "active", LowStockThreshold = request.LowStockThreshold, SortOrder = request.SortOrder ?? (maxOrder ?? -1) + 1,
            CreatedAt = now, UpdatedAt = now, CreatedBy = scope.Identity.AdminUserId, UpdatedBy = scope.Identity.AdminUserId,
        };
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.ProductVariants.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        if (request.InitialStock is int initial and > 0)
        {
            await inventory.ApplyAsync(new InventoryChange(scope.ClubId, row.Id, "stock_in", initial, 0, "初始庫存", null, scope.Identity.AdminUserId), cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return await GetVariantAsync(scope, productId, row.Id, cancellationToken);
    }

    public async Task<AdminVariantDto?> UpdateVariantAsync(AdminClubScope scope, Guid productId, Guid id, UpsertAdminVariantRequest request, CancellationToken cancellationToken)
    {
        var access = await AccessAsync(scope, cancellationToken);
        var sku = ValidateVariant(request);
        RequireCostRight(request, access);
        var row = await db.ProductVariants.FirstOrDefaultAsync(v => v.Id == id && v.ProductId == productId && v.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        if (!string.Equals(sku, row.Sku, StringComparison.Ordinal))
        {
            await EnsureSkuFreeAsync(scope, sku, id, cancellationToken);
            row.Sku = sku;
        }

        row.Size = Clean(request.Size);
        row.Colour = Clean(request.Colour);
        row.Price = request.Price;
        row.SalePrice = request.SalePrice;
        if (request.ClearCost)
        {
            row.Cost = null;
        }
        else if (request.Cost is int cost)
        {
            row.Cost = cost;
        }

        if (request.Status is not null)
        {
            row.Status = request.Status;
        }

        row.LowStockThreshold = request.LowStockThreshold;
        if (request.SortOrder is int order)
        {
            row.SortOrder = order;
        }

        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = scope.Identity.AdminUserId;
        await db.SaveChangesAsync(cancellationToken);
        return await GetVariantAsync(scope, productId, id, cancellationToken);
    }

    public async Task<bool> DeleteVariantAsync(AdminClubScope scope, Guid productId, Guid id, CancellationToken cancellationToken)
    {
        var row = await db.ProductVariants.FirstOrDefaultAsync(v => v.Id == id && v.ProductId == productId && v.ClubId == scope.ClubId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        // 只要跟訂單有關（下過單、保留、售出、回補）就不能刪，避免歷史訂單與庫存紀錄失去依據；請改為停售。
        var tied = await db.OrderItems.AsNoTracking().AnyAsync(i => i.ProductVariantId == id, cancellationToken)
            || row.ReservedQty > 0
            || await db.InventoryMovements.AsNoTracking().AnyAsync(m => m.ProductVariantId == id && m.OrderId != null, cancellationToken);
        if (tied)
        {
            throw new AdminConflictException("規格已有訂單", "這個規格已經有訂單或庫存保留紀錄，不能刪除；請改為停售。");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.CartItems.Where(c => c.ProductVariantId == id).ExecuteDeleteAsync(cancellationToken);
        await db.InventoryMovements.Where(m => m.ProductVariantId == id).ExecuteDeleteAsync(cancellationToken);
        await db.ProductVariants.Where(v => v.Id == id).ExecuteDeleteAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task ReorderVariantsAsync(AdminClubScope scope, Guid productId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (!await db.Products.AsNoTracking().AnyAsync(p => p.Id == productId && p.ClubId == scope.ClubId, cancellationToken))
        {
            throw new AdminValidationException("找不到指定的商品。");
        }

        var rows = await db.ProductVariants.Where(v => v.ProductId == productId).OrderBy(v => v.SortOrder).ThenBy(v => v.RowSeq).ToListAsync(cancellationToken);
        var order = AdminReorder.Compute(rows.Select(r => r.Id).ToList(), ids, "規格");
        var byId = rows.ToDictionary(r => r.Id);
        for (var i = 0; i < order.Count; i++)
        {
            byId[order[i]].SortOrder = i;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // ═════════════ 內部 ═════════════

    public static string DisplayStatus(string status, IEnumerable<int> activeAvailable)
    {
        if (status != "published")
        {
            return "draft";
        }

        var list = activeAvailable.ToList();
        return list.Count > 0 && list.All(a => a <= 0) ? "sold_out" : "published";
    }

    public static string VariantLabel(string? size, string? colour)
        => string.Join("／", new[] { size, colour }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Validate(UpsertAdminProductRequest request)
    {
        AdminInput.OneOf(request.Status, new HashSet<string>(["draft", "published"]), "狀態", "「下架」或「上架」", "status");
        AdminInput.RequireText(request.Content.Zh.Name, "中文商品名稱", 128, "nameZh");
        AdminInput.OptionalText(request.Content.Zh.SeoTitle, "中文搜尋標題", 200, "seoTitleZh");
        AdminInput.OptionalText(request.Content.Zh.SeoDescription, "中文搜尋描述", 300, "seoDescZh");
        AdminInput.OptionalText(request.Content.Zh.Tags, "中文標籤", 255, "tagsZh");
        if (request.Content.En is not null && !string.IsNullOrWhiteSpace(request.Content.En.Name))
        {
            AdminInput.RequireText(request.Content.En.Name, "英文商品名稱", 128, "nameEn");
            AdminInput.OptionalText(request.Content.En.SeoTitle, "英文搜尋標題", 200, "seoTitleEn");
            AdminInput.OptionalText(request.Content.En.SeoDescription, "英文搜尋描述", 300, "seoDescEn");
            AdminInput.OptionalText(request.Content.En.Tags, "英文標籤", 255, "tagsEn");
        }

        if (request.OutOfStockBehavior is not null)
        {
            AdminInput.OneOf(request.OutOfStockBehavior, ShopLabels.OutOfStock.Keys.ToHashSet(), "缺貨顯示方式", "「顯示但不可購買」或「自動隱藏」", "outOfStockBehavior");
        }

        AdminInput.OptionalNonNegative(request.SortOrder, "排序", "sortOrder");
        return string.IsNullOrWhiteSpace(request.Slug) ? null : AdminInput.Slug(request.Slug.Trim());
    }

    private static string ValidateVariant(UpsertAdminVariantRequest request)
    {
        var sku = AdminInput.RequireText(request.Sku, "商品規格編號", 64, "sku");
        if (sku.Any(char.IsWhiteSpace))
        {
            throw new AdminValidationException("商品規格編號不可包含空白。", "sku");
        }

        AdminInput.OptionalText(request.Size, "尺寸", 32, "size");
        AdminInput.OptionalText(request.Colour, "顏色", 32, "colour");
        if (request.Price < 0)
        {
            throw new AdminValidationException("售價不可為負數。", "price");
        }

        if (request.SalePrice is int sale && (sale < 0 || sale > request.Price))
        {
            throw new AdminValidationException("促銷價必須介於 0 與售價之間。", "salePrice");
        }

        AdminInput.OptionalNonNegative(request.Cost, "成本", "cost");
        AdminInput.OptionalNonNegative(request.LowStockThreshold, "低庫存門檻", "lowStockThreshold");
        AdminInput.OptionalNonNegative(request.SortOrder, "排序", "sortOrder");
        if (request.Status is not null)
        {
            AdminInput.OneOf(request.Status, ShopLabels.VariantStatus.Keys.ToHashSet(), "規格狀態", "「販售中」或「停售」", "status");
        }

        return sku;
    }

    private static void RequireCostRight(UpsertAdminVariantRequest request, Access access)
    {
        if ((request.Cost is not null || request.ClearCost) && !access.CanUpdateCost)
        {
            throw new AdminForbiddenException("你的角色不能編輯商品成本，請洽系統管理員。");
        }
    }

    private async Task EnsureSlugFreeAsync(AdminClubScope scope, string slug, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (await db.Products.AsNoTracking().AnyAsync(p => p.ClubId == scope.ClubId && p.Slug == slug && p.Id != exceptId, cancellationToken))
        {
            throw new AdminConflictException("網址名稱重複", $"網址名稱「{slug}」已經被這個俱樂部的另一件商品使用，請換一個。", "slug");
        }
    }

    /// <summary>貨號在全站唯一（跨俱樂部也一樣，規劃書 §5.4）。🔴 撞號時的訊息<b>不得透露另一個俱樂部的資料</b>：
    /// 只有「撞到本俱樂部自己的規格」才說明是誰用了；撞到別的俱樂部（呼叫端看不到、也無權看到的資料）一律只說「這個貨號無法使用」，
    /// 不說已被使用、也不說在哪裡——否則合作球隊的帳號可以用貨號探測對方有哪些商品。</summary>
    private async Task EnsureSkuFreeAsync(AdminClubScope scope, string sku, Guid? exceptId, CancellationToken cancellationToken)
    {
        var clashes = await db.ProductVariants.AsNoTracking().Where(v => v.Sku == sku && v.Id != exceptId).Select(v => v.ClubId).ToListAsync(cancellationToken);
        if (clashes.Count == 0)
        {
            return;
        }

        throw clashes.Contains(scope.ClubId)
            ? new AdminConflictException("商品規格編號重複", $"商品規格編號「{sku}」已經被這個俱樂部的另一個規格使用，請換一個。", "sku")
            : new AdminConflictException("商品規格編號無法使用", $"商品規格編號「{sku}」無法使用，請換一個。", "sku");
    }

    private async Task EnsureCollectionAsync(AdminClubScope scope, Guid? collectionId, CancellationToken cancellationToken)
    {
        if (collectionId is Guid c && !await db.Collections.AsNoTracking().AnyAsync(x => x.Id == c && x.ClubId == scope.ClubId, cancellationToken))
        {
            throw new AdminValidationException("找不到指定的商品系列，請確認系列屬於目前的俱樂部。", "collectionId");
        }
    }

    private static string? SerializeSizeChart(JsonElement? element)
    {
        if (element is not JsonElement e || e.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        // 原生 json 欄位只收物件或陣列，純量會在資料庫層變成 500（docs/18 E-111），在這裡擋成 400。
        if (e.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            throw new AdminValidationException("尺寸表格式不正確，請重新填寫尺寸表。", "sizeChart");
        }

        return e.GetRawText();
    }

    private static JsonElement? ParseSizeChart(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(raw).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void SetI18n(Product row, AdminProductContentInput content)
    {
        Upsert(row, RequestLocale.DefaultDbLocale, content.Zh);
        var en = row.ProductsI18ns.FirstOrDefault(i => i.Locale == "en");
        if (content.En is not null && !string.IsNullOrWhiteSpace(content.En.Name))
        {
            Upsert(row, "en", content.En);
        }
        else if (en is not null)
        {
            row.ProductsI18ns.Remove(en);
            db.ProductsI18ns.Remove(en);
        }
    }

    private void Upsert(Product row, string locale, AdminProductLocaleContent content)
    {
        var i18n = row.ProductsI18ns.FirstOrDefault(i => i.Locale == locale);
        if (i18n is null)
        {
            i18n = new ProductsI18n { ProductId = row.Id, Locale = locale };
            row.ProductsI18ns.Add(i18n);
            db.ProductsI18ns.Add(i18n);
        }

        i18n.Name = content.Name.Trim();
        i18n.Narrative = string.IsNullOrWhiteSpace(content.Narrative) ? null : content.Narrative;
        i18n.SeoTitle = Clean(content.SeoTitle);
        i18n.SeoDescription = Clean(content.SeoDescription);
        i18n.Tags = Clean(content.Tags);
    }

    private static AdminProductLocaleContent ToLocale(ProductsI18n? i)
        => new() { Name = i?.Name ?? "", Narrative = i?.Narrative, SeoTitle = i?.SeoTitle, SeoDescription = i?.SeoDescription, Tags = i?.Tags };

    internal static AdminVariantDto ToVariantDto(ProductVariant v, bool canViewCost, int defaultThreshold)
    {
        var available = v.StockQty - v.ReservedQty;
        var threshold = v.LowStockThreshold ?? defaultThreshold;
        return new AdminVariantDto
        {
            Id = v.Id, ProductId = v.ProductId, Sku = v.Sku, Size = v.Size, Colour = v.Colour, Label = VariantLabel(v.Size, v.Colour),
            Price = v.Price, SalePrice = v.SalePrice, EffectivePrice = v.SalePrice ?? v.Price, Cost = canViewCost ? v.Cost : null,
            StockQty = v.StockQty, ReservedQty = v.ReservedQty, AvailableQty = available, Status = v.Status,
            StatusLabel = ShopLabels.Of(ShopLabels.VariantStatus, v.Status), LowStockThreshold = v.LowStockThreshold,
            IsLowStock = v.Status == "active" && available <= threshold, SortOrder = v.SortOrder, UpdatedAt = v.UpdatedAt,
        };
    }

    private string? ThumbUrl(string? key) => key is null ? null : imageUrls.Resolve(ImageObjectKey.ForThumbnail(key));
}
