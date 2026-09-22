using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Tcrfc.Api.Tests;

/// <summary>
/// 圖片上傳測試（S0-8）用的真實圖片產生器。⚠️ **全部用 ImageSharp 在記憶體現產，不讀外部檔案**——
/// 這樣測試在任何機器（含 CI 的 Linux runner）都能重現，不依賴本機開發時用 macOS <c>sips</c>／
/// Python <c>piexif</c> 手動產生的驗證用檔案（那組手動驗證的完整過程與結果記在
/// apps/api/README.md「實測：伺服器端驗證」，這裡是把同樣的斷言轉成可重複執行的自動化測試）。
/// </summary>
public static class TestImages
{
    /// <summary>
    /// 3000×2000 的 JPEG，帶 EXIF（含相機廠牌、方向標記 6＝需要旋轉、GPS 座標）——
    /// 驗證「去 EXIF（含 GPS）」與「依 EXIF 方向轉正」都要用到真的有這些中繼資料的來源檔，
    /// 不能拿一張本來就沒有 EXIF 的圖片假裝驗證過。
    /// </summary>
    public static byte[] JpegWithExifAndGps()
    {
        using var image = new Image<Rgba32>(3000, 2000);
        image.Mutate(x => x.BackgroundColor(Color.CornflowerBlue));

        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Make, "TestCameraCo");
        exif.SetValue(ExifTag.Model, "TestModelX");
        exif.SetValue(ExifTag.Orientation, (ushort)6); // 需要旋轉 90 度才能正確顯示
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, [new Rational(24, 1), new Rational(9, 1), new Rational(0, 1)]);
        exif.SetValue(ExifTag.GPSLongitudeRef, "E");
        exif.SetValue(ExifTag.GPSLongitude, [new Rational(120, 1), new Rational(41, 1), new Rational(0, 1)]);
        image.Metadata.ExifProfile = exif;

        using var stream = new MemoryStream();
        image.Save(stream, new JpegEncoder { Quality = 90 });
        return stream.ToArray();
    }

    /// <summary>500×400 的 PNG，不帶任何中繼資料——驗證伺服器端接受 PNG 格式本身。</summary>
    public static byte[] SmallPng()
    {
        using var image = new Image<Rgba32>(500, 400);
        image.Mutate(x => x.BackgroundColor(Color.MediumSeaGreen));
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }

    /// <summary>200×200 的 WebP——驗證伺服器端接受 WebP 格式本身（規劃書 §4.0 允許清單的第三種）。</summary>
    public static byte[] SmallWebp()
    {
        using var image = new Image<Rgba32>(200, 200);
        image.Mutate(x => x.BackgroundColor(Color.Coral));
        using var stream = new MemoryStream();
        image.Save(stream, new WebpEncoder());
        return stream.ToArray();
    }

    /// <summary>副檔名假裝是圖片，內容其實是純文字——驗證伺服器端以檔頭判格式，不看副檔名。</summary>
    public static byte[] FakeImageBytes()
        => "this is not actually a jpeg, just plain text pretending to be one"u8.ToArray();

    /// <summary>超過 <see cref="Tcrfc.Api.Images.ImageUploadOptions.MaxUploadBytes"/> 一個位元組的
    /// 全零位元組陣列——不需要是真正的圖片，因為檔案大小檢查（<c>file.Length &gt; MaxUploadBytes</c>）
    /// 發生在 <see cref="Tcrfc.Api.Images.ImageProcessor"/> 真的解碼之前，回應的錯誤訊息只跟大小有關。</summary>
    public static byte[] OversizedBytes()
        => new byte[Tcrfc.Api.Images.ImageUploadOptions.MaxUploadBytes + 1];
}
