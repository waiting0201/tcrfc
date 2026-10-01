namespace Tcrfc.Api.Features.Email;

/// <summary>
/// 站內商店交易信（主站規劃書 §3.8 8.3「系統信」：商店交易信為<b>獨立的一組</b>——訂單成立、付款完成、出貨通知、退款完成四封，
/// <b>與會員系統的五封各自獨立計算</b>）。F 批做「訂單成立」與「付款完成」兩封（出貨通知、退款完成屬後台動作，見 README F 批「只留介面」）。
/// 🔴 信件只放必要資訊：訂單編號、金額、品項摘要；<b>不放完整收件地址與電話</b>；訪客訂單帶一條<b>具時效性</b>的查詢連結（權杖即憑證，信件本身須妥善保管）。
/// </summary>
public static class ShopEmailTemplates
{
    public sealed record Line(string Name, int Quantity, int LineTotal);

    public static EmailMessage OrderCreated(
        string to, string name, string clubName, string orderNo, int total, IReadOnlyList<Line> lines, DateTime expiresAtUtc, string? lookupLink, string lang)
    {
        var deadline = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc).AddHours(8).ToString("yyyy-MM-dd HH:mm"); // 台灣時間 UTC+8，同 Common/TaiwanClock
        var items = Items(lines, lang);
        if (lang == "en")
        {
            var link = lookupLink is null ? string.Empty : $"\nTrack this order (no login needed):\n{lookupLink}\n";
            return new(to, $"[{clubName}] We received your order {orderNo}",
                $"Hi {name},\n\nThank you for your order {orderNo} with {clubName}.\n\n{items}\nTotal: NT${total:N0}\n\nPlease complete the LINE Pay payment before {deadline} (Taiwan time); unpaid orders are cancelled automatically and the stock is released.\n{link}",
                "shop_order_created");
        }

        var linkZh = lookupLink is null ? string.Empty : $"\n不用登入也能查詢這張訂單（連結有時效，請妥善保管）：\n{lookupLink}\n";
        return new(to, $"【{clubName}】訂單 {orderNo} 已成立",
            $"{name} 你好，\n\n感謝你在{clubName}商店下單，訂單編號 {orderNo}。\n\n{items}\n應付金額：NT${total:N0}\n\n請在 {deadline} 前完成 LINE Pay 付款；逾時未付款的訂單會自動取消並釋回庫存。\n{linkZh}",
            "shop_order_created");
    }

    public static EmailMessage PaymentCompleted(
        string to, string name, string clubName, string orderNo, int total, string? invoiceNo, string? lookupLink, string lang)
    {
        if (lang == "en")
        {
            var invoice = invoiceNo is null ? "Your e-invoice will be issued shortly; we will keep it on your order page." : $"E-invoice number: {invoiceNo}";
            var link = lookupLink is null ? string.Empty : $"\nTrack this order:\n{lookupLink}\n";
            return new(to, $"[{clubName}] Payment received for order {orderNo}",
                $"Hi {name},\n\nWe received your payment of NT${total:N0} for order {orderNo}. We will start preparing your items.\n{invoice}\n{link}",
                "shop_payment_completed");
        }

        var invoiceZh = invoiceNo is null ? "電子發票將於開立後顯示在你的訂單頁面。" : $"電子發票號碼：{invoiceNo}";
        var linkZh = lookupLink is null ? string.Empty : $"\n查詢訂單進度：\n{lookupLink}\n";
        return new(to, $"【{clubName}】訂單 {orderNo} 已收到付款",
            $"{name} 你好，\n\n我們已收到訂單 {orderNo} 的付款 NT${total:N0}，接下來會開始備貨。\n{invoiceZh}\n{linkZh}",
            "shop_payment_completed");
    }

    private static string Items(IReadOnlyList<Line> lines, string lang)
        => string.Join("\n", lines.Select(l => lang == "en" ? $"- {l.Name} x {l.Quantity}  NT${l.LineTotal:N0}" : $"- {l.Name} × {l.Quantity}　NT${l.LineTotal:N0}"));
}
