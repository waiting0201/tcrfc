// app/types/charity.ts — 慈善捐款平台公開 API 的回應形狀（camelCase）。
// 對照 apps/api `CharityPlatform/Public/CharityPublicDtos.cs`（契約全文見 apps/api/README.md「慈善 CH-2／CH-3」）。
// 🔴 公開回應絕不含分潤百分比、金流交易識別碼、募款進度；本檔也不得出現這些欄位。

export type DonationStatus = 'created' | 'pending' | 'paid' | 'failed' | 'expired' | 'refunded'
export type InvoiceMode = 'b2c_invoice' | 'donation_receipt'
export type InvoiceStatus = 'pending' | 'issued' | 'failed'

export interface PublicSettings {
  homeIntro: string | null
  thankYouTemplate: string | null
  notice: string | null
  privacyPolicy: string | null
  defaultMinAmount: number
  defaultMaxAmount: number
  creditListEnabled: boolean
  isFallback: boolean
}

export interface PublicStore {
  slug: string
  name: string
  logoUrl: string | null
  logoAlt: string | null
  isFallback: boolean
}

export interface PublicStoreLanding {
  store: PublicStore | null
}

export interface PublicProjectCard {
  slug: string
  name: string
  oneLiner: string | null
  coverUrl: string | null
  coverAlt: string | null
  sortOrder: number
  isFallback: boolean
}

export interface PublicProjectDetail {
  slug: string
  name: string
  oneLiner: string | null
  /** 區塊編輯器輸出的 JSON，原樣回傳；由 BlockContent.vue 以純文字渲染。 */
  description: unknown
  fundUsage: string | null
  coverUrl: string | null
  coverAlt: string | null
  amountOptions: number[]
  minAmount: number
  maxAmount: number
  invoiceMode: InvoiceMode
  charityName: string | null
  charityProgramName: string | null
  charityProgramRefCode: string | null
  isFallback: boolean
}

export interface DonationInvoiceInput {
  type?: 'mobile_carrier' | 'love_code' | 'tax_id'
  mobileCarrier?: string
  loveCode?: string
  taxId?: string
  invoiceTitle?: string
  receiptTitle?: string
  nationalId?: string
  address?: string
  isAnnualSummary?: boolean
}

export interface CreateDonationRequest {
  projectSlug: string
  storeSlug?: string
  amount: number
  donorName: string
  donorEmail: string
  isAnonymous: boolean
  consentPrivacy: boolean
  invoice: DonationInvoiceInput
  lang: 'zh' | 'en'
  turnstileToken?: string
}

export interface CreateDonationResponse {
  orderNo: string
  status: DonationStatus
  amount: number
  created: boolean
}

export interface StartPaymentResponse {
  orderNo: string
  status: DonationStatus
  paymentUrl: string
}

export interface PublicDonationResult {
  orderNo: string
  status: DonationStatus
  amount: number
  createdAt: string
  paidAt: string | null
  projectSlug: string
  projectName: string
  storeName: string | null
  donorNameMasked: string
  donorEmailMasked: string
  invoiceMode: InvoiceMode
  invoiceStatus: InvoiceStatus
  invoiceNo: string | null
  processing: boolean
  canRetry: boolean
}

/** `GET /credit-list`：只有姓名（不含金額、Email、店家、單號、時間）。`enabled=false` 時 `names` 為空。 */
export interface PublicCreditList {
  enabled: boolean
  names: string[]
  page: number
  pageSize: number
  totalCount: number
}

/** `GET /impact`：已上架項目依關聯的慈善計畫分組。名稱是快照；`clubSiteUrl` 為 null 代表不顯示導回連結。 */
export interface PublicImpactProject {
  slug: string
  name: string
  oneLiner: string | null
  coverUrl: string | null
  coverAlt: string | null
}

export interface PublicImpactProgram {
  programRefCode: string | null
  programName: string | null
  charityName: string | null
  projects: PublicImpactProject[]
}

export interface PublicImpact {
  clubSiteUrl: string | null
  programs: PublicImpactProgram[]
  isFallback: boolean
}
