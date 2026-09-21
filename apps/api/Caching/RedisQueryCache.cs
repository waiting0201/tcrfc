using System.Collections.Concurrent;
using System.Text.Json;
using StackExchange.Redis;

namespace Tcrfc.Api.Caching;

/// <summary>
/// REDIS_HOST 有設定時的實作（S0-7d，2026-09-21）。逐條落實 docs/17-deployment.md §4
/// 「實作的五條硬規則」：
///
/// 1. **fail-open**：<see cref="TryBuildKeyAsync"/>／<see cref="TryGetAsync{T}"/>／
///    <see cref="TrySetAsync{T}"/>／<see cref="InvalidateAsync"/> 全部把 Redis 例外吞掉回源或忽略，
///    快取層永遠不得成為請求的失敗點——即使 <see cref="IConnectionMultiplexer"/> 整條斷線，
///    每一次讀取仍然會呼叫 <c>factory</c> 拿到正確結果，只是變慢，不是變錯或變炸。
/// 2. **失效用版本號**：key 形如 <c>v{ver}:{club}:{locale}:{entity}:{qualifier}</c>，
///    失效＝<c>INCR ver:{entity}:{club}</c>（<see cref="InvalidateAsync"/>）。⛔ 全程沒有任何
///    <c>KEYS</c>／<c>SCAN</c>。舊 key 交給 <c>allkeys-lru</c> 淘汰或自然過期，不主動刪除。
/// 3. **TTL 兜底**：見建構子的 <see cref="_ttl"/> 設定與其上的理由說明。
/// 4. **single-flight**：<see cref="_locks"/> 是 per-key 的 <see cref="SemaphoreSlim"/>，
///    同一個 redis key 的併發 cache miss 只有一個會真的呼叫 <c>factory</c>（打 SQL），
///    其餘等鎖，拿到鎖後會再讀一次快取——很可能已經被前一個請求填好了。
/// 5. **五類禁用資料的 repository 根本不注入這個服務**：這條在型別層面做不到「注入了卻不小心用錯」，
///    因為根本沒有東西會把 <see cref="IQueryCache"/> 注入那五類 repository 的建構子。
///
/// 另外一條不在 §4 五條硬規則裡、但本檔實作的通用規則：**`factory` 回傳 <c>null</c> 不寫入快取**
/// （<see cref="GetOrCreateAsync{T}"/> 內的 <c>result is not null</c> 判斷），見
/// <see cref="IQueryCache.GetOrCreateAsync{T}"/> 的 <c>remarks</c>。
/// </summary>
public sealed class RedisQueryCache : IQueryCache
{
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<RedisQueryCache> _logger;
    private readonly TimeSpan _ttl;

    // per-key 鎖，key 的基數（entity × club × locale × qualifier 的相異組合數）在本次任務範圍內很小
    // （幾個實體 × 2 個俱樂部 × 2 個語系），刻意不做移除／回收——長期執行下的記憶體成長可忽略，
    // 換來的是不用處理「移除鎖的同時有新請求排隊等它」這種競態。之後若 qualifier 帶入高基數的值
    // （例如逐筆文章 slug）導致鎖的數量顯著成長，才需要重新評估要不要加回收機制。
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public RedisQueryCache(IConnectionMultiplexer multiplexer, ILogger<RedisQueryCache> logger, IConfiguration configuration)
    {
        _multiplexer = multiplexer;
        _logger = logger;

        // TTL 預設值是本次的執行層決定（docs/17 §4 只定「一定要有 TTL」，沒給數字）：
        // 選 5 分鐘，而不是「反正之後接了版本號失效就設長一點」——因為現在後台還不存在，
        // 沒有任何寫入層會呼叫 InvalidateAsync，TTL 是目前唯一會觸發的失效機制，不是兜底而已。
        // 5 分鐘讓「後台之後才補上失效呼叫」這段期間的最大陳舊視窗有界，同時仍能吸收 SSR 的
        // 重複讀取（docs/17 §4「甜蜜點」講的是同一頁反覆打的小資料，5 分鐘內同一份資料被打
        // 幾十次是常態）。可用 QUERY_CACHE_TTL_SECONDS 覆寫。
        var ttlSeconds = configuration.GetValue<int?>("QUERY_CACHE_TTL_SECONDS") ?? 300;
        _ttl = TimeSpan.FromSeconds(ttlSeconds);
    }

    public async Task<T> GetOrCreateAsync<T>(
        string entity,
        string club,
        string locale,
        string qualifier,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        var redisKey = await TryBuildKeyAsync(entity, club, locale, qualifier);
        if (redisKey is null)
        {
            // 連版本號都讀不到＝Redis 目前不可用，不用再往下嘗試，直接回源。
            return await factory(cancellationToken);
        }

        var (found, value) = await TryGetAsync<T>(redisKey);
        if (found)
        {
            return value!;
        }

        // single-flight：同一個 redis key 的併發 miss 只讓一個打 SQL，其餘排隊等這把鎖。
        var gate = _locks.GetOrAdd(redisKey, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            // 拿到鎖之後再讀一次——排在後面的併發請求，很可能已經被前一個請求填好快取。
            var (foundAfterLock, valueAfterLock) = await TryGetAsync<T>(redisKey);
            if (foundAfterLock)
            {
                return valueAfterLock!;
            }

            var result = await factory(cancellationToken);
            if (result is not null)
            {
                // 🔴 null 不快取（見 IQueryCache.GetOrCreateAsync 的 <remarks>）：查無資料的負向結果
                // 不該被 TTL 期間內的快取擋住——草稿發布、slug 打錯字後修正都屬於這種情況。
                await TrySetAsync(redisKey, result);
            }

            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task InvalidateAsync(string entity, string club, CancellationToken cancellationToken)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            await db.StringIncrementAsync(VersionKey(entity, club));
        }
        catch (Exception ex)
        {
            // 失效失敗不得回滾任何已成功的 SQL 交易（docs/17 §4 硬規則 1）；漏掉的失效由 TTL 兜底過期。
            _logger.LogWarning(ex, "Redis 版本號遞增失敗（fail-open，交由 TTL 兜底），entity={Entity} club={Club}", entity, club);
        }
    }

    private static string VersionKey(string entity, string club) => $"ver:{entity}:{club}";

    private async Task<string?> TryBuildKeyAsync(string entity, string club, string locale, string qualifier)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var version = await db.StringGetAsync(VersionKey(entity, club));
            var versionValue = version.HasValue ? (long)version : 0L;
            return $"v{versionValue}:{club}:{locale}:{entity}:{qualifier}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis 版本號讀取失敗（fail-open，回源 SQL），entity={Entity} club={Club}", entity, club);
            return null;
        }
    }

    private async Task<(bool Found, T? Value)> TryGetAsync<T>(string redisKey)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var cached = await db.StringGetAsync(redisKey);
            if (!cached.HasValue)
            {
                return (false, default);
            }

            return (true, JsonSerializer.Deserialize<T>((string)cached!));
        }
        catch (Exception ex)
        {
            // 連線中斷、逾時、甚至反序列化失敗（例如型別改版留下舊格式的快取值），一律視為未命中回源，
            // 不讓快取層擋住請求。
            _logger.LogWarning(ex, "Redis 讀取失敗（fail-open，視為未命中），key={Key}", redisKey);
            return (false, default);
        }
    }

    private async Task TrySetAsync<T>(string redisKey, T value)
    {
        try
        {
            var db = _multiplexer.GetDatabase();
            var json = JsonSerializer.Serialize(value);
            await db.StringSetAsync(redisKey, json, _ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis 寫入快取失敗（fail-open，不影響本次回應，下次請求會再回源），key={Key}", redisKey);
        }
    }
}
