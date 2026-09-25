namespace Tcrfc.Api.Common;

/// <summary>
/// S1-10（2026-09-25，審查回饋修正）：解析「這個請求應該算誰的」，供依 IP 分區的濫用防護限流
/// 使用（<c>Program.cs</c> 的 <c>form-submission</c> Rate Limiting 政策）。
///
/// 必須在 <c>app.UseForwardedHeaders()</c> **之後**呼叫才有意義——那個中介軟體只有在連線來源
/// 落在 <see cref="Security.TrustedProxyConfiguration"/> 設定的信任清單內時，才會把
/// <see cref="Microsoft.AspNetCore.Http.ConnectionInfo.RemoteIpAddress"/> 換成
/// <c>X-Forwarded-For</c> 帶的訪客真實 IP；來源不受信任時维持原值不變（不受信任的
/// <c>X-Forwarded-For</c> 會被整個忽略，不會被拿去用）。這個方法本身只是單純讀取當下的
/// <c>RemoteIpAddress</c>，不重複判斷信任關係——信任判斷全部交給
/// <c>ForwardedHeadersMiddleware</c>，這裡不要再自己解析一次 <c>X-Forwarded-For</c>，
/// 否則等於繞過中介軟體的信任檢查，重新造成「任何人送這個標頭都算數」的漏洞。
/// </summary>
public static class ClientIpResolver
{
    public static string Resolve(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
