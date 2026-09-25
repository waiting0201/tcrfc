namespace Tcrfc.Api.Images;

/// <summary>未設定 <c>AZURE_BLOB_CONNECTION_STRING</c> 時的替身，比照
/// <see cref="UnavailableImageStorageService"/> 的既有慣例——不讓行程無法啟動，呼叫時一律回傳
/// <c>null</c>（沒有網址可以輸出，前台自然不會渲染 <c>og:image</c>），不丟例外。</summary>
public sealed class UnavailableImagePublicUrlResolver : IImagePublicUrlResolver
{
    public string? Resolve(string? objectKey) => null;
}
