// app/utils/program-session.ts — 05 課程梯次（P2）狀態與報名可否的共用判斷
//
// 🔴 梯次狀態在資料庫是**中文字面值**；公開 API 另提供穩定的 `statusCode`（open／full／waitlist／ended，shared/enums.json），本檔依 `statusCode` 判斷。字面值：（`sessions.status` 的 CHECK：開放／額滿／候補／已結束，
// db/club-schema.sql `CK_sessions_status`），不是 `open`／`waitlist` 這類英文代碼。2026-10-02 之前夏令營／冬令營
// 兩頁拿英文代碼比對，永遠找不到「開放中」的梯次（E-130）；所有判斷集中在這裡，頁面不得自己寫字面值。
//
// 規劃書 P2：「前台自動依狀態顯示『立即報名 / 額滿候補 / 已截止』」——
//   開放 → 立即報名；額滿／候補 → 額滿候補（後端仍受理，直接列入候補）；已結束 → 已截止。
//   另有「報名開始／截止時間」窗口，窗口外同樣不受理（後端 SubmitRegistrationAsync 為最終裁判）。

export const SESSION_STATUS = {
  open: '開放',
  full: '額滿',
  waitlist: '候補',
  ended: '已結束',
} as const

/** 穩定狀態代碼表（`shared/enums.json`）：梯次／試訓場次與報名的 `statusCode` → 兩語系標籤。`status` 中文字面值只是相容保留，
 * 前台一律依 `statusCode` 判斷、依語系顯示（未知代碼容錯：優先用後端給的 `statusLabelZh`／`statusLabelEn`，再退回字面 `status`，最後才是代碼本身）。 */
export type SessionStatusCode = 'open' | 'full' | 'waitlist' | 'ended'
export type RegistrationStatusCode = 'pending' | 'confirmed' | 'paid' | 'completed' | 'cancelled' | 'waitlisted'

const STATUS_LABELS: Record<string, { zh: string, en: string }> = {
  open: { zh: '開放', en: 'Open' },
  full: { zh: '額滿', en: 'Full' },
  waitlist: { zh: '候補', en: 'Waitlist' },
  ended: { zh: '已結束', en: 'Ended' },
  pending: { zh: '待確認', en: 'Pending confirmation' },
  confirmed: { zh: '已確認', en: 'Confirmed' },
  paid: { zh: '已繳費', en: 'Paid' },
  completed: { zh: '完成', en: 'Completed' },
  cancelled: { zh: '取消', en: 'Cancelled' },
  waitlisted: { zh: '候補', en: 'Waitlisted' },
}

export interface StatusFields {
  status?: string | null
  statusCode?: string | null
  statusLabelZh?: string | null
  statusLabelEn?: string | null
}

/** 依語系顯示狀態標籤；後端標籤優先，其次本地代碼表，再其次字面 `status`／代碼本身（未知代碼不丟錯）。 */
export function statusLabel(s: StatusFields, locale: 'zh' | 'en' = 'zh'): string {
  const fromApi = locale === 'en' ? s.statusLabelEn : s.statusLabelZh
  if (fromApi) return fromApi
  const known = s.statusCode ? STATUS_LABELS[s.statusCode] : undefined
  if (known) return known[locale]
  return s.status || s.statusCode || ''
}

/** 梯次狀態代碼：優先 `statusCode`；舊回應沒有時才由中文字面值反查（相容用，之後可移除）。 */
export function sessionStatusCode(s: { status?: string | null, statusCode?: string | null }): string {
  if (s.statusCode) return s.statusCode
  const hit = Object.entries(SESSION_STATUS).find(([, lit]) => lit === s.status)
  return hit ? hit[0] : ''
}

export interface ProgramSessionLike {
  status?: string | null
  statusCode?: string | null
  signupOpensAt?: string | null
  signupClosesAt?: string | null
}

export type SessionSignupState = 'open' | 'waitlist' | 'not_yet' | 'closed'

/** 前台顯示用的報名狀態（只用於呈現，不是安全判斷）。 */
export function sessionSignupState(s: ProgramSessionLike, now: Date = new Date()): SessionSignupState {
  const code = sessionStatusCode(s)
  if (code === 'ended') return 'closed'
  if (s.signupOpensAt && now < new Date(s.signupOpensAt)) return 'not_yet'
  if (s.signupClosesAt && now > new Date(s.signupClosesAt)) return 'closed'
  if (code === 'open') return 'open'
  // 未知代碼保守視為不可報名（不假裝能報；後端仍是最終裁判，新增代碼時回頭補這裡）
  return code === 'full' || code === 'waitlist' ? 'waitlist' : 'closed'
}

/** 目前收得到報名的梯次（含候補）。 */
export function isSessionRegistrable(s: ProgramSessionLike, now: Date = new Date()): boolean {
  const state = sessionSignupState(s, now)
  return state === 'open' || state === 'waitlist'
}

export const SESSION_SIGNUP_LABEL: Record<SessionSignupState, string> = {
  open: '立即報名',
  waitlist: '額滿候補',
  not_yet: '尚未開放報名',
  closed: '已截止',
}
