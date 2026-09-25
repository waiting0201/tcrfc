using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 圖片上傳共用元件（S0-8）的整合測試 fixture：真的啟動一個 <c>azurite-blob</c> 子行程
/// （沿用本專案「不 mock 資料庫／不 mock 快取」的既有紀律，這裡是「不 mock 物件儲存」）。
/// 圖片上傳走 <c>Features/AdminNews</c> 的建立／更新端點，一併需要真實授權（見
/// <see cref="AdminWriteApiFixture"/>），本 fixture 沿用同樣的登入前提。
///
/// ⚠️ 用 <c>azurite-blob</c>（只跑 Blob 服務）而不是完整的 <c>azurite</c>（含 Queue／Table），
/// 本專案目前只需要 Blob。連線字串用 Azurite 的公開預設帳號（<c>devstoreaccount1</c>），
/// 這組帳號金鑰是 Azurite 專案本身公開文件的固定值，不是任何真正的密鑰，可安心寫進原始碼。
/// </summary>
public sealed class AdminWriteAzuriteEnabledApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string AccountName = "devstoreaccount1";
    private const string AccountKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private const string ContainerName = "images-test";

    private Process? _azuriteProcess;
    private DirectoryInfo? _dataDirectory;

    /// <summary>給測試直接檢查上傳結果用的容器用戶端——跟應用程式自己那條連線分開，純粹用來斷言。</summary>
    public BlobContainerClient InspectorContainer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = await TestDatabaseGuard.ResolveAndVerifyAsync();

        var azuriteExecutablePath = ResolveAzuriteExecutablePath();
        var port = GetUnusedLocalPort();
        _dataDirectory = Directory.CreateTempSubdirectory("tcrfc-azurite-test-");

        _azuriteProcess = Process.Start(new ProcessStartInfo
        {
            FileName = azuriteExecutablePath,
            ArgumentList =
            {
                "--blobHost", "127.0.0.1",
                "--blobPort", port.ToString(),
                "--location", _dataDirectory.FullName,
                "--silent",
                // 本機 Azure.Storage.Blobs SDK 版本可能比 Azurite 認得的 API 版本新
                // （本機驗證時實際踩過，見 apps/api/README.md「本機開發：Azurite」）。
                "--skipApiVersionCheck",
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException($"無法啟動 {azuriteExecutablePath}。");

        await WaitForAzuriteReadyAsync(port);

        var connectionStringForBlob =
            $"DefaultEndpointsProtocol=http;AccountName={AccountName};AccountKey={AccountKey};" +
            $"BlobEndpoint=http://127.0.0.1:{port}/{AccountName};";

        Environment.SetEnvironmentVariable("CLUB_SQL_CONNECTION_STRING", connectionString);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("JWT_SIGNING_KEY_CLUB", TestJwtSigningKey.Value);
        Environment.SetEnvironmentVariable("REDIS_HOST", null);
        Environment.SetEnvironmentVariable("REDIS_PASSWORD", null);
        Environment.SetEnvironmentVariable("AZURE_BLOB_CONNECTION_STRING", connectionStringForBlob);
        Environment.SetEnvironmentVariable("AZURE_BLOB_CONTAINER_IMAGES", ContainerName);

        InspectorContainer = new BlobContainerClient(connectionStringForBlob, ContainerName);

        // 立刻建立測試主機，確保上面設定的環境變數在 Program.cs 執行的當下就是這個 fixture 要的值
        // （原因見 ApiFixture／RedisEnabledApiFixture 的同一段註解）。
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        Environment.SetEnvironmentVariable("AZURE_BLOB_CONNECTION_STRING", null);
        Environment.SetEnvironmentVariable("AZURE_BLOB_CONTAINER_IMAGES", null);

        if (_azuriteProcess is { HasExited: false })
        {
            _azuriteProcess.Kill(entireProcessTree: true);
            await _azuriteProcess.WaitForExitAsync();
        }

        _azuriteProcess?.Dispose();

        if (_dataDirectory is { Exists: true })
        {
            try
            {
                _dataDirectory.Delete(recursive: true);
            }
            catch
            {
                // 收尾動作，不讓暫存目錄清不掉掩蓋測試本身的結果。
            }
        }
    }

    private static string ResolveAzuriteExecutablePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("AZURITE_EXECUTABLE_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        string[] candidates =
        [
            "/usr/local/bin/azurite-blob", // npm -g 安裝在 macOS／一般 Linux 的預設位置
            "/opt/homebrew/bin/azurite-blob", // Apple Silicon 若透過其他方式安裝
        ];

        var found = candidates.FirstOrDefault(File.Exists);
        if (found is not null)
        {
            return found;
        }

        throw new InvalidOperationException(
            "找不到 azurite-blob 執行檔——這組測試需要一個真正在跑的 Azurite（不 mock）。"
            + "請先 `npm install -g azurite`，或設定環境變數 AZURITE_EXECUTABLE_PATH 指到 azurite-blob 執行檔。");
    }

    private static async Task WaitForAzuriteReadyAsync(int port)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                return; // 能建立 TCP 連線就視為 azurite-blob 已經在聽。
            }
            catch
            {
                await Task.Delay(150);
            }
        }

        throw new InvalidOperationException($"azurite-blob 在 10 秒內沒有開始監聽 127.0.0.1:{port}。");
    }

    private static int GetUnusedLocalPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
