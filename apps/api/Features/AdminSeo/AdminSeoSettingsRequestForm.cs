using System.Text.Json;

namespace Tcrfc.Api.Features.AdminSeo;

/// <summary>
/// 更新全站 SEO 設定的 <c>multipart/form-data</c> 解析。固定兩個欄位——<c>payload</c>
/// （JSON 文字）與 <c>ogImage</c>（全站預設 OG 圖片，省略＝維持原圖或本來就沒有圖片）。
/// 形狀比照 <c>Features/AdminBanners/AdminBannerRequestForm.cs</c>（該檔案是 <c>internal</c>，
/// 兩者刻意各自宣告一份，理由見該檔案上方註解說明的既有分工慣例）。
/// </summary>
internal static class AdminSeoSettingsRequestForm
{
    private const string PayloadFieldName = "payload";
    private const string OgImageFieldName = "ogImage";

    public static async Task<(UpdateSeoSettingsRequest Payload, IFormFile? OgImageFile)> ReadAsync(
        HttpRequest request, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            throw new AdminSeoValidationException(
                "請求格式錯誤，需要 multipart/form-data（欄位 payload，選填 ogImage 圖片檔案）。");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        if (!form.TryGetValue(PayloadFieldName, out var payloadValues) || string.IsNullOrWhiteSpace(payloadValues))
        {
            throw new AdminSeoValidationException("缺少 payload 欄位。");
        }

        UpdateSeoSettingsRequest payload;
        try
        {
            payload = JsonSerializer.Deserialize<UpdateSeoSettingsRequest>(payloadValues.ToString(), jsonOptions)
                ?? throw new AdminSeoValidationException("payload 欄位內容無法解析。");
        }
        catch (JsonException)
        {
            throw new AdminSeoValidationException("payload 欄位不是合法的 JSON，或缺少必填欄位。");
        }

        var ogImageFile = form.Files[OgImageFieldName];
        return (payload, ogImageFile);
    }
}
