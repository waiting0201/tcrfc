namespace Tcrfc.Api.Videos;

/// <summary>
/// <c>AZURE_BLOB_CONNECTION_STRING</c> 未設定時注入的替身，逐字比照
/// <c>Images/UnavailableImageStorageService.cs</c> 的既有做法與理由。
/// </summary>
public sealed class UnavailableVideoStorageService : IVideoStorageService
{
    public Task<UploadedVideoInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
        => throw new Tcrfc.Api.Common.FeatureNotConfiguredException("檔案儲存尚未設定", "storage_not_configured");

    public Task DeleteAsync(string? objectKey, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
