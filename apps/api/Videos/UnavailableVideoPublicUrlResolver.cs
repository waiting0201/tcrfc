namespace Tcrfc.Api.Videos;

/// <summary>未設定 <c>AZURE_BLOB_CONNECTION_STRING</c> 時的替身，比照
/// <see cref="Tcrfc.Api.Images.UnavailableImagePublicUrlResolver"/> 的既有慣例——不讓行程無法
/// 啟動，呼叫時一律回傳 <c>null</c>（沒有網址可以輸出），不丟例外。</summary>
public sealed class UnavailableVideoPublicUrlResolver : IVideoPublicUrlResolver
{
    public string? Resolve(string? objectKey) => null;
}
