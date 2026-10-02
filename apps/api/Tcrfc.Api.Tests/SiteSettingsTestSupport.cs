using Microsoft.Data.SqlClient;

namespace Tcrfc.Api.Tests;

/// <summary>I 網站設定與儀表板整合測試共用的小工具：臨時翻譯人員帳號、選單快照還原。注意：T-SQL 變數與參數名稱不分大小寫，局部變數與參數不可僅大小寫不同（docs/18 E-121）。</summary>
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

    /// <summary>拍下某俱樂部全部選單項目，回傳還原委派（測試改動共用庫前後一定要用，E-81／E-119）。</summary>
    public static async Task<Func<Task>> SnapshotMenusAsync(string clubCode)
    {
        var clubId = await C1Test.ClubIdAsync(clubCode);
        var items = new List<(Guid Id, Guid? Parent, string? Location, string? Url, bool External, int Sort)>();
        var labels = new List<(Guid ItemId, string Locale, string? Label)>();
        await using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, parent_id, menu_location, url, is_external, sort_order FROM menu_items WHERE club_id = @C";
                command.Parameters.AddWithValue("@C", clubId);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add((reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetBoolean(4), reader.GetInt32(5)));
                }
            }

            await using var command2 = connection.CreateCommand();
            command2.CommandText = "SELECT i.menu_item_id, i.locale, i.label FROM menu_items_i18n i JOIN menu_items m ON m.id = i.menu_item_id WHERE m.club_id = @C";
            command2.Parameters.AddWithValue("@C", clubId);
            await using var reader2 = await command2.ExecuteReaderAsync();
            while (await reader2.ReadAsync())
            {
                labels.Add((reader2.GetGuid(0), reader2.GetString(1), reader2.IsDBNull(2) ? null : reader2.GetString(2)));
            }
        }

        return async () =>
        {
            await BizTest.ExecuteSqlAsync(
                "UPDATE menu_items SET parent_id = NULL WHERE club_id = @C; DELETE FROM menu_items_i18n WHERE menu_item_id IN (SELECT id FROM menu_items WHERE club_id = @C); DELETE FROM menu_items WHERE club_id = @C;",
                ("@C", clubId));
            foreach (var item in items)
            {
                await BizTest.ExecuteSqlAsync(
                    "INSERT INTO menu_items (id, club_id, menu_location, url, is_external, sort_order) VALUES (@I, @C, @L, @U, @E, @S)",
                    ("@I", item.Id), ("@C", clubId), ("@L", item.Location), ("@U", item.Url), ("@E", item.External), ("@S", item.Sort));
            }

            foreach (var item in items.Where(i => i.Parent is not null))
            {
                await BizTest.ExecuteSqlAsync("UPDATE menu_items SET parent_id = @P WHERE id = @I", ("@P", item.Parent), ("@I", item.Id));
            }

            foreach (var label in labels)
            {
                await BizTest.ExecuteSqlAsync(
                    "INSERT INTO menu_items_i18n (menu_item_id, locale, label) VALUES (@I, @L, @T)", ("@I", label.ItemId), ("@L", label.Locale), ("@T", label.Label));
            }
        };
    }
}
