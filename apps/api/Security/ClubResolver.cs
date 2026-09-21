using Dapper;
using Tcrfc.Api.Caching;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Security;

/// <summary>
/// <see cref="ClubScope"/> 的唯一產生者。任何端點要碰俱樂部範圍的資料，第一步都必須先經過這裡，
/// 見 <see cref="ClubScope"/> 上的完整說明。
/// </summary>
public sealed class ClubResolver(IClubSqlConnectionFactory connectionFactory, IQueryCache cache) : IClubResolver
{
    public async Task<ClubScope> ResolveAsync(string clubCode, CancellationToken cancellationToken)
    {
        // clubs 主檔幾乎不變（新增俱樂部是行政事件，不是日常操作），是 docs/17 §4「✅ 快取的甜蜜點」
        // 明列的「每頁 SSR 都要、幾乎不變、量極小」資料。REDIS_HOST 有設定時這裡真的會走 Redis
        // （S0-7d），沒設定時 NoOpQueryCache 讓它等同直接查庫，行為對呼叫端透明。
        var normalizedCode = clubCode.Trim().ToLowerInvariant();

        var clubId = await cache.GetOrCreateAsync(
            entity: "club-scope",
            club: normalizedCode,
            locale: CacheDimensions.AnyLocale,
            qualifier: CacheDimensions.NoQualifier,
            factory: async ct =>
            {
                using var connection = connectionFactory.CreateConnection();
                // ⛔ 不 SELECT *：只取驗證與範圍建構所需的兩欄。
                const string sql = """
                    SELECT id
                    FROM clubs
                    WHERE code = @Code AND status = 'active'
                    """;
                var commandDefinition = new CommandDefinition(sql, new { Code = normalizedCode }, cancellationToken: ct);
                return await connection.QuerySingleOrDefaultAsync<Guid?>(commandDefinition);
            },
            cancellationToken);

        if (clubId is null)
        {
            throw new ClubNotFoundException(clubCode);
        }

        return new ClubScope(clubId.Value, normalizedCode);
    }
}
