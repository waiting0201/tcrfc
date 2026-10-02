/** 每日對帳：`/api/v1/donation-platform/admin/reconciliation`。檢視權限同捐款紀錄；執行與標記處理需「重新確認付款」權限。 */
import { ADMIN_ROOT, apiRequest, buildQuery } from './http'
import type { PagedResult } from './types'

const ROOT = `${ADMIN_ROOT}/reconciliation`

export type DiscrepancyType = 'site_only' | 'gateway_only' | 'amount_mismatch'

export interface ReconciliationRun {
  id: string
  runOn: string
  source: string
  status: 'completed' | 'failed'
  comparedCount: number
  matchedCount: number
  discrepancyCount: number
  pendingCount: number
  ranAt: string
}

export interface Discrepancy {
  id: string
  type: DiscrepancyType
  donationId: string | null
  orderNo: string | null
  gatewayTransactionId: string | null
  siteAmount: number | null
  gatewayAmount: number | null
  resolutionStatus: 'pending' | 'resolved'
  resolvedByName: string | null
  resolveNote: string | null
  createdAt: string
  updatedAt: string
}

export interface ReconciliationRunDetail { run: ReconciliationRun; discrepancies: Discrepancy[] }

export interface ReconciliationRunResult {
  runId: string
  runOn: string
  source: string
  status: string
  comparedCount: number
  matchedCount: number
  discrepancyCount: number
  newDiscrepancies: number
  autoResolved: number
}

export const listReconciliationRuns = (q: { from?: string; to?: string; onlyPending?: boolean; page?: number; pageSize?: number }) =>
  apiRequest<PagedResult<ReconciliationRun>>(`${ROOT}/runs${buildQuery(q)}`)
export const getReconciliationRun = (id: string) => apiRequest<ReconciliationRunDetail>(`${ROOT}/runs/${id}`)
export const runReconciliation = (date?: string) => apiRequest<ReconciliationRunResult>(`${ROOT}/runs`, { method: 'POST', body: date ? { date } : {} })
export const resolveDiscrepancy = (id: string, note: string) =>
  apiRequest<Discrepancy>(`${ROOT}/discrepancies/${id}/resolve`, { method: 'POST', body: { note } })
