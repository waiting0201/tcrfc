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
