/**
 * 慈善後台呼叫 apps/api 的底層 fetch 封裝（路徑一律 `/api/v1/donation-platform/admin/…`）。
 * 做法比照 apps/admin 的 http.ts，但**不共用程式碼與 Cookie**：
 *  1. 帶 `Authorization: Bearer <存取權杖>`（權杖只放記憶體，見 auth/session.ts）。
 *  2. 401 且屬於「權杖過期」（ProblemDetails 有 title）時，用更新權杖 Cookie 換一次再重打；救不回來就導回登入。
 *  3. 錯誤統一轉成 `AdminApiError`（status＋日常中文 detail）。
 *  4. `credentials: 'include'`：後台前端與 API 是不同來源，Cookie 要靠這個才會附上（API 端 CORS 已開 AllowCredentials）。
 */
import { clearSession, getAccessToken } from '@/auth/session'
import { refreshAccessToken } from './auth'
import router from '@/router'

// API 位址的解析（執行期注入優先、正式建置不退回 127.0.0.1）集中在 runtimeConfig.ts
export { API_BASE_URL } from './runtimeConfig'
import { API_BASE_URL } from './runtimeConfig'
export const ADMIN_ROOT = '/api/v1/donation-platform/admin'

export class AdminApiError extends Error {
  status: number
  detail: string
  /** 回應本文（已解析的 JSON）。多數錯誤只有 detail；店家 CSV 匯入的 400 會帶逐列的錯誤清單，要從這裡取。 */
  body: unknown
  constructor(status: number, message: string, body: unknown = null) {
    super(message)
    this.status = status
    this.detail = message
    this.body = body
  }
}

interface ErrorBody { status?: number; title?: string; detail?: string; message?: string }

async function readErrorBody(response: Response): Promise<ErrorBody | null> {
  const text = await response.text().catch(() => '')
  if (!text.trim()) return null
  try {
    return JSON.parse(text) as ErrorBody
  } catch {
    return null
  }
}

function toError(status: number, body: ErrorBody | null): AdminApiError {
  const detail = body?.detail ?? body?.message
  if (detail) return new AdminApiError(status, detail, body)
  const fallback: Record<number, string> = {
    400: '輸入的內容有誤，請檢查後再試一次。',
    401: '請先登入。',
    403: '你沒有權限執行這個操作。',
    404: '找不到這筆資料。',
    409: '這筆資料目前無法這樣操作。',
    422: '輸入的內容不符合規則。',
    429: '操作太頻繁，請稍候幾分鐘再試。',
    501: '這個功能尚未提供。',
    503: '服務暫時無法使用，請稍後再試。',
  }
  return new AdminApiError(status, fallback[status] ?? '伺服器發生未預期的錯誤，請稍後再試。', body)
}

let redirecting = false
function redirectToLogin(): void {
  clearSession()
  if (redirecting) return
  redirecting = true
  const current = router.currentRoute.value
  const target = current.name === 'login' ? undefined : current.fullPath
  router.push({ name: 'login', query: target ? { redirect: target } : undefined }).finally(() => { redirecting = false })
}

async function doFetch(path: string, init: RequestInit, retry: boolean): Promise<Response> {
  const headers = new Headers(init.headers)
  const token = getAccessToken()
  if (token) headers.set('Authorization', `Bearer ${token}`)
  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers, credentials: 'include' })
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') throw cause
    throw new AdminApiError(0, '無法連線到後台服務，請確認網路是否正常。')
  }
  if (response.status === 401 && !retry) {
    const body = await readErrorBody(response.clone())
    if (body && typeof body.title === 'string') {
      if (await refreshAccessToken()) return doFetch(path, init, true)
      redirectToLogin()
      throw new AdminApiError(401, '登入已逾期，請重新登入。')
    }
  }
  if (!response.ok) throw toError(response.status, await readErrorBody(response))
  return response
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const init: RequestInit = { method: options.method ?? 'GET', signal: options.signal }
  if (options.body !== undefined) {
    init.body = JSON.stringify(options.body)
    init.headers = { 'Content-Type': 'application/json' }
  }
  const response = await doFetch(path, init, false)
  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/** 上傳單一檔案（multipart 欄位 `file`；不手動設 Content-Type，交給瀏覽器帶邊界字串）。 */
export async function apiUpload<T>(path: string, file: File): Promise<T> {
  const form = new FormData()
  form.append('file', file)
  const response = await doFetch(path, { method: 'POST', body: form }, false)
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/** 上傳原始位元組（店家 CSV 匯入）：body 就是檔案內容，不是 multipart。 */
export async function apiPostRaw<T>(path: string, file: File, contentType: string): Promise<T> {
  const response = await doFetch(path, { method: 'POST', body: file, headers: { 'Content-Type': contentType } }, false)
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export interface BlobResult { blob: Blob; filename: string | null }

function parseFilename(disposition: string | null): string | null {
  if (!disposition) return null
  const star = /filename\*=UTF-8''([^;]+)/i.exec(disposition)
  if (star?.[1]) {
    try { return decodeURIComponent(star[1]) } catch { return star[1] }
  }
  const plain = /filename="?([^";]+)"?/i.exec(disposition)
  return plain?.[1] ?? null
}

/** 需要登入權杖的檔案下載（QR 圖、zip、CSV）：取回 Blob 與伺服器建議的檔名。 */
export async function apiBlob(path: string): Promise<BlobResult> {
  const response = await doFetch(path, {}, false)
  return { blob: await response.blob(), filename: parseFilename(response.headers.get('Content-Disposition')) }
}

export function saveBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export function buildQuery(params: Record<string, string | number | boolean | undefined | null>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === '' || value === false) continue
    search.set(key, String(value))
  }
  const q = search.toString()
  return q ? `?${q}` : ''
}
