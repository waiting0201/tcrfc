namespace Tcrfc.Api.Images;

/// <summary>
/// 衍生檔物件鍵的唯一推導規則（規劃書 §4.0：「鍵由主鍵推導，不另存欄位」）。
/// 資料庫只存主檔的 <c>_key</c> 一個欄位；1280／640／320／160 縮圖的鍵一律用這裡的規則從主鍵算出，
/// 前台與後台都呼叫這裡，不得各自重新拼字串。
/// </summary>
public static class ImageObjectKey
{
    /// <summary>主檔固定副檔名 <c>.webp</c>（規劃書 §4.0：「轉為 WebP 存為主檔」）。</summary>
    public const string Extension = ".webp";

    public static string ForLongEdge(string mainKey, int longEdge) => ForSuffix(mainKey, longEdge.ToString());

    public static string ForThumbnail(string mainKey) => ForSuffix(mainKey, "thumb");

    /// <summary>回傳主檔＋固定四個衍生檔，共五個物件鍵——換圖或刪除時「主檔與全部衍生檔一起刪」
    /// 要用的就是這一組。</summary>
    public static IReadOnlyCollection<string> AllObjectKeys(string mainKey)
    {
        var keys = new List<string>(capacity: ImageUploadOptions.DerivativeLongEdges.Length + 2) { mainKey };
        keys.AddRange(ImageUploadOptions.DerivativeLongEdges.Select(edge => ForLongEdge(mainKey, edge)));
        keys.Add(ForThumbnail(mainKey));
        return keys;
    }

    /// <summary>依 <see cref="ProcessedDerivative.SizeLabel"/>（"1280"／"640"／"320"／"thumb"）
    /// 推導物件鍵，<see cref="ForLongEdge"/>／<see cref="ForThumbnail"/> 本身就是這個方法的特例，
    /// 供 <c>BlobImageStorageService</c> 逐一衍生檔上傳時直接呼叫，不必自己判斷標籤是不是數字。</summary>
    public static string ForSuffix(string mainKey, string suffix)
    {
        if (!mainKey.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            // 不該發生（本服務產生的主鍵一律 .webp 結尾），但防呆一下避免產生無副檔名的怪鍵。
            return $"{mainKey}-{suffix}{Extension}";
        }

        var stem = mainKey[..^Extension.Length];
        return $"{stem}-{suffix}{Extension}";
    }
}
