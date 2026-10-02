/** N1 店家管理與 QR Code：`/api/v1/donation-platform/admin/stores`。 */
import { ADMIN_ROOT, AdminApiError, apiBlob, apiPostRaw, apiRequest, apiUpload, buildQuery, saveBlob } from './http'
import type { PagedResult } from './types'

const ROOT = `${ADMIN_ROOT}/stores`

export interface StoreListItem {
  id: string
  slug: string
  nameZh: string | null
  nameEn: string | null
  category: string | null
  status: 'active' | 'inactive'
  storeSharePct: number
  startOn: string | null
  endOn: string | null
  logoUrl: string | null
  paidCount: number
  paidTotal: number
  storeShareAccrued: number
}

export interface StoreDetail extends StoreListItem {
  logoAltZh: string | null
  logoAltEn: string | null
  address: string | null
  contactName: string | null
  contactPhone: string | null
  qrTargetUrl: string | null
}

/** 建立／更新：`storeSharePct` 省略代表不變（更新）或 0（建立）；網址名稱一律由系統產生，不送。 */
export interface StoreInput {
  nameZh: string
  nameEn: string | null
  logoAltZh: string | null
  logoAltEn: string | null
  category: string | null
  address: string | null
  contactName: string | null
  contactPhone: string | null
  startOn: string | null
  endOn: string | null
  status: 'active' | 'inactive'
  storeSharePct?: number
}

export const listStores = (q: { keyword?: string; status?: string; page?: number; pageSize?: number }) =>
  apiRequest<PagedResult<StoreListItem>>(`${ROOT}${buildQuery(q)}`)
export const getStore = (id: string) => apiRequest<StoreDetail>(`${ROOT}/${id}`)
export const createStore = (input: StoreInput) => apiRequest<StoreDetail>(ROOT, { method: 'POST', body: input })
export const updateStore = (id: string, input: StoreInput) => apiRequest<StoreDetail>(`${ROOT}/${id}`, { method: 'PUT', body: input })
export const regenerateStoreSlug = (id: string) =>
  apiRequest<{ id: string; slug: string; qrTargetUrl: string | null }>(`${ROOT}/${id}/regenerate-slug`, { method: 'POST', body: { confirm: true } })
export const uploadStoreLogo = (id: string, file: File) => apiUpload<StoreDetail>(`${ROOT}/${id}/logo`, file)
export const removeStoreLogo = (id: string) => apiRequest<void>(`${ROOT}/${id}/logo`, { method: 'DELETE' })

export async function downloadStoreQr(id: string, format: 'png' | 'svg', fallbackName: string): Promise<void> {
  const { blob, filename } = await apiBlob(`${ROOT}/${id}/qr${buildQuery({ format })}`)
  saveBlob(blob, filename ?? `${fallbackName}.${format}`)
}

export async function downloadAllStoreQr(format: 'png' | 'svg'): Promise<void> {
  const { blob, filename } = await apiBlob(`${ROOT}/qr-export${buildQuery({ format })}`)
  saveBlob(blob, filename ?? 'store-qr-codes.zip')
}

// ── 批次匯入（CSV） ────────────────────────────────────────────────────

export interface StoreImportIssue { rowNumber: number; reason: string }
export interface StoreImportResult { importedCount: number; errors: StoreImportIssue[]; skipped: StoreImportIssue[] }

/** 下載匯入範本（表頭＋一列範例，範本本身可直接匯入）。 */
export async function downloadStoreImportTemplate(): Promise<void> {
  const { blob, filename } = await apiBlob(`${ROOT}/import-template`)
  saveBlob(blob, filename ?? 'store-import-template.csv')
}

/**
 * 匯入店家 CSV。任一列有錯整批不寫入：後端回 400，本文是 `{ importedCount: 0, errors: [...] }`，
 * 這種情況不當成例外往外丟，而是回傳結果讓畫面列出所有錯誤列；其他錯誤（403、413…）照常丟 `AdminApiError`。
 */
export async function importStores(file: File, skipDuplicates: boolean): Promise<StoreImportResult> {
  try {
    return await apiPostRaw<StoreImportResult>(`${ROOT}/import${buildQuery({ skipDuplicates })}`, file, 'text/csv')
  } catch (error) {
    if (error instanceof AdminApiError && error.status === 400) {
      const body = error.body as Partial<StoreImportResult> | null
      if (body && Array.isArray(body.errors)) {
        return { importedCount: 0, errors: body.errors, skipped: body.skipped ?? [] }
      }
    }
    throw error
  }
}

/** 取 QR 圖的預覽（PNG Blob URL，呼叫端負責 revoke）。 */
export async function fetchStoreQrPreview(id: string): Promise<string> {
  const { blob } = await apiBlob(`${ROOT}/${id}/qr${buildQuery({ format: 'png' })}`)
  return URL.createObjectURL(blob)
}
