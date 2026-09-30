import { availableClubs } from '@/auth/clubAccess'
import { AdminApiError } from '@/api/http'
import type { MembershipListItemDto, MembershipStatus, MembershipTier } from '@/api/adminMemberships'
import { taipeiToday } from '@/utils/dateTime'

export type TagType = 'success' | 'info' | 'warning' | 'danger'

export const TIER_LABEL: Record<MembershipTier, string> = {
  registered: '一般會員',
  fan_club: '付費球迷會員',
}

export const STATUS_LABEL: Record<MembershipStatus, string> = {
  pending: '待確認',
  active: '有效',
  expired: '已到期',
  cancelled: '已取消',
}

export const STATUS_TAG: Record<MembershipStatus, TagType> = {
  pending: 'warning',
  active: 'success',
  expired: 'danger',
  cancelled: 'info',
}

export function tierLabel(row: Pick<MembershipListItemDto, 'tier' | 'tierLabel'>): string {
  return row.tierLabel || TIER_LABEL[row.tier] || row.tier
}

export function effectiveStatusLabel(row: Pick<MembershipListItemDto, 'effectiveStatus' | 'effectiveStatusLabel'>): string {
  return row.effectiveStatusLabel || STATUS_LABEL[row.effectiveStatus] || row.effectiveStatus
}

/** 到期日距今的說明：`daysToExpire` 正數＝還有幾天，負數＝已過期幾天。 */
export function expiryText(days: number | null | undefined): string {
  if (days === null || days === undefined) return ''
  if (days === 0) return '今天到期'
  return days > 0 ? `${days} 天後到期` : `已過期 ${-days} 天`
}

/** 俱樂部代碼對應成名稱；找不到就顯示原字串。 */
export function clubNameOf(code: string | null | undefined): string {
  if (!code) return '—'
  return availableClubs.value.find((c) => c.code === code)?.name ?? code
}

/** 今天的台灣日期 `YYYY-MM-DD`。 */
export function todayString(): string {
  return taipeiToday()
}

export function formatMoney(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—'
  return `${value.toLocaleString('zh-TW')} 元`
}

export function errorMessage(error: unknown, fallback: string): string {
  return error instanceof AdminApiError && error.message ? error.message : fallback
}
