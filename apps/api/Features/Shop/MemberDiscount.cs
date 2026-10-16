using Microsoft.EntityFrameworkCore;
using Tcrfc.Api.Common;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Features.Shop;

/// <summary>會員折扣適用結果：套用折扣當時的會籍方案與折扣比例（0–100 的整數百分比）。</summary>
public sealed record MemberDiscount(Guid PlanId, int Percent);

/// <summary>單一規格的最終單價計算結果。</summary>
/// <param name="UnitPrice">實際單價（折後）：訂單快照、LINE Pay 金額、電子發票明細、退款一律以它為準。</param>
/// <param name="ListPrice">原價。</param>
/// <param name="OnSale">true＝促銷價生效且就是實際單價（會員折扣價更低時為 false）。</param>
/// <param name="MemberDiscountPerUnit">因會員折扣每件少收的金額（相對於「原價或促銷價」中本來要付的價格）；沒有套用為 0。</param>
public readonly record struct UnitPriceResult(int UnitPrice, int ListPrice, bool OnSale, int MemberDiscountPerUnit)
{
    public bool MemberDiscountApplied => MemberDiscountPerUnit > 0;
}

/// <summary>
/// 站內商店「會員折扣」（主站規劃書 v3.29 §3.8 8.3、§4.13 S1／S3）：持有「賣方俱樂部」有效會籍的會員，在<b>勾選了「適用會員折扣」的商品</b>上自動享有該會籍方案設定的折扣比例。
/// 🔴 <b>與促銷價不疊加、取較低價</b>：商品有促銷價時，會員折扣價＝<b>原價</b> × (1 − 折扣率)，與促銷價比較取較低者（不是促銷價再打折）。
/// 🔴 <b>不讀快取</b>（docs/14：會籍與訂單狀態、購物車、金流冪等都不得讀快取）：每次計價都直接查庫。
/// 沒有折扣碼、沒有逐品項會員價。金額一律整數元，折扣價四捨五入到元。
/// </summary>
public sealed class MemberDiscountService(ClubDbContext db)
{
    /// <summary>
    /// 目前有效的會員折扣：該會員在「賣方俱樂部」持有狀態為 <c>active</c> 且期間涵蓋今天的會籍，且方案折扣比例大於 0；
    /// 同時有多份（跨球季重疊）時取折扣最高者。訪客（無 memberId）或沒有符合者回 null。
    /// </summary>
    public async Task<MemberDiscount?> ResolveAsync(Guid? memberId, Guid sellingClubId, CancellationToken cancellationToken)
    {
        if (memberId is not Guid id)
        {
            return null;
        }

        var today = TaiwanClock.Today;
        var row = await db.Memberships.AsNoTracking()
            .Where(m => m.MemberId == id && m.ClubId == sellingClubId && m.Status == "active"
                && (m.MembershipStartOn == null || m.MembershipStartOn <= today) && (m.MembershipEndOn == null || m.MembershipEndOn >= today)
                && m.MembershipPlan.ShopDiscountPercent > 0)
            .OrderByDescending(m => m.MembershipPlan.ShopDiscountPercent).ThenBy(m => m.RowSeq)
            .Select(m => new { m.MembershipPlanId, m.MembershipPlan.ShopDiscountPercent })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new MemberDiscount(row.MembershipPlanId, row.ShopDiscountPercent);
    }

    /// <summary>
    /// 純函式（可單元測試）：算出單價。<paramref name="eligible"/>＝商品是否勾選「適用會員折扣」。
    /// 促銷價只有「小於原價」才算促銷（與購物車既有規則一致）。
    /// </summary>
    public static UnitPriceResult Compute(int listPrice, int? salePrice, bool eligible, MemberDiscount? discount)
    {
        var onSale = salePrice is int sale && sale < listPrice;
        var baseUnit = onSale ? salePrice!.Value : listPrice;
        if (!eligible || discount is null || discount.Percent <= 0)
        {
            return new UnitPriceResult(baseUnit, listPrice, onSale, 0);
        }

        var memberPrice = MemberPrice(listPrice, discount.Percent);
        return memberPrice < baseUnit
            ? new UnitPriceResult(memberPrice, listPrice, false, baseUnit - memberPrice)
            : new UnitPriceResult(baseUnit, listPrice, onSale, 0);
    }

    /// <summary>會員折扣價＝原價 × (100 − 折扣率) / 100，四捨五入到整數元（整數運算，無浮點誤差）。</summary>
    public static int MemberPrice(int listPrice, int percent)
        => (int)(((long)listPrice * (100 - Math.Clamp(percent, 0, 100)) + 50) / 100);
}
