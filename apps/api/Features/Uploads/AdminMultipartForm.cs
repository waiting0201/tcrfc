using System.Text.Json;
using Tcrfc.Api.Common;

namespace Tcrfc.Api.Features.Uploads;

/// <summary>
/// 後台「JSON ＋ 圖片／檔案欄位」建立與更新端點共用的 <c>multipart/form-data</c> 解析（E1a 起新增模組共用），
/// 契約與 <c>Features/AdminNews/AdminArticleRequestForm</c> 相同：固定欄位 <c>payload</c>（JSON 文字，camelCase），
/// 其餘欄位是各端點自己宣告的檔案欄位名稱（例如 <c>logoDark</c>、<c>cover</c>、<c>file</c>）。
/// 錯誤一律丟 <see cref="AdminValidationException"/>（400）。
/// </summary>
public static class AdminMultipartForm
{
    private const string PayloadFieldName = "payload";

    public static async Task<(T Payload, IFormCollection Form)> ReadAsync<T>(
        HttpRequest request, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            throw new AdminValidationException("請求格式錯誤，需要 multipart/form-data（欄位 payload ＋ 選填的檔案欄位）。");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        if (!form.TryGetValue(PayloadFieldName, out var payloadValues) || string.IsNullOrWhiteSpace(payloadValues))
        {
            throw new AdminValidationException("缺少 payload 欄位。");
        }

        try
        {
            var payload = JsonSerializer.Deserialize<T>(payloadValues.ToString(), jsonOptions)
                ?? throw new AdminValidationException("payload 欄位內容無法解析。");
            return (payload, form);
        }
        catch (JsonException)
        {
            throw new AdminValidationException("payload 欄位不是合法的 JSON，或缺少必填欄位。");
        }
    }

    /// <summary>只有單一檔案欄位、沒有 payload 的上傳端點（圖集新增圖片）：讀出名為 <paramref name="fieldName"/> 的檔案。</summary>
    public static async Task<IFormFile> ReadFileAsync(HttpRequest request, string fieldName, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            throw new AdminValidationException($"請求格式錯誤，需要 multipart/form-data（檔案欄位 {fieldName}）。");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        return form.Files[fieldName] ?? throw new AdminValidationException("請選擇要上傳的檔案。");
    }
}
