using System.Text.Json.Nodes;
using Tcrfc.Api.Localization;

namespace Tcrfc.Api.Features.Pages;

/// <summary>
/// 把區塊內容 JSON 裡的雙語物件 <c>{"zh": "...", "en": null|"..."}</c>（見
/// <c>Features/AdminPages/PageBlockContentProcessor.cs</c> 檔頭的雙語約定）依請求語系化簡成單一
/// 字串，遞迴處理任意深度的巢狀物件與陣列。**判斷「這是不是一個雙語物件」的規則**：
/// 這個 JSON 物件恰好只有 <c>zh</c>（必要）與 <c>en</c>（非必要）兩個鍵——符合就化簡為字串，
/// 不符合就當成一般結構，往下遞迴處理它的子節點。
///
/// ⚠️ **圖片欄位的 <c>altZh</c>／<c>altEn</c> 不受影響**：那是扁平的兩個獨立鍵，不是巢狀的
/// <c>{zh,en}</c> 物件，本轉換器的規則辨識不到、也不會誤觸——**前台自己依語系挑選
/// <c>altZh</c>／<c>altEn</c>**，這是刻意的取捨（沿用後台圖片上傳通則既有的扁平雙語 Alt 文字命名，
/// 沒有為了套用同一套遞迴規則而改變那個既有慣例）。
/// </summary>
internal static class PageContentLocalizer
{
    public static JsonNode? Localize(JsonNode? node, string dbLocale)
    {
        switch (node)
        {
            case JsonObject obj when IsBilingualTextObject(obj):
                var zh = AsString(obj["zh"]);
                var en = AsString(obj["en"]);
                var requested = dbLocale == "en" ? en : zh;
                var picked = RequestLocale.Pick(requested, zh);
                return picked is null ? null : JsonValue.Create(picked);

            case JsonObject obj:
                var resultObj = new JsonObject();
                foreach (var (key, value) in obj)
                {
                    resultObj[key] = Localize(value?.DeepClone(), dbLocale);
                }

                return resultObj;

            case JsonArray array:
                var resultArray = new JsonArray();
                foreach (var item in array)
                {
                    resultArray.Add(Localize(item?.DeepClone(), dbLocale));
                }

                return resultArray;

            default:
                return node?.DeepClone();
        }
    }

    private static bool IsBilingualTextObject(JsonObject obj)
    {
        if (!obj.ContainsKey("zh"))
        {
            return false;
        }

        // 只能有 zh、en 兩個鍵（en 可省略），且兩者都必須是字串或 null，不能是巢狀物件/陣列——
        // 避免誤把「剛好有一個叫 zh 的欄位、但其實是別的意思」的結構當成雙語文字化簡掉。
        foreach (var (key, value) in obj)
        {
            if (key is not ("zh" or "en"))
            {
                return false;
            }

            if (value is not null && value is not JsonValue)
            {
                return false;
            }
        }

        return true;
    }

    private static string? AsString(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
