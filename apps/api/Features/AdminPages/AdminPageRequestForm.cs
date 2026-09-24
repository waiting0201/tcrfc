using System.Text.Json;

namespace Tcrfc.Api.Features.AdminPages;

/// <summary>
/// B1 頁面建立／更新共用的 <c>multipart/form-data</c> 解析。形狀比照
/// <c>Features/AdminNews/AdminArticleRequestForm.cs</c>，但檔案欄位從固定的單一 <c>file</c>
/// 改成**依區塊與圖片路徑命名的多檔**——頁面區塊可能同時有多張待上傳的新圖片（圖文左右各 1 張、
/// 圖片藝廊 N 張，見 <see cref="PageBlockContentProcessor"/>），不像新聞封面只有一張固定圖。
///
/// 固定欄位：<c>payload</c>（JSON 文字，型別是 <see cref="CreatePageRequest"/> 或
/// <see cref="UpdatePageRequest"/>，camelCase）；其餘檔案欄位命名為
/// <c>file:{區塊索引}:{圖片路徑}</c>（例：<c>file:0:image</c>、<c>file:2:images:1</c>）。
/// </summary>
internal static class AdminPageRequestForm
{
    private const string PayloadFieldName = "payload";
    private const string FilePrefix = "file:";

    public static async Task<(T Payload, IFormFileCollection Files)> ReadAsync<T>(
        HttpRequest request, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            throw new AdminPageValidationException(
                "請求格式錯誤，需要 multipart/form-data（欄位 payload ＋ 依需要命名為 file:{區塊索引}:{圖片路徑} 的檔案）。");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        if (!form.TryGetValue(PayloadFieldName, out var payloadValues) || string.IsNullOrWhiteSpace(payloadValues))
        {
            throw new AdminPageValidationException("缺少 payload 欄位。");
        }

        T payload;
        try
        {
            payload = JsonSerializer.Deserialize<T>(payloadValues.ToString(), jsonOptions)
                ?? throw new AdminPageValidationException("payload 欄位內容無法解析。");
        }
        catch (JsonException)
        {
            throw new AdminPageValidationException("payload 欄位不是合法的 JSON，或缺少必填欄位。");
        }

        return (payload, form.Files);
    }

    /// <summary>依區塊索引與圖片路徑組出多檔命名慣例的欄位名稱，供 <see cref="PageBlockContentProcessor"/>
    /// 的 <c>resolveUpload</c> 委派在 <see cref="IFormFileCollection"/> 裡查找對應檔案時使用。</summary>
    public static string FileFieldName(int blockIndex, string imagePath) => $"{FilePrefix}{blockIndex}:{imagePath}";
}
