/**
 * 對帳批次／對帳差異／稽核紀錄。
 *
 * 🔵 2026-09-22：資料已改為從 `fixtures/charity-fixtures.json` 衍生，**不再是手抄的**。
 *
 * 交付當下這三張表的資料在 `db/seed/generate-charity-seed-sql.py` 裡是直接寫成 SQL 字面值，
 * 沒有存成模組常數，所以 `emit-charity-fixtures.py` 匯不出來，本檔一度「逐字轉錄」種子腳本
 * 裡的數字自己存一份——那正是 fixtures 管線要防止的**第二份真實來源**：
 * 種子改了而這裡沒改，畫面上看不出來，也沒有任何檢查會發現。
 *
 * 已修正：種子腳本的 §12／§13 改成先存 `RECONCILIATION_RUNS`／`RECONCILIATION_DISCREPANCIES`／
 * `AUDIT_LOGS` 三個模組常數再迴圈 INSERT，匯出腳本的 EXPORT 清單一併補上。
 * 重構後重跑種子，資料庫總列數不變（202）、對帳 2／差異 3／稽核 3 皆正確，行為未變。
 *
 * ⛔ 不要在這裡新增種子沒有的資料。要改資料就改 `db/seed/generate-charity-seed-sql.py`，
 * 再重跑 `emit-charity-fixtures.py` 與 `apply-charity-seed.sh`——
 * `npm run lint` 的 `lint:fixtures` 會驗三份檔案是否同步，過期就擋下。
 *
 * 🔵 本檔仍保留的是**顯示層轉換與 mockup 內的編修函式**（標記已處理、追加稽核紀錄），
 * 那些是畫面行為不是資料，留在這裡是對的。
 */
import { reactive } from 'vue'
import raw from '@fixtures/charity-fixtures.json'
import type { CharityFixturesFile } from '@/types/fixtures'
import { findProjectByKey } from './fixtures'

const data = raw as unknown as CharityFixturesFile

/** 對帳批次的顯示用 id：種子沒有給 id（資料庫端是 NEWID()），用業務鍵組一個穩定值。 */
function runId(runOn: string): string {
  return `recon-${runOn}`
}

export type DiscrepancyType = 'site_only' | 'gateway_only' | 'amount_mismatch'
export type DiscrepancyResolution = 'pending' | 'resolved'
export type ReconciliationRunStatus = 'completed'

export interface ReconciliationRun {
  id: string
  runOn: string
  source: 'linepay'
  comparedCount: number
  matchedCount: number
  discrepancyCount: number
  status: ReconciliationRunStatus
}

export interface ReconciliationDiscrepancy {
  runId: string
  type: DiscrepancyType
  donationOrderNo: string | null
  gatewayTransactionId: string | null
  siteAmount: number | null
  gatewayAmount: number | null
  resolutionStatus: DiscrepancyResolution
  resolvedBy?: string
  resolveNote?: string
}

export const RECONCILIATION_RUNS: ReconciliationRun[] = data.reconciliationRuns.map((r) => ({
  id: runId(r.run_on),
  runOn: r.run_on,
  source: r.source as 'linepay',
  comparedCount: r.compared_count,
  matchedCount: r.matched_count,
  discrepancyCount: r.discrepancy_count,
  status: r.status as ReconciliationRunStatus,
}))

const runIdByKey = new Map(
  data.reconciliationRuns.map((r) => [r.key, runId(r.run_on)] as const),
)

export const RECONCILIATION_DISCREPANCIES: ReconciliationDiscrepancy[] = reactive(
  data.reconciliationDiscrepancies.map((d) => ({
    runId: runIdByKey.get(d.run_key) ?? d.run_key,
    type: d.discrepancy_type as DiscrepancyType,
    donationOrderNo: d.donation_order_no,
    gatewayTransactionId: d.gateway_transaction_id,
    siteAmount: d.site_amount,
    gatewayAmount: d.gateway_amount,
    resolutionStatus: d.resolution_status as DiscrepancyResolution,
    // undefined 而不是 null：介面把「沒有處理人」與「有處理人但名字是空的」分開處理。
    resolvedBy: d.resolved_by ?? undefined,
    resolveNote: d.resolve_note ?? undefined,
  })),
)

export interface AuditLogEntry {
  adminUsername: string
  occurredAt: string
  action: 'refund' | 'update_share_pct' | 'export_personal_data' | 'void_invoice' | 'allowance_invoice' | 'resolve_discrepancy'
  targetType: 'donation' | 'donation_project' | 'donation_list_export' | 'donation_invoice' | 'reconciliation_discrepancy'
  targetLabel: string | null
  changeSummary: string
  purposeNote: string | null
  sourceIp: string
}

/**
 * 稽核紀錄的「對象」欄位在資料庫是 target_id（外鍵值），畫面上要顯示的是人看得懂的名稱，
 * 所以這裡把捐款單號／項目名稱轉出來。⛔ 轉不出來時退回顯示原始鍵值，不顯示空白——
 * 稽核軌跡少一格資訊比顯示一個技術鍵值更糟。
 */
function auditTargetLabel(a: CharityFixturesFile['auditLogs'][number]): string | null {
  if (a.target_donation_order_no) return a.target_donation_order_no
  if (a.target_project_key) {
    return findProjectByKey(a.target_project_key)?.nameZh ?? a.target_project_key
  }
  return null
}

export const AUDIT_LOGS: AuditLogEntry[] = reactive(
  data.auditLogs.map((a) => ({
    adminUsername: a.admin_username,
    occurredAt: a.occurred_at,
    action: a.action as AuditLogEntry['action'],
    targetType: a.target_type as AuditLogEntry['targetType'],
    targetLabel: auditTargetLabel(a),
    changeSummary: a.change_summary,
    purposeNote: a.purpose_note,
    sourceIp: a.source_ip,
  })),
)

/** 標記對帳差異已處理（docs/22 §3.8：差異記錄本身不得刪除，只能更新處理狀態） */
export function markDiscrepancyResolved(runId: string, type: DiscrepancyType, note: string, operatorUsername: string) {
  const item = RECONCILIATION_DISCREPANCIES.find((d) => d.runId === runId && d.type === type)
  if (!item) return
  item.resolutionStatus = 'resolved'
  item.resolvedBy = operatorUsername
  item.resolveNote = note
}

/** 新增一筆稽核紀錄（append-only，畫面上沒有刪除或編輯既有列的操作） */
export function appendAuditLog(entry: Omit<AuditLogEntry, 'occurredAt'>) {
  AUDIT_LOGS.unshift({ ...entry, occurredAt: new Date().toISOString() })
}
