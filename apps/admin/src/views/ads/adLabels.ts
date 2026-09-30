import type { CampaignStatus } from '@/api/adminAds'

export const CAMPAIGN_STATUS_OPTIONS: { value: CampaignStatus; label: string }[] = [
  { value: 'draft', label: '草稿' },
  { value: 'pending_review', label: '待審核' },
  { value: 'scheduled', label: '已排程' },
  { value: 'running', label: '投放中' },
  { value: 'paused', label: '已暫停' },
  { value: 'ended', label: '已結束' },
  { value: 'closed', label: '已結案' },
  { value: 'voided', label: '已作廢' },
]

export function campaignStatusTag(status: string): 'success' | 'warning' | 'info' | 'danger' | 'primary' {
  if (status === 'running') return 'success'
  if (status === 'pending_review' || status === 'paused') return 'warning'
  if (status === 'scheduled') return 'primary'
  if (status === 'voided') return 'danger'
  return 'info'
}

export function reviewTag(status: string): 'success' | 'warning' | 'danger' {
  return status === 'approved' ? 'success' : status === 'rejected' ? 'danger' : 'warning'
}

export const CAMPAIGN_ACTION_LABEL: Record<string, string> = {
  submit: '送審',
  approve: '核可',
  return: '退回修改',
  pause: '緊急暫停',
  resume: '恢復投放',
  close: '結案',
  void: '作廢',
}
