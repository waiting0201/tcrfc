/** N3 捐款紀錄與異常佇列：`/api/v1/donation-platform/admin/donations`。 */
import { ADMIN_ROOT, apiBlob, apiRequest, buildQuery, saveBlob } from './http'
import type { PagedResult } from './types'

const ROOT = `${ADMIN_ROOT}/donations`

export type DonationStatus = 'created' | 'pending' | 'paid' | 'failed' | 'expired' | 'refunded'

export interface DonationFilter {
  from?: string
  to?: string
  status?: string
  projectId?: string
  storeId?: string
  noStore?: boolean
  invoiceStatus?: string
  amountMin?: number
  amountMax?: number
  keyword?: string
}

export interface DonationListItem {
  id: string
  orderNo: string
  createdAt: string
  paidAt: string | null
  amount: number
  status: DonationStatus
  projectId: string
  projectName: string | null
  storeId: string | null
  storeName: string | null
  invoiceStatus: string | null
  invoiceVoidStatus: string | null
  isAnonymous: boolean
  needsManualReview: boolean
}

export interface DonationDetail {
  id: string
  orderNo: string
  status: DonationStatus
  amount: number
  createdAt: string
  paidAt: string | null
  updatedAt: string
  project: { id: string; slug: string; name: string | null }
  store: { id: string; name: string | null } | null
  donor: { name: string; email: string; isAnonymous: boolean; revealed: boolean }
  split: { storeSharePct: number; projectSharePct: number; storeAmount: number; projectAmount: number; associationAmount: number }
  invoiceMode: string
  payments: { id: string; transactionId: string | null; status: string; requestedAt: string | null; confirmedAt: string | null; amount: number }[]
  invoice: {
    invoiceType: string
    carrierType: string | null
    carrierId: string | null
    taxId: string | null
    invoiceTitle: string | null
    receiptTitle: string | null
    nationalId: string | null
    receiptAddress: string | null
    isAnnualSummary: boolean
    issueStatus: string
    voidStatus: string
    invoiceNo: string | null
    issuedAt: string | null
    voidReason: string | null
    voidedByName: string | null
  } | null
  refund: { reason: string | null; refundedByName: string | null } | null
  needsManualReview: boolean
  timeline: { at: string; kind: string; text: string; byName: string | null }[]
}

export type AnomalyKind = 'confirm_failed' | 'invoice_failed' | 'invoice_void_pending' | 'reconciliation'

export interface Anomaly {
  kind: AnomalyKind
  donationId: string | null
  orderNo: string | null
  amount: number | null
  occurredAt: string
  discrepancyType: string | null
  resolutionStatus: string | null
  discrepancyId: string | null
}

export interface AnomalyCounts {
  confirmFailed: number
  invoiceFailed: number
  invoiceVoidPending: number
  reconciliation: number
}

export const listDonations = (filter: DonationFilter, page: number, pageSize: number) =>
  apiRequest<PagedResult<DonationListItem>>(`${ROOT}${buildQuery({ ...filter, page, pageSize })}`)
export const getDonation = (id: string, reveal = false) => apiRequest<DonationDetail>(`${ROOT}/${id}${buildQuery({ reveal })}`)
export const refundDonation = (id: string, reason: string) => apiRequest<DonationDetail>(`${ROOT}/${id}/refund`, { method: 'POST', body: { reason } })
export const recheckPayment = (id: string) => apiRequest<DonationDetail>(`${ROOT}/${id}/recheck-payment`, { method: 'POST' })
export const resendThanks = (id: string) => apiRequest<{ sent: boolean }>(`${ROOT}/${id}/resend-thanks`, { method: 'POST' })
export const reissueInvoice = (id: string) => apiRequest<DonationDetail>(`${ROOT}/${id}/invoice/reissue`, { method: 'POST' })
export const listAnomalies = (kind?: AnomalyKind) => apiRequest<Anomaly[]>(`${ROOT}/anomalies${buildQuery({ kind })}`)
export const countAnomalies = () => apiRequest<AnomalyCounts>(`${ROOT}/anomalies/counts`)

/** 含個資的明細匯出：`purpose`（用途備註，至少 4 字）必填，後端會寫稽核。 */
export async function exportDonations(filter: DonationFilter, purpose: string): Promise<void> {
  const { blob, filename } = await apiBlob(`${ROOT}/export${buildQuery({ ...filter, purpose })}`)
  saveBlob(blob, filename ?? 'donations.csv')
}
