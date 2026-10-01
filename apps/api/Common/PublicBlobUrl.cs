using Azure.Storage.Blobs;

namespace Tcrfc.Api.Common;

/// <summary>
/// 「公開網址基底」設定值（例如 <c>https://img-stg.tcrfc.tw</c>），讓公開讀取網址指到 Cloudflare CDN 子網域，
/// 而上傳／刪除仍走原連線字串（兩條路徑刻意分開，避免上傳繞 Cloudflare 受其大小與逾時限制）。
/// <c>Value</c> 為 <c>null</c> 代表未設定，行為完全維持 <see cref="BlobContainerClient.Uri"/>（本機 Azurite 不受影響）。
/// 俱樂部（<c>AZURE_BLOB_PUBLIC_BASE_URL</c>）與慈善（<c>AZURE_BLOB_PUBLIC_BASE_URL_CHARITY</c>）各自一個實例、
/// 各自讀自己的設定來源，不得共用（docs/17 §5 補償措施）。
/// </summary>
public sealed record PublicBlobBaseUrl(string? Value)
{
    public static readonly PublicBlobBaseUrl None = new((string?)null);

    /// <summary>
    /// 讀取並驗證設定。空白視為未設定；非空時必須是絕對 https URL（<paramref name="allowHttp"/> 為 true，
    /// 即 Development，才放行 http），且不得含帳密、query、fragment。格式錯誤直接丟例外，讓啟動失敗，
    /// 避免公開網址靜默指到壞掉的位址。
    /// </summary>
    public static PublicBlobBaseUrl FromConfiguration(IConfiguration configuration, string key, bool allowHttp)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return None;
        }

        var trimmed = raw.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && !(allowHttp && uri.Scheme == Uri.UriSchemeHttp))
            || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                $"{key} 必須是絕對 {(allowHttp ? "http(s)" : "https")} 網址（例如 https://img.example.com），且不得含帳密、query 或 fragment。");
        }

        // 去掉尾斜線，組網址時再統一補一個；保留可能的路徑前綴。
        return new PublicBlobBaseUrl(uri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
    }

    /// <summary>未設定回退 <see cref="BlobContainerClient.GetBlobClient"/> 的 Uri；有設定組成 <c>{base}/{container}/{blobKey}</c>。</summary>
    public string Build(BlobContainerClient container, string objectKey)
    {
        if (Value is null)
        {
            return container.GetBlobClient(objectKey).Uri.ToString();
        }

        // 逐段 EscapeDataString、保留 '/'，與 Azure SDK 組 blob 路徑時的編碼一致（測試對照 SDK 輸出）。
        var encodedKey = string.Join('/', objectKey.Split('/').Select(Uri.EscapeDataString));
        return $"{Value}/{Uri.EscapeDataString(container.Name)}/{encodedKey}";
    }
}
