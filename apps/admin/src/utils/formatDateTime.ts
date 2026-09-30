/**
 * 把 API 回傳的 ISO 8601 時間字串（如 `2026-09-22T07:16:14.426`）轉成畫面上慣用的
 * `YYYY-MM-DD HH:mm`（docs/06-conventions.md 日期格式，沿用 v1 mockup 假資料原本的寫法）。
 * 輸入不是合法日期就原樣傳回，不讓畫面整個壞掉。
 */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`
}

/**
 * 後端時間戳一律是 UTC、JSON 不帶時區記號（C1 通則）：沒有時區字尾就補上 `Z` 再解析。
 * 一般 `new Date('2026-09-30T09:12:33')` 會被當成本機時間，直接顯示會差 8 小時。
 */
export function parseUtc(value: string): Date {
  return new Date(/(Z|[+-]\d\d:?\d\d)$/.test(value) ? value : `${value}Z`)
}

/** UTC 時間戳 → 本機（台灣）`YYYY-MM-DD HH:mm`。 */
export function formatUtcDateTime(value: string | null | undefined): string {
  if (!value) return ''
  const date = parseUtc(value)
  if (Number.isNaN(date.getTime())) return value
  return formatDateTime(date.toISOString().replace('Z', '') + 'Z')
}

const pad2 = (n: number) => String(n).padStart(2, '0')

/** UTC 時間戳 → 日期時間選擇器用的本機字串（`YYYY-MM-DDTHH:mm:ss`）；空值回空字串。 */
export function utcToLocalInput(value: string | null | undefined): string {
  if (!value) return ''
  const d = parseUtc(value)
  if (Number.isNaN(d.getTime())) return ''
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}T${pad2(d.getHours())}:${pad2(d.getMinutes())}:${pad2(d.getSeconds())}`
}

/** 日期時間選擇器的本機字串 → 帶 `Z` 的 UTC ISO 字串（送給後端）；空字串回 null。 */
export function localInputToUtc(value: string | null | undefined): string | null {
  if (!value) return null
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? null : d.toISOString()
}
