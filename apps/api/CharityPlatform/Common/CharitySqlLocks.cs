using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Tcrfc.Api.CharityPlatform.Data;

namespace Tcrfc.Api.CharityPlatform.Common;

/// <summary>
/// SQL Server 應用程式鎖（<c>sp_getapplock</c>，交易層級）。慈善平台所有「必須串行化的帳務操作」共用這一支：
/// 結算產生／重算／確認／登記付款、每日對帳。鎖只在交易內有效（交易結束自動釋放，連線被回收也不會留下卡死的鎖）。
/// 🔴 為什麼不用條件式 UPDATE 當 claim：docs 的 idempotent-activation 教訓——真實並發下條件式 UPDATE 認領在 SQL Server 會死結，
/// 且「一筆捐款只能進一份結算單」是跨列的不變量，單一 UPDATE 表達不了。
/// </summary>
public static class CharitySqlLocks
{
    /// <summary>
    /// 在<b>已開啟的交易</b>內取得獨占鎖。<paramref name="timeoutMilliseconds"/> 為 0 時拿不到立刻回 <c>false</c>（不排隊）；
    /// 大於 0 時最多等待這麼久。回傳 <c>false</c> 代表逾時、被取消、死結或錯誤。
    /// </summary>
    public static async Task<bool> TryAcquireAsync(
        CharityDbContext db, IDbContextTransaction transaction, string resource, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.CommandText = "sp_getapplock";

        void Add(string name, System.Data.DbType type, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        Add("@Resource", System.Data.DbType.String, resource);
        Add("@LockMode", System.Data.DbType.String, "Exclusive");
        Add("@LockOwner", System.Data.DbType.String, "Transaction");
        Add("@LockTimeout", System.Data.DbType.Int32, timeoutMilliseconds);

        var result = command.CreateParameter();
        result.ParameterName = "@result";
        result.DbType = System.Data.DbType.Int32;
        result.Direction = System.Data.ParameterDirection.ReturnValue;
        command.Parameters.Add(result);

        await command.ExecuteNonQueryAsync(cancellationToken);
        return (int)result.Value! >= 0; // 0＝取得、1＝等待後取得；負數＝逾時、被取消、死結、錯誤
    }
}
