import type { AudienceTier, PushStatus } from '@/api/adminApp'

export const PUSH_STATUS_OPTIONS: { value: PushStatus; label: string }[] = [
  { value: 'draft', label: '草稿' },
  { value: 'pending_review', label: '待覆核' },
  { value: 'scheduled', label: '已排程' },
  { value: 'sending', label: '發送中' },
  { value: 'sent', label: '已發送' },
  { value: 'partial', label: '部分送出' },
  { value: 'failed', label: '失敗' },
  { value: 'cancelled', label: '已取消' },
]

export function pushStatusTag(status: string): 'success' | 'warning' | 'info' | 'danger' | 'primary' {
  if (status === 'sent') return 'success'
  if (status === 'pending_review' || status === 'partial' || status === 'sending') return 'warning'
  if (status === 'scheduled') return 'primary'
  if (status === 'failed') return 'danger'
  return 'info'
}

export const AUDIENCE_TIERS: { value: AudienceTier; label: string }[] = [
  { value: 'all', label: '所有裝置' },
  { value: 'fan_club', label: '付費球迷會員' },
  { value: 'registered', label: '已登入但不是付費球迷會員' },
  { value: 'anonymous', label: '尚未登入的裝置' },
]

export const audienceTierText = (v: string | null | undefined) => AUDIENCE_TIERS.find((t) => t.value === v)?.label ?? v ?? ''

export const PUSH_ACTION_LABEL: Record<string, string> = {
  submit: '送交覆核',
  approve: '核可並發送',
  return: '退回修改',
  cancel: '取消',
  retry: '重送',
}

export const PUSH_NOT_CONNECTED_TEXT =
  '推播傳輸尚未串接（尚未取得蘋果與 Google 的推播金鑰）。核可後的批次會停在「失敗」狀態，不會送出任何通知，也不會動到任何裝置；串接完成後可以在批次頁按「重送」。'
