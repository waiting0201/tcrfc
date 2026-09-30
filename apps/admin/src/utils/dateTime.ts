/**
 * 全站唯一的日期時間工具（時區規則的單一來源）。
 *
 * 規則（apps/api/README.md C1 通則、`Common/UtcDateTimeJsonConverter.cs`）：
 * - 後端**輸出**的時間戳一律是 UTC 並帶 `Z`；**輸入**不帶時區記號的時間一律「視為 UTC」。
 *   所以畫面送出的時間一定要帶時區（`Z` 或 `+08:00`），不能送「2026-10-01T09:00:00」這種本地時間，
 *   否則後端會當成 UTC，整整差 8 小時。
 * - 畫面一律以**台灣時間（Asia/Taipei，UTC+8，沒有日光節約）**顯示與輸入，不依瀏覽器所在時區。
 *   為了不受瀏覽器時區影響，這裡不用 `Date` 的本機欄位（`getHours` 等）也不用 `toLocaleString`，
 *   一律用固定 +8 小時位移計算。
 * - 純日期欄位（`YYYY-MM-DD`）不是時間戳，不經過這裡的轉換。
 *
 * 🔴 全站其他檔案不得自行 `.toISOString()`、`toLocale*String()`、`getHours()`……
 * （`eslint.config.mjs` 的 `no-restricted-syntax` 會擋；要新增時間欄位請用這裡的函式）。
 */

const TAIPEI_OFFSET_MS = 8 * 60 * 60 * 1000
const HAS_ZONE = /(Z|[+-]\d\d:?\d\d)$/i
const pad2 = (n: number) => String(n).padStart(2, '0')

/** 解析後端時間戳：沒有時區記號一律視為 UTC（與後端輸入規則一致）。解析失敗回傳 `Invalid Date`。 */
export function parseUtc(value: string): Date {
  return new Date(HAS_ZONE.test(value) ? value : `${value}Z`)
}

/** 台灣時間的各欄位（把時間點加 8 小時後讀 UTC 欄位）。 */
function taipeiParts(date: Date) {
  const shifted = new Date(date.getTime() + TAIPEI_OFFSET_MS)
  return {
    y: shifted.getUTCFullYear(),
    m: shifted.getUTCMonth() + 1,
    d: shifted.getUTCDate(),
    h: shifted.getUTCHours(),
    mi: shifted.getUTCMinutes(),
    s: shifted.getUTCSeconds(),
  }
}

/** 台灣日期字串 `YYYY-MM-DD`。 */
function dateKey(p: { y: number; m: number; d: number }): string {
  return `${p.y}-${pad2(p.m)}-${pad2(p.d)}`
}

/** UTC 時間戳 → 台灣時間 `YYYY-MM-DD HH:mm`。空值回空字串；無法解析就原樣傳回（不讓畫面壞掉）。 */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) return ''
  const date = parseUtc(value)
  if (Number.isNaN(date.getTime())) return value
  const p = taipeiParts(date)
  return `${dateKey(p)} ${pad2(p.h)}:${pad2(p.mi)}`
}

/** UTC 時間戳 → 台灣日期 `YYYY-MM-DD`（月曆分格、依日期歸類都用這個，台灣清晨的活動才不會落到前一天）。 */
export function formatDate(value: string | null | undefined): string {
  if (!value) return ''
  const date = parseUtc(value)
  if (Number.isNaN(date.getTime())) return value
  return dateKey(taipeiParts(date))
}

/** 現在的台灣日期 `YYYY-MM-DD`（取代 `new Date()` 拼日期）。 */
export function taipeiToday(now: number = Date.now()): string {
  return dateKey(taipeiParts(new Date(now)))
}

/** UTC 時間戳 → 日期時間輸入框用的台灣時間字串 `YYYY-MM-DDTHH:mm:ss`（搭配 `value-format="YYYY-MM-DDTHH:mm:ss"`）。 */
export function utcToTaipeiInput(value: string | null | undefined): string {
  if (!value) return ''
  const date = parseUtc(value)
  if (Number.isNaN(date.getTime())) return ''
  const p = taipeiParts(date)
  return `${dateKey(p)}T${pad2(p.h)}:${pad2(p.mi)}:${pad2(p.s)}`
}

/**
 * 日期時間輸入框的台灣時間字串 → 帶 `Z` 的 UTC ISO（送給後端）；空字串回 null。
 * 輸入若已帶時區記號就照它算，沒有就當台灣時間（+08:00）。
 */
export function taipeiInputToUtc(value: string | null | undefined): string | null {
  if (!value) return null
  const date = new Date(HAS_ZONE.test(value) ? value : `${value}+08:00`)
  return Number.isNaN(date.getTime()) ? null : date.toISOString()
}

/**
 * UTC 時間戳 → 給 `el-date-picker`（`Date` 模型）的值：回傳的 `Date` 其「本機欄位」等於台灣時間的牆上時鐘，
 * 這樣不論瀏覽器在哪個時區，選擇器顯示的都是台灣時間。只用在選擇器模型，不能拿去做時間差運算。
 */
export function utcToPickerDate(value: string | null | undefined): Date | null {
  if (!value) return null
  const date = parseUtc(value)
  if (Number.isNaN(date.getTime())) return null
  const p = taipeiParts(date)
  return new Date(p.y, p.m - 1, p.d, p.h, p.mi, p.s)
}

/** `el-date-picker`（`Date` 模型，本機欄位＝台灣牆上時鐘）→ 帶 `Z` 的 UTC ISO；空值回 null。 */
export function pickerDateToUtc(date: Date | null | undefined): string | null {
  if (!date || Number.isNaN(date.getTime())) return null
  return taipeiInputToUtc(
    `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}T${pad2(date.getHours())}:${pad2(date.getMinutes())}:${pad2(date.getSeconds())}`,
  )
}

/**
 * 全天活動專用：後端規定全天活動以「UTC 日期」整天計（README 行事曆節）。
 * 選擇器選的台灣日期，送 `該日T00:00:00Z`——換算成台灣時間是當天 08:00，日期不會跑到前一天。
 */
export function pickerDateToAllDayUtc(date: Date | null | undefined): string | null {
  if (!date || Number.isNaN(date.getTime())) return null
  return `${pickerDateToDateOnly(date)}T00:00:00Z`
}

/** 現在時間對應的選擇器 `Date`（本機欄位＝台灣牆上時鐘），用來比較「排程時間是否已過」。 */
export function nowAsPickerDate(now: number = Date.now()): Date {
  const p = taipeiParts(new Date(now))
  return new Date(p.y, p.m - 1, p.d, p.h, p.mi, p.s)
}

/** 選擇器的 `Date`（純日期用，本機欄位）→ `YYYY-MM-DD`。日期欄位不含時區，不做位移；空值回 null。 */
export function pickerDateToDateOnly(date: Date): string
export function pickerDateToDateOnly(date: Date | null | undefined): string | null
export function pickerDateToDateOnly(date: Date | null | undefined): string | null {
  if (!date) return null
  return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`
}

/** `YYYY-MM-DD` → 選擇器的 `Date`（本機當天 00:00）；無法解析回 null。 */
export function dateOnlyToPickerDate(value: string | null | undefined): Date | null {
  if (!value) return null
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value)
  if (!m) return null
  return new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]))
}

/** 日期時間輸入框的台灣時間字串 → 毫秒時間點（只用來比較先後）；空或無法解析回 NaN。 */
export function taipeiInputToMs(value: string | null | undefined): number {
  const iso = taipeiInputToUtc(value)
  return iso ? new Date(iso).getTime() : Number.NaN
}

/** 台灣日期（`YYYY-MM-DD`）當天 00:00 對應的 UTC ISO——依日期區間查詢時用。 */
export function taipeiDayStartUtc(day: string): string | null {
  return taipeiInputToUtc(`${day}T00:00:00`)
}

/** UTC 時間戳加減天數（以「一天＝24 小時」計，台灣沒有日光節約，等同台灣日曆天）；解析失敗回 null。 */
export function addDaysUtc(value: string, days: number): string | null {
  const date = parseUtc(value)
  if (Number.isNaN(date.getTime())) return null
  return new Date(date.getTime() + days * 86400000).toISOString()
}

/** 兩個台灣日期（`YYYY-MM-DD`）相差幾天（純日期運算，不受時區影響）。 */
export function daysBetweenDates(fromDay: string, toDay: string): number {
  return Math.round((new Date(`${toDay}T00:00:00Z`).getTime() - new Date(`${fromDay}T00:00:00Z`).getTime()) / 86400000)
}

/** 兩個 UTC 時間戳的先後（毫秒差）；任一無法解析回 NaN。 */
export function diffMs(a: string, b: string): number {
  return parseUtc(a).getTime() - parseUtc(b).getTime()
}
