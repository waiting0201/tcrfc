/**
 * `L1` 行事曆總覽／`L2` 自建事件的畫面型別。逐欄位對照 `apps/api` 的
 * `Features/AdminCalendar`（見 apps/api/README.md「S1-11」）。中文標籤對照
 * docs/06-conventions.md §1 與主站規劃書 §4.12。
 */

// ── 共用 ─────────────────────────────────────────────────────────────────────────

export type CalendarSourceType = 'match' | 'custom'

export const CALENDAR_SOURCE_TYPE_LABEL: Record<CalendarSourceType, string> = {
  match: '賽事',
  custom: '自建活動',
}

/** `team` 查詢參數：省略＝不限、`club`＝俱樂部活動（沒有掛任何球隊的自建事件）、其餘＝球隊代碼。 */
export const CALENDAR_CLUB_TEAM_VALUE = 'club'

// ── L2 重複規則（`apps/api` `Common/RecurrenceExpander.AllowedRepeatRules`）───────────

export type RepeatRule = 'weekly' | 'biweekly' | 'monthly'

export const REPEAT_RULE_LABEL: Record<RepeatRule, string> = {
  weekly: '每週',
  biweekly: '每兩週',
  monthly: '每月',
}

export const REPEAT_RULE_ORDER: RepeatRule[] = ['weekly', 'biweekly', 'monthly']

export function repeatRuleLabel(value: string | null | undefined): string {
  if (!value) return '不重複'
  return REPEAT_RULE_LABEL[value as RepeatRule] ?? value
}

export function calendarSourceTypeLabel(value: string): string {
  return CALENDAR_SOURCE_TYPE_LABEL[value as CalendarSourceType] ?? value
}

export function calendarSourceTagType(value: string): 'success' | 'warning' {
  return value === 'match' ? 'success' : 'warning'
}
