/** 回饋金結算：`/api/v1/donation-platform/admin/settlements`。 */
import { ADMIN_ROOT, apiBlob, apiRequest, buildQuery, saveBlob } from './http'
import type { PagedResult } from './types'

const ROOT = `${ADMIN_ROOT}/settlements`

export type SettlementStatus = 'pending' | 'settled' | 'paid'
export type PayeeType = 'store' | 'project'

export interface SettlementItem {
  id: string
  periodStart: string
  periodEnd: string
  payeeType: PayeeType
  payeeId: string
  payeeName: string | null
  status: SettlementStatus
  donationCount: number
  donationTotal: number
  clawbackCount: number
  clawbackAmount: number
  /** 含沖回負項，可能為負。 */
  payableAmount: number
  remittedOn: string | null
  remitMethod: string | null
  remitNote: string | null
  settledAt: string | null
  settledByName: string | null
  paidRegisteredAt: string | null
  paidRegisteredByName: string | null
  createdAt: string
}

export interface SettlementLine {
  lineId: string
  donationId: string
  orderNo: string
  paidAt: string | null
  donationAmount: number
  sharePct: number
  shareAmount: number
  isClawback: boolean
  clawbackReason: string | null
}

export interface SettlementDetail { settlement: SettlementItem; lines: SettlementLine[] }

export interface SettlementFilter {
  status?: string
  payeeType?: string
  payeeId?: string
  from?: string
  to?: string
}

export interface RunSettlementInput {
  periodStart: string
  periodEnd: string
  payeeType?: PayeeType
  payeeId?: string
}

export interface RunSettlementResult {
  created: SettlementItem[]
  skipped: { payeeType: PayeeType; payeeId: string; payeeName: string | null; reason: string }[]
}

export interface RecalculationResult {
  detail: SettlementDetail
  removedOrderNos: string[]
  addedCount: number
  addedClawbackCount: number
}

export const listSettlements = (filter: SettlementFilter, page: number, pageSize: number) =>
  apiRequest<PagedResult<SettlementItem>>(`${ROOT}${buildQuery({ ...filter, page, pageSize })}`)
export const getSettlement = (id: string) => apiRequest<SettlementDetail>(`${ROOT}/${id}`)
export const runSettlements = (input: RunSettlementInput) => apiRequest<RunSettlementResult>(`${ROOT}/run`, { method: 'POST', body: input })
export const recalculateSettlement = (id: string) => apiRequest<RecalculationResult>(`${ROOT}/${id}/recalculate`, { method: 'POST' })
export const settleSettlement = (id: string) => apiRequest<SettlementDetail>(`${ROOT}/${id}/settle`, { method: 'POST' })
export const markSettlementPaid = (id: string, input: { remittedOn: string; remitMethod: string; remitNote: string | null }) =>
  apiRequest<SettlementDetail>(`${ROOT}/${id}/mark-paid`, { method: 'POST', body: input })
export const deleteSettlementDraft = (id: string) => apiRequest<void>(`${ROOT}/${id}`, { method: 'DELETE' })

/** 對帳單 CSV（不含任何捐款人資料）。 */
export async function exportSettlement(id: string, fallbackName: string): Promise<void> {
  const { blob, filename } = await apiBlob(`${ROOT}/${id}/export`)
  saveBlob(blob, filename ?? `${fallbackName}.csv`)
}
