// app/utils/program-session.ts — 05 課程梯次（P2）狀態與報名可否的共用判斷
//
// 🔴 梯次狀態在資料庫與公開 API 都是**中文字面值**（`sessions.status` 的 CHECK：開放／額滿／候補／已結束，
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

export interface ProgramSessionLike {
  status: string
  signupOpensAt?: string | null
  signupClosesAt?: string | null
}

export type SessionSignupState = 'open' | 'waitlist' | 'not_yet' | 'closed'

/** 前台顯示用的報名狀態（只用於呈現，不是安全判斷）。 */
export function sessionSignupState(s: ProgramSessionLike, now: Date = new Date()): SessionSignupState {
  if (s.status === SESSION_STATUS.ended) return 'closed'
  if (s.signupOpensAt && now < new Date(s.signupOpensAt)) return 'not_yet'
  if (s.signupClosesAt && now > new Date(s.signupClosesAt)) return 'closed'
  if (s.status === SESSION_STATUS.open) return 'open'
  return 'waitlist'
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
