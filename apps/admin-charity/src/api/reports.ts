/** 捐款報表：`/api/v1/donation-platform/admin/reports`。全部報表不含捐款人個資。 */
import { ADMIN_ROOT, apiBlob, apiRequest, buildQuery, saveBlob } from './http'
import type { PagedResult } from './types'

const ROOT = `${ADMIN_ROOT}/reports`

export interface ReportFilter {
  from?: string
  to?: string
  storeId?: string
  noStore?: boolean
  projectId?: string
  /** `paid`（預設）、`refunded`、`all`（含已退款，看毛額）。 */
  paymentStatus?: string
  invoiceType?: string
  amountMin?: number
  amountMax?: number
}

export interface TrendPoint { period: string; count: number; amount: number }

export interface ReportOverview {
  donationCount: number
  totalAmount: number
  averageAmount: number
  createdCount: number
  convertedCount: number
  /** 0–1 的比例。 */
  conversionRate: number
  granularity: 'day' | 'month'
  trend: TrendPoint[]
}

export interface StoreRow {
  storeId: string | null
  storeName: string | null
  donationCount: number
  totalAmount: number
  /** 0–1 的比例。 */
  amountRatio: number
  /** 百分比數值（5 代表 5%）。 */
  currentSharePct: number
  payableAmount: number
  settledAmount: number
  unsettledAmount: number
}

export interface ProjectRow {
  projectId: string
  projectName: string | null
  donationCount: number
  totalAmount: number
  amountRatio: number
  averageAmount: number
  currentSharePct: number
  payableAmount: number
  settledAmount: number
  unsettledAmount: number
}

export interface InvoiceStatusSummary {
  issued: number
  pending: number
  failed: number
  voided: number
  allowance: number
  noInvoice: number
}

export interface DetailRow {
  donationId: string
  orderNo: string
  createdAt: string
  paidAt: string | null
  status: string
  amount: number
  projectName: string | null
  storeName: string | null
  invoiceStatus: string | null
  invoiceVoidStatus: string | null
  isAnonymous: boolean
  storeAmount: number
  projectAmount: number
  associationAmount: number
}

export type ReportName = 'overview' | 'by-store' | 'by-project' | 'invoice-status' | 'details'

export const getOverview = (f: ReportFilter, granularity: 'day' | 'month') =>
  apiRequest<ReportOverview>(`${ROOT}/overview${buildQuery({ ...f, granularity })}`)
export const getByStore = (f: ReportFilter) => apiRequest<StoreRow[]>(`${ROOT}/by-store${buildQuery({ ...f })}`)
export const getByProject = (f: ReportFilter) => apiRequest<ProjectRow[]>(`${ROOT}/by-project${buildQuery({ ...f })}`)
export const getInvoiceStatus = (f: ReportFilter) => apiRequest<InvoiceStatusSummary>(`${ROOT}/invoice-status${buildQuery({ ...f })}`)
export const getDetails = (f: ReportFilter, page: number, pageSize: number) =>
  apiRequest<PagedResult<DetailRow>>(`${ROOT}/details${buildQuery({ ...f, page, pageSize })}`)

/** CSV 匯出需要另外的授權（能看不等於能匯出，沒有授權後端回 403）。 */
export async function exportReport(name: ReportName, f: ReportFilter, granularity?: 'day' | 'month'): Promise<void> {
  const { blob, filename } = await apiBlob(`${ROOT}/${name}${buildQuery({ ...f, granularity, format: 'csv' })}`)
  saveBlob(blob, filename ?? `report-${name}.csv`)
}
