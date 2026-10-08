namespace Tcrfc.Api.CharityPlatform.Common;

/// <summary>
/// 後台給人看的中文標籤（CSV 匯出共用）。詞彙以 apps/admin-charity 畫面為準
/// （DonationStatusTag.vue、InvoiceStatusTag.vue、InvoiceListView.vue），不自創新詞；
/// 新增匯出欄位時一律走這裡，不直接輸出狀態代碼。輸入為 null 回 null（沒有憑證的捐款單，該欄留空）。
/// 未知代碼原樣輸出，讓資料異常看得見，而不是被預設值掩蓋。
/// </summary>
public static class CharityLabels
{
    public static string? DonationStatus(string? code) => code switch
    {
        null or "" => null,
        Common.DonationStatus.Created => "已建立",
        Common.DonationStatus.Pending => "處理中",
        Common.DonationStatus.Paid => "已完成",
        Common.DonationStatus.Failed => "付款失敗",
        Common.DonationStatus.Expired => "已逾時",
        Common.DonationStatus.Refunded => "已退款",
        _ => code,
    };

    public static string? InvoiceMode(string? code) => code switch
    {
        null or "" => null,
        InvoiceModes.B2cInvoice => "電子發票",
        InvoiceModes.DonationReceipt => "捐贈收據",
        _ => code,
    };

    /// <summary>開立狀態（<c>donation_invoices.issue_status</c>）。</summary>
    public static string? InvoiceIssueStatus(string? code) => code switch
    {
        null or "" => null,
        "issued" => "已開立",
        "failed" => "開立失敗",
        "pending" => "待開立",
        _ => code,
    };

    /// <summary>作廢／折讓狀態（<c>donation_invoices.void_status</c>）；無異動顯示「正常」。</summary>
    public static string? InvoiceVoidStatus(string? code) => code switch
    {
        null or "" => null,
        "none" => "正常",
        "voided" => "已作廢",
        "allowance" => "已折讓",
        _ => code,
    };
}
