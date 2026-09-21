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
        // 明列的「每頁 SSR 都要、幾乎不變、量極小」資料——用快取接縫示範用法，no-op 實作下等於直接查庫。
        var normalizedCode = clubCode.Trim().ToLowerInvariant();

        var clubId = await cache.GetOrCreateAsync(
            $"club-id:{normalizedCode}",
            async ct =>
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
