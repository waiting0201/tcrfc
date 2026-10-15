using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tcrfc.Api.Features.Uploads;

namespace Tcrfc.Api.Features.AdminNews;

/// <summary>
/// 後台送進來的一個新聞內文區塊（雙語、一份清單）。外層欄位沿用 B1 頁面區塊的實際形狀
/// （<c>blockType</c>／<c>content</c>，見 <c>Features/AdminPages/AdminPageBlockInput</c>）；
/// 契約文件寫的 <c>type</c> 也接受（別名，兩者擇一帶即可，都帶時以 <c>blockType</c> 為準）。
/// </summary>
public sealed record NewsBlockInput
{
    public string? BlockType { get; init; }

    /// <summary><see cref="BlockType"/> 的別名（前後台契約初稿用 <c>type</c>）。</summary>
    public string? Type { get; init; }

    public JsonNode? Content { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? ResolvedType => string.IsNullOrWhiteSpace(BlockType) ? Type : BlockType;
}

/// <summary>內文區塊解析後的結果：zh／en 兩份單語 JSON（<c>{"blocks":[…]}</c>）與主檔圖片鍵清單。</summary>
public sealed record NewsBodyResolved(string? ZhJson, string? EnJson, IReadOnlyList<string> ImageKeys);

/// <summary>
/// 新聞內文區塊（主站規劃書 v3.28 §4.2 B2、§5.1 Article）的驗證、雙語拆合與圖片處理。
/// <para>
/// <b>儲存（B2）</b>：後台是「一份雙語清單」，儲存時拆成 zh、en 兩份單語 JSON，各存
/// <c>articles_i18n.body</c>（<c>{"blocks":[…]}</c>），<b>結構、順序、圖片 key 兩份完全相同</b>；
/// 讀取（後台）以 zh 為骨架、依索引合併 en。公開 API 直接回傳單語 JSON，不在 API 組圖片網址。
/// </para>
/// <para>
/// <b>圖片</b>沿用 B1 的欄位直傳：圖片欄位物件 <c>{pendingUpload:true,altZh,altEn}</c>（新圖，檔案放同一個
/// multipart 請求的 <c>file:{區塊索引}:{路徑}</c>）或 <c>{key,width,height,altZh,altEn}</c>（既有圖）；解析、上傳、
/// 補償刪除由 <see cref="BlockImageUploader"/>／<see cref="BlockImageFields"/>（與 B1 共用）負責。
/// 與 B1 不同的兩點：① 內文圖片寬度下限 800px（規劃書 §4.0「內文插圖」）；② 既有圖片的 <c>key</c> 必須是
/// 「這篇文章目前已存的內文圖片」，寬高以伺服器記錄為準，不採信客戶端（防止拿別處的圖片鍵、日後隨文章一併刪除）。
/// </para>
/// </summary>
public static partial class NewsBodyBlocks
{
    // 上限（規劃書未給數字，執行層決定，寫進 apps/api/README.md「新聞內文區塊」）。
    public const int MaxBlocks = 100;
    public const int MaxImages = 60;
    public const int MaxGalleryImages = 30;
    public const int MaxListItems = 50;
    public const int MinImageWidth = 800;
    public const int MaxParagraphLength = 20000;
    public const int MaxHeadingLength = 200;
    public const int MaxQuoteLength = 2000;
    public const int MaxCiteLength = 200;
    public const int MaxListItemLength = 500;
    public const int MaxCaptionLength = 300;
    public const int MaxAltLength = 200;

    private const string FieldRoot = "bodyBlocks";

    public static class Types
    {
        public const string Text = "text";
        public const string Heading = "heading";
        public const string Quote = "quote";
        public const string List = "list";
        public const string Image = "image";
        public const string Gallery = "gallery";
        public const string VideoEmbed = "video_embed";

        public static readonly IReadOnlyList<string> All = [Text, Heading, Quote, List, Image, Gallery, VideoEmbed];
    }

    private static readonly JsonSerializerOptions StoreOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex VideoIdPattern();

    // ─────────────────────────── 寫入：驗證 → 上傳 → 拆 zh／en ───────────────────────────

    /// <param name="blocks">後台送來的雙語區塊清單（<c>bodyBlocks</c>）。</param>
    /// <param name="knownImages">這篇文章目前已存的內文圖片（key → 寬高）；新建文章為空。</param>
    public static async Task<NewsBodyResolved> ResolveAsync(
        IReadOnlyList<NewsBlockInput> blocks, BlockImageUploader uploader,
        IReadOnlyDictionary<string, (int Width, int Height)> knownImages, CancellationToken cancellationToken)
    {
        if (blocks.Count > MaxBlocks)
        {
            throw Fail($"內文最多 {MaxBlocks} 個區塊，請精簡內容。", FieldRoot);
        }

        var zhBlocks = new JsonArray();
        var enBlocks = new JsonArray();
        var imageKeys = new List<string>();
        var enHasContent = false;

        for (var i = 0; i < blocks.Count; i++)
        {
            var type = blocks[i].ResolvedType;
            if (string.IsNullOrWhiteSpace(type) || !Types.All.Contains(type, StringComparer.Ordinal))
            {
                throw Fail($"第 {i + 1} 個區塊的型別「{type}」不是支援的區塊類型。", Key(i, "type"));
            }

            if (blocks[i].Content is not JsonObject content)
            {
                throw Fail($"第 {i + 1} 個區塊的內容格式錯誤，請重新整理頁面後再編輯。", Key(i));
            }

            // 深拷貝：就地解析圖片欄位不得改動呼叫端傳入的節點。
            content = (JsonObject)content.DeepClone();
            var (zh, en) = await BuildBlockAsync(i, type, content, uploader, knownImages, imageKeys, cancellationToken);
            zhBlocks.Add(zh);
            enBlocks.Add(en);
            enHasContent |= HasEnglish(en);
        }

        if (imageKeys.Count > MaxImages)
        {
            throw Fail($"內文最多放 {MaxImages} 張圖片，請精簡內容。", FieldRoot);
        }

        if (blocks.Count == 0)
        {
            return new NewsBodyResolved(null, null, imageKeys);
        }

        return new NewsBodyResolved(
            Serialize(zhBlocks),
            // 英文版完全沒有任何文字（所有區塊的英文都空白）＝這篇沒有英文內文，存 NULL 讓前台回退中文版。
            enHasContent ? Serialize(enBlocks) : null,
            imageKeys);
    }

    private static string Serialize(JsonArray blocks)
        => new JsonObject { ["blocks"] = blocks }.ToJsonString(StoreOptions);

    private static async Task<(JsonObject Zh, JsonObject En)> BuildBlockAsync(
        int index, string type, JsonObject c, BlockImageUploader uploader,
        IReadOnlyDictionary<string, (int Width, int Height)> known, List<string> imageKeys, CancellationToken ct)
    {
        switch (type)
        {
            case Types.Text:
            {
                var (zh, en) = Bilingual(c, "body", index, "內文", "bodyZh", "bodyEn", MaxParagraphLength, required: true);
                return (Block("paragraph", ("text", zh)), Block("paragraph", ("text", en)));
            }

            case Types.Heading:
            {
                var (zh, en) = Bilingual(c, "text", index, "小標", "textZh", "textEn", MaxHeadingLength, required: true);
                return (Block("heading", ("text", zh)), Block("heading", ("text", en)));
            }

            case Types.Quote:
            {
                var (zh, en) = Bilingual(c, "text", index, "引言文字", "textZh", "textEn", MaxQuoteLength, required: true);
                var (citeZh, citeEn) = Bilingual(c, "attribution", index, "引言來源", "attributionZh", "attributionEn", MaxCiteLength, required: false);
                var zhBlock = Block("quote", ("text", zh));
                var enBlock = Block("quote", ("text", en));
                if (citeZh.Length > 0)
                {
                    zhBlock["cite"] = citeZh;
                }

                if (citeEn.Length > 0)
                {
                    enBlock["cite"] = citeEn;
                }

                return (zhBlock, enBlock);
            }

            case Types.List:
            {
                if (c["items"] is not JsonArray items || items.Count == 0)
                {
                    throw Fail($"第 {index + 1} 個區塊（清單）至少需要 1 筆項目。", Key(index, "items"));
                }

                if (items.Count > MaxListItems)
                {
                    throw Fail($"第 {index + 1} 個區塊（清單）最多 {MaxListItems} 筆項目。", Key(index, "items"));
                }

                var zhItems = new JsonArray();
                var enItems = new JsonArray();
                for (var j = 0; j < items.Count; j++)
                {
                    var (zh, en) = BilingualValue(items[j], index, $"第 {j + 1} 筆項目", $"items[{j}].zh", $"items[{j}].en", MaxListItemLength, required: true);
                    zhItems.Add(zh);
                    enItems.Add(en);
                }

                var ordered = c["ordered"] is JsonValue ov && ov.TryGetValue<bool>(out var ob) && ob;
                var zhBlock = Block("list");
                zhBlock["ordered"] = ordered;
                zhBlock["items"] = zhItems;
                var enBlock = Block("list");
                enBlock["ordered"] = ordered;
                enBlock["items"] = enItems;
                return (zhBlock, enBlock);
            }

            case Types.Image:
            {
                if (c["image"] is not JsonObject img)
                {
                    throw Fail($"第 {index + 1} 個區塊缺少必要的圖片欄位。", Key(index, "image"));
                }

                await ResolveImageAsync(img, index, "image", "image", uploader, known, ct);
                var (capZh, capEn) = Bilingual(c, "caption", index, "圖片說明", "captionZh", "captionEn", MaxCaptionLength, required: false);
                var zhBlock = ImageBlock("image", img, zh: true);
                var enBlock = ImageBlock("image", img, zh: false);
                if (capZh.Length > 0)
                {
                    zhBlock["caption"] = capZh;
                }

                if (capEn.Length > 0)
                {
                    enBlock["caption"] = capEn;
                }

                imageKeys.Add(GetString(img, "key")!);
                return (zhBlock, enBlock);
            }

            case Types.Gallery:
            {
                if (c["images"] is not JsonArray imgs || imgs.Count == 0)
                {
                    throw Fail($"第 {index + 1} 個區塊（圖集）至少需要 1 張圖片。", Key(index, "images"));
                }

                if (imgs.Count > MaxGalleryImages)
                {
                    throw Fail($"第 {index + 1} 個區塊（圖集）最多 {MaxGalleryImages} 張圖片。", Key(index, "images"));
                }

                var zhImgs = new JsonArray();
                var enImgs = new JsonArray();
                for (var j = 0; j < imgs.Count; j++)
                {
                    if (imgs[j] is not JsonObject img)
                    {
                        throw Fail($"第 {index + 1} 個區塊第 {j + 1} 張圖片的格式錯誤。", Key(index, $"images[{j}]"));
                    }

                    await ResolveImageAsync(img, index, $"images:{j}", $"images[{j}]", uploader, known, ct);
                    zhImgs.Add(ImageObject(img, zh: true));
                    enImgs.Add(ImageObject(img, zh: false));
                    imageKeys.Add(GetString(img, "key")!);
                }

                var zhBlock = Block("gallery");
                zhBlock["images"] = zhImgs;
                var enBlock = Block("gallery");
                enBlock["images"] = enImgs;
                return (zhBlock, enBlock);
            }

            default: // Types.VideoEmbed
            {
                var provider = GetString(c, "provider");
                if (provider is not ("youtube" or "vimeo"))
                {
                    throw Fail($"第 {index + 1} 個區塊的「影音來源」必須從選項中擇一。", Key(index, "provider"));
                }

                var videoId = GetString(c, "videoId")?.Trim();
                if (string.IsNullOrEmpty(videoId) || !VideoIdPattern().IsMatch(videoId))
                {
                    throw Fail($"第 {index + 1} 個區塊的「影片代碼」格式不正確（只能包含英數字、底線與連字號）。", Key(index, "videoId"));
                }

                var (capZh, capEn) = Bilingual(c, "caption", index, "影片說明", "captionZh", "captionEn", MaxCaptionLength, required: false);
                var zhBlock = Block("video");
                zhBlock["provider"] = provider;
                zhBlock["videoId"] = videoId;
                var enBlock = Block("video");
                enBlock["provider"] = provider;
                enBlock["videoId"] = videoId;
                if (capZh.Length > 0)
                {
                    zhBlock["caption"] = capZh;
                }

                if (capEn.Length > 0)
                {
                    enBlock["caption"] = capEn;
                }

                return (zhBlock, enBlock);
            }
        }
    }

    private static async Task ResolveImageAsync(
        JsonObject img, int index, string uploadPath, string fieldPath, BlockImageUploader uploader,
        IReadOnlyDictionary<string, (int Width, int Height)> known, CancellationToken ct)
    {
        var pending = img["pendingUpload"] is JsonValue pv && pv.TryGetValue<bool>(out var pb) && pb;

        await BlockImageFields.ResolveAsync(
            img, index, uploadPath, fieldPath, FieldRoot, uploader.ResolverFor(index),
            (message, field) => new AdminArticleValidationException(message, field), ct);

        if (!pending)
        {
            // 既有圖片：必須是這篇文章目前已存的內文圖片，寬高以伺服器記錄為準。
            var key = GetString(img, "key")!;
            if (!known.TryGetValue(key, out var dims))
            {
                throw Fail($"第 {index + 1} 個區塊的圖片已經不存在，請重新選擇圖片。", Key(index, fieldPath));
            }

            img["width"] = dims.Width;
            img["height"] = dims.Height;
        }

        if ((GetString(img, "altZh")?.Length ?? 0) > MaxAltLength || (GetString(img, "altEn")?.Length ?? 0) > MaxAltLength)
        {
            throw Fail($"第 {index + 1} 個區塊的圖片替代文字最多 {MaxAltLength} 字。", Key(index, fieldPath + ".altZh"));
        }
    }

    private static JsonObject ImageObject(JsonObject img, bool zh)
    {
        var obj = new JsonObject
        {
            ["key"] = GetString(img, "key"),
            ["width"] = img["width"]?.DeepClone(),
            ["height"] = img["height"]?.DeepClone(),
        };
        var alt = zh ? GetString(img, "altZh") : GetString(img, "altEn");
        obj["alt"] = string.IsNullOrWhiteSpace(alt) ? null : alt.Trim();
        return obj;
    }

    private static JsonObject ImageBlock(string type, JsonObject img, bool zh)
    {
        var block = ImageObject(img, zh);
        var result = new JsonObject { ["type"] = type };
        foreach (var kv in block.ToList())
        {
            block.Remove(kv.Key);
            result[kv.Key] = kv.Value;
        }

        return result;
    }

    private static JsonObject Block(string type, params (string Name, string Value)[] fields)
    {
        var obj = new JsonObject { ["type"] = type };
        foreach (var (name, value) in fields)
        {
            obj[name] = value;
        }

        return obj;
    }

    // ─────────────────────────── 雙語文字欄位 ───────────────────────────

    private static (string Zh, string En) Bilingual(
        JsonObject parent, string property, int index, string label, string zhKey, string enKey, int maxLength, bool required)
        => BilingualValue(parent[property], index, label, zhKey, enKey, maxLength, required, optionalNull: !required);

    private static (string Zh, string En) BilingualValue(
        JsonNode? node, int index, string label, string zhKey, string enKey, int maxLength, bool required, bool optionalNull = false)
    {
        if (node is null)
        {
            if (optionalNull)
            {
                return ("", "");
            }

            throw Fail($"第 {index + 1} 個區塊的「{label}」為必填欄位。", Key(index, zhKey));
        }

        if (node is not JsonObject obj)
        {
            throw Fail($"第 {index + 1} 個區塊的「{label}」格式錯誤，請重新整理頁面後再編輯。", Key(index, zhKey));
        }

        var zh = GetString(obj, "zh")?.Trim() ?? "";
        var en = GetString(obj, "en")?.Trim() ?? "";

        if (required && zh.Length == 0)
        {
            throw Fail($"第 {index + 1} 個區塊的「{label}」中文為必填欄位。", Key(index, zhKey));
        }

        if (zh.Length > maxLength)
        {
            throw Fail($"第 {index + 1} 個區塊的「{label}」中文最多 {maxLength} 字。", Key(index, zhKey));
        }

        if (en.Length > maxLength)
        {
            throw Fail($"第 {index + 1} 個區塊的「{label}」英文最多 {maxLength} 字。", Key(index, enKey));
        }

        return (zh, en);
    }

    /// <summary>英文那份是否有任何實際文字（文字、來源、說明、替代文字、清單項目任一非空白）。</summary>
    private static bool HasEnglish(JsonObject enBlock)
    {
        foreach (var name in new[] { "text", "cite", "caption" })
        {
            if (!string.IsNullOrWhiteSpace(GetString(enBlock, name)))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(GetString(enBlock, "alt")))
        {
            return true;
        }

        if (enBlock["items"] is JsonArray items && items.Any(x => x is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)))
        {
            return true;
        }

        return enBlock["images"] is JsonArray imgs
            && imgs.Any(x => x is JsonObject o && !string.IsNullOrWhiteSpace(GetString(o, "alt")));
    }

    // ─────────────────────────── 讀取：儲存形狀 → 後台雙語清單 ───────────────────────────

    /// <summary>主檔圖片鍵清單（依出現順序，可重複）；非區塊形狀（舊純文字、null）回傳空。</summary>
    public static IReadOnlyList<string> ExtractImageKeys(string? storedJson)
    {
        var keys = new List<string>();
        if (TryParseBlocks(storedJson) is not { } blocks)
        {
            return keys;
        }

        foreach (var b in blocks.OfType<JsonObject>())
        {
            if (GetString(b, "type") == "image" && GetString(b, "key") is { Length: > 0 } k)
            {
                keys.Add(k);
            }
            else if (GetString(b, "type") == "gallery" && b["images"] is JsonArray imgs)
            {
                keys.AddRange(imgs.OfType<JsonObject>().Select(o => GetString(o, "key")).Where(x => !string.IsNullOrEmpty(x))!);
            }
        }

        return keys;
    }

    /// <summary>目前已存的內文圖片（key → 寬高），給「既有圖片 key 必須存在」檢查用。</summary>
    public static Dictionary<string, (int Width, int Height)> KnownImages(string? zhStoredJson)
    {
        var result = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        if (TryParseBlocks(zhStoredJson) is not { } blocks)
        {
            return result;
        }

        void Add(JsonObject o)
        {
            if (GetString(o, "key") is { Length: > 0 } k)
            {
                result[k] = (Int(o, "width"), Int(o, "height"));
            }
        }

        foreach (var b in blocks.OfType<JsonObject>())
        {
            if (GetString(b, "type") == "image")
            {
                Add(b);
            }
            else if (GetString(b, "type") == "gallery" && b["images"] is JsonArray imgs)
            {
                foreach (var o in imgs.OfType<JsonObject>())
                {
                    Add(o);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 後台讀取：以 zh 為骨架、依索引合併 en，輸出 B1 形狀的雙語區塊（<c>blockType</c>／<c>content</c>）。
    /// 舊格式（<c>{"text":"…"}</c> 或純文字）轉成單一 <c>text</c> 區塊，zh／en 各自填入。
    /// 圖片物件附唯讀 <c>url</c>／<c>thumbUrl</c>。
    /// </summary>
    public static List<JsonObject> ToAdminBlocks(
        string? zhStored, string? enStored, Func<string?, string?> resolveUrl, Func<string?, string?> resolveThumb)
    {
        var zh = ParseForAdmin(zhStored);
        var en = ParseForAdmin(enStored);
        var result = new List<JsonObject>();

        for (var i = 0; i < zh.Count; i++)
        {
            var z = zh[i];
            var e = i < en.Count && GetString(en[i], "type") == GetString(z, "type") ? en[i] : null;
            result.Add(MergeBlock(z, e, resolveUrl, resolveThumb));
        }

        return result;
    }

    private static List<JsonObject> ParseForAdmin(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return [];
        }

        if (TryParseBlocks(stored) is { } blocks)
        {
            return blocks.OfType<JsonObject>().ToList();
        }

        // 舊格式：{"text":"…"}、JSON 字串純量或非 JSON 純文字 → 單一段落。其他未知形狀視為沒有內文。
        var text = Common.JsonColumn.UnwrapText(stored);
        if (!string.IsNullOrWhiteSpace(text) && !LooksLikeJsonContainer(text))
        {
            return [new JsonObject { ["type"] = "paragraph", ["text"] = text }];
        }

        return [];
    }

    private static bool LooksLikeJsonContainer(string text)
    {
        var t = text.TrimStart();
        return t.StartsWith('{') || t.StartsWith('[');
    }

    private static JsonArray? TryParseBlocks(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(stored) is JsonObject root && root["blocks"] is JsonArray arr ? arr : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonObject MergeBlock(JsonObject z, JsonObject? e, Func<string?, string?> resolveUrl, Func<string?, string?> resolveThumb)
    {
        static JsonObject Bi(string? zh, string? en) => new() { ["zh"] = zh ?? "", ["en"] = en ?? "" };

        JsonObject Img(JsonObject zi, JsonObject? ei)
        {
            var key = GetString(zi, "key");
            return new JsonObject
            {
                ["key"] = key,
                ["width"] = zi["width"]?.DeepClone(),
                ["height"] = zi["height"]?.DeepClone(),
                ["altZh"] = GetString(zi, "alt") ?? "",
                ["altEn"] = ei is null ? null : GetString(ei, "alt"),
                ["url"] = resolveUrl(key),
                ["thumbUrl"] = resolveThumb(key),
            };
        }

        static JsonObject Wrap(string type, JsonObject content) => new() { ["blockType"] = type, ["content"] = content };

        switch (GetString(z, "type"))
        {
            case "heading":
                return Wrap(Types.Heading, new JsonObject { ["text"] = Bi(GetString(z, "text"), e is null ? null : GetString(e, "text")) });

            case "quote":
                return Wrap(Types.Quote, new JsonObject
                {
                    ["text"] = Bi(GetString(z, "text"), e is null ? null : GetString(e, "text")),
                    ["attribution"] = Bi(GetString(z, "cite"), e is null ? null : GetString(e, "cite")),
                });

            case "list":
            {
                var zi = z["items"] as JsonArray ?? [];
                var ei = e?["items"] as JsonArray;
                var items = new JsonArray();
                for (var j = 0; j < zi.Count; j++)
                {
                    items.Add(Bi(Str(zi[j]), ei is not null && j < ei.Count ? Str(ei[j]) : null));
                }

                return Wrap(Types.List, new JsonObject
                {
                    ["ordered"] = z["ordered"] is JsonValue ov && ov.TryGetValue<bool>(out var ob) && ob,
                    ["items"] = items,
                });
            }

            case "image":
                return Wrap(Types.Image, new JsonObject
                {
                    ["image"] = Img(z, e),
                    ["caption"] = Bi(GetString(z, "caption"), e is null ? null : GetString(e, "caption")),
                });

            case "gallery":
            {
                var zi = z["images"] as JsonArray ?? [];
                var ei = e?["images"] as JsonArray;
                var imgs = new JsonArray();
                for (var j = 0; j < zi.Count; j++)
                {
                    if (zi[j] is JsonObject zo)
                    {
                        imgs.Add(Img(zo, ei is not null && j < ei.Count ? ei[j] as JsonObject : null));
                    }
                }

                return Wrap(Types.Gallery, new JsonObject { ["images"] = imgs });
            }

            case "video":
                return Wrap(Types.VideoEmbed, new JsonObject
                {
                    ["provider"] = GetString(z, "provider"),
                    ["videoId"] = GetString(z, "videoId"),
                    ["caption"] = Bi(GetString(z, "caption"), e is null ? null : GetString(e, "caption")),
                });

            default: // paragraph（含舊格式轉換）
                return Wrap(Types.Text, new JsonObject { ["body"] = Bi(GetString(z, "text"), e is null ? null : GetString(e, "text")) });
        }
    }

    // ─────────────────────────── 共用小工具 ───────────────────────────

    private static string? Str(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static string? GetString(JsonObject obj, string property) => BlockImageFields.GetString(obj, property);

    private static int Int(JsonObject obj, string property)
        => obj[property] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0;

    private static string Key(int index, string? sub = null) => Common.FieldKey.Item(FieldRoot, index, sub);

    private static AdminArticleValidationException Fail(string message, string field) => new(message, field);
}
