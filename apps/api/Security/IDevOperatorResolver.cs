namespace Tcrfc.Api.Security;

/// <summary>
/// 🔴 這不是身分驗證。<c>created_by</c>／<c>updated_by</c> 這兩個稽核欄位在登入與權限系統
/// 做出來之前，先接一個「開發模式下由請求標頭指定的假操作者 id」——沒有任何簽章、沒有任何
/// session、呼叫端說是誰就是誰。只在 <see cref="DevWriteGate"/> 開啟時才會被呼叫到（寫入端點
/// 本身就掛在那個開關後面），且回傳值一律先驗證該 id 在 <c>admin_users</c> 表真的存在
/// （FK 約束要求 <c>created_by</c>／<c>updated_by</c> 存在的話就必須指向真實列），
/// 驗不到就回傳 <c>null</c>（兩個欄位本來就允許 NULL）而不是讓外鍵違反炸成 500。
/// </summary>
public interface IDevOperatorResolver
{
    /// <summary>
    /// 讀取 <c>X-Dev-Operator-Id</c> 標頭，解析成 GUID 並確認它在 <c>admin_users</c> 存在。
    /// 標頭缺漏、格式不是 GUID、或查無此人一律回傳 <c>null</c>（不丟例外——這只是稽核欄位，
    /// 不該讓整個寫入請求因為這個標頭寫錯而失敗）。
    /// </summary>
    Task<Guid?> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken);
}
