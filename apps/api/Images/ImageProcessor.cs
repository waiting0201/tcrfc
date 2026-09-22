using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Tcrfc.Api.Images;

/// <summary>
/// 純轉檔邏輯（不含任何 I/O）：驗證格式 → 依 EXIF 方向轉正 → 去除全部中繼資料（含 EXIF GPS）→
/// 長邊超過 2560px 等比縮小 → 轉 WebP 存為主檔 → 固定產 1280／640／320 三個長邊尺寸與
/// 160px 置中裁切正方形縮圖，逐字對應規劃書 §4.0「後台圖片上傳通則」（v3.9）。
///
/// ⚠️ 「不放大補齊」只套用在長邊三個尺寸——主檔本身小於某個目標尺寸時，該尺寸直接沿用主檔
/// （不外插放大）。160px 縮圖是**固定尺寸**的裁切版位（後台列表格用），本質是「填滿裁切」，
/// 如果來源真的小於 160px 才會被迫放大——這種極端小圖本來就應該在呼叫端（版位下限驗證）擋掉，
/// 這一層不重複做那件事（那是「依版位另定」的業務規則，此為通用元件，見 README「已知缺口」）。
/// </summary>
public static class ImageProcessor
{
    /// <summary>伺服器端只接受這三種格式，逐字比對 <see cref="IImageFormat.Name"/>（不分大小寫）。
    /// ⚠️ 不看副檔名——這是「假副檔名檔案」與「HEIC/HEIF」都會被擋下的關鍵：兩者都通不過下面的
    /// 格式偵測（假檔頭偵測不出任何格式；HEIC 偵測得出格式名稱但 ImageSharp 沒有內建解碼器，
    /// 實際 <see cref="Image.Load(System.IO.Stream)"/> 會丟例外）。</summary>
    private static readonly HashSet<string> AllowedFormatNames =
        new(StringComparer.OrdinalIgnoreCase) { "JPEG", "PNG", "WEBP" };

    public static ProcessedImageSet Process(byte[] rawBytes)
    {
        if (rawBytes.Length == 0)
        {
            throw new EmptyImageException();
        }

        IImageFormat format;
        using (var probeStream = new MemoryStream(rawBytes, writable: false))
        {
            try
            {
                format = Image.DetectFormat(probeStream);
            }
            catch (UnknownImageFormatException)
            {
                // 偵測不出任何已註冊的解碼器——涵蓋「.jpg 副檔名但內容其實是純文字」這類假檔案。
                throw new UnsupportedImageFormatException();
            }
        }

        if (!AllowedFormatNames.Contains(format.Name))
        {
            // 偵測得出格式名稱，但不在允許清單——涵蓋 HEIC/HEIF（ImageSharp 認得出容器格式，
            // 但沒有對應解碼器可以真的讀取像素），以及 BMP／TIFF／GIF 等本規格未接受的格式。
            throw new UnsupportedImageFormatException();
        }

        using var image = LoadOrThrow(rawBytes);

        // 依 EXIF 方向轉正（手機直向拍攝常見的方向標記），轉正後才去除 EXIF，否則會丟失方向資訊。
        image.Mutate(x => x.AutoOrient());

        // 去除全部中繼資料，含 EXIF（內含 GPS 位置）、ICC 色彩描述、IPTC、XMP——只留像素本身。
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        // 長邊超過 2560px 等比縮小；不足則維持原尺寸，不放大補齊。
        var longEdge = Math.Max(image.Width, image.Height);
        if (longEdge > ImageUploadOptions.MainLongEdge)
        {
            var scale = (double)ImageUploadOptions.MainLongEdge / longEdge;
            image.Mutate(x => x.Resize(
                (int)Math.Round(image.Width * scale),
                (int)Math.Round(image.Height * scale)));
        }

        var mainBytes = EncodeWebp(image);
        var mainWidth = image.Width;
        var mainHeight = image.Height;

        var derivatives = new List<ProcessedDerivative>(capacity: ImageUploadOptions.DerivativeLongEdges.Length + 1);
        foreach (var edge in ImageUploadOptions.DerivativeLongEdges)
        {
            var currentLongEdge = Math.Max(image.Width, image.Height);
            if (currentLongEdge <= edge)
            {
                // 主檔已經比這個目標尺寸小，直接沿用主檔（不放大補齊），仍固定產出這個物件。
                derivatives.Add(new ProcessedDerivative(edge.ToString(), image.Width, image.Height, mainBytes));
                continue;
            }

            var scale = (double)edge / currentLongEdge;
            var width = (int)Math.Round(image.Width * scale);
            var height = (int)Math.Round(image.Height * scale);
            using var clone = image.Clone(x => x.Resize(width, height));
            derivatives.Add(new ProcessedDerivative(edge.ToString(), width, height, EncodeWebp(clone)));
        }

        // 160px 方形縮圖：置中裁切，後台列表格專用（規劃書 §4.0「衍生檔尺寸」）。
        using var thumbnail = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new Size(ImageUploadOptions.ThumbnailSize, ImageUploadOptions.ThumbnailSize),
            Mode = ResizeMode.Crop,
            Position = AnchorPositionMode.Center,
        }));
        derivatives.Add(new ProcessedDerivative(
            "thumb", ImageUploadOptions.ThumbnailSize, ImageUploadOptions.ThumbnailSize, EncodeWebp(thumbnail)));

        return new ProcessedImageSet(mainBytes, mainWidth, mainHeight, derivatives);
    }

    private static Image LoadOrThrow(byte[] rawBytes)
    {
        try
        {
            using var stream = new MemoryStream(rawBytes, writable: false);
            return Image.Load(stream);
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            // 格式偵測通過但實際解碼失敗（截斷檔案、偽造檔頭但內容損毀等），一律視為格式不支援，
            // ⛔ 不把 ImageSharp 的內部例外訊息吐給呼叫端。
            throw new UnsupportedImageFormatException();
        }
    }

    private static byte[] EncodeWebp(Image image)
    {
        using var stream = new MemoryStream();
        image.Save(stream, new WebpEncoder
        {
            Quality = ImageUploadOptions.WebPQuality,
            FileFormat = WebpFileFormatType.Lossy,
        });
        return stream.ToArray();
    }
}
