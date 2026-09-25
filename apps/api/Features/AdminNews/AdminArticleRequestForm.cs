using System.Text.Json;

namespace Tcrfc.Api.Features.AdminNews;

/// <summary>
/// 🔴🔴🔴 S0-8 修正（規劃書 §4.0／第 53 行「選檔不上傳、儲存才上傳」）：建立／更新文章共用的
/// <c>multipart/form-data</c> 解析。固定三個欄位——<c>payload</c>（JSON 文字，型別是
/// <see cref="CreateArticleRequest"/> 或 <see cref="UpdateArticleRequest"/>，camelCase）、可選的
/// <c>file</c>（封面圖片）與可選的 <c>ogImage</c>（S1-12 新增，OG 圖片覆寫，獨立於封面圖片之外）。
/// 取代舊版「先呼叫獨立上傳端點拿 key、再把 key 塞進純 JSON 請求」的兩段式做法——那個做法在
/// 使用者選檔的當下就已經真的把檔案寫進物件儲存，不是等按下「儲存」，違反規劃書明文。
/// </summary>
internal static class AdminArticleRequestForm
{
    private const string PayloadFieldName = "payload";
    private const string FileFieldName = "file";
    private const string OgImageFieldName = "ogImage";

    public static async Task<(T Payload, IFormFile? File, IFormFile? OgImageFile)> ReadAsync<T>(
        HttpRequest request, JsonSerializerOptions jsonOptions, CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            throw new AdminArticleValidationException(
                "請求格式錯誤，需要 multipart/form-data（欄位 payload ＋ 選填的 file／ogImage）。");
        }

        var form = await request.ReadFormAsync(cancellationToken);

        if (!form.TryGetValue(PayloadFieldName, out var payloadValues) || string.IsNullOrWhiteSpace(payloadValues))
        {
            throw new AdminArticleValidationException("缺少 payload 欄位。");
        }

        T payload;
        try
        {
            payload = JsonSerializer.Deserialize<T>(payloadValues.ToString(), jsonOptions)
                ?? throw new AdminArticleValidationException("payload 欄位內容無法解析。");
        }
        catch (JsonException)
        {
            // ⛔ 不把 System.Text.Json 的內部例外訊息吐給呼叫端（跟既有例外處理紀律一致），
            // 涵蓋「不是合法 JSON」與「缺少 required 屬性」兩種情況。
            throw new AdminArticleValidationException("payload 欄位不是合法的 JSON，或缺少必填欄位。");
        }

        var file = form.Files[FileFieldName];
        var ogImageFile = form.Files[OgImageFieldName];
        return (payload, file, ogImageFile);
    }
}
