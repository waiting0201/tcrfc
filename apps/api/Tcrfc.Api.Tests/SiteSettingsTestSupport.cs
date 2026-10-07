using Microsoft.Data.SqlClient;

namespace Tcrfc.Api.Tests;

/// <summary>I 網站設定與儀表板整合測試共用的小工具：臨時翻譯人員帳號。注意：T-SQL 變數與參數名稱不分大小寫，局部變數與參數不可僅大小寫不同（docs/18 E-121）。</summary>
internal static class SiteSettingsTest
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING") ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    /// <summary>建一個只授權 tcrfc 的翻譯人員帳號（直接寫庫，不依賴種子）；用 <see cref="DeleteAccountAsync"/> 清除。需要 migration 已把 <c>site.string.*</c> 指派給 translator 角色。</summary>
    public static async Task<string> CreateTranslatorAsync()
    {
        var username = $"zz-translator-{Guid.NewGuid():N}"[..30] + "@tcrfc.test";
        await BizTest.ExecuteSqlAsync(
            """
            DECLARE @u uniqueidentifier = NEWID();
            INSERT INTO admin_users (id, username, password_hash, display_name, status, is_super_admin)
            VALUES (@u, @Name, N'not-a-real-hash', N'ZZTEST 翻譯人員', N'active', 0);
            INSERT INTO admin_user_roles (admin_user_id, admin_role_id) SELECT @u, id FROM admin_roles WHERE code = N'translator';
            INSERT INTO admin_user_clubs (admin_user_id, club_id, granted_on, is_active)
            SELECT @u, id, CAST(SYSUTCDATETIME() AS date), 1 FROM clubs WHERE code = N'tcrfc';
            """,
            ("@Name", username));
        return username;
    }

    public static async Task DeleteAccountAsync(string username)
    {
        await BizTest.ExecuteSqlAsync(
            """
            DECLARE @u uniqueidentifier = (SELECT id FROM admin_users WHERE username = @Name);
            IF @u IS NOT NULL
            BEGIN
              DELETE FROM admin_user_roles WHERE admin_user_id = @u;
              DELETE FROM admin_user_clubs WHERE admin_user_id = @u;
              DELETE FROM admin_refresh_tokens WHERE admin_user_id = @u;
              DELETE FROM admin_users WHERE id = @u;
            END
            """,
            ("@Name", username));
    }
}
