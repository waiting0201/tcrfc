using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Localization;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Features.AdminShop;

public sealed record AdminInventoryItemDto
{
    public required Guid VariantId { get; init; }
    public required Guid ProductId { get; init; }
    public string? ProductName { get; init; }
    public required string ProductStatus { get; init; }
    public required string Sku { get; init; }
    public required string Label { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required int StockQty { get; init; }
    public required int ReservedQty { get; init; }
    public required int AvailableQty { get; init; }

    /// <summary>實際套用的低庫存門檻（規格自己的門檻，沒有就是俱樂部預設）。</summary>
    public required int LowStockThreshold { get; init; }
    public required bool IsLowStock { get; init; }
}

public sealed record AdminInventoryMovementDto
{
    public required Guid Id { get; init; }
    public required Guid VariantId { get; init; }
    public required string Sku { get; init; }
    public string? ProductName { get; init; }
    public required string MovementType { get; init; }
    public required string MovementTypeLabel { get; init; }

    /// <summary>有正負號的變動量。<c>reserve</c>／<c>release</c> 變動的是保留量，其餘是庫存量。</summary>
    public required int Quantity { get; init; }
    public int? StockAfter { get; init; }
    public int? ReservedAfter { get; init; }
    public string? Reason { get; init; }
    public Guid? OrderId { get; init; }
    public string? OrderNo { get; init; }
    public string? HandledByName { get; init; }
    public required DateTime OccurredAt { get; init; }
}

public sealed record CreateAdminInventoryMovementRequest
{
    public required Guid VariantId { get; init; }

    /// <summary><c>stock_in</c> 進貨／<c>stocktake</c> 盤點／<c>damage</c> 報損／<c>adjust</c> 調整。</summary>
    public required string Type { get; init; }

    /// <summary>進貨、報損：正整數（報損會減少庫存）；調整：正負整數（正＝增加）；盤點：實際盤點的庫存總數（≥ 0）。</summary>
    public required int Quantity { get; init; }

    /// <summary>原因。報損與調整必填；進貨與盤點選填。</summary>
    public string? Reason { get; init; }
}

public sealed record AdminInventoryMovementResultDto
{
    public required AdminInventoryMovementDto Movement { get; init; }
    public required AdminInventoryItemDto Item { get; init; }
}

/// <summary>
/// S2 庫存管理（規劃書 §4.13 S2）：以 SKU 為單位的可售量檢視、低庫存補貨提醒、進貨／盤點／報損／調整，
/// 每次異動寫入 <c>inventory_movements</c>（數量、原因、經辦人）。<b>不做</b>：多倉別、批號與效期、預購與補貨排程。
/// 🔴 庫存不得讀快取；本 repository 不注入快取服務。庫存量只透過 <see cref="InventoryService"/> 改動。
/// </summary>
public sealed class AdminShopInventoryRepository(ClubDbContext db, InventoryService inventory, ShopSettingsReader shopSettings)
{
    private static readonly HashSet<string> ManualTypes = new(["stock_in", "stocktake", "damage", "adjust"], StringComparer.Ordinal);

    public async Task<PagedResult<AdminInventoryItemDto>> ListAsync(
        AdminClubScope scope, string? keyword, Guid? productId, bool? lowStockOnly, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var threshold = await shopSettings.GetLowStockThresholdAsync(scope.ClubId, cancellationToken);
        var query = db.ProductVariants.AsNoTracking().Where(v => v.ClubId == scope.ClubId);
        if (productId is Guid p)
        {
            query = query.Where(v => v.ProductId == p);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            AdminInput.OneOf(status, ShopLabels.VariantStatus.Keys.ToHashSet(), "規格狀態", "「販售中」或「停售」");
            query = query.Where(v => v.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(v => v.Sku.Contains(k) || v.Product.ProductsI18ns.Any(i => i.Name != null && i.Name.Contains(k)));
        }

        if (lowStockOnly == true)
        {
            // 只算販售中的規格；可售量 ≤ 該規格門檻（沒有就用俱樂部預設）。
            query = query.Where(v => v.Status == "active" && v.StockQty - v.ReservedQty <= (v.LowStockThreshold ?? threshold));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(v => v.StockQty - v.ReservedQty).ThenBy(v => v.Sku).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new
            {
                Variant = v, ProductStatus = v.Product.Status,
                Name = v.Product.ProductsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        return new PagedResult<AdminInventoryItemDto>
        {
            Items = rows.Select(r => ToItem(r.Variant, r.Name, r.ProductStatus, threshold)).ToList(), Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    public async Task<PagedResult<AdminInventoryMovementDto>> ListMovementsAsync(
        AdminClubScope scope, Guid? variantId, string? type, DateOnly? from, DateOnly? to, Guid? orderId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.InventoryMovements.AsNoTracking().Where(m => m.ClubId == scope.ClubId);
        if (variantId is Guid v)
        {
            query = query.Where(m => m.ProductVariantId == v);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            AdminInput.OneOf(type, ShopLabels.Movement.Keys.ToHashSet(), "異動類型", "進貨、盤點、報損、調整、下單保留、釋回保留、售出扣減、取消回補或退貨回補");
            query = query.Where(m => m.MovementType == type);
        }

        if (from is DateOnly f)
        {
            var fromUtc = TaiwanClock.StartOfDayUtc(f);
            query = query.Where(m => m.OccurredAt >= fromUtc);
        }

        if (to is DateOnly t)
        {
            var toUtc = TaiwanClock.StartOfDayUtc(t.AddDays(1));
            query = query.Where(m => m.OccurredAt < toUtc);
        }

        if (orderId is Guid o)
        {
            query = query.Where(m => m.OrderId == o);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(m => m.OccurredAt).ThenByDescending(m => m.RowSeq).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new
            {
                Movement = m, m.ProductVariant.Sku, OrderNo = m.Order == null ? null : m.Order.OrderNo,
                Name = m.ProductVariant.Product.ProductsI18ns.Where(i => i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefault(),
            }).ToListAsync(cancellationToken);
        var handlerIds = rows.Select(r => r.Movement.HandledBy).OfType<Guid>().Distinct().ToList();
        var handlers = await db.AdminUsers.AsNoTracking().Where(a => handlerIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.DisplayName, cancellationToken);
        return new PagedResult<AdminInventoryMovementDto>
        {
            Items = rows.Select(r => ToMovement(r.Movement, r.Sku, r.Name, r.OrderNo, r.Movement.HandledBy is Guid h && handlers.TryGetValue(h, out var n) ? n : null)).ToList(),
            Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    public async Task<AdminInventoryMovementResultDto> CreateMovementAsync(
        AdminClubScope scope, CreateAdminInventoryMovementRequest request, CancellationToken cancellationToken)
    {
        AdminInput.OneOf(request.Type, ManualTypes, "異動類型", "「進貨」「盤點」「報損」或「調整」");
        var reason = AdminInput.OptionalText(request.Reason, "原因", 255);
        var (stockDelta, counted) = request.Type switch
        {
            "stock_in" => (Positive(request.Quantity, "進貨數量"), (int?)null),
            "damage" => (-Positive(request.Quantity, "報損數量"), null),
            "adjust" => (request.Quantity != 0 ? request.Quantity : throw new AdminValidationException("調整數量不可為 0。"), null),
            _ => (0, request.Quantity >= 0 ? request.Quantity : throw new AdminValidationException("盤點的庫存總數不可為負數。")),
        };
        if (request.Type is "damage" or "adjust" && reason is null)
        {
            throw new AdminValidationException("報損與調整必須填寫原因。");
        }

        if (request.Type == "stocktake")
        {
            reason ??= "盤點";
        }

        var variant = await db.ProductVariants.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.VariantId && v.ClubId == scope.ClubId, cancellationToken)
            ?? throw new AdminValidationException("找不到指定的商品規格，請確認規格屬於目前的俱樂部。");
        await inventory.ApplyAsync(
            new InventoryChange(scope.ClubId, variant.Id, request.Type, stockDelta, 0, reason, null, scope.Identity.AdminUserId, counted), cancellationToken);
        var movement = await db.InventoryMovements.AsNoTracking().Where(m => m.ProductVariantId == variant.Id).OrderByDescending(m => m.RowSeq).FirstAsync(cancellationToken);
        var threshold = await shopSettings.GetLowStockThresholdAsync(scope.ClubId, cancellationToken);
        var name = await ProductNameAsync(variant.ProductId, cancellationToken);
        var fresh = await db.ProductVariants.AsNoTracking().FirstAsync(v => v.Id == variant.Id, cancellationToken);
        return new AdminInventoryMovementResultDto
        {
            Movement = ToMovement(movement, variant.Sku, name, null, scope.Identity.Username),
            Item = ToItem(fresh, name, await db.Products.AsNoTracking().Where(p => p.Id == variant.ProductId).Select(p => p.Status).FirstAsync(cancellationToken), threshold),
        };
    }

    private async Task<string?> ProductNameAsync(Guid productId, CancellationToken cancellationToken)
        => await db.ProductsI18ns.AsNoTracking().Where(i => i.ProductId == productId && i.Locale == RequestLocale.DefaultDbLocale).Select(i => i.Name).FirstOrDefaultAsync(cancellationToken);

    private static int Positive(int value, string label)
        => value > 0 ? value : throw new AdminValidationException($"{label}必須是大於 0 的整數。");

    private static AdminInventoryItemDto ToItem(Data.EfEntities.ProductVariant v, string? productName, string productStatus, int defaultThreshold)
    {
        var available = v.StockQty - v.ReservedQty;
        var threshold = v.LowStockThreshold ?? defaultThreshold;
        return new AdminInventoryItemDto
        {
            VariantId = v.Id, ProductId = v.ProductId, ProductName = productName, ProductStatus = productStatus, Sku = v.Sku,
            Label = AdminShopProductsRepository.VariantLabel(v.Size, v.Colour), Status = v.Status, StatusLabel = ShopLabels.Of(ShopLabels.VariantStatus, v.Status),
            StockQty = v.StockQty, ReservedQty = v.ReservedQty, AvailableQty = available, LowStockThreshold = threshold,
            IsLowStock = v.Status == "active" && available <= threshold,
        };
    }

    private static AdminInventoryMovementDto ToMovement(Data.EfEntities.InventoryMovement m, string sku, string? productName, string? orderNo, string? handledByName) => new()
    {
        Id = m.Id, VariantId = m.ProductVariantId, Sku = sku, ProductName = productName, MovementType = m.MovementType,
        MovementTypeLabel = ShopLabels.Of(ShopLabels.Movement, m.MovementType), Quantity = m.Quantity, StockAfter = m.StockAfter, ReservedAfter = m.ReservedAfter,
        Reason = m.Reason, OrderId = m.OrderId, OrderNo = orderNo, HandledByName = handledByName, OccurredAt = m.OccurredAt,
    };
}
