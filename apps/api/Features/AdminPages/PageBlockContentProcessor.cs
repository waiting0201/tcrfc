using System.Text.Json.Nodes;
using Tcrfc.Api.Images;

namespace Tcrfc.Api.Features.AdminPages;

/// <summary>
/// B1 頁面管理的區塊內容驗證與圖片欄位解析。<c>page_blocks.content</c> 是「只存不查」的 JSON
/// （docs/12 §1.4 第 2 項），本類別是**寫入路徑**唯一做結構檢查的地方——資料庫本身沒有任何約束
/// 能保護這個欄位的形狀。
///
/// 🔴🔴🔴 **雙語怎麼落在這個欄位裡**：v3.5「後台圖片改為欄位直傳」把 <c>page_blocks.content</c>
/// 定案為區塊主表自己的欄位（不是側表），<c>db/club-schema.sql</c> 檔頭明確拒絕另建
/// <c>page_blocks_i18n</c>（「主表已放的欄位優先」）。**這代表雙語不是靠側表切分，而是每一個
/// 使用者看得到的文字欄位本身，在這份 JSON 裡就是一個 <c>{"zh": "...", "en": null|"..."}</c> 物件**
/// （CLAUDE.md 全域規定 4：英文可空但欄位必須存在，落在 JSON 層級就是「這個鍵一定存在，值可以是
/// <c>null</c>」）。不是每個欄位都要雙語：**網址、識別碼、日期、數值這類不是「語言內容」的欄位維持
/// 單一純值**（例：<c>videoId</c>、<c>buttonUrl</c>、<c>fileUrl</c>、時間軸的 <c>date</c>）。
///
/// 🔴 **圖片欄位的「選檔不上傳、儲存才上傳」怎麼落在 JSON 裡**：後台圖片上傳通則
/// （主站規劃書 §4.0）原本設計給「一張圖＝一個獨立資料表欄位」（例：<c>articles.cover_key</c>），
/// 但頁面區塊的圖片活在 JSON 裡、同一次請求可能同時有多個區塊、每個區塊可能有 0～N 張圖
/// （圖文左右 1 張、圖片藝廊 N 張）。本次的執行層設計：
/// 呼叫端（前端）對還沒真正上傳的圖片，在對應的圖片欄位物件裡放 <c>{"pendingUpload": true,
/// "altZh": "...", "altEn": "..."}</c>（不含 <c>key</c>／<c>width</c>／<c>height</c>），
/// 並在同一個 multipart 請求夾一個檔案欄位，命名為 <c>file:{區塊索引}:{圖片路徑}</c>
/// （圖文左右固定是 <c>image</c>；圖片藝廊是 <c>images:0</c>、<c>images:1</c>……，
/// 索引依 <c>images</c> 陣列的位置）。<see cref="AdminPagesEndpoints"/> 負責從 multipart 表單
/// 找出對應檔案、呼叫 <see cref="Images.IImageStorageService"/> 上傳，再由本類別把回傳的
/// <c>key</c>／<c>width</c>／<c>height</c> 寫回 JSON 節點、移除 <c>pendingUpload</c> 標記。
/// 沒有標記 <c>pendingUpload</c> 的既有圖片欄位必須已經帶著非空白的 <c>key</c>（代表「沿用既有
/// 圖片，不換圖」），否則視為缺漏必填欄位。圖片的替代文字沿用扁平的 <c>altZh</c>／<c>altEn</c>
/// 兩個鍵（不是巢狀 <c>{zh,en}</c> 物件）——這是延續既有後台圖片上傳通則「雙語 Alt 文字」的既有
/// 命名慣例（比照規劃書 §4.0 圖片欄位組的既有寫法），跟本檔其餘文字欄位的巢狀寫法不同，
/// 刻意不統一成同一種形狀。
/// </summary>
internal static class PageBlockContentProcessor
{
    public delegate Task<UploadedImageInfo?> ImageUploadResolver(string imagePath, CancellationToken cancellationToken);

    /// <summary>
    /// 驗證單一區塊的內容並就地（in place）解析圖片欄位。<paramref name="content"/> 驗證通過後
    /// 已經是「可以直接序列化寫進 <c>page_blocks.content</c>」的最終形狀。
    /// </summary>
    public static async Task ValidateAndResolveAsync(
        int blockIndex, string blockType, JsonNode? content, ImageUploadResolver resolveUpload, CancellationToken cancellationToken)
    {
        if (!PageBlockTypes.IsKnown(blockType))
        {
            throw new AdminPageValidationException(
                $"第 {blockIndex + 1} 個區塊的型別「{blockType}」不是支援的區塊類型。");
        }

        if (content is not JsonObject obj)
        {
            throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊的內容格式錯誤，必須是一個 JSON 物件。");
        }

        switch (blockType)
        {
            case PageBlockTypes.Text:
                RequireBilingualText(obj, "body", blockIndex, "內文");
                break;

            case PageBlockTypes.TextImage:
                RequireBilingualText(obj, "body", blockIndex, "內文");
                RequireOneOf(obj, "imagePosition", blockIndex, "圖片位置", "left", "right");
                await ResolveImageObjectAsync(obj, "image", blockIndex, "image", resolveUpload, cancellationToken);
                break;

            case PageBlockTypes.Gallery:
                await ResolveImageArrayAsync(obj, "images", blockIndex, resolveUpload, cancellationToken);
                break;

            case PageBlockTypes.VideoEmbed:
                RequireOneOf(obj, "provider", blockIndex, "影音來源", "youtube", "vimeo");
                RequireNonEmptyString(obj, "videoId", blockIndex, "影片代碼");
                RequireOptionalBilingualText(obj, "caption", blockIndex);
                break;

            case PageBlockTypes.Quote:
                RequireBilingualText(obj, "text", blockIndex, "引言文字");
                RequireOptionalBilingualText(obj, "attribution", blockIndex);
                break;

            case PageBlockTypes.Cta:
                RequireBilingualText(obj, "text", blockIndex, "文字");
                RequireBilingualText(obj, "buttonLabel", blockIndex, "按鈕文字");
                RequireNonEmptyString(obj, "buttonUrl", blockIndex, "按鈕連結網址");
                break;

            case PageBlockTypes.AccordionFaq:
                RequireItemArray(obj, "items", blockIndex, minCount: 1, (item, itemIndex) =>
                {
                    RequireBilingualText(item, "question", blockIndex, $"第 {itemIndex + 1} 筆的問題");
                    RequireBilingualText(item, "answer", blockIndex, $"第 {itemIndex + 1} 筆的答案");
                });
                break;

            case PageBlockTypes.Timeline:
                RequireItemArray(obj, "items", blockIndex, minCount: 1, (item, itemIndex) =>
                {
                    RequireNonEmptyString(item, "date", blockIndex, $"第 {itemIndex + 1} 筆的日期"); // 日期本身不分語言
                    RequireBilingualText(item, "title", blockIndex, $"第 {itemIndex + 1} 筆的標題");
                    RequireOptionalBilingualText(item, "description", blockIndex);
                });
                break;

            case PageBlockTypes.Steps:
                RequireItemArray(obj, "items", blockIndex, minCount: 1, (item, itemIndex) =>
                {
                    RequireBilingualText(item, "title", blockIndex, $"第 {itemIndex + 1} 筆的標題");
                    RequireOptionalBilingualText(item, "description", blockIndex);
                });
                break;

            case PageBlockTypes.StatCards:
                RequireItemArray(obj, "items", blockIndex, minCount: 1, (item, itemIndex) =>
                {
                    RequireNonEmptyString(item, "value", blockIndex, $"第 {itemIndex + 1} 筆的數據值"); // 數值本身不分語言
                    RequireBilingualText(item, "label", blockIndex, $"第 {itemIndex + 1} 筆的說明文字");
                });
                break;

            case PageBlockTypes.Table:
                RequireTable(obj, blockIndex);
                break;

            case PageBlockTypes.FileDownload:
                RequireBilingualText(obj, "label", blockIndex, "檔案名稱");
                RequireNonEmptyString(obj, "fileUrl", blockIndex, "檔案網址");
                break;
        }
    }

    /// <summary>供刪除／換圖時計算「這個區塊目前引用了哪些物件鍵」，供呼叫端 diff 出不再被引用、
    /// 該一併刪除的舊物件（規劃書 §4.0「換圖與刪除」「一張圖只屬於一筆資料列」）。
    /// 只認得到 <see cref="PageBlockTypes.TextImage"/>／<see cref="PageBlockTypes.Gallery"/>
    /// 這兩種目前唯一有圖片欄位的型別，其餘型別一律回傳空集合。</summary>
    public static IEnumerable<string> ExtractImageKeys(string blockType, JsonNode? content)
    {
        if (content is not JsonObject obj)
        {
            yield break;
        }

        switch (blockType)
        {
            case PageBlockTypes.TextImage:
                if (obj["image"] is JsonObject img && GetString(img, "key") is { Length: > 0 } key)
                {
                    yield return key;
                }

                break;

            case PageBlockTypes.Gallery:
                if (obj["images"] is JsonArray images)
                {
                    foreach (var item in images)
                    {
                        if (item is JsonObject itemObj && GetString(itemObj, "key") is { Length: > 0 } itemKey)
                        {
                            yield return itemKey;
                        }
                    }
                }

                break;
        }
    }

    // ───────────────────────────── 圖片欄位解析 ─────────────────────────────

    private static async Task ResolveImageObjectAsync(
        JsonObject parent, string propertyName, int blockIndex, string uploadPath,
        ImageUploadResolver resolveUpload, CancellationToken cancellationToken)
    {
        if (parent[propertyName] is not JsonObject imageObj)
        {
            throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊缺少必要的圖片欄位。");
        }

        await ResolveSingleImageAsync(imageObj, blockIndex, uploadPath, resolveUpload, cancellationToken);
    }

    private static async Task ResolveImageArrayAsync(
        JsonObject parent, string propertyName, int blockIndex, ImageUploadResolver resolveUpload, CancellationToken cancellationToken)
    {
        if (parent[propertyName] is not JsonArray array || array.Count == 0)
        {
            throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊（圖片藝廊）至少需要 1 張圖片。");
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject imageObj)
            {
                throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊第 {i + 1} 張圖片的格式錯誤。");
            }

            await ResolveSingleImageAsync(imageObj, blockIndex, $"images:{i}", resolveUpload, cancellationToken);
        }
    }

    private static async Task ResolveSingleImageAsync(
        JsonObject imageObj, int blockIndex, string uploadPath, ImageUploadResolver resolveUpload, CancellationToken cancellationToken)
    {
        var isPending = imageObj["pendingUpload"] is JsonValue pendingValue
            && pendingValue.TryGetValue<bool>(out var pendingBool) && pendingBool;

        if (isPending)
        {
            var uploaded = await resolveUpload(uploadPath, cancellationToken)
                ?? throw new AdminPageValidationException(
                    $"第 {blockIndex + 1} 個區塊標示了新圖片待上傳，但這次請求沒有夾對應的檔案（欄位 file:{blockIndex}:{uploadPath}）。");

            imageObj.Remove("pendingUpload");
            imageObj["key"] = uploaded.Key;
            imageObj["width"] = uploaded.Width;
            imageObj["height"] = uploaded.Height;
        }
        else
        {
            var existingKey = GetString(imageObj, "key");
            if (string.IsNullOrWhiteSpace(existingKey))
            {
                throw new AdminPageValidationException(
                    $"第 {blockIndex + 1} 個區塊的圖片欄位缺少既有圖片，且未標示要上傳新圖片（缺少 pendingUpload 或 key）。");
            }
        }

        var altZh = GetString(imageObj, "altZh");
        if (string.IsNullOrWhiteSpace(altZh))
        {
            throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊的圖片替代文字（中文）為必填。");
        }

        // 英文可空但欄位必須存在（CLAUDE.md 全域規定 4）。
        if (imageObj["altEn"] is null)
        {
            imageObj["altEn"] = null;
        }
    }

    // ───────────────────────────── 通用結構驗證 ─────────────────────────────

    /// <summary>雙語文字欄位（必填）：<c>{ "zh": "非空白字串", "en": null|"字串" }</c>。
    /// <c>en</c> 缺鍵時就地補一個值為 <c>null</c> 的鍵（CLAUDE.md 全域規定 4）。</summary>
    private static void RequireBilingualText(JsonObject obj, string property, int blockIndex, string fieldLabel)
    {
        if (obj[property] is not JsonObject textObj)
        {
            throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊的「{fieldLabel}」為必填欄位，且必須是雙語物件（{{zh, en}}）。");
        }

        var zh = GetString(textObj, "zh");
        if (string.IsNullOrWhiteSpace(zh))
        {
            throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊的「{fieldLabel}」中文為必填欄位。");
        }

        if (textObj["en"] is null)
        {
            textObj["en"] = null;
        }
    }

    /// <summary>雙語文字欄位（整個欄位可省略，例：引言的來源署名、影音的說明文字）。
    /// 一旦提供，中文一樣必填非空白，理由與 <see cref="RequireBilingualText"/> 相同。</summary>
    private static void RequireOptionalBilingualText(JsonObject obj, string property, int blockIndex)
    {
        if (obj[property] is null)
        {
            return;
        }

        RequireBilingualText(obj, property, blockIndex, property);
    }

    private static void RequireNonEmptyString(JsonObject obj, string property, int blockIndex, string fieldLabel)
    {
        var value = GetString(obj, property);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊的「{fieldLabel}」為必填欄位。");
        }
    }

    private static void RequireOneOf(JsonObject obj, string property, int blockIndex, string fieldLabel, params string[] allowedValues)
    {
        var value = GetString(obj, property);
        if (value is null || !allowedValues.Contains(value, StringComparer.Ordinal))
        {
            throw new AdminPageValidationException(
                $"第 {blockIndex + 1} 個區塊的「{fieldLabel}」必須是「{string.Join("、", allowedValues)}」其中之一。");
        }
    }

    private static void RequireItemArray(
        JsonObject obj, string property, int blockIndex, int minCount, Action<JsonObject, int> validateItem)
    {
        if (obj[property] is not JsonArray array || array.Count < minCount)
        {
            throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊至少需要 {minCount} 筆項目。");
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject item)
            {
                throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊第 {i + 1} 筆項目格式錯誤。");
            }

            validateItem(item, i);
        }
    }

    /// <summary>表格區塊：<c>headers</c> 是雙語物件陣列（欄位標題要翻譯），<c>rows</c> 每一列是
    /// 純字串陣列（儲存格資料多為數字或專有名詞，不強制雙語——規劃書只給「表格」這個名稱，
    /// 沒有進一步定義儲存格是否要雙語，這裡採保守解讀，只對「標題」這種已知一定是自然語言標籤的
    /// 部分要求雙語，儲存格內容留給編輯者自行決定要不要在文字裡混雙語）。</summary>
    private static void RequireTable(JsonObject obj, int blockIndex)
    {
        if (obj["headers"] is not JsonArray headers || headers.Count == 0)
        {
            throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊（表格）至少需要 1 個欄位標題。");
        }

        for (var h = 0; h < headers.Count; h++)
        {
            // ⚠️ 不透過 RequireBilingualText——那個方法用 JsonObject 的屬性名稱索引，
            // headers 是 JsonArray，元素要用位置索引，兩者索引方式不同，這裡直接展開同一套檢查。
            if (headers[h] is not JsonObject headerObj)
            {
                throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊（表格）第 {h + 1} 個欄位標題必須是雙語物件（{{zh, en}}）。");
            }

            var headerZh = GetString(headerObj, "zh");
            if (string.IsNullOrWhiteSpace(headerZh))
            {
                throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊（表格）第 {h + 1} 個欄位標題中文為必填。");
            }

            if (headerObj["en"] is null)
            {
                headerObj["en"] = null;
            }
        }

        if (obj["rows"] is not JsonArray rows)
        {
            throw new AdminPageValidationException($"第 {blockIndex + 1} 個區塊（表格）缺少資料列（可以是空陣列，但欄位必須存在）。");
        }

        for (var r = 0; r < rows.Count; r++)
        {
            if (rows[r] is not JsonArray row || row.Count != headers.Count)
            {
                throw new AdminPageValidationException(
                    $"第 {blockIndex + 1} 個區塊（表格）第 {r + 1} 列的欄數（{(rows[r] as JsonArray)?.Count ?? 0}）與標題欄數（{headers.Count}）不一致。");
            }
        }
    }

    private static string? GetString(JsonObject obj, string property)
        => obj[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
