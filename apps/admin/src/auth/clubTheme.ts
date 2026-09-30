/**
 * 俱樂部配色切換（主站規劃書 v3.16 §4.0、docs/21-admin-ui.md §5.1）。
 *
 * 切換站台時後台主色隨之換成該俱樂部的品牌色：本檔只負責在 <html> 上設 `data-club` 屬性，
 * 實際色值全部在 `styles/admin-theme.css` 的 `html.dark[data-club='bw']` 區塊。
 *
 * 首次繪製：重新整理頁面時 JS 模組還沒執行、`/auth/me` 還沒回來，若等這些才設屬性會先閃磐石色。
 * 所以「目前俱樂部」另存一份在 localStorage（純每位使用者的便利，讀寫全部 try/catch，存不了就退回
 * 磐石預設），並由 index.html 的內嵌 script 在第一次繪製前同步套用——那段 script 的邏輯與
 * `readPersistedClub()`／`applyClubTheme()` 必須一致（改一邊要改另一邊）。
 * 登入畫面（尚未選擇俱樂部）一律用磐石預設，見 router 的 beforeEach。
 */

export const DEFAULT_CLUB_CODE = 'tcrfc'
const STORAGE_KEY = 'tcrfc-admin-active-club'

/** 有專屬配色的俱樂部代碼（其餘一律走預設磐石色）。 */
const THEMED_CLUBS = new Set(['tcrfc', 'bw'])

export function applyClubTheme(clubCode: string): void {
  const code = THEMED_CLUBS.has(clubCode) ? clubCode : DEFAULT_CLUB_CODE
  document.documentElement.setAttribute('data-club', code)
}

export function readPersistedClub(): string | null {
  try {
    const v = window.localStorage.getItem(STORAGE_KEY)
    return v && THEMED_CLUBS.has(v) ? v : null
  } catch {
    return null
  }
}

export function persistClub(clubCode: string | null): void {
  try {
    if (clubCode) window.localStorage.setItem(STORAGE_KEY, clubCode)
    else window.localStorage.removeItem(STORAGE_KEY)
  } catch {
    // 私密視窗或封鎖網站資料時存不了：只是重新整理後退回登入者的主要俱樂部，配色仍由 applyClubTheme 決定
  }
}
