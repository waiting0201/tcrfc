/**
 * db/seed/charity-fixtures.json 的原始形狀（未加工）。
 *
 * 這份 JSON 是 db/seed/generate-charity-seed-sql.py 的衍生物（見該檔與
 * db/seed/emit-charity-fixtures.py 檔頭），欄位大多是 Python tuple 轉出來的位置陣列，
 * 不是具名物件——這裡先照原始形狀定型別，語意化的欄位名稱與關聯查找放在
 * src/data/fixtures.ts 做一次轉換，畫面元件一律只用轉換後的型別，不直接碰這份原始形狀。
 */

/** [code, nameZh, nameEn, isSystem] */
export type RawRole = [string, string, string, boolean]

/** [code, submoduleCode, domain, action, nameZh, nameEn, isRestricted, sysadminOnly] */
export type RawPermission = [string, string, string, string, string, string, boolean, boolean]

/** [username, email, displayName, roleCode, isSuperAdmin] */
export type RawAdminUser = [string, string | null, string, string, boolean]

/** [refCode, name]（協會端唯讀複本，僅供本機開發顯示） */
export type RawCharityRef = [string, string]

/** [refCode, charityRefCode, name] */
export type RawCharityProgramRef = [string, string, string]

export type DonationStoreStatus = 'active' | 'inactive'

export interface RawStore {
  key: string
  name_zh: string
  name_en: string
  category: string
  address: string
  contact_name: string
  contact_phone: string
  share_pct: string
  has_logo: boolean
  status: DonationStoreStatus
  start_on: string
  end_on: string | null
}

export type InvoiceMode = 'b2c_invoice' | 'donation_receipt'
export type DonationProjectStatus = 'draft' | 'published'

export interface RawProject {
  key: string
  name_zh: string
  name_en: string
  one_liner_zh: string
  one_liner_en: string
  min_amount: number
  max_amount: number
  share_pct: string
  invoice_mode: InvoiceMode
  charity_ref: string
  program_ref: string
  status: DonationProjectStatus
  sort_order: number
  fund_usage_zh: string
  fund_usage_en: string
}

export type DonationStatus = 'created' | 'pending' | 'paid' | 'failed' | 'expired' | 'refunded'

export interface RawDonation {
  key: string
  order_no: string
  store: string | null
  project: string
  amount: number
  status: DonationStatus
  donor_name: string
  donor_email: string
  is_anonymous: boolean
  created_at: string
  paid_at: string | null
  refund_reason?: string
  refunded_by?: string
}

/** [storeAmount, projectAmount, associationAmount]（成立時快照，事後改設定不追溯） */
export type RawDonationSplit = [number, number, number]

export type PaymentStatus = 'requested' | 'confirmed' | 'failed' | 'cancelled'

export interface RawPayment {
  status: PaymentStatus
  requested_at: string
  confirmed_at: string | null
}

export type InvoiceIssueStatus = 'pending' | 'issued' | 'failed'
export type InvoiceVoidStatus = 'none' | 'voided' | 'allowance'

export interface RawInvoice {
  invoice_type: InvoiceMode
  invoice_no: string | null
  issued_at: string | null
  issue_status: InvoiceIssueStatus
  void_status: InvoiceVoidStatus
  carrier_type?: string
  carrier_id_encrypted?: string
  tax_id?: string
  invoice_title?: string
  national_id_encrypted?: string
  receipt_address?: string
  receipt_title?: string
  void_reason?: string
  voided_by?: string
  is_annual_summary?: boolean
}

export type SettlementPayeeType = 'store' | 'project'
export type SettlementStatus = 'pending' | 'settled' | 'paid'

/** [payeeType, payeeKey, [periodStart, periodEnd], [[donationKey, isClawback]...], status, remittedOn, remitMethod, remitNote] */
export type RawSettlementPlanRow = [
  SettlementPayeeType,
  string,
  [string, string],
  Array<[string, boolean]>,
  SettlementStatus,
  string | null,
  string | null,
  string | null,
]

/** [key, value]（純值設定，如金額上下限預設值） */
export type RawSetting = [string, string]

/** [key, zh, en]（雙語文案設定） */
export type RawSettingI18n = [string, string, string]

/** [code, subjectZh, bodyZh, subjectEn, bodyEn] */
export type RawEmailTemplate = [string, string, string, string, string]

/** [templateCode, donationKey, status, sentAt] */
export type RawEmailLog = [string, string, 'sent' | 'failed', string]

/** 對帳批次（db/seed 的 RECONCILIATION_RUNS） */
export interface RawReconciliationRun {
  key: string
  run_on: string
  source: string
  compared_count: number
  matched_count: number
  discrepancy_count: number
  status: string
}

/** 對帳差異（db/seed 的 RECONCILIATION_DISCREPANCIES），三種類型見 docs/16 §2.1b */
export interface RawReconciliationDiscrepancy {
  run_key: string
  discrepancy_type: string
  donation_key: string | null
  donation_order_no: string | null
  gateway_transaction_id: string | null
  site_amount: number | null
  gateway_amount: number | null
  resolution_status: string
  resolved_by: string | null
  resolve_note: string | null
}

/** 稽核紀錄（db/seed 的 AUDIT_LOGS），勸募法遵要求，append-only */
export interface RawAuditLog {
  admin_username: string
  occurred_at: string
  action: string
  target_type: string
  target_donation_order_no: string | null
  target_project_key: string | null
  change_summary: string
  purpose_note: string | null
  source_ip: string
}

export interface CharityFixturesFile {
  _comment: string
  _source: string
  roles: RawRole[]
  permissions: RawPermission[]
  rolePermissionMap: Record<string, string[]>
  adminUsers: RawAdminUser[]
  charityRefs: RawCharityRef[]
  charityProgramRefs: RawCharityProgramRef[]
  stores: RawStore[]
  storeSlugs: Record<string, string>
  projects: RawProject[]
  projectSlugs: Record<string, string>
  amountOptions: Record<string, number[]>
  donations: RawDonation[]
  donationSplits: Record<string, RawDonationSplit>
  payments: Record<string, RawPayment>
  invoices: Record<string, RawInvoice>
  settlementPlan: RawSettlementPlanRow[]
  settings: RawSetting[]
  settingsI18n: RawSettingI18n[]
  emailTemplates: RawEmailTemplate[]
  emailLogs: RawEmailLog[]
  reconciliationRuns: RawReconciliationRun[]
  reconciliationDiscrepancies: RawReconciliationDiscrepancy[]
  auditLogs: RawAuditLog[]
}
