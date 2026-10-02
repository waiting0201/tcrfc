/** 共用格式化工具（日期格式規則見 docs/06-conventions.md §5：日期一律 yyyy-mm-dd） */

export function formatMoney(amount: number): string {
  return `NT$${amount.toLocaleString('zh-Hant-TW')}`
}

/** 可能為負數的金額（結算沖回、應付淨額）：負數顯示成「−NT$500」，不是「NT$-500」。 */
export function formatMoneySigned(amount: number): string {
  return amount < 0 ? `−${formatMoney(-amount)}` : formatMoney(amount)
}

/** 0–1 的比例 → 百分比字串（保留一位小數，整數不帶小數點）。 */
export function formatRatio(ratio: number): string {
  const pct = Math.round(ratio * 1000) / 10
  return `${pct}%`
}

export function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  return iso.slice(0, 10)
}

export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—'
  return iso.replace('T', ' ').slice(0, 16)
}

export function formatPercent(value: number): string {
  return `${value}%`
}

/**
 * 時間處理（沿用主站後台 E-90 的教訓）：API 的時間一律是 UTC 並帶 `Z`；畫面一律以台灣時間（UTC+8，固定偏移，
 * 台灣無日光節約）顯示，不依賴瀏覽器所在時區。
 */
export function parseUtcMs(iso: string): number {
  return Date.parse(/[zZ]|[+-]\d{2}:?\d{2}$/.test(iso) ? iso : `${iso}Z`)
}

const TAIPEI_OFFSET_MS = 8 * 60 * 60 * 1000

/** UTC 時間字串 → `yyyy-mm-dd HH:mm`（台灣時間）。 */
export function formatTaipei(iso: string | null | undefined): string {
  if (!iso) return '—'
  const shifted = new Date(parseUtcMs(iso) + TAIPEI_OFFSET_MS)
  return shifted.toISOString().replace('T', ' ').slice(0, 16)
}

/** 目前的台灣日期 `yyyy-mm-dd`。 */
export function todayTaipei(): string {
  return new Date(Date.now() + TAIPEI_OFFSET_MS).toISOString().slice(0, 10)
}
