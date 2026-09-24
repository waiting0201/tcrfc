namespace Tcrfc.Api.Videos;

/// <summary>
/// <c>AZURE_BLOB_CONNECTION_STRING</c> 未設定時注入的替身，逐字比照
/// <c>Images/UnavailableImageStorageService.cs</c> 的既有做法與理由。
/// </summary>
public sealed class UnavailableVideoStorageService : IVideoStorageService
{
    public Task<UploadedVideoInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
        => throw new InvalidOperationException(
            "影片上傳功能尚未設定物件儲存（AZURE_BLOB_CONNECTION_STRING 未設定）。" +
            "本機開發請參考 apps/api/README.md「本機開發：Azurite」。");

    public Task DeleteAsync(string? objectKey, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
