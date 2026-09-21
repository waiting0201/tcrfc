namespace Tcrfc.Api.Caching;

/// <summary>
/// 快取接縫（docs/17-deployment.md §4）。DI 依 <c>REDIS_HOST</c> 是否有設定切換實作：
/// 沒設定注入 <see cref="Tcrfc.Api.Caching.NoOpQueryCache"/>（本機 <c>dotnet run</c> 不需要 Redis
/// 也能跑），有設定注入 <see cref="Tcrfc.Api.Caching.RedisQueryCache"/>（S0-7d，2026-09-21）。
/// 之後要再擴充或替換實作，只需要改 DI 註冊，**端點與 repository 完全不用改**。
///
/// 🔴 ⛔ 不得為以下五類資料注入任何非 no-op 的實作（docs/17 §4「⛔ 不得讀快取的資料」、
/// docs/14-invariants.md）：
///   1. 庫存與商品可購買狀態（讀到陳舊值＝超賣）
///   2. 金流回呼的冪等檢查（讀到陳舊值＝重複入帳、重複開發票）
///   3. 會員卡 /m/&lt;token&gt; 驗證（讀到陳舊值＝已撤銷的卡通過查驗，是安全問題不是新鮮度問題）
///   4. 會籍有效性、訂單與付款狀態（讀到陳舊值＝已過期會籍被認可）
///   5. 購物車（讀到陳舊值＝結帳金額與內容不一致）
/// 本次任務範圍（球員／教練／新聞／賽程／俱樂部主檔）完全不在這五類之列，全部可以安全接上快取；
/// 但接的時候仍要照 docs/17 §4 的五條實作硬規則做（fail-open、版本號失效、TTL 兜底、single-flight、
/// 五類禁用的 repository 根本不注入）。
/// </summary>
public interface IQueryCache
{
    /// <summary>
    /// 依 <paramref name="entity"/>／<paramref name="club"/>／<paramref name="locale"/>／
    /// <paramref name="qualifier"/> 四個維度組出快取 key 並取值，未命中則呼叫
    /// <paramref name="factory"/> 取得結果並回填。no-op 實作永遠視為未命中，直接呼叫
    /// <paramref name="factory"/>。
    /// </summary>
    /// <param name="entity">資料種類（例如 "club-scope"、"players"），對應版本號命名空間
    /// <c>ver:{entity}:{club}</c>——之後後台寫入層呼叫 <see cref="InvalidateAsync"/> 時
    /// 也是用這個字串比對，同一種資料在不同呼叫點要用同一個 <paramref name="entity"/> 字串。</param>
    /// <param name="club">俱樂部代碼維度。跨俱樂部共用的資料用 <see cref="CacheDimensions.SharedClub"/>，
    /// ⛔ 不得省略——省略會讓不同俱樂部的請求互相污染快取（docs/17 §4「key 命名必須含 club_id 與
    /// locale 維度」）。</param>
    /// <param name="locale">語系維度（<c>zh</c>／<c>en</c>），與語系無關的資料用
    /// <see cref="CacheDimensions.AnyLocale"/>。</param>
    /// <param name="qualifier">同一 entity/club/locale 底下的額外區分（例如文章 slug、分頁頁碼），
    /// 沒有就傳 <see cref="CacheDimensions.NoQualifier"/>。**必須涵蓋每一個會改變查詢結果的參數**
    /// （篩選條件、分頁、slug……）——漏一個就是「換了篩選條件卻拿到上一次的結果」。</param>
    /// <remarks>
    /// 🔴 **`factory` 回傳 <c>null</c> 時不會寫入快取**（reference type 的 <c>null</c> 或
    /// <c>Nullable&lt;T&gt;</c> 沒有值都算），下一次同樣的請求會直接回源，不會把「查無資料」快取住。
    /// 這是刻意的通用規則，不是為單一呼叫點特例：草稿或尚未發布的內容一旦有了資料就該立刻查得到，
    /// 不該被一個 TTL 內的負向快取擋住（S0-7d，2026-09-21，落點見 <see cref="RedisQueryCache"/>）。
    /// **傳回空清單（例如 <c>PagedResult&lt;T&gt;</c> 的 <c>Items</c> 為空）不受影響、正常快取**——
    /// 只有「這個型別本身是 null」才不快取，「查到 0 筆但物件本身存在」是合法且穩定的答案。
    /// </remarks>
    Task<T> GetOrCreateAsync<T>(
        string entity,
        string club,
        string locale,
        string qualifier,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken);

    /// <summary>
    /// 讓 <paramref name="entity"/>＋<paramref name="club"/> 底下所有衍生 key 一次全部失效——
    /// 遞增 <c>ver:{entity}:{club}</c> 版本號，不使用 <c>KEYS</c> 掃描（docs/17 §4 硬規則 2）。
    /// 舊 key 不用主動刪，交給 Redis 的 <c>allkeys-lru</c> 淘汰或自然過期。
    ///
    /// 目前後台還不存在，沒有任何寫入層會呼叫這個方法——這是已知且被接受的取捨，
    /// 讀取端完全靠 TTL 兜底過期（見 <see cref="RedisQueryCache"/> 的 TTL 說明）。
    /// 之後後台寫入模組要做 write-invalidate 時，直接呼叫這個方法即可，介面不用再改。
    /// no-op 實作這個方法什麼都不做。
    /// </summary>
    Task InvalidateAsync(string entity, string club, CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="IQueryCache"/> 呼叫端共用的維度常數，避免各處各自寫一份「跨俱樂部共用」
/// 或「與語系無關」的魔術字串（字串不一致＝快取 key 對不起來，等於快取永遠失效或誤命中）。
/// </summary>
public static class CacheDimensions
{
    /// <summary>資料跨俱樂部共用（9 張 club_id 可為空的表），不屬於任一單一俱樂部。</summary>
    public const string SharedClub = "_shared";

    /// <summary>資料與語系無關（例如純 ID 對照），不需要依 locale 分開快取。</summary>
    public const string AnyLocale = "_any";

    /// <summary>沒有額外區分維度。</summary>
    public const string NoQualifier = "";
}
