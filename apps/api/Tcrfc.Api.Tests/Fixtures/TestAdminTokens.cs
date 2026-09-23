using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Tcrfc.Api.Security;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 測試用：直接呼叫 <see cref="AdminTokenService"/> 簽出一個有效存取權杖，不必真的打
/// <c>/api/v1/admin/auth/login</c> 走完密碼與 2FA 流程——大多數測試關心的是「這個角色／
/// 這個俱樂部授權組合會不會被擋下」，不是登入流程本身（登入流程另有
/// <c>AdminAuthTests</c> 專門驗證）。金鑰固定用 <see cref="TestJwtSigningKey"/>，
/// 與 fixture 設定進行程環境變數的值一致。
/// </summary>
public static class TestAdminTokens
{
    public static string IssueAccessToken(Guid adminUserId, string username, bool isSuperAdmin)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AdminTokenService.ConfigKey] = TestJwtSigningKey.Value,
            })
            .Build();

        var tokenService = new AdminTokenService(configuration);
        var (token, _) = tokenService.IssueAccessToken(adminUserId, username, isSuperAdmin);
        return token;
    }

    /// <summary>
    /// 直接依 <c>db/seed/generate-club-seed-sql.py</c「18.4 admin_users」種下的測試帳號 username
    /// 查出真實 id 與 is_super_admin，簽一把有效的存取權杖——大多數既有 AdminNews 測試改走真實授權
    /// 後都是用這個方法取得一把「content.editor@tcrfc.test（content_editor 角色，已授權 tcrfc、
    /// 已完成 2FA、免強制改密）」的權杖，語意上等同於「以這個角色登入」但跳過實際登入 HTTP 往返。
    /// </summary>
    public static async Task<string> IssueAccessTokenForSeededUserAsync(string username, CancellationToken cancellationToken = default)
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定，無法查詢種子測試帳號。");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, is_super_admin FROM admin_users WHERE username = @Username";
        command.Parameters.AddWithValue("@Username", username);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"找不到種子測試帳號 '{username}'——請先跑 ./db/seed/apply-seed.sh 灌入最新種子資料。");
        }

        var adminUserId = reader.GetGuid(0);
        var isSuperAdmin = reader.GetBoolean(1);
        return IssueAccessToken(adminUserId, username, isSuperAdmin);
    }
}
