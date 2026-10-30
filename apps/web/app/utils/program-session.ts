// app/utils/program-session.ts — 05 課程梯次（P2）狀態與報名可否的共用判斷
//
// 🔴 本檔一律依公開 API 的穩定 `statusCode` 判斷（open／full／waitlist／ended／not_yet_open，shared/enums.json
// 「ProgramSessionDto.statusCode 對外輸出值域」）。`status` 是資料庫儲存的中文字面值，**不得拿來判斷**（E-130 曾因英文／中文字面值混用，
// 永遠找不到開放中的梯次）；**也不得在前台比對報名起訖時間推算狀態**——`not_yet_open` 與「已過報名截止＝ended」都由伺服器
// （Common/SignupWindow）算好，前台只讀（主站 v3.32 P2 判定優先序：已結束→尚未開放→額滿／候補→開放；SO-3）。
//
// 規劃書 P2：「前台自動依狀態顯示『立即報名 / 額滿候補 / 已截止』」——
//   開放 → 立即報名；額滿／候補 → 額滿候補（後端仍受理，直接列入候補）；已結束 → 已截止；
//   尚未開放 → 停用按鈕＋「M 月 D 日開放報名」，不可送出、不可候補。

export const SESSION_STATUS = {
  open: '開放',
  full: '額滿',
  waitlist: '候補',
  ended: '已結束',
} as const

/** 穩定狀態代碼表（`shared/enums.json`）：梯次／試訓場次與報名的 `statusCode` → 兩語系標籤。`status` 中文字面值只是相容保留，
 * 前台一律依 `statusCode` 判斷、依語系顯示（未知代碼容錯：優先用後端給的 `statusLabelZh`／`statusLabelEn`，再退回字面 `status`，最後才是代碼本身）。 */
export type SessionStatusCode = 'open' | 'full' | 'waitlist' | 'ended' | 'not_yet_open'
export type RegistrationStatusCode = 'pending' | 'confirmed' | 'paid' | 'completed' | 'cancelled' | 'waitlisted'

const STATUS_LABELS: Record<string, { zh: string, en: string }> = {
  open: { zh: '開放', en: 'Open' },
  full: { zh: '額滿', en: 'Full' },
  waitlist: { zh: '候補', en: 'Waitlist' },
  ended: { zh: '已結束', en: 'Ended' },
  not_yet_open: { zh: '尚未開放', en: 'Not yet open' },
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

/** 梯次狀態代碼：一律讀伺服器回傳的 `statusCode`（SO-3，主站 v3.32 P2）。`status` 是儲存的中文字面值，**不得拿來判斷**；
 * 缺 `statusCode` 視為未知（保守當作不可報名）。「尚未開放」`not_yet_open` 是伺服器依報名開始時間推算的輸出，前台不自行比對時間。 */
export function sessionStatusCode(s: { statusCode?: string | null }): string {
  return s.statusCode ?? ''
}

export interface ProgramSessionLike {
  status?: string | null
  statusCode?: string | null
  statusLabelZh?: string | null
  statusLabelEn?: string | null
  /** UTC ISO。只用於顯示「M 月 D 日開放報名」，不用來推算狀態。 */
  signupOpensAt?: string | null
  signupClosesAt?: string | null
}

export type SessionSignupState = 'open' | 'waitlist' | 'not_yet' | 'closed'

/** 前台顯示用的報名狀態（只依 `statusCode`，不比對時間；後端 SubmitRegistrationAsync 為最終裁判）。
 * open→open；full／waitlist→waitlist（額滿候補）；not_yet_open→not_yet（停用、不可候補）；ended 與未知代碼→closed。 */
export function sessionSignupState(s: ProgramSessionLike): SessionSignupState {
  switch (sessionStatusCode(s)) {
    case 'open': return 'open'
    case 'full':
    case 'waitlist': return 'waitlist'
    case 'not_yet_open': return 'not_yet'
    default: return 'closed'
  }
}

/** 目前收得到報名的梯次（含候補；不含尚未開放）。 */
export function isSessionRegistrable(s: ProgramSessionLike): boolean {
  const state = sessionSignupState(s)
  return state === 'open' || state === 'waitlist'
}

/** 報名區要列出的梯次：可報名者加上尚未開放者（後者停用顯示開放日期）；已截止與未知不列。 */
export function isSessionListed(s: ProgramSessionLike): boolean {
  return isSessionRegistrable(s) || sessionSignupState(s) === 'not_yet'
}

export const SESSION_SIGNUP_LABEL: Record<SessionSignupState, string> = {
  open: '立即報名',
  waitlist: '額滿候補',
  not_yet: '尚未開放報名',
  closed: '已截止',
}

/** 英文版標籤（C-6／S2-13）。zh 沿用 `SESSION_SIGNUP_LABEL`，en 版由 `sessionSignupLabel(state, locale)` 取值。 */
export const SESSION_SIGNUP_LABEL_EN: Record<SessionSignupState, string> = {
  open: 'Register now',
  waitlist: 'Full - join waitlist',
  not_yet: 'Registration not open yet',
  closed: 'Closed',
}

export function sessionSignupLabel(state: SessionSignupState, locale: 'zh' | 'en' = 'zh'): string {
  return locale === 'en' ? SESSION_SIGNUP_LABEL_EN[state] : SESSION_SIGNUP_LABEL[state]
}

/** 「M 月 D 日開放報名」／`Opens for registration on D Mon`。只顯示日期，一律以台北時間換算（與使用者所在時區無關）；
 * 無法解析時回 null（呼叫端退回「尚未開放報名」）。 */
export function signupOpensText(iso: string | null | undefined, locale: 'zh' | 'en' = 'zh'): string | null {
  if (!iso) return null
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return null
  const parts = new Intl.DateTimeFormat('en-US', { timeZone: 'Asia/Taipei', month: 'numeric', day: 'numeric' }).formatToParts(d)
  const month = Number(parts.find((x) => x.type === 'month')?.value)
  const day = Number(parts.find((x) => x.type === 'day')?.value)
  if (!month || !day) return null
  if (locale === 'en') {
    const mon = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'][month - 1]
    return `Opens for registration on ${day} ${mon}`
  }
  return `${month} 月 ${day} 日開放報名`
}
