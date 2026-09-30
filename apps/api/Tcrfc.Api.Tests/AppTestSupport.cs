using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Tcrfc.Api.Features.AdminApp;

namespace Tcrfc.Api.Tests;

/// <summary>D 批（G3／E4–E6／M1–M5／App 公開端點）整合測試共用的小工具。
/// 🔴 測試資料一律用 <c>ZZTEST</c>／<c>test-dev-</c>／<c>zz-test-</c> 前綴，並在 finally 清掉；會動到共用庫設定的測試（更新門檻旗標、維護模式、
/// 功能開關、自動推播規則）一律用 <see cref="AppTest.SnapshotAppStateAsync"/> 拍照再還原，不得以「這批鍵理論上不存在」當還原手段（E-81／E-89：種子會種這些鍵）。</summary>
internal static class AppTest
{
    public static string NewDeviceId() => "test-dev-" + Guid.NewGuid().ToString("N");

    public static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(x => x.BackgroundColor(Color.SteelBlue));
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    public static async Task<HttpResponseMessage> PutJsonAsync(HttpClient client, string url, object payload) => await client.PutAsync(url, BizTest.Json(payload));

    public static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object payload) => await client.PostAsync(url, BizTest.Json(payload));

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) => await BizTest.ReadAsync<T>(response);

    /// <summary>註冊一台測試裝置（含推播權杖與已允許推播）。回傳裝置識別碼。</summary>
    public static async Task<string> RegisterDeviceAsync(HttpClient client, string platform = "ios", string locale = "zh", string? token = null, string permission = "granted")
    {
        var id = NewDeviceId();
        var response = await PutJsonAsync(client, $"/api/v1/app/devices/{id}", new
        {
            platform, osVersion = "test", appVersion = "1.0.0", locale, pushToken = token ?? ("token-" + Guid.NewGuid().ToString("N")), pushPermission = permission,
        });
        response.EnsureSuccessStatusCode();
        return id;
    }

    /// <summary>刪除所有測試裝置與相關診斷／訂閱（訂閱由外鍵串聯刪除）。</summary>
    public static async Task CleanupDevicesAsync()
    {
        await BizTest.ExecuteSqlAsync("DELETE FROM app_diagnostic_reports WHERE device_install_id LIKE 'test-dev-%'");
        await BizTest.ExecuteSqlAsync("DELETE FROM app_devices WHERE device_install_id LIKE 'test-dev-%'");
    }

    /// <summary>刪除所有測試廣告資料（依 ZZTEST 前綴的廣告主、版位）。順序：事件／日聚合 → 素材 → 檔期 → 廣告主 → 版位。</summary>
    public static async Task CleanupAdsAsync()
    {
        const string campaigns = "SELECT c.id FROM ad_campaigns c JOIN advertisers_i18n a ON a.advertiser_id = c.advertiser_id WHERE a.name LIKE 'ZZTEST%'";
        await BizTest.ExecuteSqlAsync($"DELETE FROM ad_events WHERE campaign_id IN ({campaigns})");
        await BizTest.ExecuteSqlAsync($"DELETE FROM ad_daily_stats WHERE campaign_id IN ({campaigns})");
        await BizTest.ExecuteSqlAsync($"DELETE FROM ad_creatives WHERE campaign_id IN ({campaigns})");
        await BizTest.ExecuteSqlAsync($"DELETE FROM ad_campaigns WHERE id IN ({campaigns})");
        await BizTest.ExecuteSqlAsync("DELETE FROM advertisers WHERE id IN (SELECT advertiser_id FROM advertisers_i18n WHERE name LIKE 'ZZTEST%')");
        await BizTest.ExecuteSqlAsync("DELETE FROM ad_slots WHERE slot_code LIKE 'zztest\\_%' ESCAPE '\\'");
    }

    /// <summary>直接對資料庫插入一個「已通過審核」的素材（跳過圖片上傳，供狀態機與投放測試使用）。</summary>
    public static async Task<Guid> InsertCreativeAsync(Guid campaignId, string locale = "zh-Hant", string reviewStatus = "approved", bool paused = false, string clickUrl = "https://example.com/zz")
    {
        var id = Guid.NewGuid();
        await BizTest.ExecuteSqlAsync(
            "INSERT INTO ad_creatives (id, campaign_id, locale, alt_text, title, click_url, theme, review_status, is_paused) VALUES (@I, @C, @L, N'ZZTEST 替代文字', N'ZZTEST 標題', @U, N'both', @R, @P)",
            ("@I", id), ("@C", campaignId), ("@L", locale), ("@U", clickUrl), ("@R", reviewStatus), ("@P", paused));
        return id;
    }

    /// <summary>拍下會被 D 批測試改動的共用狀態（版本旗標、維護模式與自動推播規則設定、功能開關、憑證），回傳還原委派。</summary>
    public static async Task<Func<Task>> SnapshotAppStateAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING") ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");
        var releaseFlags = new List<(Guid Id, bool Min, bool Rec)>();
        var settings = new List<(Guid Id, string Key, string? Value)>();
        var flags = new List<(Guid Id, string Key, bool Enabled, string? Str, string Platform)>();
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using (var c1 = new SqlCommand("SELECT id, is_min_supported, is_recommended FROM app_releases", connection))
            await using (var r1 = await c1.ExecuteReaderAsync())
            {
                while (await r1.ReadAsync()) { releaseFlags.Add((r1.GetGuid(0), r1.GetBoolean(1), r1.GetBoolean(2))); }
            }

            await using (var c2 = new SqlCommand("SELECT id, setting_key, setting_value FROM app_settings", connection))
            await using (var r2 = await c2.ExecuteReaderAsync())
            {
                while (await r2.ReadAsync()) { settings.Add((r2.GetGuid(0), r2.GetString(1), r2.IsDBNull(2) ? null : r2.GetString(2))); }
            }

            await using (var c3 = new SqlCommand("SELECT id, flag_key, is_enabled, string_value, platform FROM app_feature_flags", connection))
            await using (var r3 = await c3.ExecuteReaderAsync())
            {
                while (await r3.ReadAsync()) { flags.Add((r3.GetGuid(0), r3.GetString(1), r3.GetBoolean(2), r3.IsDBNull(3) ? null : r3.GetString(3), r3.GetString(4))); }
            }
        }

        return async () =>
        {
            // 測試新增的版本、開關與設定先清掉，再把種子原本的值放回去。
            await BizTest.ExecuteSqlAsync("DELETE FROM app_releases WHERE version LIKE '9.%' OR version LIKE '8.%'");
            await BizTest.ExecuteSqlAsync("DELETE FROM app_feature_flags WHERE flag_key LIKE 'zztest\\_%' ESCAPE '\\'");
            await BizTest.ExecuteSqlAsync("DELETE FROM app_credentials WHERE label LIKE 'ZZTEST%'");
            foreach (var r in releaseFlags)
            {
                await BizTest.ExecuteSqlAsync("UPDATE app_releases SET is_min_supported = @M, is_recommended = @R WHERE id = @I", ("@M", r.Min), ("@R", r.Rec), ("@I", r.Id));
            }

            foreach (var f in flags)
            {
                await BizTest.ExecuteSqlAsync("UPDATE app_feature_flags SET is_enabled = @E, string_value = @S WHERE id = @I", ("@E", f.Enabled), ("@S", f.Str), ("@I", f.Id));
            }

            await BizTest.ExecuteSqlAsync("DELETE FROM app_settings WHERE setting_key LIKE 'maintenance.%' OR setting_key = 'push.rules'");
            foreach (var s in settings.Where(s => s.Key.StartsWith("maintenance.", StringComparison.Ordinal) || s.Key == "push.rules"))
            {
                await BizTest.ExecuteSqlAsync("INSERT INTO app_settings (id, setting_key, setting_value) VALUES (@I, @K, @V)", ("@I", s.Id), ("@K", s.Key), ("@V", s.Value));
            }
        };
    }
}

/// <summary>測試用推播傳輸：記錄每一次送出，並可依權杖前綴指定結果。<see cref="IsConfigured"/> 為 true（模擬「已串接」）。</summary>
internal sealed class RecordingPushTransport : IPushTransport
{
    public static readonly List<(string Platform, string Title, string Token)> Sent = [];
    public static Func<string, PushSendOutcome> Decide { get; set; } = _ => PushSendOutcome.Accepted;

    public bool IsConfigured => true;

    public Task<PushSendResult> SendAsync(PushTarget target, PushPayload payload, CancellationToken cancellationToken)
    {
        lock (Sent)
        {
            Sent.Add((target.Platform, payload.Title, target.Token));
        }

        return Task.FromResult(new PushSendResult(Decide(target.Token)));
    }

    public static void Reset()
    {
        lock (Sent) { Sent.Clear(); }
        Decide = _ => PushSendOutcome.Accepted;
    }
}

/// <summary>整合測試主機不跑 App 定時維護（避免背景作業與測試資料互相干擾），並把 App 公開端點的限流額度調到形同不限流
/// （TestServer 底下所有請求共用同一個「unknown」IP 分區，見 <c>TestRateLimitOverrides</c> 檔頭）。</summary>
internal static class AppTestEnvironment
{
    [ModuleInitializer]
    internal static void Init()
    {
        Environment.SetEnvironmentVariable("APP_JOBS_INTERVAL_SECONDS", "0");
        Environment.SetEnvironmentVariable("APP_PUBLIC_RATE_LIMIT_PERMITS", "100000");
    }
}
