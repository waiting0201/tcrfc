/** 金額的日常寫法：`NT$ 1,280`；沒有值回「—」。 */
export function formatMoney(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—'
  return `NT$ ${value.toLocaleString('zh-TW')}`
}
