/** J3 帳號活動概況，對照 apps/api/README.md「D 批」節「J3」。僅系統管理員。 */
import { apiRequest } from './adminCommon'

export type SecurityAlertKind = 'locked' | 'failed_attempts' | 'dormant' | 'never_logged_in'

export interface SecurityAlertDto {
  kind: SecurityAlertKind
  kindLabel: string
  accountId: string
  username: string
  message: string
}

export interface SecurityAccountDto {
  id: string
  username: string
  displayName: string | null
  status: string
  isSuperAdmin: boolean
  twoFactorEnabled: boolean
  lastLoginAt: string | null
  failedAttemptCount: number
  lockedUntil: string | null
  isLockedNow: boolean
  daysSinceLastLogin: number | null
  passwordChangedAt: string | null
  createdAt: string
}

export interface SecurityOverviewDto {
  generatedAt: string
  /** 恆為 false：目前系統不保存操作稽核紀錄（見 J3 判定）。 */
  auditTrailAvailable: boolean
  auditTrailMessage: string
  activeAccounts: number
  lockedAccounts: number
  dormantAccounts: number
  alerts: SecurityAlertDto[]
  accounts: SecurityAccountDto[]
}

export function getSecurityOverview() {
  return apiRequest<SecurityOverviewDto>('/api/v1/admin/security/overview')
}
