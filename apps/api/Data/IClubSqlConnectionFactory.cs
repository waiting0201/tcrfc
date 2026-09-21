using System.Data;

namespace Tcrfc.Api.Data;

/// <summary>
/// 建立指向主站庫（<c>tcrfc_club_dev</c> / <c>sqldb-club</c>）的資料庫連線。
/// 本次任務範圍只讀這一個庫——⛔ 不建立任何連到慈善庫（sqldb-charity）的連線工廠，
/// 那個庫是另一個法人的資料，docs/14-invariants.md、docs/17-deployment.md §5 明訂不得跨庫存取。
/// </summary>
public interface IClubSqlConnectionFactory
{
    /// <summary>回傳一條尚未開啟（closed）的連線，呼叫端以 <c>using</c> 自行開啟與釋放。</summary>
    IDbConnection CreateConnection();
}
