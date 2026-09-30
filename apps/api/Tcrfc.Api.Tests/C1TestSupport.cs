using System.Net.Http.Json;
using Microsoft.Data.SqlClient;

namespace Tcrfc.Api.Tests;

/// <summary>C1（F1 漫畫／F2 球迷會活動／S1–S6 商店／K5 抽獎）測試共用小工具：直接對測試庫查值與清殘留。
/// 測試資料一律在 finally 清掉；不動種子（示範資料的識別是「【示範】」前綴或固定代碼，見 db/seed/backoffice_seed.py）。</summary>
internal static class C1Test
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING") ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");

    public static async Task<T?> ScalarAsync<T>(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)Convert.ChangeType(result, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    public static Task<Guid> ClubIdAsync(string code) => BizTest.ScalarGuidAsync("SELECT id FROM clubs WHERE code = @C", ("@C", code));

    /// <summary>取一個俱樂部的球員 id（漫畫角色關聯球員用）。</summary>
    public static Task<Guid> PlayerIdAsync(string clubCode) =>
        BizTest.ScalarGuidAsync("SELECT TOP 1 p.id FROM players p JOIN clubs c ON c.id = p.club_id WHERE c.code = @C ORDER BY p.row_seq", ("@C", clubCode));

    /// <summary>取一個俱樂部的文章 id（球迷會活動回顧關聯用）。</summary>
    public static Task<Guid> ArticleIdAsync(string clubCode, int skip = 0) =>
        BizTest.ScalarGuidAsync(
            "SELECT a.id FROM articles a JOIN clubs c ON c.id = a.club_id WHERE c.code = @C ORDER BY a.row_seq OFFSET @S ROWS FETCH NEXT 1 ROWS ONLY",
            ("@C", clubCode), ("@S", skip));

    public static async Task<string> BodyAsync(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) => await BizTest.ReadAsync<T>(response);

    public static async Task<HttpResponseMessage> PutJsonAsync(HttpClient client, string url, object payload) => await client.PutAsync(url, BizTest.Json(payload));

    public static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object payload) => await client.PostAsync(url, BizTest.Json(payload));

    public static async Task<HttpResponseMessage> PostEmptyAsync(HttpClient client, string url) => await client.PostAsync(url, null);

    /// <summary>拍下某俱樂部符合 <paramref name="likePattern"/> 的設定（含雙語值），回傳「還原」委派——測試改動共用庫的設定前後一定要用它，
    /// 不得以「這批鍵理論上不存在」當還原手段（E-81：種子會種這些鍵）。</summary>
    public static async Task<Func<Task>> SnapshotSettingsAsync(string clubCode, string likePattern)
    {
        var clubId = await ClubIdAsync(clubCode);
        var settingRows = new List<(Guid Id, string Key, string? Value, string? Group)>();
        var i18nRows = new List<(Guid SettingId, string Locale, string? Value)>();
        await using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id, setting_key, setting_value, setting_group FROM settings WHERE club_id = @C AND setting_key LIKE @P";
                command.Parameters.AddWithValue("@C", clubId);
                command.Parameters.AddWithValue("@P", likePattern);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    settingRows.Add((reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3)));
                }
            }

            await using var command2 = connection.CreateCommand();
            command2.CommandText = "SELECT si.setting_id, si.locale, si.value FROM settings_i18n si JOIN settings s ON s.id = si.setting_id WHERE s.club_id = @C AND s.setting_key LIKE @P";
            command2.Parameters.AddWithValue("@C", clubId);
            command2.Parameters.AddWithValue("@P", likePattern);
            await using var reader2 = await command2.ExecuteReaderAsync();
            while (await reader2.ReadAsync())
            {
                i18nRows.Add((reader2.GetGuid(0), reader2.GetString(1), reader2.IsDBNull(2) ? null : reader2.GetString(2)));
            }
        }

        return async () =>
        {
            await BizTest.ExecuteSqlAsync(
                "DELETE FROM settings_i18n WHERE setting_id IN (SELECT id FROM settings WHERE club_id = @C AND setting_key LIKE @P); DELETE FROM settings WHERE club_id = @C AND setting_key LIKE @P;",
                ("@C", clubId), ("@P", likePattern));
            foreach (var row in settingRows)
            {
                await BizTest.ExecuteSqlAsync(
                    "INSERT INTO settings (id, club_id, setting_key, setting_value, setting_group) VALUES (@I, @C, @K, @V, @G)",
                    ("@I", row.Id), ("@C", clubId), ("@K", row.Key), ("@V", row.Value), ("@G", row.Group));
            }

            foreach (var row in i18nRows)
            {
                await BizTest.ExecuteSqlAsync("INSERT INTO settings_i18n (setting_id, locale, value) VALUES (@I, @L, @V)", ("@I", row.SettingId), ("@L", row.Locale), ("@V", row.Value));
            }
        };
    }
}
