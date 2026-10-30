/**
 * P1–P3「課程與活動」的畫面型別。逐欄位對照 `apps/api` 的
 * `Features/AdminPrograms`／`AdminSessions`／`AdminRegistrations`（見 apps/api/README.md「S1-9」）。
 * 中文標籤對照 docs/06-conventions.md §1 與主站規劃書 §4.4。
 */

// ── P1 課程／營隊項目 ────────────────────────────────────────────────────────────

export type ProgramType = 'children_training' | 'summer_camp' | 'winter_camp' | 'specialist_training' | 'school_community'

export const PROGRAM_TYPE_LABEL: Record<ProgramType, string> = {
  children_training: '兒童訓練',
  summer_camp: '夏令營',
  winter_camp: '冬令營',
  specialist_training: '專項訓練',
  school_community: '校園社區',
}

export const PROGRAM_TYPE_ORDER: ProgramType[] = [
  'children_training',
  'summer_camp',
  'winter_camp',
  'specialist_training',
  'school_community',
]

/** `programs.status` 只有兩態（草稿／已發布），比照一般內容型別的兩態慣例——P1 沒有排程發布需求。 */
export type ProgramStatus = 'draft' | 'published'

export const PROGRAM_STATUS_LABEL: Record<ProgramStatus, string> = {
  draft: '草稿',
  published: '已發布',
}

// ── P2 梯次與場次 ────────────────────────────────────────────────────────────────

/**
 * 對外狀態代碼（`effectiveStatusCode`，伺服器推算，唯讀）的標籤色。多一個「尚未開放」，
 * 它不是儲存值，後台狀態欄不提供。畫面只顯示伺服器回傳的中文標籤，不顯示代碼。
 */
export function effectiveStatusTagType(code: string): 'success' | 'warning' | 'info' | 'danger' {
  if (code === 'open') return 'success'
  if (code === 'full') return 'warning'
  if (code === 'waitlist' || code === 'not_yet_open') return 'info'
  return 'danger' // ended
}

/** 對外狀態的說明文字（列表與詳情共用）。 */
export const EFFECTIVE_STATUS_HINT = '報名開始前自動顯示尚未開放、截止後自動顯示已結束'

/** `sessions.status`：中文值（見 `db/club-schema.sql` `CK_sessions_status`），省略時後端自動依名額推定。 */
export type SessionStatus = '開放' | '額滿' | '候補' | '已結束'

export const SESSION_STATUS_ORDER: SessionStatus[] = ['開放', '額滿', '候補', '已結束']

export function sessionStatusTagType(status: string): 'success' | 'warning' | 'info' | 'danger' {
  if (status === '開放') return 'success'
  if (status === '額滿') return 'warning'
  if (status === '候補') return 'info'
  return 'danger' // 已結束
}

// ── P3 報名管理 ──────────────────────────────────────────────────────────────────

/** `registrations.status`：規劃書行 1104「待確認 → 已確認 → 已繳費 → 完成 / 取消 / 候補」。 */
export type RegistrationStatus = '待確認' | '已確認' | '已繳費' | '完成' | '取消' | '候補'

export const REGISTRATION_STATUS_ORDER: RegistrationStatus[] = ['待確認', '已確認', '已繳費', '完成', '取消', '候補']

export function registrationStatusTagType(status: string): 'success' | 'warning' | 'info' | 'danger' {
  if (status === '完成' || status === '已繳費') return 'success'
  if (status === '待確認' || status === '候補') return 'warning'
  if (status === '取消') return 'danger'
  return 'info' // 已確認
}
