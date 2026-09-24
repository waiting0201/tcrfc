using System.Text.Json;

namespace Tcrfc.Api.Features.AdminStaff;

/// <summary>C3 教練與團隊成員建立／更新共用的 <c>multipart/form-data</c> 解析，形狀比照
/// <c>Features/AdminNews/AdminArticleRequestForm.cs</c>：固定兩個欄位——<c>payload</c>（JSON 文字）
/// 與可選的 <c>file</c>（照片，對應 <c>staff.photo_key</c>）。</summary>
internal static class AdminStaffRequestForm
{
    private const string PayloadFieldName = "payload";
    private const string FileFieldName = "file";

    public static async Task<(T Payload, IFormFile? File)> ReadAsync<T>(
        HttpRequest request, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            throw new AdminStaffValidationException("請求格式錯誤，需要 multipart/form-data（欄位 payload ＋ 選填的 file）。");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        if (!form.TryGetValue(PayloadFieldName, out var payloadValues) || string.IsNullOrWhiteSpace(payloadValues))
        {
            throw new AdminStaffValidationException("缺少 payload 欄位。");
        }

        T payload;
        try
        {
            payload = JsonSerializer.Deserialize<T>(payloadValues.ToString(), jsonOptions)
                ?? throw new AdminStaffValidationException("payload 欄位內容無法解析。");
        }
        catch (JsonException)
        {
            throw new AdminStaffValidationException("payload 欄位不是合法的 JSON，或缺少必填欄位。");
        }

        var file = form.Files[FileFieldName];
        return (payload, file);
    }
}
