using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Tcrfc.Api.Tests.Fixtures;

namespace Tcrfc.Api.Tests;

/// <summary>E1a（E1／E2／E3／B5／B6／C5）後台與公開端點測試共用的小工具：已登入用戶端、multipart 組裝、
/// 直接對測試庫執行 SQL（種共同列、清殘留）。</summary>
internal static class BizTest
{
    public static async Task<HttpClient> ClientAsync(WebApplicationFactory<Program> factory, string? username)
    {
        var client = factory.CreateClient();
        if (username is not null)
        {
            var token = await TestAdminTokens.IssueAccessTokenForSeededUserAsync(username);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public static StringContent Json(object payload)
        => new(JsonSerializer.Serialize(payload, TestJson.WriteOptions), Encoding.UTF8, "application/json");

    public static MultipartFormDataContent Multipart(object payload, params (string Field, byte[] Bytes, string FileName, string ContentType)[] files)
    {
        var form = new MultipartFormDataContent();
        var content = new StringContent(JsonSerializer.Serialize(payload, TestJson.WriteOptions), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(content, "payload");
        foreach (var (field, bytes, fileName, contentType) in files)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(file, field, fileName);
        }

        return form;
    }

    public static byte[] Pdf() => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");

    public static byte[] Zip() => [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"預期成功但得到 {(int)response.StatusCode}：{await response.Content.ReadAsStringAsync()}");
        }

        var value = await response.Content.ReadFromJsonAsync<T>(TestJson.Options);
        return value ?? throw new InvalidOperationException("回應無法解析：" + await response.Content.ReadAsStringAsync());
    }

    public static async Task<int> ExecuteSqlAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    public static async Task<Guid> ScalarGuidAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("查無資料：" + sql));
    }

    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)];

    public static object Zh(string name, string? content = null) => new { name, content };
}
