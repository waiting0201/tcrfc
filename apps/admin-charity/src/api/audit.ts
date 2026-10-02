/** 操作紀錄查詢（僅系統管理員，唯讀）：`/api/v1/donation-platform/admin/audit-logs`。 */
import { ADMIN_ROOT, apiRequest, buildQuery } from './http'
import type { PagedResult } from './types'

const ROOT = `${ADMIN_ROOT}/audit-logs`

export interface AuditLogItem {
  id: string
  occurredAt: string
  adminUserId: string
  adminName: string | null
  action: string
  actionLabel: string
  targetType: string
  targetTypeLabel: string
  targetId: string | null
  targetLabel: string | null
  changeSummary: string | null
  purposeNote: string | null
  sourceIp: string | null
}

export interface AuditFilter { from?: string; to?: string; action?: string; targetType?: string; keyword?: string }

export const queryAuditLogs = (filter: AuditFilter, page: number, pageSize: number) =>
  apiRequest<PagedResult<AuditLogItem>>(`${ROOT}${buildQuery({ ...filter, page, pageSize })}`)
export const listAuditActions = () => apiRequest<{ action: string; label: string }[]>(`${ROOT}/actions`)
