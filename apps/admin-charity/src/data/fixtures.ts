/**
 * 假資料的單一入口（任務交付說明「一律用 db/seed/charity-fixtures.json，不要自己寫」）。
 *
 * 實際 import 的是 fixtures/charity-fixtures.json（本專案目錄內），內容是
 * db/seed/charity-fixtures.json 的機器產生鏡射複本，不是另外手寫的資料——理由是
 * docker-compose.yml 把本專案的 Docker build context 定為 apps/admin-charity（docs/20-cicd.md
 * §3），db/seed/ 在那個 build context 之外，容器內建置時讀不到；db/seed/emit-charity-fixtures.py
 * 已擴充成同時輸出三份內容相同的檔案（db/seed／apps/web-charity／apps/admin-charity 各一份），
 * `npm run lint` 的 `lint:fixtures` 會驗證三者一致，改了種子沒重新產生會直接 exit 1。
 * ⛔ 不要手動編輯 fixtures/charity-fixtures.json。
 *
 * 這裡只做兩件事：① 把 fixtures/charity-fixtures.json 的原始（位置陣列）形狀轉成畫面好用的
 * 具名物件與關聯查找 ② 依 docs/10-charity-donation-site.md §4「分潤怎麼算」的公式，從
 * donations／donationSplits 算出結算單的筆數／總額／應付金額——這組數字在 Python 種子腳本裡
 * 是迴圈當場算的，沒有被存回 JSON 常數，所以這裡重新算一次，公式完全比照該檔第 756–774 行。
 * ⛔ 除了這個「照公式算」的衍生計算，不在這裡加任何憑空捏造的欄位或紀錄。
 *
 * ⚠️ 對帳（ReconciliationRun／ReconciliationDiscrepancy）與稽核紀錄（AuditLog）不在這份
 * JSON 裡——db/seed/generate-charity-seed-sql.py 把這兩類資料直接寫成 SQL 字面值（f-string
 * 內嵌），沒有存成模組層級常數，emit-charity-fixtures.py 因此沒有東西可以匯出。這是本次
 * 交付發現的落差，已在回報中列出；對應的過渡資料見 ./reconciliationAudit.ts 檔頭說明。
 */
import { reactive } from 'vue'
import raw from '@fixtures/charity-fixtures.json'
import type {
  CharityFixturesFile,
  DonationProjectStatus,
  DonationStatus,
  DonationStoreStatus,
  InvoiceMode,
  InvoiceIssueStatus,
  InvoiceVoidStatus,
  PaymentStatus,
  SettlementPayeeType,
  SettlementStatus,
} from '@/types/fixtures'

const data = raw as unknown as CharityFixturesFile

export interface Store {
  key: string
  slug: string
  nameZh: string
  nameEn: string
  category: string
  address: string
  contactName: string
  contactPhone: string
  sharePct: number
  hasLogo: boolean
  status: DonationStoreStatus
  startOn: string
  endOn: string | null
  donationCount: number
  donationTotal: number
  payableTotal: number
}

export interface Project {
  key: string
  slug: string
  nameZh: string
  nameEn: string
  oneLinerZh: string
  oneLinerEn: string
  minAmount: number
  maxAmount: number
  sharePct: number
  invoiceMode: InvoiceMode
  charityRefCode: string
  charityRefName: string
  programRefCode: string
  programRefName: string
  status: DonationProjectStatus
  sortOrder: number
  fundUsageZh: string
  fundUsageEn: string
  amountOptions: number[]
  donationCount: number
  donationTotal: number
}

export interface DonationSplit {
  storeAmount: number
  projectAmount: number
  associationAmount: number
}

export interface Payment {
  status: PaymentStatus
  requestedAt: string
  confirmedAt: string | null
}

export interface Invoice {
  invoiceType: InvoiceMode
  invoiceNo: string | null
  issuedAt: string | null
  issueStatus: InvoiceIssueStatus
  voidStatus: InvoiceVoidStatus
  carrierType?: string
  carrierIdEncrypted?: string
  taxId?: string
  invoiceTitle?: string
  nationalIdEncrypted?: string
  receiptAddress?: string
  receiptTitle?: string
  voidReason?: string
  voidedBy?: string
  isAnnualSummary?: boolean
}

export interface Donation {
  key: string
  orderNo: string
  storeKey: string | null
  storeName: string | null
  projectKey: string
  projectName: string
  amount: number
  status: DonationStatus
  donorName: string
  donorEmail: string
  isAnonymous: boolean
  createdAt: string
  paidAt: string | null
  refundReason?: string
  refundedBy?: string
  split?: DonationSplit
  payment?: Payment
  invoice?: Invoice
  /** 具名徵信名單資格：已完成付款且非匿名（docs/22 §2.8） */
  creditListEligible: boolean
}

export interface SettlementLine {
  donationKey: string
  orderNo: string
  amount: number
  isClawback: boolean
  shareAmount: number
}

export interface Settlement {
  id: string
  payeeType: SettlementPayeeType
  payeeKey: string
  payeeName: string
  periodStart: string
  periodEnd: string
  lines: SettlementLine[]
  donationCount: number
  donationTotal: number
  payableAmount: number
  status: SettlementStatus
  remittedOn: string | null
  remitMethod: string | null
  remitNote: string | null
}

export interface Role {
  code: string
  nameZh: string
  nameEn: string
  isSystem: boolean
  permissionCodes: string[]
}

export interface Permission {
  code: string
  submoduleCode: string
  domain: string
  action: string
  nameZh: string
  nameEn: string
  isRestricted: boolean
  sysadminOnly: boolean
}

export interface AdminUser {
  username: string
  displayName: string
  roleCode: string
  roleNameZh: string
  isSuperAdmin: boolean
}

// ── 轉換 ──────────────────────────────────────────────────────────────────

const storesByKey = new Map(data.stores.map((s) => [s.key, s]))
const projectsByKey = new Map(data.projects.map((p) => [p.key, p]))
const donationsByKey = new Map(data.donations.map((d) => [d.key, d]))
const charityRefNameByCode = new Map(data.charityRefs.map(([code, name]) => [code, name]))
const programRefNameByCode = new Map(data.charityProgramRefs.map(([code, , name]) => [code, name]))
const roleNameByCode = new Map(data.roles.map(([code, nameZh]) => [code, nameZh]))

function splitFor(donationKey: string): DonationSplit | undefined {
  const raw = data.donationSplits[donationKey]
  if (!raw) return undefined
  return { storeAmount: raw[0], projectAmount: raw[1], associationAmount: raw[2] }
}

export const DONATIONS: Donation[] = reactive(data.donations.map((d) => {
  const rawPayment = data.payments[d.key]
  const rawInvoice = data.invoices[d.key]
  return {
    key: d.key,
    orderNo: d.order_no,
    storeKey: d.store,
    storeName: d.store ? (storesByKey.get(d.store)?.name_zh ?? d.store) : null,
    projectKey: d.project,
    projectName: projectsByKey.get(d.project)?.name_zh ?? d.project,
    amount: d.amount,
    status: d.status,
    donorName: d.donor_name,
    donorEmail: d.donor_email,
    isAnonymous: d.is_anonymous,
    createdAt: d.created_at,
    paidAt: d.paid_at,
    refundReason: d.refund_reason,
    refundedBy: d.refunded_by,
    split: splitFor(d.key),
    payment: rawPayment
      ? { status: rawPayment.status, requestedAt: rawPayment.requested_at, confirmedAt: rawPayment.confirmed_at }
      : undefined,
    invoice: rawInvoice
      ? {
          invoiceType: rawInvoice.invoice_type,
          invoiceNo: rawInvoice.invoice_no,
          issuedAt: rawInvoice.issued_at,
          issueStatus: rawInvoice.issue_status,
          voidStatus: rawInvoice.void_status,
          carrierType: rawInvoice.carrier_type,
          carrierIdEncrypted: rawInvoice.carrier_id_encrypted,
          taxId: rawInvoice.tax_id,
          invoiceTitle: rawInvoice.invoice_title,
          nationalIdEncrypted: rawInvoice.national_id_encrypted,
          receiptAddress: rawInvoice.receipt_address,
          receiptTitle: rawInvoice.receipt_title,
          voidReason: rawInvoice.void_reason,
          voidedBy: rawInvoice.voided_by,
          isAnnualSummary: rawInvoice.is_annual_summary,
        }
      : undefined,
    creditListEligible: !d.is_anonymous && d.status === 'paid',
  }
}))

const donationByOrderNo = new Map(DONATIONS.map((d) => [d.orderNo, d]))

export const STORES: Store[] = reactive(data.stores.map((s) => {
  const related = DONATIONS.filter((d) => d.storeKey === s.key && d.status === 'paid')
  return {
    key: s.key,
    slug: data.storeSlugs[s.key] ?? '',
    nameZh: s.name_zh,
    nameEn: s.name_en,
    category: s.category,
    address: s.address,
    contactName: s.contact_name,
    contactPhone: s.contact_phone,
    sharePct: Number(s.share_pct),
    hasLogo: s.has_logo,
    status: s.status,
    startOn: s.start_on,
    endOn: s.end_on,
    donationCount: related.length,
    donationTotal: related.reduce((sum, d) => sum + d.amount, 0),
    payableTotal: related.reduce((sum, d) => sum + (d.split?.storeAmount ?? 0), 0),
  }
}))

export const PROJECTS: Project[] = reactive(data.projects.map((p) => {
  const related = DONATIONS.filter((d) => d.projectKey === p.key && d.status === 'paid')
  return {
    key: p.key,
    slug: data.projectSlugs[p.key] ?? '',
    nameZh: p.name_zh,
    nameEn: p.name_en,
    oneLinerZh: p.one_liner_zh,
    oneLinerEn: p.one_liner_en,
    minAmount: p.min_amount,
    maxAmount: p.max_amount,
    sharePct: Number(p.share_pct),
    invoiceMode: p.invoice_mode,
    charityRefCode: p.charity_ref,
    charityRefName: charityRefNameByCode.get(p.charity_ref) ?? p.charity_ref,
    programRefCode: p.program_ref,
    programRefName: programRefNameByCode.get(p.program_ref) ?? p.program_ref,
    status: p.status,
    sortOrder: p.sort_order,
    fundUsageZh: p.fund_usage_zh,
    fundUsageEn: p.fund_usage_en,
    amountOptions: data.amountOptions[p.key] ?? [],
    donationCount: related.length,
    donationTotal: related.reduce((sum, d) => sum + d.amount, 0),
  }
}))

const storeSlugByKey = data.storeSlugs
const projectSlugByKey = data.projectSlugs

function payeeName(type: SettlementPayeeType, key: string): string {
  if (type === 'store') return storesByKey.get(key)?.name_zh ?? key
  return projectsByKey.get(key)?.name_zh ?? key
}

/**
 * 結算單筆數／總額／應付金額：docs/10 §4「分潤怎麼算」公式的實作，逐字比照
 * generate-charity-seed-sql.py 第 756–774 行的計算方式（不是另一套邏輯）。
 */
export const SETTLEMENTS: Settlement[] = reactive(data.settlementPlan.map(
  ([payeeType, payeeKey, [periodStart, periodEnd], lines, status, remittedOn, remitMethod, remitNote], index) => {
    const lineDetails: SettlementLine[] = lines.map(([donationKey, isClawback]) => {
      const donation = donationsByKey.get(donationKey)
      const split = splitFor(donationKey)
      const share = payeeType === 'store' ? (split?.storeAmount ?? 0) : (split?.projectAmount ?? 0)
      return {
        donationKey,
        orderNo: donation?.order_no ?? donationKey,
        amount: donation?.amount ?? 0,
        isClawback,
        shareAmount: isClawback ? -share : share,
      }
    })
    return {
      id: `${payeeType}-${payeeKey}-${periodStart}-${index}`,
      payeeType,
      payeeKey,
      payeeName: payeeName(payeeType, payeeKey),
      periodStart,
      periodEnd,
      lines: lineDetails,
      donationCount: lineDetails.length,
      donationTotal: lineDetails.reduce((sum, l) => sum + l.amount, 0),
      payableAmount: lineDetails.reduce((sum, l) => sum + l.shareAmount, 0),
      status,
      remittedOn,
      remitMethod,
      remitNote,
    }
  },
))

export const ROLES: Role[] = data.roles.map(([code, nameZh, nameEn, isSystem]) => ({
  code,
  nameZh,
  nameEn,
  isSystem,
  permissionCodes: data.rolePermissionMap[code] ?? [],
}))

export const PERMISSIONS: Permission[] = data.permissions.map(
  ([code, submoduleCode, domain, action, nameZh, nameEn, isRestricted, sysadminOnly]) => ({
    code,
    submoduleCode,
    domain,
    action,
    nameZh,
    nameEn,
    isRestricted,
    sysadminOnly,
  }),
)

export const ADMIN_USERS: AdminUser[] = data.adminUsers.map(([username, , displayName, roleCode, isSuperAdmin]) => ({
  username,
  displayName,
  roleCode,
  roleNameZh: roleNameByCode.get(roleCode) ?? roleCode,
  isSuperAdmin,
}))

export const SETTINGS_PLAIN = new Map(data.settings.map(([key, value]) => [key, value]))
export const SETTINGS_I18N = data.settingsI18n.map(([key, zh, en]) => ({ key, zh, en }))
export const EMAIL_TEMPLATES = data.emailTemplates.map(([code, subjectZh, bodyZh, subjectEn, bodyEn]) => ({
  code,
  subjectZh,
  bodyZh,
  subjectEn,
  bodyEn,
}))
export const EMAIL_LOGS = data.emailLogs.map(([templateCode, donationKey, status, sentAt]) => ({
  templateCode,
  donationKey,
  orderNo: donationsByKey.get(donationKey)?.order_no ?? donationKey,
  status,
  sentAt,
}))

export function findDonationByOrderNo(orderNo: string): Donation | undefined {
  return donationByOrderNo.get(orderNo)
}

export function findStoreByKey(key: string): Store | undefined {
  return STORES.find((s) => s.key === key)
}

export function findProjectByKey(key: string): Project | undefined {
  return PROJECTS.find((p) => p.key === key)
}

export { storeSlugByKey, projectSlugByKey }

// ── mockup 用的畫面內編修（僅存在於這次瀏覽階段的記憶體，重新整理頁面就會回到種子資料原狀，
// 沒有後端、不寫回 db/seed）。目的是讓「編輯後存檔」「執行退款」「登記結算款項」這類操作
// 在畫面上是真的看得到變化，不是按了沒反應的假按鈕。⚠️ 只有直接操作的清單會反映變化，
// 例如退款只更新 DONATIONS 那一列，不會回頭重算 STORES／PROJECTS／SETTLEMENTS 已經算好的
// 累計數字（那些是頁面載入當下算一次的快照），這是刻意的簡化，不是規格。────────────────

let nextLocalId = 1
function localKey(prefix: string): string {
  return `${prefix}-local-${nextLocalId++}`
}

export function addStore(input: Omit<Store, 'key' | 'slug' | 'donationCount' | 'donationTotal' | 'payableTotal'>): Store {
  const store: Store = { ...input, key: localKey('store'), slug: localKey('store-slug'), donationCount: 0, donationTotal: 0, payableTotal: 0 }
  STORES.push(store)
  return store
}

export function updateStore(key: string, patch: Partial<Store>) {
  const store = STORES.find((s) => s.key === key)
  if (store) Object.assign(store, patch)
}

export function addProject(
  input: Omit<Project, 'key' | 'slug' | 'donationCount' | 'donationTotal'>,
): Project {
  const project: Project = { ...input, key: localKey('project'), slug: localKey('project-slug'), donationCount: 0, donationTotal: 0 }
  PROJECTS.push(project)
  return project
}

export function updateProject(key: string, patch: Partial<Project>) {
  const project = PROJECTS.find((p) => p.key === key)
  if (project) Object.assign(project, patch)
}

/** 人工退款（docs/22 §3.7.3）：更新捐款單狀態＋原因，並比照規劃書把相關發票標記作廢 */
export function refundDonation(orderNo: string, reason: string, operatorUsername: string) {
  const donation = DONATIONS.find((d) => d.orderNo === orderNo)
  if (!donation) return
  donation.status = 'refunded'
  donation.refundReason = reason
  donation.refundedBy = operatorUsername
  if (donation.invoice && donation.invoice.voidStatus === 'none' && donation.invoice.issueStatus === 'issued') {
    donation.invoice.voidStatus = 'voided'
    donation.invoice.voidReason = `捐款人申請退款，原發票配合作廢：${reason}`
    donation.invoice.voidedBy = operatorUsername
  }
}

export function reissueInvoice(orderNo: string) {
  const donation = DONATIONS.find((d) => d.orderNo === orderNo)
  if (!donation?.invoice) return
  donation.invoice.issueStatus = 'issued'
  donation.invoice.issuedAt = new Date().toISOString()
  if (!donation.invoice.invoiceNo) donation.invoice.invoiceNo = `DEVTEST-INV-REISSUE-${donation.key}`
}

export function voidInvoice(orderNo: string, reason: string, operatorUsername: string) {
  const donation = DONATIONS.find((d) => d.orderNo === orderNo)
  if (!donation?.invoice) return
  donation.invoice.voidStatus = 'voided'
  donation.invoice.voidReason = reason
  donation.invoice.voidedBy = operatorUsername
}

export function allowanceInvoice(orderNo: string, reason: string, operatorUsername: string) {
  const donation = DONATIONS.find((d) => d.orderNo === orderNo)
  if (!donation?.invoice) return
  donation.invoice.voidStatus = 'allowance'
  donation.invoice.voidReason = reason
  donation.invoice.voidedBy = operatorUsername
}

export interface RegisterPaymentInput {
  remittedOn: string
  remitMethod: string
  remitNote: string
}

export function registerSettlementPayment(settlementId: string, input: RegisterPaymentInput) {
  const settlement = SETTLEMENTS.find((s) => s.id === settlementId)
  if (!settlement) return
  settlement.status = 'paid'
  settlement.remittedOn = input.remittedOn
  settlement.remitMethod = input.remitMethod
  settlement.remitNote = input.remitNote
}

export function executeSettlement(settlementId: string) {
  const settlement = SETTLEMENTS.find((s) => s.id === settlementId)
  if (!settlement) return
  if (settlement.status === 'pending') settlement.status = 'settled'
}
