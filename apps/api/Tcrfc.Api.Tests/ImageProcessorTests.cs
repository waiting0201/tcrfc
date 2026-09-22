using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Tcrfc.Api.Images;
using Xunit;

namespace Tcrfc.Api.Tests;

/// <summary>
/// <see cref="ImageProcessor"/> 的純邏輯單元測試（不碰 HTTP、不碰資料庫、不碰物件儲存）——
/// 逐條對應規劃書 §4.0「後台圖片上傳通則」（v3.9）：格式允許清單、長邊縮放上限、
/// 固定四個衍生檔、去除全部中繼資料（含 EXIF GPS）、依方向轉正。
/// </summary>
public sealed class ImageProcessorTests
{
    [Fact]
    public void 正常JPEG_長邊超過2560會等比縮小_且固定產出四個衍生檔()
    {
        var result = ImageProcessor.Process(TestImages.JpegWithExifAndGps());

        // 來源是 3000x2000（橫向），但 Orientation=6 要求旋轉 90 度，轉正後變成 2000x3000（縱向），
        // 長邊 3000 超過 2560 上限，等比縮小為長邊 2560：2560/3000*2000 = 1706.67 → 四捨五入 1707。
        Assert.Equal(1707, result.MainWidth);
        Assert.Equal(2560, result.MainHeight);

        Assert.Equal(4, result.Derivatives.Count);
        Assert.Equal(["1280", "640", "320", "thumb"], result.Derivatives.Select(d => d.SizeLabel));

        // 160px 縮圖固定是正方形（置中裁切）。
        var thumbnail = result.Derivatives.Single(d => d.SizeLabel == "thumb");
        Assert.Equal(160, thumbnail.Width);
        Assert.Equal(160, thumbnail.Height);

        // 1280／640／320 三個長邊尺寸維持原圖長寬比（縱向：寬 < 高）。
        var d1280 = result.Derivatives.Single(d => d.SizeLabel == "1280");
        Assert.Equal(1280, d1280.Height);
        Assert.True(d1280.Width < d1280.Height);
    }

    [Fact]
    public void 全部五個輸出都是真正的WebP格式()
    {
        var result = ImageProcessor.Process(TestImages.JpegWithExifAndGps());

        Assert.Equal("WEBP", Image.DetectFormat(result.MainWebPBytes).Name, StringComparer.OrdinalIgnoreCase);
        foreach (var derivative in result.Derivatives)
        {
            Assert.Equal("WEBP", Image.DetectFormat(derivative.WebPBytes).Name, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void 主檔與衍生檔的EXIF與GPS全部被移除()
    {
        var result = ImageProcessor.Process(TestImages.JpegWithExifAndGps());

        using var mainImage = Image.Load(result.MainWebPBytes);
        Assert.Null(mainImage.Metadata.ExifProfile);
        Assert.Null(mainImage.Metadata.IccProfile);
        Assert.Null(mainImage.Metadata.IptcProfile);
        Assert.Null(mainImage.Metadata.XmpProfile);

        foreach (var derivative in result.Derivatives)
        {
            using var derivativeImage = Image.Load(derivative.WebPBytes);
            Assert.Null(derivativeImage.Metadata.ExifProfile);
        }
    }

    [Fact]
    public void 主檔小於目標尺寸時衍生檔不放大補齊_直接沿用主檔()
    {
        var result = ImageProcessor.Process(TestImages.SmallPng()); // 500x400，比 1280/640 都小

        Assert.Equal(500, result.MainWidth);
        Assert.Equal(400, result.MainHeight);

        var d1280 = result.Derivatives.Single(d => d.SizeLabel == "1280");
        var d640 = result.Derivatives.Single(d => d.SizeLabel == "640");
        var d320 = result.Derivatives.Single(d => d.SizeLabel == "320");

        // 1280／640 都比主檔大，不放大——沿用主檔尺寸。320 比主檔小，正常縮小。
        Assert.Equal(500, d1280.Width);
        Assert.Equal(400, d1280.Height);
        Assert.Equal(500, d640.Width);
        Assert.Equal(400, d640.Height);
        Assert.True(d320.Width <= 320);
        Assert.NotEqual(500, d320.Width);
    }

    [Fact]
    public void 接受PNG格式()
    {
        var result = ImageProcessor.Process(TestImages.SmallPng());
        Assert.Equal(500, result.MainWidth);
        Assert.Equal(400, result.MainHeight);
    }

    [Fact]
    public void 接受WebP格式()
    {
        var result = ImageProcessor.Process(TestImages.SmallWebp());
        Assert.Equal(200, result.MainWidth);
        Assert.Equal(200, result.MainHeight);
    }

    [Fact]
    public void 假副檔名的純文字檔被擋下()
    {
        Assert.Throws<UnsupportedImageFormatException>(() => ImageProcessor.Process(TestImages.FakeImageBytes()));
    }

    [Fact]
    public void 空檔案被擋下()
    {
        Assert.Throws<EmptyImageException>(() => ImageProcessor.Process([]));
    }

    [Fact]
    public void 損毀的JPEG檔頭被擋下_而不是讓內部例外洩漏出去()
    {
        var truncated = TestImages.JpegWithExifAndGps();
        // 只留檔頭前 20 個位元組——格式偵測會辨認出 JPEG，但實際解碼一定失敗。
        var corrupted = truncated[..20];

        Assert.Throws<UnsupportedImageFormatException>(() => ImageProcessor.Process(corrupted));
    }
}
