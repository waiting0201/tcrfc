using System.Data.Common;
using Microsoft.Data.SqlClient;
using StackExchange.Redis;
using Tcrfc.Api.Data;

namespace Tcrfc.Api.Common;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // /healthz：存活探針，行程還在跑就回 200，不碰任何相依服務。
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
            .WithTags("Health")
            .ExcludeFromDescription();

        // /readyz：apps/api/Dockerfile 的 HEALTHCHECK 打這支。
        // S0-7d（2026-09-21）補齊慈善庫與 Redis 兩項檢查，三項規則不同：
        //   - club_db：必檢查，失敗＝not ready（既有行為，未改）。
        //   - charity_db：有設定 CHARITY_SQL_CONNECTION_STRING 才檢查；沒設定＝not_configured
        //     （不影響 ready）；設定了卻連不上＝fail，算 not ready——慈善平台還沒開工，
        //     大多數環境本來就不會設這個變數，不該為一個沒人用的連線讓整個容器變 unhealthy。
        //   - redis：依 docs/17-deployment.md §4「連線失敗算警告不算失敗」，連不上只標示 degraded，
        //     不影響 ready；沒有註冊 IConnectionMultiplexer（REDIS_HOST 未設定）則標示 not_configured。
        app.MapGet("/readyz", async (
            IClubSqlConnectionFactory clubConnectionFactory,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("Readyz");
            var overallReady = true;

            string clubDbStatus;
            try
            {
                using var connection = clubConnectionFactory.CreateConnection();
                await ((DbConnection)connection).OpenAsync(cancellationToken);
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await ((DbCommand)command).ExecuteScalarAsync(cancellationToken);
                clubDbStatus = "ok";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "/readyz 檢查主站庫連線失敗");
                clubDbStatus = "fail";
                overallReady = false;
            }

            string charityDbStatus;
            var charityConnectionString = configuration["CHARITY_SQL_CONNECTION_STRING"];
            if (string.IsNullOrWhiteSpace(charityConnectionString))
            {
                // 慈善平台是還沒開工的獨立交付物，本檔不建立任何常駐的連線工廠或 repository——
                // 這裡只在 readyz 這一次性動作裡直接開連線，用完即丟，不留下可被誤用來查資料的管道。
                charityDbStatus = "not_configured";
            }
            else
            {
                try
                {
                    // 🔴 只開連線查 SELECT 1，不做任何查詢，也絕不跨庫 JOIN——慈善庫是另一個法人
                    // （台灣足球策略發展協會）的資料，docs/14-invariants.md、docs/17 §5 明訂不得跨庫存取。
                    await using var connection = new SqlConnection(charityConnectionString);
                    await connection.OpenAsync(cancellationToken);
                    await using var command = connection.CreateCommand();
                    command.CommandText = "SELECT 1";
                    await command.ExecuteScalarAsync(cancellationToken);
                    charityDbStatus = "ok";
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "/readyz 檢查慈善庫連線失敗");
                    charityDbStatus = "fail";
                    overallReady = false;
                }
            }

            string redisStatus;
            var multiplexer = serviceProvider.GetService<IConnectionMultiplexer>();
            if (multiplexer is null)
            {
                // REDIS_HOST 未設定，Program.cs 就沒有註冊這個服務——not_configured 而不是 fail。
                redisStatus = "not_configured";
            }
            else
            {
                try
                {
                    await multiplexer.GetDatabase().PingAsync();
                    redisStatus = "ok";
                }
                catch (Exception ex)
                {
                    // ⚠️ 刻意不把 overallReady 設為 false：docs/17-deployment.md §4 明文「Redis 掛掉
                    // 不得讓請求失敗」，readyz 也適用同一條規則——Redis 壞掉時服務仍應收流量
                    // （每個讀取會 fail-open 回源 SQL，只是變慢，不是不能用）。
                    logger.LogWarning(ex, "/readyz 檢查 Redis 連線失敗（不影響就緒狀態，docs/17 §4）");
                    redisStatus = "degraded";
                }
            }

            var body = new
            {
                status = overallReady ? "ready" : "not_ready",
                club_db = clubDbStatus,
                charity_db = charityDbStatus,
                redis = redisStatus,
            };

            return overallReady
                ? Results.Ok(body)
                : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithTags("Health")
        .ExcludeFromDescription();
    }
}
