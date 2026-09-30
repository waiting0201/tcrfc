/**
 * E1a 六個模組（夥伴／贊助／提案下載／慈善／媒體專區／榮譽與里程碑）共用的 API 小工具。
 * 契約：apps/api/README.md「E1a」節「通則」。
 */
import { apiBlobRequest, apiRequest, apiUploadRequest } from './http'
import { saveBlob } from '@/utils/downloadFile'

/** 雙語內容的單一語系，各模組欄位不同，這裡只約束形狀。 */
export interface BilingualContentInput<T> {
  zh: T
  en?: T | null
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface BatchResultDto {
  updatedCount: number
  skipped: { id: string; reason: string }[]
}

export function buildQuery(params: Record<string, string | number | boolean | undefined | null>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '') continue
    search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

/** 圖片欄位語意（通則）：新檔案＝換圖、`removeXxx`＝移除、都沒有＝維持；兩者同時給後端回 400，
 * 所以這裡選了新檔案就一律不送 `removeXxx`。 */
export function imageIntent(file: File | null, remove: boolean): { file: File | null; remove: boolean } {
  return file ? { file, remove: false } : { file: null, remove }
}

/** 組 `multipart/form-data`：`payload`（JSON 文字）＋各檔案欄位（沒選檔就不放）。 */
export function buildMultipart(payload: unknown, files: Record<string, File | null | undefined> = {}): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  for (const [field, file] of Object.entries(files)) {
    if (file) form.append(field, file)
  }
  return form
}

/** 只有檔案欄位 `file` 的上傳（圖集一次一張）。 */
export function buildSingleFile(file: File): FormData {
  const form = new FormData()
  form.append('file', file)
  return form
}

/** 排序：`PUT …/order`，body `{ ids }`，成功 204。 */
export function putOrder(path: string, ids: string[]): Promise<void> {
  return apiRequest<void>(path, { method: 'PUT', body: { ids } })
}

export function postBatch(path: string, body: unknown): Promise<BatchResultDto> {
  return apiRequest<BatchResultDto>(path, { method: 'POST', body })
}

export { apiRequest, apiUploadRequest }

/** 把雙語輸入的英文版整理成「空白就省略」：後端規則是「英文版名稱／標題空白＝沒有英文版」，
 * 這裡先過濾掉，避免送出只有空字串的英文物件。 */
export function enOrUndefined<T extends object>(en: T, requiredKey: keyof T & string, ...anyKeys: (keyof T & string)[]): T | undefined {
  const keys = [requiredKey, ...anyKeys]
  const hasAny = keys.some((k) => {
    const v = en[k]
    return typeof v === 'string' && v.trim() !== ''
  })
  return hasAny ? en : undefined
}

/** 空字串轉 `null`（選填欄位）。 */
export function nullIfBlank(value: string | null | undefined): string | null {
  const v = (value ?? '').trim()
  return v === '' ? null : v
}

/** 需要登入權杖的檔案下載（CSV 匯出、簽到表以外的檔案）：取回 Blob 後交給瀏覽器另存。
 * `fallbackName` 只在伺服器沒給檔名時使用。 */
export async function downloadExport(path: string, fallbackName: string): Promise<void> {
  const result = await apiBlobRequest(path)
  saveBlob(result.blob, result.filename ?? fallbackName)
}
