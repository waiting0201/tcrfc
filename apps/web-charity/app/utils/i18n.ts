// app/utils/i18n.ts — 慈善捐款平台前台的介面文字字典。
//
// 本專案刻意不裝 @nuxtjs/i18n（規模只有約 10 個路由、不需要複雜的 lazy-load／SEO 中介層），
// 用一個純物件字典 + [lang] 動態路由區段就能滿足「繁中預設＋英文」的雙語需求
// （規劃書 §2.4：所有前台可見內容皆須有 zh/en 雙欄位，英文可空但欄位必須存在）。
//
// ⚠️ 協會的正式英文全名尚未確認（STATUS.md B-7，docs/22-charity-ui.md §6 第 5 項），
// ⛔ 不得自行決定或翻譯協會的英文名稱——英文版一律用通用描述詞「the Association」，
// 不創造任何看起來像正式定案的英文專有名詞。這是 docs/22 §6 第 5 項本身給的做法，不是本檔新發明。
export type Lang = 'zh' | 'en'

export function normalizeLang(value: unknown): Lang {
  return value === 'en' ? 'en' : 'zh'
}

/** 內容型別的 zh/en 雙欄位，英文缺漏時 fallback 顯示繁中並標示尚無此語系版本（規劃書 §2.4）。 */
export function pickText(lang: Lang, zh: string, en: string | null | undefined): { text: string, isFallback: boolean } {
  if (lang === 'zh') return { text: zh, isFallback: false }
  if (en && en.trim().length > 0) return { text: en, isFallback: false }
  return { text: zh, isFallback: true }
}

export const dict = {
  zh: {
    associationName: '台灣足球策略發展協會',
    associationNameAria: '台灣足球策略發展協會',
    fallbackNotice: '本頁尚無此語系版本，暫以繁體中文顯示。',
    skipToContent: '跳到主要內容',
    langSwitch: { zh: '中文', en: 'English' },

    home: {
      title: '公益捐款',
      introHeadingWithStore: '感謝 {store} 與{association}一起做公益',
      introHeadingPlain: '與{association}一起做公益',
      introFallback: '每一筆捐款，都會用在球場整建、獎學金與偏鄉足球推廣。',
      projectsHeading: '選擇您想支持的計畫',
      storeEndedNotice: '這家店家已暫停與本平台的合作，您仍可從下方選擇項目捐款。',
      learnMoreAndDonate: '了解並捐款',
      noProjects: '目前沒有開放中的捐款項目，請稍後再回來看看。',
    },

    trust: {
      heading: '關於{association}',
      registrationNo: '立案字號',
      establishedOn: '成立日期',
      address: '會址',
      taxId: '統一編號',
      taxIdPending: '洽詢中（尚未取得）',
      fundraisingPermit: '勸募許可字號',
      fundraisingPermitPending: '洽詢中（申請中）',
    },

    project: {
      storeAttribution: '您正透過 {store} 捐款',
      fundUsageHeading: '款項用途',
      relatedProgramHeading: '相關的慈善計畫',
      relatedProgramHint: '以下計畫資料來自官網公益專區的快照，如需查詢最新進度請至官網查看',
      viewOnMainSite: '前往官網查看公益專區 →',
      faqHeading: '常見問題',
      faqInvoiceQ: '捐款後會拿到什麼憑證？',
      faqInvoiceA: '依項目設定，您會收到電子發票或捐贈收據，開立後將寄送至您留下的 Email。',
      faqFundQ: '我的捐款會用在哪裡？',
      faqFundA: '請參考上方「款項用途」說明；各項目的實際運用彙整由協會定期於後台掌握。',
      faqRefundQ: '捐款後可以退款嗎？',
      faqRefundA: '捐款一經完成，原則上不受理退款申請，詳見捐款須知。',
      minMaxHint: '本項目單筆捐款金額介於 NT${min} 至 NT${max} 之間',
    },

    form: {
      heading: '捐款表單',
      amountHeading: '選擇捐款金額',
      customAmountLabel: '其他金額',
      customAmountSuffix: '元',
      donorName: '姓名',
      donorEmail: 'Email',
      emailHint: '用於寄送感謝信與發票通知',
      identityHeading: '具名或匿名',
      named: '具名（列於捐款徵信名單，僅顯示姓名）',
      anonymous: '匿名',
      voucherHeading: '憑證資訊',
      invoiceTypeLabel: '發票類型',
      invoiceTypeMobileCarrier: '手機條碼載具',
      invoiceTypeDonation: '捐贈發票',
      invoiceTypeTaxId: '統一編號',
      mobileCarrierLabel: '手機條碼',
      mobileCarrierHint: '格式為斜線加 7 碼英數，例如 /ABC1234',
      loveCodeLabel: '愛心碼',
      taxIdLabel: '統一編號',
      invoiceTitleLabel: '發票抬頭',
      receiptTitleLabel: '收據抬頭',
      nationalIdLabel: '身分證字號（選填）',
      nationalIdHint: '供列舉扣除用，將加密儲存，送出後不會在畫面上完整顯示',
      addressLabel: '通訊地址（選填）',
      addressHint: '僅供收據內容與後續聯繫，不做實體寄送',
      consentLabel: '我已閱讀並同意',
      privacyPolicyLink: '《隱私權政策》',
      submit: '以 LINE Pay 捐款 NT${amount}',
      submitNoAmount: '請先選擇捐款金額',
      errorRequired: '這個欄位必須填寫',
      errorEmail: 'Email 格式不正確，請確認有沒有打錯',
      errorAmountRange: '捐款金額需介於 NT${min} 至 NT${max} 之間',
      errorMobileCarrier: '手機條碼格式不正確，需為斜線加 7 碼英數',
      errorTaxId: '統一編號檢查碼不符，請確認號碼',
      errorConsent: '請先閱讀並同意隱私權政策',
    },

    pay: {
      heading: '正在為您導向付款頁面…',
      orderNo: '訂單編號',
      amount: '捐款金額',
      fallbackHint: '沒有自動跳轉？',
      retryLink: '點此重試',
      mockNotice: '本頁為 mockup 示範用的模擬轉場，尚未串接 LINE Pay 正式金流（見 docs/22-charity-ui.md §2.6）。',
    },

    result: {
      successTitle: '感謝您的捐款！',
      pendingTitle: '您的捐款正在確認中，請勿關閉此頁面',
      pendingHint: '這通常在數秒內完成',
      pendingTimeoutHint: '確認時間較長，我們會將結果寄至您的 Email；您也可以稍後用此連結查詢：',
      failedTitle: '這筆捐款尚未完成',
      failedReason: '可能原因：您取消了付款，或連線逾時',
      retryPayment: '重新嘗試付款',
      contactIssue: '仍有問題？請聯絡',
      orderNo: '訂單編號',
      amount: '捐款金額',
      project: '捐款項目',
      voucher: '發票／收據',
      voucherIssued: '已開立，將寄送至您的信箱',
      voucherPending: '開立中，完成後將寄送至您的信箱',
      voucherFailed: '開立失敗，協會將盡快人工補開',
      voucherVoided: '已作廢',
      voucherAllowance: '已折讓',
      share: '分享此頁',
      backHome: '返回首頁',
      notFound: '找不到這筆捐款紀錄，請確認連結是否正確。',
    },

    donors: {
      heading: '捐款徵信名單',
      hint: '僅列出選擇「具名」的捐款人姓名，不顯示金額、Email 或店家資訊。',
      filterProject: '依項目篩選',
      filterAll: '全部項目',
      empty: '目前沒有具名捐款紀錄。',
      donatedTo: '捐給',
    },

    privacy: {
      heading: '隱私權政策',
      placeholderNotice: '以下為開發測試用占位文字，正式內容待客戶與協會確認後更新。',
    },

    terms: {
      heading: '捐款須知',
      noRefundHeading: '關於退款',
      placeholderNotice: '以下為開發測試用占位文字，正式內容待客戶與協會確認後更新。',
    },

    footer: {
      privacy: '隱私權政策',
      terms: '捐款須知',
      donors: '捐款徵信名單',
      contact: '聯絡方式',
      contactValue: '如有任何問題，請透過官方網站的聯絡表單與{association}聯繫。',
    },

    status: {
      created: '已建立',
      pending: '處理中',
      paid: '已完成',
      failed: '付款失敗',
      expired: '已逾時',
      refunded: '已退款',
    },
  },

  en: {
    associationName: 'the Association',
    associationNameAria: 'the Association (Taiwan Football Strategic Development Association)',
    fallbackNotice: 'This page has no English version yet; showing Traditional Chinese content.',
    skipToContent: 'Skip to main content',
    langSwitch: { zh: '中文', en: 'English' },

    home: {
      title: 'Donate',
      introHeadingWithStore: 'Thanks to {store} for supporting {association} together',
      introHeadingPlain: 'Support {association} with us',
      introFallback: 'Every donation goes toward pitch renovation, scholarships, and grassroots football.',
      projectsHeading: 'Choose a program to support',
      storeEndedNotice: 'This store has ended its partnership with this platform. You can still choose a program below.',
      learnMoreAndDonate: 'Learn more and donate',
      noProjects: 'No donation programs are open right now. Please check back later.',
    },

    trust: {
      heading: 'About {association}',
      registrationNo: 'Registration number',
      establishedOn: 'Established on',
      address: 'Registered address',
      taxId: 'Tax ID',
      taxIdPending: 'Pending (not yet issued)',
      fundraisingPermit: 'Fundraising permit number',
      fundraisingPermitPending: 'Pending (application in progress)',
    },

    project: {
      storeAttribution: 'You are donating through {store}',
      fundUsageHeading: 'How funds are used',
      relatedProgramHeading: 'Related charity program',
      relatedProgramHint: 'This is a read-only snapshot from the main site\'s charity section. Visit the main site for the latest progress.',
      viewOnMainSite: 'View on the official site →',
      faqHeading: 'FAQ',
      faqInvoiceQ: 'What proof of donation will I receive?',
      faqInvoiceA: 'Depending on the program, you will receive an e-invoice or a donation receipt by email once issued.',
      faqFundQ: 'How is my donation used?',
      faqFundA: 'See "How funds are used" above; the Association reviews actual usage periodically.',
      faqRefundQ: 'Can I get a refund?',
      faqRefundA: 'Donations are final and not eligible for refund once completed. See the donation notice page.',
      minMaxHint: 'This program accepts single donations between NT${min} and NT${max}',
    },

    form: {
      heading: 'Donation form',
      amountHeading: 'Choose an amount',
      customAmountLabel: 'Other amount',
      customAmountSuffix: 'NTD',
      donorName: 'Name',
      donorEmail: 'Email',
      emailHint: 'Used to send your thank-you message and invoice notice',
      identityHeading: 'Named or anonymous',
      named: 'Named (listed by name only in the donor list)',
      anonymous: 'Anonymous',
      voucherHeading: 'Invoice / receipt details',
      invoiceTypeLabel: 'Invoice type',
      invoiceTypeMobileCarrier: 'Mobile barcode carrier',
      invoiceTypeDonation: 'Donation invoice',
      invoiceTypeTaxId: 'Tax ID',
      mobileCarrierLabel: 'Mobile barcode',
      mobileCarrierHint: 'A slash followed by 7 alphanumeric characters, e.g. /ABC1234',
      loveCodeLabel: 'Love code',
      taxIdLabel: 'Tax ID',
      invoiceTitleLabel: 'Invoice title',
      receiptTitleLabel: 'Receipt title',
      nationalIdLabel: 'National ID (optional)',
      nationalIdHint: 'Used for tax deduction purposes, stored encrypted, never shown in full afterward',
      addressLabel: 'Mailing address (optional)',
      addressHint: 'Used only for the receipt content and follow-up contact; nothing is physically mailed',
      consentLabel: 'I have read and agree to the',
      privacyPolicyLink: 'Privacy Policy',
      submit: 'Donate NT${amount} via LINE Pay',
      submitNoAmount: 'Please choose an amount first',
      errorRequired: 'This field is required',
      errorEmail: 'This email address doesn\'t look right, please double-check it',
      errorAmountRange: 'The donation amount must be between NT${min} and NT${max}',
      errorMobileCarrier: 'Invalid mobile barcode format: a slash followed by 7 alphanumeric characters',
      errorTaxId: 'This tax ID checksum doesn\'t match, please check the number',
      errorConsent: 'Please read and agree to the privacy policy first',
    },

    pay: {
      heading: 'Redirecting you to the payment page…',
      orderNo: 'Order number',
      amount: 'Donation amount',
      fallbackHint: 'Not redirected automatically?',
      retryLink: 'Click to retry',
      mockNotice: 'This is a mockup transition screen; LINE Pay is not connected yet (see docs/22-charity-ui.md §2.6).',
    },

    result: {
      successTitle: 'Thank you for your donation!',
      pendingTitle: 'Your donation is being confirmed, please keep this page open',
      pendingHint: 'This usually completes within seconds',
      pendingTimeoutHint: 'Confirmation is taking longer than usual. We will email you the result; you can also check back later at:',
      failedTitle: 'This donation was not completed',
      failedReason: 'Possible reasons: you cancelled the payment, or the connection timed out',
      retryPayment: 'Try payment again',
      contactIssue: 'Still having trouble? Contact',
      orderNo: 'Order number',
      amount: 'Donation amount',
      project: 'Program',
      voucher: 'Invoice / receipt',
      voucherIssued: 'Issued, will be emailed to you',
      voucherPending: 'Being issued, will be emailed once done',
      voucherFailed: 'Issuance failed, the Association will follow up manually',
      voucherVoided: 'Voided',
      voucherAllowance: 'Allowance issued',
      share: 'Share this page',
      backHome: 'Back to home',
      notFound: 'This donation record could not be found. Please check the link.',
    },

    donors: {
      heading: 'Donor recognition list',
      hint: 'Only names of donors who chose to be listed are shown here. No amount, email, or store is displayed.',
      filterProject: 'Filter by program',
      filterAll: 'All programs',
      empty: 'No named donations yet.',
      donatedTo: 'Donated to',
    },

    privacy: {
      heading: 'Privacy Policy',
      placeholderNotice: 'The text below is a development placeholder pending client/association confirmation.',
    },

    terms: {
      heading: 'Donation Notice',
      noRefundHeading: 'About refunds',
      placeholderNotice: 'The text below is a development placeholder pending client/association confirmation.',
    },

    footer: {
      privacy: 'Privacy Policy',
      terms: 'Donation Notice',
      donors: 'Donor Recognition List',
      contact: 'Contact',
      contactValue: 'For any questions, please contact {association} via the contact form on the official site.',
    },

    status: {
      created: 'Created',
      pending: 'Processing',
      paid: 'Completed',
      failed: 'Payment failed',
      expired: 'Expired',
      refunded: 'Refunded',
    },
  },
} as const

export function t(lang: Lang) {
  return dict[lang]
}

export function interpolate(template: string, vars: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (_, key: string) => String(vars[key] ?? ''))
}
