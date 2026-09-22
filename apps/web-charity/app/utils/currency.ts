// app/utils/currency.ts — 新台幣金額格式化，全站共用避免各頁面各自拼字串。
export function formatTwd(amount: number): string {
  return `NT$${amount.toLocaleString('en-US')}`
}
