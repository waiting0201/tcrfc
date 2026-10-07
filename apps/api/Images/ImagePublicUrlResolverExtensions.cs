namespace Tcrfc.Api.Images;

/// <summary>後台讀取 DTO 的 <c>{X}Url</c>／<c>{X}ThumbUrl</c> 成對補值用。衍生鍵一律走
/// <see cref="ImageObjectKey"/>，不在各模組自己拼字串；鍵為 <c>null</c>／空字串時回傳 <c>null</c>。
/// 沒設 Blob 時（<see cref="UnavailableImagePublicUrlResolver"/>）自然回傳 <c>null</c>，不丟例外。</summary>
public static class ImagePublicUrlResolverExtensions
{
    public static string? ResolveThumbnail(this IImagePublicUrlResolver resolver, string? mainKey)
        => string.IsNullOrEmpty(mainKey) ? null : resolver.Resolve(ImageObjectKey.ForThumbnail(mainKey));
}
