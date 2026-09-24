namespace Tcrfc.Api.Videos;

/// <summary>
/// Hero 輪播影片上傳（主站規劃書 §4.2 B3「圖／影片」，v3.14）的數字常數，唯一來源——
/// 不得在別處重複寫死同一組數字。**執行層決定，規劃書不記技術選型**（CLAUDE.md 全域規定），
/// 完整理由見 docs/17-deployment.md §6「Hero 輪播影片上傳」。
///
/// 🔴 **只收 MP4（H.264／AAC），伺服器端不轉碼**——跟圖片上傳（伺服器端一律重新編碼為 WebP）
/// 刻意不同：影片轉碼需要 ffmpeg 這類額外相依與可觀的 CPU／時間成本，這套系統跑在單一 Azure VM
/// （docs/17 §1），沒有另外的轉碼佇列或算力可以吸收，勉強做只會拖垮同一台機器上的 API／DB 容器。
/// 不轉碼的代價由「收窄格式與大小上限、只信任瀏覽器原生播放得動的格式」來承擔。
/// </summary>
public static class VideoUploadOptions
{
    /// <summary>單檔上限 50 MB（docs/17-deployment.md §6：Hero 輪播用短片，不是完整賽事錄影，
    /// 50 MB 大約是 1080p、30 秒上下、中等位元率的 H.264 影片，一般社群媒體剪輯工具的匯出品質
    /// 落在這個範圍內）。</summary>
    public const long MaxUploadBytes = 50 * 1024 * 1024;

    /// <summary>唯一接受的容器格式副檔名。</summary>
    public const string Extension = ".mp4";

    /// <summary>固定內容類型——本服務只接受 MP4，不像圖片有多種輸出格式需要依內容決定。</summary>
    public const string ContentType = "video/mp4";
}
