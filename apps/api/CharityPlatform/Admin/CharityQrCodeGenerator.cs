using QRCoder;

namespace Tcrfc.Api.CharityPlatform.Admin;

/// <summary>
/// 店家 QR Code 產生（規劃書 §2.3）：<b>容錯等級 Q（25%）</b>（印刷品常有污損與局部遮擋）、四周留白 4 個模組寬（QR 規格標準值，
/// 規劃書要求「至少 4 個模組寬」）。QR 內容<b>只有目標網址</b>——網址只帶 <c>store_slug</c>，不得攜帶金額或分潤參數
/// （§2.2、§11.2，由 <see cref="CharityStoresAdminService.BuildQrTargetUrl"/> 組出，這裡只負責把字串編成圖）。
///
/// 產出格式 PNG（一般用途）與 SVG（可無損放大）。⚠️ <b>含店名的印刷版 PDF 沒有做</b>：版型需要協會標誌（資產尚未提供，規劃書明文
/// 「提供前一律留佔位；不得沿用 TCRFC 標誌，亦不得自行為協會排字造標」）與中文字型（伺服器端沒有隨附授權明確的 CJK 字型），
/// 端點對 <c>format=pdf</c> 回 501，待資產到位後在這裡補一個產生器即可，端點契約不變。
/// 實際印刷尺寸：規劃書要求碼區不小於 3×3 公分（§2.3）。PNG 預設每個模組 10 像素（約 30 個模組的碼，碼區約 300 像素），
/// 以 300 dpi 印刷碼區約 2.5 公分，<b>未達 3 公分</b>——要印 3 公分以上請用 SVG（向量，任意放大），或把 <c>size</c> 參數調大（每模組像素數）。
/// </summary>
public static class CharityQrCodeGenerator
{
    public const int DefaultPixelsPerModule = 10;
    public const int MaxPixelsPerModule = 40;

    public static byte[] Png(string content, int pixelsPerModule = DefaultPixelsPerModule)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(Clamp(pixelsPerModule), drawQuietZones: true);
    }

    public static string Svg(string content, int pixelsPerModule = DefaultPixelsPerModule)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        return new SvgQRCode(data).GetGraphic(Clamp(pixelsPerModule), "#000000", "#FFFFFF", drawQuietZones: true, SvgQRCode.SizingMode.ViewBoxAttribute);
    }

    private static int Clamp(int pixelsPerModule) => Math.Clamp(pixelsPerModule, 2, MaxPixelsPerModule);
}
