using System.Text.Json.Nodes;
using Tcrfc.Api.Images;

namespace Tcrfc.Api.Features.Uploads;

/// <summary>解析區塊圖片欄位時，把「這張圖的新檔案」交給呼叫端（沒有對應檔案回傳 <c>null</c>）。</summary>
public delegate Task<UploadedImageInfo?> BlockImageUploadResolver(string imagePath, CancellationToken cancellationToken);

/// <summary>
/// 區塊化內容（B1 頁面管理、B2 新聞內文）共用的「圖片欄位上傳」元件：把同一次 multipart 請求裡
/// 命名為 <c>file:{區塊索引}:{圖片路徑}</c> 的檔案讀進記憶體、交給 <see cref="IImageStorageService"/> 上傳，
/// 並登記本次已上傳的物件鍵（<see cref="UploadedKeys"/>），失敗時由呼叫端一次補償刪除。
/// 圖片路徑：單張圖片 <c>image</c>；圖集 <c>images:{i}</c>。
/// <para>
/// 🔴 原本是 <c>AdminPagesRepository</c> 內的區域函式，新聞內文需要同一套行為而抽出共用
/// （B1 既有行為不變：檔案欄位命名、物件鍵前綴 <c>{根}/{區塊索引}/{路徑的 ':' 換 '-'}</c>、10 MB 上限、空檔 400）。
/// </para>
/// </summary>
public sealed class BlockImageUploader(
    IImageStorageService images, IFormFileCollection files, string objectKeyRoot, int? minWidth = null)
{
    public const string FilePrefix = "file:";

    /// <summary>本次請求已成功上傳的主檔物件鍵（含後來因其他原因失敗而需要補償刪除的）。</summary>
    public List<string> UploadedKeys { get; } = [];

    /// <summary>依區塊索引與圖片路徑組出多檔命名慣例的欄位名稱。</summary>
    public static string FileFieldName(int blockIndex, string imagePath) => $"{FilePrefix}{blockIndex}:{imagePath}";

    public BlockImageUploadResolver ResolverFor(int blockIndex) => (imagePath, ct) => UploadAsync(blockIndex, imagePath, ct);

    private async Task<UploadedImageInfo?> UploadAsync(int blockIndex, string imagePath, CancellationToken cancellationToken)
    {
        var file = files[FileFieldName(blockIndex, imagePath)];
        if (file is null)
        {
            return null;
        }

        if (file.Length == 0)
        {
            throw new EmptyImageException();
        }

        if (file.Length > ImageUploadOptions.MaxUploadBytes)
        {
            throw new ImageTooLargeException();
        }

        byte[] rawBytes;
        using (var buffer = new MemoryStream())
        {
            await file.CopyToAsync(buffer, cancellationToken);
            rawBytes = buffer.ToArray();
        }

        // 物件鍵前綴含俱樂部代碼／實體 id／區塊索引／圖片路徑，確保「一張圖只屬於一個區塊的一個圖片欄位」。
        var safePath = imagePath.Replace(':', '-');
        var uploaded = await images.UploadAsync(rawBytes, $"{objectKeyRoot}/{blockIndex}/{safePath}", cancellationToken);
        UploadedKeys.Add(uploaded.Key);

        // 寬度下限在上傳（重新編碼）之後以主檔實際寬度判斷；不合格者仍已登記，由呼叫端的補償刪除清掉。
        if (minWidth is { } min && uploaded.Width < min)
        {
            throw new ImageTooNarrowException(min);
        }

        return uploaded;
    }

    /// <summary>補償刪除本次請求上傳的全部物件（請求被取消也要清，E-47）。</summary>
    public async Task RollbackAsync()
    {
        foreach (var key in UploadedKeys)
        {
            await images.DeleteAsync(key, CancellationToken.None);
        }
    }
}

/// <summary>區塊內「圖片欄位物件」（<c>{key,width,height,altZh,altEn}</c> 或 <c>{pendingUpload:true,altZh,altEn}</c>）的共用解析。</summary>
public static class BlockImageFields
{
    /// <summary>建立驗證例外：參數為（訊息, 欄位鍵）。頁面與新聞各自丟自己的 400 例外型別。</summary>
    public delegate Exception FailureFactory(string message, string fieldKey);

    /// <summary>
    /// 就地解析單一圖片欄位：<c>pendingUpload</c> 為真則取新檔案上傳結果寫回 <c>key／width／height</c> 並移除標記；
    /// 否則必須已帶非空白 <c>key</c>（沿用既有圖）。<c>altZh</c> 必填，<c>altEn</c> 缺鍵補 <c>null</c>。
    /// 讀取端附的 <c>url／thumbUrl</c> 一律丟掉（不得寫回資料庫）。
    /// </summary>
    public static async Task ResolveAsync(
        JsonObject imageObj, int blockIndex, string uploadPath, string fieldPath, string fieldRoot,
        BlockImageUploadResolver resolveUpload, FailureFactory fail, CancellationToken cancellationToken)
    {
        string Key(string? path) => FieldKeyOf(fieldRoot, blockIndex, path);

        imageObj.Remove("url");
        imageObj.Remove("thumbUrl");

        var isPending = imageObj["pendingUpload"] is JsonValue pendingValue
            && pendingValue.TryGetValue<bool>(out var pendingBool) && pendingBool;

        if (isPending)
        {
            var uploaded = await resolveUpload(uploadPath, cancellationToken)
                ?? throw fail($"第 {blockIndex + 1} 個區塊有圖片還沒有選擇檔案，請重新選擇圖片。", Key(fieldPath));

            imageObj.Remove("pendingUpload");
            imageObj["key"] = uploaded.Key;
            imageObj["width"] = uploaded.Width;
            imageObj["height"] = uploaded.Height;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(GetString(imageObj, "key")))
            {
                throw fail($"第 {blockIndex + 1} 個區塊的圖片欄位還沒有圖片，請選擇要上傳的圖片。", Key(fieldPath));
            }
        }

        if (string.IsNullOrWhiteSpace(GetString(imageObj, "altZh")))
        {
            throw fail($"第 {blockIndex + 1} 個區塊的圖片替代文字（中文）為必填。", Key(fieldPath + ".altZh"));
        }

        // 英文可空但欄位必須存在（CLAUDE.md 全域規定 4）。
        if (imageObj["altEn"] is null)
        {
            imageObj["altEn"] = null;
        }
    }

    /// <summary>讀取輸出用：在圖片物件旁補唯讀 <c>url</c>／<c>thumbUrl</c>（只存在於輸出）。</summary>
    public static void AttachUrls(JsonObject imageObj, Func<string?, string?> resolveUrl, Func<string?, string?> resolveThumb)
    {
        var key = GetString(imageObj, "key");
        imageObj["url"] = resolveUrl(key);
        imageObj["thumbUrl"] = resolveThumb(key);
    }

    public static string? GetString(JsonObject obj, string property)
        => obj[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static string FieldKeyOf(string root, int index, string? path) => Common.FieldKey.Item(root, index, path);
}
