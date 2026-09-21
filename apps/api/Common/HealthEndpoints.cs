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

        // /readyz：apps/api/Dockerfile 的 HEALTHCHECK 打這支。真的開一條連線驗證主站庫可連。
        // ⚠️ 本次任務範圍只做唯讀讀取 API、只碰主站庫（docs/17-deployment.md §5 慈善庫不得共用執行環境／
        // 連線），因此這裡只檢查 IClubSqlConnectionFactory；慈善庫連線與 Redis 連線檢查留給那兩塊功能
        // 真正實作時再補（Redis 依 docs/17 §4「連線失敗不得讓請求失敗」，屆時要算警告不算失敗）。
        app.MapGet("/readyz", async (IClubSqlConnectionFactory connectionFactory, ILoggerFactory loggerFactory, CancellationToken cancellationToken) =>
        {
            try
            {
                using var connection = connectionFactory.CreateConnection();
                await ((System.Data.Common.DbConnection)connection).OpenAsync(cancellationToken);
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await ((System.Data.Common.DbCommand)command).ExecuteScalarAsync(cancellationToken);
                return Results.Ok(new { status = "ready", club_db = "ok" });
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger("Readyz").LogError(ex, "/readyz 檢查主站庫連線失敗");
                return Results.Json(new { status = "not_ready", club_db = "fail" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithTags("Health")
        .ExcludeFromDescription();
    }
}
