/**
 * A 儀表板（H 批，apps/api/README.md「H 批」§1）。三支 GET，權限＝持有儀表板會用到的任一檢視／建立權限，
 * 一個都沒有 → 403。**每個區塊再依對應模組權限決定有沒有：沒權限的區塊是 `null`／不在清單，不是 0**——
 * 畫面上的規則是「沒有就不顯示該區塊」，不可把 `null` 當 0 顯示。
 */
import { apiRequest } from './http'

export interface DashboardTodoItem {
  id: string
  title: string
  date?: string | null
}

export interface DashboardTodo {
  /** 內部識別（enquiries_new／registrations_pending／sessions_closing_soon／sponsor_contracts_expiring），不顯示。 */
  code: string
  label: string
  count: number
  hint?: string | null
  items: DashboardTodoItem[]
}

export interface DashboardUntranslatedType {
  type: string
  typeLabel: string
  count: number
}

export interface DashboardUntranslated {
  locale: string
  localeName: string
  count: number
  byType: DashboardUntranslatedType[]
}

export interface DashboardContent {
  publishedThisMonth?: number | null
  draftCount?: number | null
  scheduledCount?: number | null
  untranslated: DashboardUntranslated[]
}

export interface DashboardFaqItem {
  id: string
  question: string
  viewCount: number
  helpfulCount: number
  unhelpfulCount: number
}

export interface DashboardFaq {
  topQuestions: DashboardFaqItem[]
  negativeFeedback: DashboardFaqItem[]
}

export type DashboardUpcomingSource = 'match' | 'session' | 'trial' | 'event' | 'fan_event'

export interface DashboardUpcomingItem {
  source: DashboardUpcomingSource
  id: string
  date: string
  time?: string | null
  title: string
  teamCode?: string | null
  venueName?: string | null
  warnings: string[]
}

export interface DashboardMembers {
  activeMemberships: number
  activePaidMemberships: number
  expiringIn30Days: number
  pendingUpgrades: number
}

export interface DashboardQuickEntry {
  /** publish_news／add_match／add_session／add_faq／add_calendar_event */
  code: string
  label: string
}

export interface DashboardDto {
  generatedAt: string
  todos: DashboardTodo[]
  content?: DashboardContent | null
  faq?: DashboardFaq | null
  upcoming: DashboardUpcomingItem[]
  members?: DashboardMembers | null
  quickEntries: DashboardQuickEntry[]
}

export type ConversionPeriod = 'week' | 'month'

/** 各序列依權限，沒權限為 `null`／省略。 */
export interface ConversionBucket {
  start?: string | null
  enquiries?: number | null
  registrations?: number | null
  proposalDownloads?: number | null
  newMembers?: number | null
  newPaidMemberships?: number | null
  renewals?: number | null
}

export interface ConversionDto {
  period: ConversionPeriod
  from: string
  to: string
  totals: ConversionBucket
  buckets: ConversionBucket[]
  forms: { formCode: string; formName: string; count: number }[]
}

export interface TrafficDto {
  configured: boolean
  message: string
  from: string
  to: string
  overview?: {
    pageViews: number
    sessions: number
    topPages: { path: string; views: number }[]
    sources: { source: string; sessions: number }[]
  } | null
}

const base = (club: string) => `/api/v1/admin/${club}/dashboard`

export function getDashboard(club: string): Promise<DashboardDto> {
  return apiRequest<DashboardDto>(base(club))
}

export function getDashboardConversion(club: string, period: ConversionPeriod): Promise<ConversionDto> {
  return apiRequest<ConversionDto>(`${base(club)}/conversion?period=${period}`)
}

export function getDashboardTraffic(club: string): Promise<TrafficDto> {
  return apiRequest<TrafficDto>(`${base(club)}/traffic`)
}
