namespace Tcrfc.Api.Security;

/// <summary>
/// 唯一能把「路由上的一段字串」轉換成已驗證 <see cref="ClubScope"/> 的管道。
/// 每個端點在呼叫任何 repository 之前，一律先呼叫這裡——沒有第二條路。
/// </summary>
public interface IClubResolver
{
    /// <summary>
    /// 驗證 <paramref name="clubCode"/> 對應到 <c>clubs</c> 表中一筆狀態為 active 的資料列。
    /// 查不到或非 active 一律丟 <see cref="ClubNotFoundException"/>（對應 404）。
    /// </summary>
    Task<ClubScope> ResolveAsync(string clubCode, CancellationToken cancellationToken);
}
