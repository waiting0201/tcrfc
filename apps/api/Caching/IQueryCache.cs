namespace Tcrfc.Api.Caching;

/// <summary>
/// 快取接縫（docs/17-deployment.md §4）。本次任務刻意不接 Redis——快取失效的觸發點是
/// 「後台寫入成功後刪 key」，但後台目前還不存在，這時候接 Redis 等於加一層永遠不會失效的快取。
///
/// 本機與本次上線一律注入 <see cref="Tcrfc.Api.Caching.NoOpQueryCache"/>：
/// 直接把 <paramref name="factory"/> 的結果原樣回傳，不做任何快取，讓「有沒有快取」對呼叫端透明。
/// 之後要接 Redis，只需要新增一個 cache-aside 實作並改 DI 註冊，**端點與 repository 完全不用改**。
///
/// 🔴 ⛔ 不得為以下五類資料注入任何非 no-op 的實作（docs/17 §4「⛔ 不得讀快取的資料」、
/// docs/14-invariants.md）：
///   1. 庫存與商品可購買狀態（讀到陳舊值＝超賣）
///   2. 金流回呼的冪等檢查（讀到陳舊值＝重複入帳、重複開發票）
///   3. 會員卡 /m/&lt;token&gt; 驗證（讀到陳舊值＝已撤銷的卡通過查驗，是安全問題不是新鮮度問題）
///   4. 會籍有效性、訂單與付款狀態（讀到陳舊值＝已過期會籍被認可）
///   5. 購物車（讀到陳舊值＝結帳金額與內容不一致）
/// 本次任務範圍（球員／教練／新聞／賽程／俱樂部主檔）完全不在這五類之列，全部可以之後安全地接上快取；
/// 但接的時候仍要照 docs/17 §4 的五條實作硬規則做（cache-aside、write-invalidate、版本號失效、
/// 每個 key 有 TTL、單飛鎖）。
/// </summary>
public interface IQueryCache
{
    /// <summary>
    /// 依 <paramref name="key"/> 取值，未命中則呼叫 <paramref name="factory"/> 取得結果。
    /// no-op 實作永遠視為未命中。
    /// </summary>
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken);
}
