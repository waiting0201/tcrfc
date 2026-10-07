using System.Collections.Concurrent;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tcrfc.Api.CharityPlatform.Common;
using Tcrfc.Api.CharityPlatform.Invoices;
using Tcrfc.Api.CharityPlatform.Mail;
using Tcrfc.Api.CharityPlatform.Payments;
using Tcrfc.Api.CharityPlatform.Security;
using Tcrfc.Api.CharityPlatform.Storage;
using Tcrfc.Api.Images;
using Tcrfc.Api.Security;
using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 慈善（CH-2／CH-3）整合測試共用的 fixture：打真正的 HTTP 管線與真正的 <c>tcrfc_charity</c>（由
/// <see cref="CharityTestDatabaseGuard"/> 硬性限定只能連這一個庫）。金流、電子發票、寄信三個外部接縫換成可編排的測試替身
/// （<see cref="ScriptedPaymentGateway"/> 等），圖片儲存換成記憶體版——這樣才走得到「Confirm 結果未知」「憑證開立失敗」這些
/// 正式假實作不會產生的分支，也不需要 Azurite。
///
/// 🔴 測試資料一律用可辨識的標記（帳號 <c>ct-*@charity-test.invalid</c>、捐款人 Email <c>*@charity-test.invalid</c>、
/// 項目網址 <c>ct-*</c>、店家名稱 <c>CT店家*</c>），<see cref="CleanupAsync"/> 依標記清掉，<b>絕不碰種子資料</b>
/// （測試不得假設種子捐款的狀態，也不得改動它們）。🔴 背景工作（逾時、憑證重試）在測試主機關閉，測試直接呼叫
/// <c>CharityMaintenanceRunner</c>，結果才確定。
/// </summary>
public class CharityApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtKey = "test-only-charity-jwt-signing-key-not-for-production-32b";
    public const string TestEmailDomain = "charity-test.invalid";
    public const string PublicBaseUrl = "http://charity.test";
    public const string AssociationNotifyEmail = "notify@charity-test.invalid";

    public ScriptedPaymentGateway Gateway { get; } = new();
    public ScriptedInvoiceIssuer Invoices { get; } = new();
    public CapturingEmailSender Mail { get; } = new();
    public ScriptedReconciliationSource Reconciliation { get; } = new();
    public InMemoryCharityImageStorage Images { get; } = new();

    private string _connectionString = string.Empty;
    public string ConnectionString => _connectionString;

    /// <summary>三組依 IP 的限流額度（公開寫入、結果頁輪詢、後台登入與更新權杖）。一般 fixture 用寬鬆值；
    /// 專門驗證 429 的 fixture（<see cref="CharityRateLimitApiFixture"/>）覆寫成很小的數字。</summary>
    protected virtual string WritePermits => TestRateLimitOverrides.LoosePermitLimit;
    protected virtual string ReadPermits => TestRateLimitOverrides.LoosePermitLimit;
    protected virtual string LoginPermits => TestRateLimitOverrides.LoosePermitLimit;

    public ScriptedTurnstileVerifier Turnstile { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        TestRateLimitOverrides.ApplyAdminAuthOverrides(builder, LoginPermits);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [CharityRateLimitPolicies.WritePermitLimitConfigKey] = WritePermits,
            [CharityRateLimitPolicies.ReadPermitLimitConfigKey] = ReadPermits,
            [CharityOptions.WorkersEnabledConfigKey] = "false",
            [CharityOptions.PublicBaseUrlConfigKey] = PublicBaseUrl,
            [CharityInvoiceService.AssociationNotifyEmailConfigKey] = AssociationNotifyEmail,
        }));
        builder.ConfigureTestServices(ReplaceExternalServices);
    }

    private void ReplaceExternalServices(IServiceCollection services)
    {
        services.RemoveAll<IPaymentGateway>();
        services.RemoveAll<IInvoiceIssuer>();
        services.RemoveAll<IEmailSender>();
        services.RemoveAll<ICharityImageStorage>();
        services.RemoveAll<IPaymentReconciliationSource>();
        services.AddSingleton<IPaymentReconciliationSource>(Reconciliation);
        services.RemoveAll<ITurnstileVerifier>();
        services.AddSingleton<ITurnstileVerifier>(Turnstile);
        services.AddSingleton<IPaymentGateway>(Gateway);
        services.AddSingleton<IInvoiceIssuer>(Invoices);
        services.AddSingleton<IEmailSender>(Mail);
        services.AddSingleton<ICharityImageStorage>(Images);
    }

    public async Task InitializeAsync()
    {
        // 主站庫也要驗證（Program.cs 一律需要 CLUB_SQL_CONNECTION_STRING），慈善庫另有自己的護欄。
        var clubConnection = await TestDatabaseGuard.ResolveAndVerifyAsync();
        _connectionString = await CharityTestDatabaseGuard.ResolveAndVerifyAsync();

        Environment.SetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING", clubConnection);
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CLUB", TestJwtSigningKey.Value);
        Environment.SetEnvironmentVariable("CHARITY_SQL_CONNECTION_STRING", _connectionString);
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CHARITY", TestJwtKey);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("REDIS_HOST", null);
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", null);

        await CleanupAsync(); // 前一次測試中途失敗可能留下殘骸
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await CleanupAsync();
        Environment.SetEnvironmentVariable("CHARITY_SQL_CONNECTION_STRING", null);
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CHARITY", null);
        await base.DisposeAsync();
    }

    /// <summary>每個測試開始時呼叫：把三個替身與圖片儲存回到預設行為。</summary>
    public void ResetDoubles()
    {
        Gateway.Reset();
        Invoices.Reset();
        Mail.Reset();
        Reconciliation.Reset();
        Images.Reset();
        Turnstile.Reset();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 帳號與權杖
    // ═══════════════════════════════════════════════════════════════════════

    public const string TestPassword = "CharityTest@12345";

    /// <summary>建立一個測試後台帳號（真的 Argon2id 密碼雜湊，可走 /login）。<paramref name="roleCodes"/> 是種子角色代碼。</summary>
    public async Task<CharityTestAdmin> CreateAdminAsync(bool isSuperAdmin = false, params string[] roleCodes)
    {
        var id = Guid.NewGuid();
        var username = $"ct-{id:N}@{TestEmailDomain}";
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
                INSERT INTO admin_users (id, username, display_name, password_hash, must_change_password, is_super_admin, status)
                VALUES (@id, @u, @n, @h, 0, @s, N'active')
                """;
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@u", username);
            insert.Parameters.AddWithValue("@n", "測試後台人員");
            insert.Parameters.AddWithValue("@h", PasswordHasher.Hash(TestPassword));
            insert.Parameters.AddWithValue("@s", isSuperAdmin);
            await insert.ExecuteNonQueryAsync();
        }

        foreach (var role in roleCodes)
        {
            await using var assign = connection.CreateCommand();
            assign.CommandText = "INSERT INTO admin_user_roles (admin_user_id, admin_role_id) SELECT @id, id FROM admin_roles WHERE code = @c";
            assign.Parameters.AddWithValue("@id", id);
            assign.Parameters.AddWithValue("@c", role);
            if (await assign.ExecuteNonQueryAsync() != 1)
            {
                throw new InvalidOperationException($"找不到種子角色 '{role}'——請先跑 ./db/seed/apply-charity-seed.sh。");
            }
        }

        return new CharityTestAdmin(id, username, isSuperAdmin, IssueToken(id, username, isSuperAdmin));
    }

    /// <summary>建立一個只持有指定權限碼的臨時角色（代碼 <c>ct-role-*</c>，<see cref="CleanupAsync"/> 會清掉），回傳角色代碼——用來驗證「核對的人」與「登記付款的人」權限分離這類種子角色表達不出的組合。</summary>
    public async Task<string> CreateRoleAsync(params string[] permissionCodes)
    {
        var code = $"ct-role-{Guid.NewGuid():N}"[..20];
        await ExecuteAsync("INSERT INTO admin_roles (id, code, name_zh, name_en, is_system) VALUES (NEWID(), @c, N'測試角色', N'Test Role', 0)", ("@c", code));
        foreach (var permission in permissionCodes)
        {
            var linked = await ExecuteAsync(
                "INSERT INTO role_permissions (admin_role_id, permission_id) SELECT r.id, p.id FROM admin_roles r CROSS JOIN permissions p WHERE r.code = @c AND p.code = @p",
                ("@c", code), ("@p", permission));
            if (linked != 1)
            {
                throw new InvalidOperationException($"找不到權限碼 '{permission}'——請先執行 dotnet ef database update --context CharityDbContext。");
            }
        }

        return code;
    }

    public static string IssueToken(Guid adminUserId, string username, bool isSuperAdmin)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [CharityTokenService.ConfigKey] = TestJwtKey })
            .Build();
        return new CharityTokenService(configuration).IssueAccessToken(adminUserId, username, isSuperAdmin).Token;
    }

    public HttpClient CreateClientFor(CharityTestAdmin admin)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.Token);
        return client;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 直接操作資料庫（arrange／assert 用）
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<T?> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)Convert.ChangeType(result, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    public async Task<int> ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>依標記清掉本 fixture 建立的所有測試資料（見類別說明），絕不碰種子資料。順序依外鍵相依。</summary>
    public async Task CleanupAsync()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            return;
        }

        const string sql = """
            DELETE FROM audit_logs WHERE admin_user_id IN (SELECT id FROM admin_users WHERE username LIKE N'ct-%@charity-test.invalid');
            DELETE FROM email_logs WHERE recipient_email LIKE N'%@charity-test.invalid';
            DELETE FROM reconciliation_discrepancies WHERE reconciliation_run_id IN (SELECT id FROM reconciliation_runs WHERE source LIKE N'ct-%');
            DELETE FROM reconciliation_runs WHERE source LIKE N'ct-%';
            DELETE FROM reconciliation_discrepancies WHERE donation_id IN (SELECT id FROM donations WHERE donor_email LIKE N'%@charity-test.invalid');
            DELETE FROM settlement_lines WHERE donation_id IN (SELECT id FROM donations WHERE donor_email LIKE N'%@charity-test.invalid');
            DELETE FROM settlements WHERE id NOT IN (SELECT settlement_id FROM settlement_lines)
                AND (payee_id IN (SELECT donation_store_id FROM donation_stores_i18n WHERE name LIKE N'CT店家%')
                     OR payee_id IN (SELECT id FROM donation_projects WHERE project_slug LIKE N'ct-%'));
            DELETE FROM donation_payments WHERE donation_id IN (SELECT id FROM donations WHERE donor_email LIKE N'%@charity-test.invalid');
            DELETE FROM donation_invoices WHERE donation_id IN (SELECT id FROM donations WHERE donor_email LIKE N'%@charity-test.invalid');
            DELETE FROM donations WHERE donor_email LIKE N'%@charity-test.invalid';
            DECLARE @orphanProjects TABLE (id uniqueidentifier);
            INSERT INTO @orphanProjects SELECT id FROM donation_projects
                WHERE project_slug NOT LIKE N'ct-%'
                  AND (created_by IN (SELECT id FROM admin_users WHERE username LIKE N'ct-%@charity-test.invalid')
                       OR updated_by IN (SELECT id FROM admin_users WHERE username LIKE N'ct-%@charity-test.invalid'));
            DELETE FROM donation_amount_options WHERE donation_project_id IN (SELECT id FROM @orphanProjects);
            DELETE FROM donation_projects_i18n WHERE donation_project_id IN (SELECT id FROM @orphanProjects);
            DELETE FROM donation_projects WHERE id IN (SELECT id FROM @orphanProjects);
            DELETE FROM donation_amount_options WHERE donation_project_id IN (SELECT id FROM donation_projects WHERE project_slug LIKE N'ct-%');
            DELETE FROM donation_projects WHERE project_slug LIKE N'ct-%';
            DECLARE @orphanStores TABLE (id uniqueidentifier);
            INSERT INTO @orphanStores SELECT id FROM donation_stores
                WHERE created_by IN (SELECT id FROM admin_users WHERE username LIKE N'ct-%@charity-test.invalid')
                   OR updated_by IN (SELECT id FROM admin_users WHERE username LIKE N'ct-%@charity-test.invalid');
            DELETE FROM donation_stores_i18n WHERE donation_store_id IN (SELECT id FROM @orphanStores);
            DELETE FROM donation_stores WHERE id IN (SELECT id FROM @orphanStores);
            DELETE FROM donation_stores WHERE id IN (SELECT donation_store_id FROM donation_stores_i18n WHERE name LIKE N'CT店家%');
            DELETE FROM admin_user_roles WHERE admin_role_id IN (SELECT id FROM admin_roles WHERE code LIKE N'ctrole[_]%');
            DELETE FROM role_permissions WHERE admin_role_id IN (SELECT id FROM admin_roles WHERE code LIKE N'ctrole[_]%');
            DELETE FROM admin_roles WHERE code LIKE N'ctrole[_]%';
            UPDATE admin_roles SET created_by = NULL WHERE created_by IN (SELECT id FROM admin_users WHERE username LIKE N'ct-%@charity-test.invalid');
            UPDATE admin_roles SET updated_by = NULL WHERE updated_by IN (SELECT id FROM admin_users WHERE username LIKE N'ct-%@charity-test.invalid');
            DELETE FROM admin_users WHERE username LIKE N'ct-%@charity-test.invalid';
            DELETE FROM role_permissions WHERE admin_role_id IN (SELECT id FROM admin_roles WHERE code LIKE N'ct-role-%');
            DELETE FROM admin_roles WHERE code LIKE N'ct-role-%';
            """;
        await ExecuteAsync(sql);
    }
}

public sealed record CharityTestAdmin(Guid Id, string Username, bool IsSuperAdmin, string Token);

/// <summary>測試用的記憶體版圖片儲存：沿用正式的 <see cref="ImageProcessor"/> 驗證與轉檔（壞圖一樣會被擋下），只是不寫 Blob。</summary>
public sealed class InMemoryCharityImageStorage : ICharityImageStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    public IReadOnlyCollection<string> Keys => _objects.Keys.ToList();

    public void Reset() => _objects.Clear();

    public string? Resolve(string? objectKey) => string.IsNullOrWhiteSpace(objectKey) ? null : $"https://img.charity-test.invalid/{objectKey}";

    public Task<UploadedImageInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
    {
        var processed = ImageProcessor.Process(rawBytes);
        var key = $"{objectKeyPrefix}/{Guid.NewGuid():N}{ImageObjectKey.Extension}";
        _objects[key] = processed.MainWebPBytes;
        return Task.FromResult(new UploadedImageInfo(key, processed.MainWidth, processed.MainHeight, processed.MainWebPBytes.LongLength));
    }

    public Task DeleteAsync(string? mainObjectKey, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(mainObjectKey))
        {
            _objects.TryRemove(mainObjectKey, out _);
        }

        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class CharityCollection : ICollectionFixture<CharityApiFixture>
{
    public const string Name = "charity-api";
}

/// <summary>專門驗證「額度用盡後回 429」的 fixture：三組額度都壓到很小。獨立的測試主機、獨立的限流計數，不影響其他測試。</summary>
public sealed class CharityRateLimitApiFixture : CharityApiFixture
{
    public const int Permits = 3;
    protected override string WritePermits => Permits.ToString();
    protected override string ReadPermits => Permits.ToString();
    protected override string LoginPermits => Permits.ToString();
}

[CollectionDefinition(Name)]
public sealed class CharityRateLimitCollection : ICollectionFixture<CharityRateLimitApiFixture>
{
    public const string Name = "charity-api-rate-limit";
}

/// <summary>
/// 專門驗證徵信名單與成果回顧的公開讀取限流：獨立的測試主機（獨立的限流計數）。不與 <see cref="CharityRateLimitApiFixture"/> 共用——
/// 測試主機的 <c>RemoteIpAddress</c> 是空的，所有請求落在同一個計數桶，把「公開讀取」額度用盡會害同一個主機上的其他限流測試失敗。
/// </summary>
public sealed class CharityRecognitionRateLimitApiFixture : CharityApiFixture
{
    public const int Permits = 3;
    protected override string ReadPermits => Permits.ToString();
}

[CollectionDefinition(Name)]
public sealed class CharityRecognitionRateLimitCollection : ICollectionFixture<CharityRecognitionRateLimitApiFixture>
{
    public const string Name = "charity-api-rate-limit-recognition";
}

/// <summary>未設定慈善連線字串時的測試主機（Production 環境，不讀 appsettings.Development.json）：整個慈善平台不應存在。</summary>
public sealed class CharityDisabledApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var clubConnection = await TestDatabaseGuard.ResolveAndVerifyAsync();
        Environment.SetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING", clubConnection);
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CLUB", TestJwtSigningKey.Value);
        Environment.SetEnvironmentVariable("CHARITY_SQL_CONNECTION_STRING", null);
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CHARITY", null);
        // Production：不載入 appsettings.Development.json（那份本機檔案可能帶有慈善連線字串，會讓慈善被啟用）。
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        // Production 啟動檢查要求金鑰環目錄存在且可寫（E-109）：給暫存目錄，不放寬檢查。
        _keysDir = Directory.CreateTempSubdirectory("tcrfc-dp-keys-").FullName;
        Environment.SetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH", _keysDir);
        Environment.SetEnvironmentVariable("REDIS_HOST", null);
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", null);
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH", null);
        await base.DisposeAsync();
        if (_keysDir is not null)
        {
            try { Directory.Delete(_keysDir, true); } catch (IOException) { }
        }
    }

    private string? _keysDir;
}

[CollectionDefinition(Name)]
public sealed class CharityDisabledCollection : ICollectionFixture<CharityDisabledApiFixture>
{
    public const string Name = "charity-api-disabled";
}
