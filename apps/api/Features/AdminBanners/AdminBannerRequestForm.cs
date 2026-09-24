using System.Text.Json;

namespace Tcrfc.Api.Features.AdminBanners;

/// <summary>
/// 建立／更新首頁輪播共用的 <c>multipart/form-data</c> 解析。固定三個欄位——<c>payload</c>
/// （JSON 文字）、<c>file</c>（輪播圖片／影片模式下的海報格，建立時必填、更新時省略＝維持原圖）
/// 與 <c>video</c>（v3.14 新增，影片檔案，僅 <c>mediaType="video"</c> 時使用，省略＝維持原影片
/// 或本來就是圖片模式）。形狀逐字比照 <c>Features/AdminNews/AdminArticleRequestForm.cs</c>
/// （該檔案是 <c>internal</c>，兩者刻意各自宣告一份而不是抽共用元件，見該檔案上方註解說明的
/// 既有分工慣例）。
/// </summary>
internal static class AdminBannerRequestForm
{
    private const string PayloadFieldName = "payload";
    private const string FileFieldName = "file";
    private const string VideoFieldName = "video";

    public static async Task<(T Payload, IFormFile? File, IFormFile? VideoFile)> ReadAsync<T>(
        HttpRequest request, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            throw new AdminBannerValidationException(
                "請求格式錯誤，需要 multipart/form-data（欄位 payload ＋ 圖片欄位 file，影片模式另加 video）。");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        if (!form.TryGetValue(PayloadFieldName, out var payloadValues) || string.IsNullOrWhiteSpace(payloadValues))
        {
            throw new AdminBannerValidationException("缺少 payload 欄位。");
        }

        T payload;
        try
        {
            payload = JsonSerializer.Deserialize<T>(payloadValues.ToString(), jsonOptions)
                ?? throw new AdminBannerValidationException("payload 欄位內容無法解析。");
        }
        catch (JsonException)
        {
            throw new AdminBannerValidationException("payload 欄位不是合法的 JSON，或缺少必填欄位。");
        }

        var file = form.Files[FileFieldName];
        var videoFile = form.Files[VideoFieldName];
        return (payload, file, videoFile);
    }
}
