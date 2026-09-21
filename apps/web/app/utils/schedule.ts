// app/utils/schedule.ts — 13 賽事行事曆頁的顯示格式工具（S0-9 資料驅動頁搬遷）
//
// mockup 的 fixture-card 需要好幾個「從日期算出來」的顯示值（星期、月份英文縮寫、
// 月份標題）；原本是 build.mjs 從 JSON 的日期字串算好烤進 HTML，這裡改成從
// MatchDto.matchOn（YYYY-MM-DD）即時計算。⚠️ 一律用 Date.UTC 組日期再取
// getUTCDay()／getUTCMonth()，不要用 `new Date(dateStr)` 直接讀本地時區的
// getDay()——後者在 SSR（伺服器時區）與瀏覽器（使用者時區）可能算出不同的星期，
// 純日期（無時間)字串不該受時區影響。

const WEEKDAY_ZH = ['週日', '週一', '週二', '週三', '週四', '週五', '週六']
const WEEKDAY_EN = ['SUN', 'MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT']
const MONTH_EN_FULL = [
  'JANUARY', 'FEBRUARY', 'MARCH', 'APRIL', 'MAY', 'JUNE',
  'JULY', 'AUGUST', 'SEPTEMBER', 'OCTOBER', 'NOVEMBER', 'DECEMBER',
]
const MONTH_EN_ABBR = ['JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN', 'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC']
const MONTH_ZH = ['一月', '二月', '三月', '四月', '五月', '六月', '七月', '八月', '九月', '十月', '十一月', '十二月']

function parseDateOnly(dateStr: string): { y: number; m: number; d: number } {
  const [y, m, d] = dateStr.split('-').map(Number)
  return { y: y!, m: m!, d: d! }
}

export function matchWeekday(dateStr: string): { zh: string; en: string } {
  const { y, m, d } = parseDateOnly(dateStr)
  const dow = new Date(Date.UTC(y, m - 1, d)).getUTCDay()
  return { zh: WEEKDAY_ZH[dow]!, en: WEEKDAY_EN[dow]! }
}

export function matchDay(dateStr: string): string {
  return parseDateOnly(dateStr).d.toString().padStart(2, '0')
}

export function matchMonthAbbr(dateStr: string): string {
  return MONTH_EN_ABBR[parseDateOnly(dateStr).m - 1]!
}

/** 月份分組標題，例："九月 SEPTEMBER 2026" */
export function monthHeading(monthKey: string): string {
  const [y, m] = monthKey.split('-').map(Number)
  return `${MONTH_ZH[m! - 1]} ${MONTH_EN_FULL[m! - 1]} ${y}`
}

/** 月曆檢視用（cal-month__title），例："九月 2026" */
export function calMonthTitle(monthKey: string): string {
  const [y, m] = monthKey.split('-').map(Number)
  return `${MONTH_ZH[m! - 1]} ${y}`
}

/** API status → mockup 的 data-status／狀態文字。⚠️ 目前 21 筆種子資料全是
 * 'scheduled'，只有 upcoming 一種狀態實際驗證過；其餘三種是依 CSS 既有的
 * status-pill--live／--finished／--postponed／--cancelled class 合理推斷，
 * 沒有真實資料可比對，之後有賽果資料時要重新核對顯示文字。 */
export function mapMatchStatus(status: string | null): { code: string; label: string } {
  switch (status) {
    case 'finished':
      return { code: 'finished', label: '已結束' }
    case 'live':
      return { code: 'live', label: '比賽中' }
    case 'postponed':
      return { code: 'postponed', label: '延賽' }
    case 'cancelled':
      return { code: 'cancelled', label: '取消' }
    case 'scheduled':
    default:
      return { code: 'upcoming', label: '未開始' }
  }
}

export function compTagLabel(comp: string | null): string {
  switch (comp) {
    case 'cup':
      return '盃賽 CUP'
    case 'friendly':
      return '友誼賽 FRIENDLY'
    case 'other':
      return '其他 OTHER'
    case 'league':
    default:
      return '聯賽 LEAGUE'
  }
}

export function venueMapUrl(venue: string): string {
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(`${venue} 台灣`)}`
}

/** data-ha 值：API 是大寫 HOME／AWAY，mockup 用小寫 */
export function haCode(homeAway: string | null): 'home' | 'away' {
  return homeAway === 'HOME' ? 'home' : 'away'
}

/** 賽事卡片錨點 id，與 mockup 逐字元相同：fx-{日期}-{h|a}-{場次編號}
 * （例如 fx-2026-09-13-a-3）。⚠️ id 裡的主客場是單一字母 a／h，
 * 跟 {@link haCode} 給 data-ha 屬性用的完整單字 home／away 是兩套不同的編碼，
 * 不要互用（已踩過一次：套用 haCode() 讓 id 變成 fx-2026-09-13-away-3，
 * 跟 mockup 對不起來）。matchNo 來自 matches.match_no（v3.11 補進規格與 DDL 的
 * 聯賽官方場次編號欄位），S0-9 搬遷當下 API 尚未吐出這個欄位時曾經退化成
 * `fx-{日期}-{h|a}`，該落差已隨本次補齊消除。 */
export function fixtureId(dateStr: string, homeAway: string | null, matchNo: number | null): string {
  return `fx-${dateStr}-${homeAway === 'HOME' ? 'h' : 'a'}-${matchNo}`
}
