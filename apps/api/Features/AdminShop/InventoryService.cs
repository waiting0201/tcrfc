using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;
using Tcrfc.Api.Data.EfEntities;

namespace Tcrfc.Api.Features.AdminShop;

/// <summary>異動後的庫存水位。可售量＝庫存量－已保留量。</summary>
public readonly record struct StockLevels(int Stock, int Reserved)
{
    public int Available => Stock - Reserved;
}

/// <summary>一次庫存異動的請求。<c>StockDelta</c> 變動「庫存量」，<c>ReservedDelta</c> 變動「已保留量」（下單保留）。</summary>
public sealed record InventoryChange(
    Guid ClubId, Guid VariantId, string MovementType, int StockDelta, int ReservedDelta, string? Reason, Guid? OrderId, Guid? OperatorId,
    int? CountedStock = null);

/// <summary>可售量不足（或異動後會讓庫存為負）。呼叫端應轉成 409，訊息是日常中文。</summary>
public sealed class InsufficientStockException(string sku, int available)
    : Exception($"規格「{sku}」的可售量不足（目前只剩 {available}），無法完成這次異動。")
{
    public string Sku { get; } = sku;
    public int Available { get; } = available;
}

/// <summary>
/// 庫存異動的唯一寫入口（規劃書 §4.13 S2）：<b>庫存量與已保留量只透過本類別改動</b>，每次異動同一個交易內寫一列
/// <c>inventory_movements</c>（類型、數量、原因、經辦人、關聯訂單、異動後水位），可追溯。
///
/// 🔴 <b>庫存不得讀快取</b>（docs/14「五類資料不得讀快取」）：本類別與所有商店 repository <b>刻意不注入快取服務</b>
/// （<c>ArchitectureTests</c> 掃描鎖定）。並行安全靠「交易內先對規格列加更新鎖（<c>UPDLOCK</c>）再讀水位、算新值、寫回」，
/// 兩個同時扣同一規格的請求會排隊，不會超賣，也不會讓庫存變負。
///
/// 數量語意：<c>quantity</c> 是有正負號的變動量——<c>reserve</c>／<c>release</c> 變動的是已保留量，其餘變動的是庫存量。
/// 流程：下單保留（<c>reserve</c>）→ 付款成立扣減（<c>sale</c>，庫存與保留同時減）；付款失敗／逾時釋回（<c>release</c>）；
/// 取消與退貨回補（<c>cancel_restock</c>／<c>return_restock</c>）。現場收款的訂單沒有保留階段，直接 <c>sale</c>。
/// 注意：本服務用原始 SQL 改規格列，呼叫端在同一個 <see cref="ClubDbContext"/> 內不要以追蹤模式讀取 <c>ProductVariant</c> 的庫存欄位。
/// </summary>
public sealed class InventoryService(ClubDbContext db)
{
    private sealed class LevelRow
    {
        public int Stock { get; set; }
        public int Reserved { get; set; }
        public string Sku { get; set; } = "";
    }

    /// <summary>在交易內套用一次庫存異動。若呼叫端已開交易就沿用，否則自己開一個。</summary>
    public async Task<StockLevels> ApplyAsync(InventoryChange change, CancellationToken cancellationToken)
    {
        if (change.CountedStock is null)
        {
            ValidateShape(change);
        }

        var owned = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var rows = await db.Database.SqlQuery<LevelRow>(
                $"SELECT stock_qty AS Stock, reserved_qty AS Reserved, sku AS Sku FROM product_variants WITH (UPDLOCK, ROWLOCK) WHERE id = {change.VariantId} AND club_id = {change.ClubId}")
                .ToListAsync(cancellationToken);
            if (rows.Count == 0)
            {
                throw new AdminValidationException("找不到指定的商品規格。");
            }

            var row = rows[0];
            if (change.CountedStock is int counted)
            {
                // 盤點：以實際盤點數為準，差異在鎖內算出，避免與並行的下單扣減互相覆蓋。
                change = change with { StockDelta = counted - row.Stock };
                ValidateShape(change);
            }

            var stock = row.Stock + change.StockDelta;
            var reserved = row.Reserved + change.ReservedDelta;
            if (stock < 0 || reserved < 0 || reserved > stock)
            {
                throw new InsufficientStockException(row.Sku, row.Stock - row.Reserved);
            }

            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE product_variants SET stock_qty = {stock}, reserved_qty = {reserved}, updated_at = SYSUTCDATETIME() WHERE id = {change.VariantId}",
                cancellationToken);
            var now = DateTime.UtcNow;
            db.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(), ClubId = change.ClubId, ProductVariantId = change.VariantId, OrderId = change.OrderId,
                MovementType = change.MovementType, Quantity = change.MovementType is "reserve" or "release" ? change.ReservedDelta : change.StockDelta,
                StockAfter = stock, ReservedAfter = reserved, Reason = change.Reason, HandledBy = change.OperatorId,
                OccurredAt = now, CreatedAt = now, UpdatedAt = now, CreatedBy = change.OperatorId, UpdatedBy = change.OperatorId,
            });
            await db.SaveChangesAsync(cancellationToken);
            if (owned is not null)
            {
                await owned.CommitAsync(cancellationToken);
            }

            return new StockLevels(stock, reserved);
        }
        finally
        {
            if (owned is not null)
            {
                await owned.DisposeAsync();
            }
        }
    }

    private static void ValidateShape(InventoryChange c)
    {
        var ok = c.MovementType switch
        {
            "stock_in" or "cancel_restock" or "return_restock" => c.StockDelta > 0 && c.ReservedDelta == 0,
            "damage" => c.StockDelta < 0 && c.ReservedDelta == 0,
            "adjust" => c.StockDelta != 0 && c.ReservedDelta == 0,
            "stocktake" => c.ReservedDelta == 0,
            "reserve" => c.ReservedDelta > 0 && c.StockDelta == 0,
            "release" => c.ReservedDelta < 0 && c.StockDelta == 0,
            // 付款成立：庫存減少；若先前有保留，保留量同時等量減少。
            "sale" => c.StockDelta < 0 && (c.ReservedDelta == 0 || c.ReservedDelta == c.StockDelta),
            _ => false,
        };
        if (!ok)
        {
            throw new ArgumentException($"庫存異動類型與數量不相容：{c.MovementType}／{c.StockDelta}／{c.ReservedDelta}。");
        }
    }
}
