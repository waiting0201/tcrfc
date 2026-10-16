using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Tcrfc.Api.Common;

/// <summary>
/// SQL Server 應用程式鎖（<c>sp_getapplock</c>，交易層級）的共用入口，給「讀 → 判斷 → 寫」無法用單句條件式 UPDATE 或唯一索引表達的
/// 臨界區（對外部服務的呼叫、跨表的重複檢查）。🔴 <b>為什麼不用「讀取會被寫入擋住」</b>：正式庫（Azure SQL）預設
/// <c>READ_COMMITTED_SNAPSHOT = ON</c>，讀取永遠讀已提交的舊版本、不會等寫入；只有鎖（UPDLOCK／應用程式鎖）與寫入語句本身會序列化（<c>E-338</c>）。
/// 鎖只在交易內有效（交易結束自動釋放，連線被回收也不會留下卡死的鎖）。
/// </summary>
public static class SqlAppLocks
{
    /// <summary>
    /// 在<b>已開啟的交易</b>內取得獨占鎖。<paramref name="timeoutMilliseconds"/> 為 0 時拿不到立刻回 <c>false</c>；大於 0 時最多等待這麼久。
    /// 回傳 <c>false</c> 代表逾時、被取消、死結或錯誤。
    /// </summary>
    public static async Task<bool> TryAcquireAsync(
        DbContext db, IDbContextTransaction transaction, string resource, int timeoutMilliseconds, CancellationToken cancellationToken)
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
        return result.Value is int code && code >= 0; // 0＝取得、1＝等待後取得；負數＝逾時、被取消、死結、錯誤
    }
}
