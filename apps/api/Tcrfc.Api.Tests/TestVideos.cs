namespace Tcrfc.Api.Tests;

/// <summary>
/// 影片上傳測試（v3.14，Hero 輪播）用的檔案產生器。逐字比照 <see cref="TestImages"/> 的既有原則：
/// 全部在記憶體現產，不讀外部檔案，在任何機器上都能重現。
///
/// ⚠️ <see cref="SmallValidMp4"/> **不是一支真的可播放的影片**——只組出合法的 <c>ftyp</c> box
/// 表頭讓 <c>Tcrfc.Api.Videos.VideoValidator</c> 的 magic bytes 檢查通過。這對應
/// <c>VideoProcessingExceptions.cs</c> 檔頭明講的驗證邊界：伺服器端只驗證容器格式，
/// 不解封裝、不驗證內部視訊／音訊軌道是否真的是 H.264／AAC（那需要額外的媒體處理函式庫，
/// 本服務刻意不引入，見 docs/17-deployment.md §6）。測試因此也只能驗證到「容器格式檢查生效」
/// 這個邊界，驗證不到「非 H.264／AAC 編碼的 MP4 容器仍會被接受」這件事——這是已知的測試覆蓋
/// 邊界，不是遺漏。
/// </summary>
public static class TestVideos
{
    /// <summary>只含合法 <c>ftyp</c> box 表頭＋任意填充位元組的最小 MP4 樣本。</summary>
    public static byte[] SmallValidMp4()
    {
        var box = new List<byte>
        {
            0, 0, 0, 20, // box size（big-endian）：4（大小）+4（"ftyp"）+4（major brand）+4（minor version）+4（compatible brand）
        };
        box.AddRange("ftyp"u8.ToArray());
        box.AddRange("isom"u8.ToArray()); // major brand
        box.AddRange(new byte[] { 0, 0, 2, 0 }); // minor version
        box.AddRange("isom"u8.ToArray()); // compatible brand

        // 補一段任意填充位元組，模擬影片本體（驗證邏輯不會讀到這裡，純粹讓檔案看起來不只有表頭）。
        var padding = new byte[256];
        box.AddRange(padding);
        return box.ToArray();
    }

    /// <summary>副檔名假裝是 MP4，內容其實是純文字——驗證伺服器端以 <c>ftyp</c> 表頭判格式，不看副檔名。</summary>
    public static byte[] FakeVideoBytes()
        => "this is not actually an mp4, just plain text pretending to be one"u8.ToArray();

    /// <summary>超過 <see cref="Tcrfc.Api.Videos.VideoUploadOptions.MaxUploadBytes"/> 一個位元組的
    /// 全零位元組陣列——檔案大小檢查發生在 magic bytes 驗證之前，回應的錯誤訊息只跟大小有關，
    /// 不需要是真正的（哪怕是最小的）合法 MP4。</summary>
    public static byte[] OversizedBytes()
        => new byte[Tcrfc.Api.Videos.VideoUploadOptions.MaxUploadBytes + 1];
}
