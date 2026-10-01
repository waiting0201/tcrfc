// app/utils/donation-status.ts — 捐款單狀態的顯示對照表。
// 一律「文字標籤為主，顏色為輔」（docs/22-charity-ui.md §1.8），不得只靠顏色傳達狀態。
import type { DonationStatus } from '../types/charity'
import type { Lang } from './i18n'
import { t } from './i18n'

export type ResultState = 'success' | 'processing' | 'incomplete'

/** 結果頁三態：已完成（含已退款，當初付款是成功的）／處理中（仍在確認，不可誤導成失敗）／未完成（可重試）。 */
export function resultStateOf(status: DonationStatus, processing: boolean): ResultState {
  if (status === 'paid' || status === 'refunded') return 'success'
  if (processing || status === 'pending') return 'processing'
  return 'incomplete'
}

export function donationStatusTagClass(status: DonationStatus): string {
  switch (status) {
    case 'paid':
      return 'tag-success'
    case 'failed':
      return 'tag-danger'
    case 'expired':
      return 'tag-warning'
    case 'refunded':
      return 'tag-refund'
    case 'pending':
      return 'tag-info'
    case 'created':
    default:
      return 'tag-neutral'
  }
}

export function donationStatusLabel(lang: Lang, status: DonationStatus): string {
  return t(lang).status[status]
}
