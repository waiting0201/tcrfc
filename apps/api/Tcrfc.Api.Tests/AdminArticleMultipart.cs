using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 🔴🔴🔴 S0-8 修正（2026-09-22）：建立／更新後台新聞的請求主體從純 JSON 改成
/// <c>multipart/form-data</c>（規劃書 §4.0／第 53 行「選檔不上傳、儲存才上傳」，見
/// apps/api/README.md「圖片上傳共用元件」整節的新契約）。取代原本測試裡到處直接呼叫
/// <c>PostAsJsonAsync</c>／<c>PutAsJsonAsync</c> 的寫法，固定兩個欄位：
/// <c>payload</c>（JSON 文字，camelCase，用跟既有 <see cref="TestJson.WriteOptions"/> 一致的
/// 命名政策）與可選的 <c>file</c>（封面圖片）。
/// </summary>
public static class AdminArticleMultipart
{
    public static MultipartFormDataContent Build(object payload, byte[]? fileBytes = null, string fileName = "cover.png", string fileContentType = "image/png")
    {
        var form = new MultipartFormDataContent();

        var json = JsonSerializer.Serialize(payload, payload.GetType(), TestJson.WriteOptions);
        var payloadContent = new StringContent(json, Encoding.UTF8);
        payloadContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(payloadContent, "payload");

        if (fileBytes is not null)
        {
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(fileContentType);
            form.Add(fileContent, "file", fileName);
        }

        return form;
    }
}
