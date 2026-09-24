using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Tcrfc.Api.Tests;

/// <summary>
/// B1 頁面建立／更新的 <c>multipart/form-data</c> 請求建構工具，形狀比照
/// <see cref="AdminArticleMultipart"/>，差異：檔案欄位可以有多個、依區塊索引與圖片路徑命名
/// （見 <c>Features/AdminPages/AdminPageRequestForm.cs</c>）。
/// </summary>
public static class AdminPageMultipart
{
    public static MultipartFormDataContent Build(object payload, IReadOnlyDictionary<string, byte[]>? files = null)
    {
        var form = new MultipartFormDataContent();

        var json = JsonSerializer.Serialize(payload, payload.GetType(), TestJson.WriteOptions);
        var payloadContent = new StringContent(json, Encoding.UTF8);
        payloadContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(payloadContent, "payload");

        if (files is not null)
        {
            foreach (var (fieldName, bytes) in files)
            {
                var fileContent = new ByteArrayContent(bytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                form.Add(fileContent, fieldName, "upload.png");
            }
        }

        return form;
    }
}
