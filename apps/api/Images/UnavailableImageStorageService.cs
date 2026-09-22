namespace Tcrfc.Api.Images;

/// <summary>
/// <c>AZURE_BLOB_CONNECTION_STRING</c> 未設定時注入的替身。⚠️ 這不是「no-op 快取」那種
/// 可以安靜降級的情境——沒有物件儲存就不可能真的處理圖片上傳，唯一合理的行為是在真的被呼叫時
/// 丟出一個訊息清楚的例外，而不是在應用程式啟動階段就讓整個服務無法開機（多數環境／測試
/// 完全不需要這個功能，見 <c>Program.cs</c> 的註解）。
/// </summary>
public sealed class UnavailableImageStorageService : IImageStorageService
{
    public Task<UploadedImageInfo> UploadAsync(byte[] rawBytes, string objectKeyPrefix, CancellationToken cancellationToken)
        => throw new InvalidOperationException(
            "圖片上傳功能尚未設定物件儲存（AZURE_BLOB_CONNECTION_STRING 未設定）。" +
            "本機開發請參考 apps/api/README.md「本機開發：Azurite」。");

    public Task DeleteAsync(string? mainObjectKey, CancellationToken cancellationToken)
        // 刪除本來就是 fail-open 的收尾動作（見 IImageStorageService.DeleteAsync 上的說明），
        // 儲存體根本沒設定時同樣不該讓呼叫端炸掉，安靜略過即可。
        => Task.CompletedTask;
}
