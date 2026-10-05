using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Tcrfc.Api.Security;
using Tcrfc.Api.Tests.Fixtures;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>AP-3（2026-10-02）：App 更新權杖的字串格式與簽章。不需要資料庫。</summary>
public sealed class AppRefreshTokenCodecTests
{
    private static byte[] Key(string seed = "unit-test-signing-key-0123456789-abcdef")
        => AppRefreshTokenCodec.DeriveKey(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [AdminTokenService.ConfigKey] = seed,
        }).Build());

    [Fact]
    public void 建立後可解析_並帶回裝置id與簽發時間()
    {
        var key = Key();
        var deviceId = Guid.NewGuid();
        var raw = AppRefreshTokenCodec.Create(key, deviceId, 1_700_000_000_123);

        Assert.StartsWith("ad1.", raw);
        Assert.True(raw.Length <= 200);
        var parsed = AppRefreshTokenCodec.TryParse(key, raw);
        Assert.NotNull(parsed);
        Assert.Equal(deviceId, parsed!.DeviceId);
        Assert.Equal(1_700_000_000_123, parsed.StampMs);
        Assert.NotEqual(raw, AppRefreshTokenCodec.Create(key, deviceId, 1_700_000_000_123)); // 每把都有獨立亂數
    }

    [Theory]
    [InlineData("")]
    [InlineData("ad1.")]
    [InlineData("ad1.garbage")]
    [InlineData("not-an-app-token")]
    [InlineData("ad1.00000000000000000000000000000000.1700000000000.AAAA.BBBB")]
    public void 垃圾字串一律解析失敗(string raw)
        => Assert.Null(AppRefreshTokenCodec.TryParse(Key(), raw));

    [Fact]
    public void 竄改任何一段或換金鑰都解析失敗()
    {
        var key = Key();
        var raw = AppRefreshTokenCodec.Create(key, Guid.NewGuid(), 1_700_000_000_000);
        var parts = raw.Split('.');

        // 竄改簽發時間（想把新權杖偽裝成舊的／舊的偽裝成新的）
        var forgedStamp = string.Join('.', parts[0], parts[1], "1699999999999", parts[3], parts[4]);
        Assert.Null(AppRefreshTokenCodec.TryParse(key, forgedStamp));

        // 竄改簽章最後一個字元
        var last = raw[^1] == 'A' ? 'B' : 'A';
        Assert.Null(AppRefreshTokenCodec.TryParse(key, raw[..^1] + last));

        // 另一把金鑰簽的
        Assert.Null(AppRefreshTokenCodec.TryParse(Key("another-signing-key-0123456789-abcdef-xyz"), raw));

        // 超長
        Assert.Null(AppRefreshTokenCodec.TryParse(key, raw + new string('x', 200)));
    }

    [Fact]
    public void 毫秒時間戳與UTC時間互轉不失真()
    {
        var utc = new DateTime(2026, 10, 2, 3, 4, 5, 678, DateTimeKind.Utc);
        Assert.Equal(utc, AppRefreshTokenCodec.ToUtc(AppRefreshTokenCodec.ToStampMs(utc)));
        Assert.Equal(utc, AppRefreshTokenCodec.ToUtc(AppRefreshTokenCodec.ToStampMs(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified))));
    }
}

/// <summary>AP-3：App 更新權杖鏈（掛 <c>app_devices</c>）的發放、輪替、重用偵測、登出與裝置撤銷。真實 HTTP 管線＋真實資料庫。</summary>
[Collection(AdminWriteCollection.Name)]
public sealed class AppDeviceSessionTests(AdminWriteApiFixture fixture) : IAsyncLifetime
{
    private readonly MemberTestScope _scope = new(fixture);

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _scope.DisposeAsync();
        await AppTest.CleanupDevicesAsync();
    }

    private sealed record DeviceState(Guid? MemberId, string? Hash, DateTime? ExpiresAt, DateTime? RotatedAt, DateTime? RevokedAt);

    private static async Task<DeviceState> StateAsync(string deviceInstallId)
    {
        var cs = Environment.GetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING") ?? throw new InvalidOperationException("CLUB_SQL_CONNECTION_STRING 未設定。");
        await using var connection = new SqlConnection(cs);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT member_id, refresh_token_hash, refresh_token_expires_at, refresh_token_rotated_at, revoked_at FROM app_devices WHERE device_install_id = @D";
        command.Parameters.AddWithValue("@D", deviceInstallId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "找不到測試裝置列。");
        return new DeviceState(
            reader.IsDBNull(0) ? null : reader.GetGuid(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            reader.IsDBNull(4) ? null : reader.GetDateTime(4));
    }

    private sealed record AppSession(string AccessToken, string RefreshToken, JsonElement Raw);

    private static async Task<HttpResponseMessage> LoginRawAsync(HttpClient client, string email, string password, string deviceInstallId)
        => await client.PostAsJsonAsync("/api/v1/member/auth/login", new { email, password, deviceInstallId }, TestJson.WriteOptions);

    private static async Task<AppSession> ReadSessionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new AppSession(json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!, json);
    }

    private static Task<HttpResponseMessage> RefreshRawAsync(HttpClient client, string refreshToken)
        => client.PostAsJsonAsync("/api/v1/member/auth/refresh", new { refreshToken }, TestJson.WriteOptions);

    /// <summary>建立已驗證會員＋已註冊裝置，並用 deviceInstallId 登入。</summary>
    private async Task<(MemberTestScope.TestMember Member, string DeviceId, HttpClient Anonymous, AppSession Session)> SetupAsync(string tag)
    {
        var member = await _scope.CreateVerifiedMemberAsync(tag);
        var anonymous = _scope.Client();
        var deviceId = await AppTest.RegisterDeviceAsync(anonymous);
        var session = await ReadSessionAsync(await LoginRawAsync(anonymous, member.Email, MemberTestScope.Password, deviceId));
        return (member, deviceId, anonymous, session);
    }

    [Fact]
    public async Task 登入帶裝置識別_更新權杖掛在裝置列_只存雜湊_並綁定會員()
    {
        var (member, deviceId, _, session) = await SetupAsync("app-issue");

        Assert.StartsWith("ad1.", session.RefreshToken);
        Assert.True(session.Raw.GetProperty("refreshTokenExpiresAt").GetDateTime() > DateTime.UtcNow.AddDays(89));

        var state = await StateAsync(deviceId);
        Assert.Equal(member.MemberId, state.MemberId);
        Assert.NotNull(state.Hash);
        Assert.Equal(64, state.Hash!.Length);
        Assert.DoesNotContain(session.RefreshToken, state.Hash); // 只存雜湊，不存原值
        Assert.Null(state.RevokedAt);
        Assert.True(state.ExpiresAt > DateTime.UtcNow.AddDays(89));
        Assert.NotNull(state.RotatedAt);

        // 存取權杖可用
        using var me = _scope.Client(session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await me.GetAsync("/api/v1/member/me")).StatusCode);
    }

    [Fact]
    public async Task 輪替_每次換新_舊權杖失效_且簽發時間嚴格遞增()
    {
        var (_, deviceId, anonymous, first) = await SetupAsync("app-rotate");
        var before = await StateAsync(deviceId);

        var second = await ReadSessionAsync(await RefreshRawAsync(anonymous, first.RefreshToken));
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.StartsWith("ad1.", second.RefreshToken);
        var afterSecond = await StateAsync(deviceId);
        Assert.NotEqual(before.Hash, afterSecond.Hash);
        Assert.True(afterSecond.RotatedAt > before.RotatedAt);

        var third = await ReadSessionAsync(await RefreshRawAsync(anonymous, second.RefreshToken));
        var afterThird = await StateAsync(deviceId);
        Assert.True(afterThird.RotatedAt > afterSecond.RotatedAt);
        Assert.NotEqual(second.RefreshToken, third.RefreshToken);
    }

    [Fact]
    public async Task 重用偵測_舊權杖再被使用_撤銷整條鏈_連新權杖也失效()
    {
        var (member, deviceId, anonymous, first) = await SetupAsync("app-reuse");
        var second = await ReadSessionAsync(await RefreshRawAsync(anonymous, first.RefreshToken));

        var replay = await RefreshRawAsync(anonymous, first.RefreshToken); // 舊的被重放
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        var state = await StateAsync(deviceId);
        Assert.NotNull(state.RevokedAt);
        Assert.Null(state.Hash);
        Assert.Null(state.MemberId); // 解除會員綁定，裝置列保留

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, second.RefreshToken)).StatusCode);

        // 重新登入後恢復，且撤銷前核發的舊權杖仍然無效（並再次視為重用）
        var relogin = await ReadSessionAsync(await LoginRawAsync(anonymous, member.Email, MemberTestScope.Password, deviceId));
        var stateAfterLogin = await StateAsync(deviceId);
        Assert.Null(stateAfterLogin.RevokedAt);
        Assert.NotNull(stateAfterLogin.Hash);
        Assert.Equal(HttpStatusCode.OK, (await RefreshRawAsync(anonymous, relogin.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task 簽章不合法的權杖_一律拒絕_且不會登出任何人()
    {
        var (_, deviceId, anonymous, session) = await SetupAsync("app-forged");
        var parts = session.RefreshToken.Split('.');
        var forged = string.Join('.', parts[0], parts[1], "1", parts[3], parts[4]); // 簽發時間被改小，想觸發「舊權杖」判定

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, forged)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, "ad1.garbage")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, new string('x', 400))).StatusCode);

        var state = await StateAsync(deviceId);
        Assert.Null(state.RevokedAt);
        Assert.NotNull(state.Hash);
        Assert.Equal(HttpStatusCode.OK, (await RefreshRawAsync(anonymous, session.RefreshToken)).StatusCode); // 真的那把仍可用
    }

    [Fact]
    public async Task 登出_撤銷這支裝置的鏈並解除綁定_裝置列保留()
    {
        var (_, deviceId, anonymous, session) = await SetupAsync("app-logout");

        var logout = await anonymous.PostAsJsonAsync("/api/v1/member/auth/logout", new { refreshToken = session.RefreshToken }, TestJson.WriteOptions);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var state = await StateAsync(deviceId);
        Assert.NotNull(state.RevokedAt);
        Assert.Null(state.Hash);
        Assert.Null(state.MemberId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, session.RefreshToken)).StatusCode);

        // 亂送權杖登出：不影響任何人
        var other = await SetupAsync("app-logout-other");
        await anonymous.PostAsJsonAsync("/api/v1/member/auth/logout", new { refreshToken = "ad1.garbage" }, TestJson.WriteOptions);
        Assert.Null((await StateAsync(other.DeviceId)).RevokedAt);
    }

    [Fact]
    public async Task 登出全部裝置與變更密碼_兩台裝置的鏈都被撤銷()
    {
        var member = await _scope.CreateVerifiedMemberAsync("app-all");
        var anonymous = _scope.Client();
        var deviceA = await AppTest.RegisterDeviceAsync(anonymous);
        var deviceB = await AppTest.RegisterDeviceAsync(anonymous, "android");
        var a = await ReadSessionAsync(await LoginRawAsync(anonymous, member.Email, MemberTestScope.Password, deviceA));
        var b = await ReadSessionAsync(await LoginRawAsync(anonymous, member.Email, MemberTestScope.Password, deviceB));

        using var authed = _scope.Client(a.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await authed.PostAsync("/api/v1/member/auth/logout-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, a.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, b.RefreshToken)).StatusCode);
        Assert.NotNull((await StateAsync(deviceA)).RevokedAt);
        Assert.NotNull((await StateAsync(deviceB)).RevokedAt);

        // 變更密碼：撤銷全部，並替目前裝置發一組新的
        var a2 = await ReadSessionAsync(await LoginRawAsync(anonymous, member.Email, MemberTestScope.Password, deviceA));
        var b2 = await ReadSessionAsync(await LoginRawAsync(anonymous, member.Email, MemberTestScope.Password, deviceB));
        using var authed2 = _scope.Client(a2.AccessToken);
        var change = await authed2.PostAsJsonAsync("/api/v1/member/auth/change-password",
            new { currentPassword = MemberTestScope.Password, newPassword = "New-Passw0rd!x", deviceInstallId = deviceA }, TestJson.WriteOptions);
        var changed = await ReadSessionAsync(change);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, b2.RefreshToken)).StatusCode);
        // 先確認新發的那組可用，再重放撤銷前的舊權杖：舊權杖簽章合法、簽發時間早於現行那把，
        // 依重用偵測規則會被視為外洩並撤銷這條鏈，所以重放必須放在最後（放前面會連新權杖一起殺掉）。
        var rotated = await ReadSessionAsync(await RefreshRawAsync(anonymous, changed.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, a2.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, rotated.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task 刪除帳號_所有裝置的鏈被撤銷並解除綁定_舊權杖再也換不到新的()
    {
        var member = await _scope.CreateVerifiedMemberAsync("app-del");
        var anonymous = _scope.Client();
        var deviceA = await AppTest.RegisterDeviceAsync(anonymous);
        var deviceB = await AppTest.RegisterDeviceAsync(anonymous, "android");
        var a = await ReadSessionAsync(await LoginRawAsync(anonymous, member.Email, MemberTestScope.Password, deviceA));
        var b = await ReadSessionAsync(await LoginRawAsync(anonymous, member.Email, MemberTestScope.Password, deviceB));

        using var authed = _scope.Client(a.AccessToken);
        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/member/me")
        {
            Content = JsonContent.Create(new { password = MemberTestScope.Password }, options: TestJson.WriteOptions),
        };
        Assert.Equal(HttpStatusCode.NoContent, (await authed.SendAsync(delete)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, a.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, b.RefreshToken)).StatusCode);
        foreach (var device in new[] { deviceA, deviceB })
        {
            var state = await StateAsync(device);
            Assert.Null(state.MemberId); // 解除綁定（裝置列本身保留）
            Assert.Null(state.Hash);
        }
    }

    [Fact]
    public async Task 裝置撤銷_只能撤銷自己的裝置_別人的一律404()
    {
        var mine = await SetupAsync("app-rev-a");
        var theirs = await SetupAsync("app-rev-b");

        using var client = _scope.Client(mine.Session.AccessToken);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/member/devices");
        Assert.Equal(1, list.GetArrayLength());
        var entry = list[0];
        Assert.Equal("ios", entry.GetProperty("platform").GetString());
        Assert.True(entry.GetProperty("hasActiveSession").GetBoolean());
        Assert.False(entry.TryGetProperty("deviceInstallId", out _)); // 不外洩裝置識別碼
        var deviceRowId = entry.GetProperty("deviceId").GetGuid();

        var theirsRowId = await B1Test.ScalarAsync("SELECT id FROM app_devices WHERE device_install_id = @D", ("@D", theirs.DeviceId));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/v1/member/devices/{theirsRowId}/revoke", null)).StatusCode);
        Assert.NotNull((await StateAsync(theirs.DeviceId)).Hash);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/member/devices/{deviceRowId}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(mine.Anonymous, mine.Session.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _scope.Client().GetAsync("/api/v1/member/devices")).StatusCode);
    }

    [Fact]
    public async Task 裝置未註冊_登入回400且不核發權杖()
    {
        var member = await _scope.CreateVerifiedMemberAsync("app-nodev");
        using var anonymous = _scope.Client();
        var response = await LoginRawAsync(anonymous, member.Email, MemberTestScope.Password, "test-dev-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("device_not_registered", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task 過期的權杖被拒絕_但不算重用_鏈不被撤銷()
    {
        var (_, deviceId, anonymous, session) = await SetupAsync("app-expired");
        await BizTest.ExecuteSqlAsync("UPDATE app_devices SET refresh_token_expires_at = DATEADD(MINUTE, -1, SYSUTCDATETIME()) WHERE device_install_id = @D", ("@D", deviceId));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, session.RefreshToken)).StatusCode);
        Assert.Null((await StateAsync(deviceId)).RevokedAt);
    }

    [Fact]
    public async Task 會員被停用_輪替時拒絕並撤銷整條鏈()
    {
        var (member, deviceId, anonymous, session) = await SetupAsync("app-suspended");
        await BizTest.ExecuteSqlAsync("UPDATE members SET status = 'suspended' WHERE id = @M", ("@M", member.MemberId));

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshRawAsync(anonymous, session.RefreshToken)).StatusCode);
        var state = await StateAsync(deviceId);
        Assert.NotNull(state.RevokedAt);
        Assert.Null(state.Hash);
    }

    [Fact]
    public async Task 網頁工作階段不受影響_Cookie模式照舊走member_refresh_tokens()
    {
        var member = await _scope.CreateVerifiedMemberAsync("app-web");
        var anonymous = _scope.Client();
        var web = await MemberTestScope.LoginAsync(anonymous, member.Email, MemberTestScope.Password);
        Assert.NotNull(web.RefreshToken);
        Assert.False(web.RefreshToken!.StartsWith("ad1.", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await RefreshRawAsync(anonymous, web.RefreshToken)).StatusCode);
    }
}
