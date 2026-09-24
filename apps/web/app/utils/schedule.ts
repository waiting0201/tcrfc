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

/**
 * `matches.status`（DB／API 字面值）→ 畫面顯示碼／文字／schema.org 型別的**全站唯一對照表**。
 *
 * 🔴 **這是本檔案存在的核心理由，不要在別處另開第二份。** S0-9j 修的那個 bug
 * （藍鯨 21 場已完成賽事顯示成「未開始」）根因就是同一組 enum 曾經有兩份各寫各的對照表：
 * 本函式的 switch 對到的是 `'finished'`，但 `apps/web/app/pages/zh/schedule.vue` 的
 * SportsEvent JSON-LD（GEO-08）另外寫了一份 `EVENT_STATUS_MAP` 對到 `'played'`——
 * 後者才是資料庫真正存的值，前者從來沒被任何真實資料打中過。兩份表沒有任何機制
 * 互相對照，型別也擋不住（都是 `string`），才會放到上線後才被發現。
 * `matchStatusSchemaOrg()`（本檔另一個 export）就是原本那份 JSON-LD 用表收斂過來的，
 * `schedule.vue` 不應該再自己維護一份。
 *
 * **已用真實資料驗證的值**（2026-09-23，`SELECT status, COUNT(*) FROM matches
 * GROUP BY status` 查 `tcrfc_club_dev`）：`'scheduled'` 21 筆（磐石一線隊）、
 * `'played'` 21 筆（藍鯨一線隊 2023 木蘭聯賽＋2025 總統盃），無 NULL、無其他值
 * （42 筆賽事查全表，`matches.status` 是 `nvarchar(16) NULL`、無 CHECK 約束，
 * 見 `db/club-schema.sql`，值完全由應用層——目前是 `db/seed/generate-club-seed-sql.py`
 * ——自行決定，DB 層不提供任何保證）。
 * `'postponed'`／`'cancelled'`／`'live'` 三個字面值**尚未有真實資料可核對**，
 * 沿用既有 CSS class（`status-pill--postponed`／`--cancelled`／`--live`）與
 * `scheduled`／`played` 的命名風格推斷，之後有賽事真的延期／取消／進行中時要重新核對。
 * `postponed` 的中文顯示字定為「延賽」（規劃書 v3.13 修訂摘要：「賽事狀態的中文用語
 * 定為球界慣用的『延賽』」，3.13、4.3 C4、5.1 `Match` 三處已一致改用「延賽」，
 * 舊版「延期」寫法已汰換）。
 */
interface MatchStatusMeta {
  /** 畫面用：CSS class（`status-pill--{code}`）與篩選邏輯（`cardMatches`）的比對碼 */
  code: string
  /** 畫面用：status-pill 中文顯示文字 */
  label: string
  /** SportsEvent JSON-LD（GEO-08）用：schema.org EventStatusType */
  schemaOrg: string
}

const MATCH_STATUS_MAP: Record<string, MatchStatusMeta> = {
  scheduled: { code: 'upcoming', label: '未開始', schemaOrg: 'https://schema.org/EventScheduled' },
  played: { code: 'finished', label: '已結束', schemaOrg: 'https://schema.org/EventCompleted' },
  postponed: { code: 'postponed', label: '延賽', schemaOrg: 'https://schema.org/EventPostponed' },
  cancelled: { code: 'cancelled', label: '取消', schemaOrg: 'https://schema.org/EventCancelled' },
  // schema.org 沒有「進行中」對應的 EventStatusType（官方列舉只有 Scheduled／Cancelled／
  // Postponed／Rescheduled／MovedOnline），比賽進行中維持算 EventScheduled 最接近事實。
  live: { code: 'live', label: '比賽中', schemaOrg: 'https://schema.org/EventScheduled' },
}

/** 未知或空值一律 fallback 為「未開始」，與 mockup 原本 switch 的 default 行為一致 */
const DEFAULT_STATUS_META: MatchStatusMeta = MATCH_STATUS_MAP.scheduled!

export function mapMatchStatus(status: string | null): { code: string; label: string } {
  const meta = (status && MATCH_STATUS_MAP[status]) || DEFAULT_STATUS_META
  return { code: meta.code, label: meta.label }
}

/** SportsEvent JSON-LD（GEO-08）用，見 `schedule.vue` 的 `sportsEvents`。
 * 取代原本獨立維護的 `EVENT_STATUS_MAP`，收斂成單一來源。 */
export function matchStatusSchemaOrg(status: string | null): string {
  const meta = (status && MATCH_STATUS_MAP[status]) || DEFAULT_STATUS_META
  return meta.schemaOrg
}

/**
 * 延賽賽事卡片的原定時間顯示文字，例如「原定 2026-10-03 19:30」（規劃書 v3.13 §3.13：
 * 「延賽須標示原定時間」；`MatchDto.originalMatchOn`／`originalKickoff` 僅在賽事狀態為
 * 「延賽」時才有值，其餘狀態一律是 `null`，呼叫端不需要另外判斷 `status`）。
 * 只有原定日期、沒有原定時間時仍要能正常顯示（只顯示日期）；`originalMatchOn` 為
 * `null` 時視為兩欄皆空，回傳 `null`、呼叫端不渲染任何東西。
 * ⚠️ **只用人造資料驗證過**（S0-9l，2026-09-24）：資料庫目前沒有任何 `status = 'postponed'`
 * 的真實賽事（見本檔 `MATCH_STATUS_MAP` 上方註解），驗證方式是本機起一支假 `apps/api`
 * 回傳兩筆人造延賽資料（一筆有原定時間、一筆只有原定日期）跑一次 SSR 輸出比對，
 * 沒有真實資料可核對。之後真的出現延賽資料時要重新核對顯示文字與版位。
 */
export function postponedNote(originalMatchOn: string | null, originalKickoff: string | null): string | null {
  if (!originalMatchOn) return null
  return originalKickoff ? `原定 ${originalMatchOn} ${originalKickoff}` : `原定 ${originalMatchOn}`
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
