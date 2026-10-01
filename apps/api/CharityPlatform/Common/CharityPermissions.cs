namespace Tcrfc.Api.CharityPlatform.Common;

/// <summary>
/// 慈善後台權限碼常數（唯一來源，端點與測試都引用這裡，不得散落字串字面值）。
/// 值對應 <c>db/seed/generate-charity-seed-sql.py</c> 的 <c>PERMISSIONS</c>（<c>permissions.code</c>）。
/// 🔴 介面不得顯示這些代碼（規劃書 §4.0），它們只給程式判斷。
/// </summary>
public static class CharityPermissions
{
    // N1 店家管理與 QR Code
    public const string StoreView = "n1.donation_store.view";
    public const string StoreManage = "n1.donation_store.manage";
    /// <summary>🔴 分潤百分比設定，需獨立授權且每次寫稽核（規劃書 §10、§11.2）。</summary>
    public const string StoreSharePct = "n1.donation_store.share_pct";
    public const string StoreExport = "n1.donation_store.export";

    // N2 捐款項目管理
    public const string ProjectView = "n2.donation_project.view";
    public const string ProjectManage = "n2.donation_project.manage";
    public const string ProjectPublish = "n2.donation_project.publish";
    /// <summary>🔴 分潤百分比設定，需獨立授權且每次寫稽核。</summary>
    public const string ProjectSharePct = "n2.donation_project.share_pct";

    // N3 捐款紀錄
    public const string DonationView = "n3.donation.view";
    /// <summary>🔴 檢視捐款人個資明文（姓名／Email／身分證字號／地址）。僅系統管理員與客服／行政。</summary>
    public const string DonationReveal = "n3.donation.reveal";
    /// <summary>🔴 人工退款，需獨立授權且每次寫稽核；<c>sysadmin_only</c>（見種子）。</summary>
    public const string DonationRefund = "n3.donation.refund";
    /// <summary>🔴 含個資的明細匯出，需額外授權、記錄用途備註並寫稽核。</summary>
    public const string DonationExport = "n3.donation.export";

    /// <summary>重新向金流確認「結果未知」的付款（異常佇列的處理動作）。僅系統管理員與客服／行政。</summary>
    public const string DonationRecheckPayment = "n3.donation.recheck_payment";

    // N5 發票與收據（N3 的「重新開立發票」操作用）
    public const string InvoiceView = "n5.donation_invoice.view";
    public const string InvoiceIssue = "n5.donation_invoice.issue";
}

/// <summary>稽核紀錄的動作代碼（<c>audit_logs.action</c>，上限 32 字元）。</summary>
public static class CharityAuditActions
{
    public const string DonationRefund = "donation.refund";
    public const string DonationRevealPii = "donation.reveal_pii";
    public const string DonationExport = "donation.export_pii";
    public const string DonationResendThanks = "donation.resend_thanks";
    public const string DonationReissueInvoice = "donation.reissue_invoice";
    public const string DonationRecheckPayment = "donation.recheck_payment";
    public const string StoreSharePctSet = "store.share_pct_set";
    public const string ProjectSharePctSet = "project.share_pct_set";
    public const string StoreSlugRegenerate = "store.slug_regenerate";
    public const string StoreQrExport = "store.qr_export";
}

public static class CharityAuditTargets
{
    public const string Donation = "donation";
    public const string Store = "donation_store";
    public const string Project = "donation_project";
}
