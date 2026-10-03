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

    /// <summary>作廢或折讓憑證（N5）。每次操作記錄經辦人與原因（規劃書 §6.5）。</summary>
    public const string InvoiceVoid = "n5.donation_invoice.void";

    /// <summary>N3「隱藏於徵信名單」（規劃書 §6.3）：改變公開頁面的內容，所以獨立一個碼並寫稽核。</summary>
    public const string DonationHideCredit = "n3.donation.hide_credit";

    // N4 回饋金結算
    public const string SettlementView = "n4.settlement.view";

    /// <summary>執行結算（產生草稿、重算、確認結算、刪除草稿）。</summary>
    public const string SettlementExecute = "n4.settlement.execute";

    public const string SettlementExport = "n4.settlement.export";

    /// <summary>🔴 登記「已付款」。與 <see cref="SettlementExecute"/> 分開，讓「核對結算的人」與「登記匯款的人」可以是不同的人
    /// （規劃書 §10：已付款登記需與執行匯款者分離；docs/16 §4.2：以權限碼分離，不落資料表）。</summary>
    public const string SettlementMarkPaid = "n4.settlement.mark_paid";

    // N6 捐款報表
    public const string ReportView = "n6.report.view";
    public const string ReportExport = "n6.report.export";

    // N7 站台設定
    public const string SettingView = "n7.setting.view";
    public const string SettingManage = "n7.setting.manage";

    /// <summary>🔴 金流與發票憑證、環境切換。僅系統管理員（種子 <c>sysadmin_only</c>）。</summary>
    public const string PaymentChannelManage = "n7.payment_channel.manage";

    /// <summary>🔴 查詢稽核紀錄。僅系統管理員（種子 <c>sysadmin_only</c>）：稽核本身含操作者與來源 IP。</summary>
    public const string AuditLogView = "n7.audit_log.view";

    // 後台帳號與角色管理（規劃書 §10「完整的後台帳號權限表」）。比照主站 system.account.*／system.role.*：全部 sysadmin_only。
    public const string AdminAccountView = "n7.admin_account.view";
    public const string AdminAccountManage = "n7.admin_account.manage";
    public const string AdminRoleView = "n7.admin_role.view";
    public const string AdminRoleManage = "n7.admin_role.manage";
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
    public const string StoreImport = "store.import_csv";

    // CH-4／CH-5 新增
    public const string DonationCreditVisibility = "donation.credit_visibility";
    public const string SettlementRun = "settlement.run";
    public const string SettlementRecalculate = "settlement.recalculate";
    public const string SettlementSettle = "settlement.settle";
    public const string SettlementMarkPaid = "settlement.mark_paid";
    public const string SettlementDeleteDraft = "settlement.delete_draft";
    public const string ReconciliationRun = "reconciliation.run";
    public const string ReconciliationResolve = "reconciliation.resolve";
    public const string InvoiceManualNumber = "invoice.manual_number";
    public const string InvoiceVoid = "invoice.void";
    public const string InvoiceAllowance = "invoice.allowance";
    public const string InvoiceExport = "invoice.export";
    public const string SettingsUpdate = "setting.update";
    public const string EmailTemplateUpdate = "email_template.update";
    public const string PaymentCredentialSet = "payment_channel.credential_set";
    public const string PaymentEnvironmentSwitch = "payment_channel.env_switch";
    public const string ProjectContentUpdate = "project.content_update";

    // 後台帳號與角色管理（動作代碼上限 32 字元）
    public const string AdminAccountCreate = "admin_account.create";
    public const string AdminAccountUpdate = "admin_account.update";
    public const string AdminAccountStatus = "admin_account.status";
    public const string AdminAccountResetPassword = "admin_account.reset_password";
    public const string AdminAccountResetTotp = "admin_account.reset_totp";
    public const string AdminRoleCreate = "admin_role.create";
    public const string AdminRoleUpdate = "admin_role.update";
    public const string AdminRoleDelete = "admin_role.delete";
    public const string AdminRolePermissions = "admin_role.permissions";

    /// <summary>動作代碼 → 日常中文（稽核紀錄查詢畫面的篩選與顯示用；介面不得顯示代碼本身，規劃書 §4.0）。</summary>
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [DonationRefund] = "人工退款",
        [DonationRevealPii] = "檢視捐款人個資明文",
        [DonationExport] = "匯出含個資明細",
        [DonationResendThanks] = "重寄感謝信",
        [DonationReissueInvoice] = "重新開立憑證",
        [DonationRecheckPayment] = "重新確認付款結果",
        [StoreSharePctSet] = "設定店家分潤",
        [ProjectSharePctSet] = "設定項目分潤",
        [StoreSlugRegenerate] = "重新產生店家網址",
        [StoreQrExport] = "批次匯出店家 QR",
        [StoreImport] = "批次匯入店家",
        [DonationCreditVisibility] = "調整徵信名單顯示",
        [SettlementRun] = "產生結算單",
        [SettlementRecalculate] = "重算結算單",
        [SettlementSettle] = "確認結算",
        [SettlementMarkPaid] = "登記已付款",
        [SettlementDeleteDraft] = "刪除結算草稿",
        [ReconciliationRun] = "手動執行對帳",
        [ReconciliationResolve] = "處理對帳差異",
        [InvoiceManualNumber] = "手動填入憑證號碼",
        [InvoiceVoid] = "作廢憑證",
        [InvoiceAllowance] = "折讓憑證",
        [InvoiceExport] = "匯出憑證明細",
        [SettingsUpdate] = "更新站台設定",
        [EmailTemplateUpdate] = "更新系統信樣板",
        [PaymentCredentialSet] = "更新金流或發票憑證",
        [PaymentEnvironmentSwitch] = "切換金流或發票環境",
        [ProjectContentUpdate] = "編輯項目內文",
        [AdminAccountCreate] = "建立後台帳號",
        [AdminAccountUpdate] = "更新後台帳號",
        [AdminAccountStatus] = "啟用或停用後台帳號",
        [AdminAccountResetPassword] = "重設後台帳號密碼",
        [AdminAccountResetTotp] = "重設後台帳號兩階段驗證",
        [AdminRoleCreate] = "建立角色",
        [AdminRoleUpdate] = "更新角色",
        [AdminRoleDelete] = "刪除角色",
        [AdminRolePermissions] = "調整角色權限",
    };
}

public static class CharityAuditTargets
{
    public const string Donation = "donation";
    public const string Store = "donation_store";
    public const string Project = "donation_project";
    public const string Settlement = "settlement";
    public const string Reconciliation = "reconciliation_run";
    public const string ReconciliationDiscrepancy = "reconciliation_discrepancy";
    public const string Invoice = "donation_invoice";
    public const string Setting = "setting";
    public const string EmailTemplate = "email_template";
    public const string PaymentChannel = "payment_channel";
    public const string AdminAccount = "admin_account";
    public const string AdminRole = "admin_role";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Donation] = "捐款",
        [Store] = "捐款合作店家",
        [Project] = "捐款項目",
        [Settlement] = "結算單",
        [Reconciliation] = "對帳批次",
        [ReconciliationDiscrepancy] = "對帳差異",
        [Invoice] = "憑證",
        [Setting] = "站台設定",
        [EmailTemplate] = "系統信樣板",
        [PaymentChannel] = "金流與發票憑證",
        [AdminAccount] = "後台帳號",
        [AdminRole] = "角色",
    };
}
