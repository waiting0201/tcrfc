// app/utils/donation-status.ts — 捐款單狀態的顯示對照表。
// 一律「文字標籤為主，顏色為輔」（docs/22-charity-ui.md §1.8），不得只靠顏色傳達狀態。
import type { DonationStatus } from '../../server/utils/fixtures'
import type { Lang } from './i18n'
import { t } from './i18n'

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
