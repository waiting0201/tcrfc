using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 建立一個「看起來像是 JWT Bearer 中介軟體已經驗證過」的最小 <see cref="HttpContext"/>，
/// 供直接呼叫 <see cref="Tcrfc.Api.Security.IAdminClubAuthorizer.AuthorizeAsync"/>（略過完整 HTTP
/// 管線）的測試使用——例如需要拿到真正的 <c>AdminClubScope</c> 才能建構
/// <c>AdminPagesRepository</c> 的單元層測試（<c>AdminPagesImageTests</c> 的補償刪除 token 測試）。
///
/// 只需要設定 <see cref="HttpContext.User"/>：<see cref="Tcrfc.Api.Security.AdminIdentity.FromClaimsPrincipal"/>
/// 只讀 <c>sub</c>／<c>username</c>／<c>is_super_admin</c> 三個 claim，不重新解析 JWT 字串本身
/// （見該類別原始碼），也不需要 <see cref="HttpContext.RequestServices"/>——
/// <see cref="Tcrfc.Api.Security.AdminClubAuthorizer"/> 的相依項全部來自它自己的建構子注入，
/// 不是從 <c>httpContext.RequestServices</c> 解析。
/// </summary>
public static class TestAdminHttpContext
{
    /// <summary>依 <c>db/seed/generate-club-seed-sql.py</c> 種下的測試帳號 <paramref name="username"/>
    /// 查出真實 id，組一個通過「已驗證」判定的 <see cref="HttpContext"/>。</summary>
    public static async Task<HttpContext> CreateAuthenticatedAsync(string username, CancellationToken cancellationToken = default)
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定，無法查詢種子測試帳號。");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM admin_users WHERE username = @Username";
        command.Parameters.AddWithValue("@Username", username);

        var adminUserId = await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException($"找不到種子測試帳號 '{username}'。");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, adminUserId.ToString()!),
            new Claim("username", username),
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuthentication");

        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}
