// server/utils/fixtures.ts — 讀取唯一真實來源的假資料，並整理成前台頁面好用的形狀。
//
// ⛔ 這支檔案是本專案唯一讀 db/seed/charity-fixtures.json（經 scripts/sync-fixtures.mjs
// 同步到本機的 .data/charity-fixtures.json）的地方。⛔ 不要在頁面或元件裡另外 import 這份
// JSON、也不要另外手寫一份假資料——一律透過下面這些函式，並且只在 server/ 端使用
// （server/api/* 路由），資料經由 useFetch 的 payload 交給頁面，JSON 本身不會被打進
// 瀏覽器端的 bundle。
//
// 型別只涵蓋前台實際會用到的欄位，完整綱要見 docs/16-charity-schema.md。
import fixturesJson from '../../.data/charity-fixtures.json'

export interface StoreFixture {
  key: string
  name_zh: string
  name_en: string
  category: string
  address: string
  contact_name: string
  contact_phone: string
  share_pct: string
  has_logo: boolean
  status: 'active' | 'inactive'
  start_on: string
  end_on: string | null
}

export interface ProjectFixture {
  key: string
  name_zh: string
  name_en: string
  one_liner_zh: string
  one_liner_en: string
  min_amount: number
  max_amount: number
  share_pct: string
  invoice_mode: 'b2c_invoice' | 'donation_receipt'
  charity_ref: string
  program_ref: string
  status: 'draft' | 'published'
  sort_order: number
  fund_usage_zh: string
  fund_usage_en: string
}

export type DonationStatus = 'created' | 'pending' | 'paid' | 'failed' | 'expired' | 'refunded'

export interface DonationFixture {
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

export interface PaymentFixture {
  status: 'requested' | 'confirmed' | 'failed' | 'cancelled'
  requested_at: string
  confirmed_at: string | null
}

export interface InvoiceFixture {
  invoice_type: 'b2c_invoice' | 'donation_receipt'
  invoice_no?: string | null
  issued_at?: string | null
  carrier_type?: string
  carrier_id_encrypted?: string
  tax_id?: string
  invoice_title?: string
  receipt_title?: string
  receipt_address?: string
  national_id_encrypted?: string
  issue_status: 'pending' | 'issued' | 'failed' | 'voided' | 'allowance'
  void_status: 'none' | 'voided' | 'allowance'
  void_reason?: string
  voided_by?: string
  is_annual_summary?: boolean
}

interface FixturesFile {
  charityRefs: [string, string][]
  charityProgramRefs: [string, string, string][]
  stores: StoreFixture[]
  storeSlugs: Record<string, string>
  projects: ProjectFixture[]
  projectSlugs: Record<string, string>
  amountOptions: Record<string, number[]>
  donations: DonationFixture[]
  donationSplits: Record<string, [number, number, number]>
  payments: Record<string, PaymentFixture>
  invoices: Record<string, InvoiceFixture>
  settingsI18n: [string, string, string][]
}

const data = fixturesJson as unknown as FixturesFile

const charityRefMap = new Map(data.charityRefs.map(([ref, name]) => [ref, name]))
const programRefMap = new Map(data.charityProgramRefs.map(([ref, , name]) => [ref, name]))
const storeSlugToKey = new Map(Object.entries(data.storeSlugs).map(([key, slug]) => [slug, key]))
const projectSlugToKey = new Map(Object.entries(data.projectSlugs).map(([key, slug]) => [slug, key]))

export function getSettingText(id: string): { zh: string, en: string } {
  const row = data.settingsI18n.find((r) => r[0] === id)
  return { zh: row?.[1] ?? '', en: row?.[2] ?? '' }
}

export function listActiveStores() {
  return data.stores.map((store) => ({
    ...store,
    slug: data.storeSlugs[store.key],
  }))
}

export function findStoreBySlug(slug: string) {
  const key = storeSlugToKey.get(slug)
  if (!key) return null
  const store = data.stores.find((s) => s.key === key)
  if (!store) return null
  return { ...store, slug }
}

export function listPublishedProjects() {
  return data.projects
    .filter((p) => p.status === 'published')
    .sort((a, b) => a.sort_order - b.sort_order)
    .map((p) => ({
      ...p,
      slug: data.projectSlugs[p.key],
      amountOptions: data.amountOptions[p.key] ?? [],
    }))
}

export function findProjectBySlug(slug: string) {
  const key = projectSlugToKey.get(slug)
  if (!key) return null
  const project = data.projects.find((p) => p.key === key)
  if (!project) return null
  return {
    ...project,
    slug,
    amountOptions: data.amountOptions[project.key] ?? [],
    charityName: charityRefMap.get(project.charity_ref) ?? null,
    programName: programRefMap.get(project.program_ref) ?? null,
  }
}

export function findDonationByOrderNo(orderNo: string) {
  const donation = data.donations.find((d) => d.order_no === orderNo)
  if (!donation) return null
  const project = data.projects.find((p) => p.key === donation.project) ?? null
  const store = donation.store ? (data.stores.find((s) => s.key === donation.store) ?? null) : null
  const payment = data.payments[donation.key] ?? null
  const invoice = data.invoices[donation.key] ?? null
  return { donation, project, store, payment, invoice }
}

export function listNamedCompletedDonations() {
  // 徵信名單只列「具名」且「已完成付款」的捐款（規劃書 §3.6：僅顯示姓名，不顯示金額／Email／店家）。
  // 已退款的捐款款項已經退回，不再視為完成的捐贈，一併排除。
  return data.donations
    .filter((d) => !d.is_anonymous && d.status === 'paid')
    .map((d) => {
      const project = data.projects.find((p) => p.key === d.project)
      return {
        donorName: d.donor_name,
        projectKey: d.project,
        projectNameZh: project?.name_zh ?? '',
        projectNameEn: project?.name_en ?? '',
        createdAt: d.created_at,
      }
    })
}
