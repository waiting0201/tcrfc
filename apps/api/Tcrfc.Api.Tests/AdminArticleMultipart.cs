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
    /// <summary><paramref name="videoBytes"/>（v3.14 新增，選填）：Hero 輪播影片模式的第二個檔案
    /// 欄位（<c>video</c>），逐字比照 <paramref name="fileBytes"/>／<c>file</c> 欄位的既有寫法。
    /// 其餘呼叫端（AdminNews／AdminPages／AdminTeams…）不受影響，省略即維持原行為。</summary>
    public static MultipartFormDataContent Build(
        object payload, byte[]? fileBytes = null, string fileName = "cover.png", string fileContentType = "image/png",
        byte[]? videoBytes = null, string videoFileName = "banner.mp4", string videoContentType = "video/mp4")
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

        if (videoBytes is not null)
        {
            var videoContent = new ByteArrayContent(videoBytes);
            videoContent.Headers.ContentType = new MediaTypeHeaderValue(videoContentType);
            form.Add(videoContent, "video", videoFileName);
        }

        return form;
    }
}
