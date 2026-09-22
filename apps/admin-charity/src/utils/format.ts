/** 共用格式化工具（日期格式規則見 docs/06-conventions.md §5：日期一律 yyyy-mm-dd） */

export function formatMoney(amount: number): string {
  return `NT$${amount.toLocaleString('zh-Hant-TW')}`
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
