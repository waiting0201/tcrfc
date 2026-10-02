/** 發票與收據（憑證）管理：`/api/v1/donation-platform/admin/invoices`。 */
import { ADMIN_ROOT, apiBlob, apiRequest, buildQuery, saveBlob } from './http'
import type { PagedResult } from './types'

const ROOT = `${ADMIN_ROOT}/invoices`

export interface InvoiceItem {
  invoiceId: string
  donationId: string
  orderNo: string
  invoiceType: 'b2c_invoice' | 'donation_receipt'
  invoiceNo: string | null
  issuedAt: string | null
  issueStatus: 'pending' | 'issued' | 'failed'
  voidStatus: 'none' | 'voided' | 'allowance'
  voidReason: string | null
  amount: number
  paidAt: string | null
  donationStatus: string
  projectName: string | null
  taxId: string | null
  invoiceTitle: string | null
  isAnnualSummary: boolean
}

export interface InvoiceFilter {
  from?: string
  to?: string
  issueStatus?: string
  voidStatus?: string
  invoiceType?: string
  projectId?: string
  keyword?: string
}

export const listInvoices = (filter: InvoiceFilter, page: number, pageSize: number) =>
  apiRequest<PagedResult<InvoiceItem>>(`${ROOT}${buildQuery({ ...filter, page, pageSize })}`)
export const setManualInvoiceNumber = (id: string, input: { invoiceNo: string; issuedOn: string | null; reason: string }) =>
  apiRequest<InvoiceItem>(`${ROOT}/${id}/manual-number`, { method: 'POST', body: input })
export const voidInvoice = (id: string, reason: string) => apiRequest<InvoiceItem>(`${ROOT}/${id}/void`, { method: 'POST', body: { reason } })
export const allowInvoice = (id: string, reason: string) => apiRequest<InvoiceItem>(`${ROOT}/${id}/allowance`, { method: 'POST', body: { reason } })

/** 供會計申報的明細 CSV（不含個資）。 */
export async function exportInvoices(filter: InvoiceFilter): Promise<void> {
  const { blob, filename } = await apiBlob(`${ROOT}/export${buildQuery({ ...filter })}`)
  saveBlob(blob, filename ?? 'invoices.csv')
}
