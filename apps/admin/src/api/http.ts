/**
 * `apps/api`（.NET 後台）的底層 fetch 封裝——真實登入版（取代已刪除的
 * `X-Dev-Operator-Id`／開發寫入閘門機制，見 apps/api/README.md「開發模式開關：已刪除」）。
 *
 * 四件事集中在這裡做，讓每支呼叫端點的函式不必重複處理：
 * 1. 帶上 `Authorization: Bearer <存取權杖>`（見 `@/auth/session`，權杖只放記憶體）。
 * 2. 401 時自動用更新權杖（HttpOnly Cookie）換一次新的存取權杖，成功就重打原本這次請求一次；
 *    仍然失敗就清除工作階段、導回登入頁——這整套只在「權杖過期」這種情況下觸發，不會跟
 *    「密碼／驗證碼本身就是錯的」這種業務層 401 搞混（見 `looksLikeSessionExpired()` 的說明）。
 * 3. 把後端的 ProblemDetails（`{status,title,detail}`）與少數端點自己的 `{message}` 形狀、
 *   以及「完全連不上」三種情況，統一轉成帶有明確種類（kind）的 `AdminApiError`。
 * 4. `credentials: 'include'`——後台前端與 API 是不同來源（子網域），更新權杖 Cookie 要靠這個
 *   才會被瀏覽器帶上（`docs/17-deployment.md` 的部署拓撲，API 端已設 `AllowCredentials()`）。
 */
import { clearSession, getAccessToken } from '@/auth/session'
import { refreshAccessToken } from './adminAuth'
import router from '@/router'

export const API_BASE_URL = (import.meta.env.VITE_ADMIN_API_BASE_URL as string | undefined)?.replace(/\/$/, '')
  || 'http://127.0.0.1:5299'

export type AdminApiErrorKind =
  | 'validation' // 400
  | 'slug-conflict' // 409，網址名稱重複
  | 'concurrency-conflict' // 409，資料已被其他人變更
  | 'featured-limit' // 409，置頂精選已達上限
  | 'status-conflict' // 409，狀態轉換不允許
  | 'forbidden' // 403，沒有權限／共用內容唯讀
  | 'not-found' // 404
  | 'invalid-credential' // 401，但不是「登入已逾期」——是密碼／驗證碼本身不正確一類的業務判斷
  | 'unauthenticated' // 401 且判定為「登入已逾期」，重新整理權杖失敗後最終丟出
  | 'network' // fetch 本身失敗：離線、CORS 被擋、伺服器沒啟動
  | 'server' // 500
  | 'unknown'

export class AdminApiError extends Error {
  kind: AdminApiErrorKind
  status?: number
  detail?: string

  constructor(kind: AdminApiErrorKind, message: string, options?: { status?: number; detail?: string }) {
    super(message)
    this.kind = kind
    this.status = options?.status
    this.detail = options?.detail
  }
}

interface ErrorBody {
  status?: number
  title?: string
  detail?: string
  message?: string
}

/**
 * 判斷這個 401 是不是「權杖過期／根本沒登入」——`AdminUnauthenticatedException` 走 ASP.NET Core
 * 的 ProblemDetails 中介軟體，一定有 `title`／`status` 欄位（見 apps/api/README.md 情境一的
 * curl 範例：`{"title":"請先登入","status":401,"detail":"請先登入後台。",...}`）。
 * 相對地，`/auth/change-password`／`/auth/2fa/confirm`／`/auth/2fa/disable` 這幾支端點對「密碼／
 * 驗證碼本身不正確」的 401／400 是自己手刻 `Results.Json(new { message = "..." })`，**沒有
 * `title` 欄位**——這種 401 重打一次原始請求也不會變成功（因為問題不是權杖），不該觸發整套
 * refresh-retry 機制，只是單純把訊息顯示給使用者。
 */
function looksLikeSessionExpired(body: ErrorBody | null): boolean {
  return !!body && typeof body.title === 'string'
}

function classifyByStatus(body: ErrorBody | null, status: number): AdminApiError {
  const detail = body?.detail ?? body?.message ?? ''
  const title = body?.title

  if (status === 400) return new AdminApiError('validation', detail || '輸入內容有誤', { status, detail })
  if (status === 401) return new AdminApiError('invalid-credential', detail || '帳號或密碼不正確。', { status, detail })
  if (status === 403) return new AdminApiError('forbidden', detail || '你沒有權限執行這個操作。', { status, detail })
  if (status === 404) return new AdminApiError('not-found', detail || '找不到這筆資料', { status, detail })
  if (status === 409) {
    if (title === '網址名稱重複') return new AdminApiError('slug-conflict', detail, { status, detail })
    if (title === '資料已被變更') return new AdminApiError('concurrency-conflict', detail, { status, detail })
    if (title === '置頂精選已達上限') return new AdminApiError('featured-limit', detail, { status, detail })
    if (title === '狀態轉換不允許') return new AdminApiError('status-conflict', detail, { status, detail })
    return new AdminApiError('unknown', detail || '這筆資料目前無法這樣操作', { status, detail })
  }
  return new AdminApiError('server', '伺服器發生未預期的錯誤，請稍後再試。', { status, detail })
}

async function readErrorBody(response: Response): Promise<ErrorBody | null> {
  const rawText = await response.text().catch(() => '')
  if (!rawText.trim()) return null
  try {
    return JSON.parse(rawText) as ErrorBody
  } catch {
    return null
  }
}

let redirectingToLogin = false

/** 更新權杖也救不回來：清工作階段、導回登入頁（帶 `redirect` 讓登入後能回到原本要去的頁面）。
 * 用旗標避免同一批並發請求各自觸發一次導頁。 */
function redirectToLogin(): void {
  clearSession()
  if (redirectingToLogin) return
  redirectingToLogin = true
  const current = router.currentRoute.value
  const target = current.name === 'login' ? undefined : current.fullPath
  router.push({ name: 'login', query: target ? { redirect: target } : undefined }).finally(() => {
    redirectingToLogin = false
  })
}

function buildHeaders(hasJsonBody: boolean): Record<string, string> {
  const headers: Record<string, string> = {}
  if (hasJsonBody) headers['Content-Type'] = 'application/json'
  const token = getAccessToken()
  if (token) headers.Authorization = `Bearer ${token}`
  return headers
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
}

async function performRequest<T>(path: string, options: RequestOptions, isRetry: boolean): Promise<T> {
  const { method = 'GET', body, signal } = options

  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers: buildHeaders(body !== undefined),
      credentials: 'include',
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal,
    })
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') throw cause
    throw new AdminApiError('network', '無法連線到後台服務，請確認 apps/api 是否已啟動、網路是否正常。', {
      detail: cause instanceof Error ? cause.message : String(cause),
    })
  }

  if (response.status === 401 && !isRetry) {
    const body401 = await readErrorBody(response.clone())
    if (looksLikeSessionExpired(body401)) {
      const refreshed = await refreshAccessToken()
      if (refreshed) {
        return performRequest<T>(path, options, true)
      }
      redirectToLogin()
      throw new AdminApiError('unauthenticated', '登入已逾期，請重新登入。', { status: 401 })
    }
    throw classifyByStatus(body401, 401)
  }

  if (!response.ok) {
    const errorBody = await readErrorBody(response)
    throw classifyByStatus(errorBody, response.status)
  }

  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  return performRequest<T>(path, options, false)
}

/**
 * 帶檔案的建立／更新請求專用：`multipart/form-data`，固定兩個欄位 `payload`（JSON 文字）與選填的
 * `file`。錯誤分類與 401 的 refresh-retry 邏輯與 `apiRequest` 共用，唯一差異是 413——這個狀態碼
 * 永遠是平台層級的空白回應，不是本服務的 ProblemDetails 格式（見 apps/api/README.md「Kestrel
 * 請求主體上限」）。
 */
export interface UploadRequestOptions {
  method?: 'POST' | 'PUT'
  signal?: AbortSignal
}

async function performUploadRequest<T>(path: string, formData: FormData, options: UploadRequestOptions, isRetry: boolean): Promise<T> {
  const { method = 'POST', signal } = options

  const headers: Record<string, string> = {}
  const token = getAccessToken()
  if (token) headers.Authorization = `Bearer ${token}`

  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      // 🔴 不手動設定 Content-Type：交給瀏覽器依 FormData 內容自動加上 multipart 邊界字串。
      headers,
      credentials: 'include',
      body: formData,
      signal,
    })
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') throw cause
    throw new AdminApiError('network', '無法連線到後台服務，請確認 apps/api 是否已啟動、網路是否正常。', {
      detail: cause instanceof Error ? cause.message : String(cause),
    })
  }

  if (response.status === 401 && !isRetry) {
    const body401 = await readErrorBody(response.clone())
    if (looksLikeSessionExpired(body401)) {
      const refreshed = await refreshAccessToken()
      if (refreshed) {
        return performUploadRequest<T>(path, formData, options, true)
      }
      redirectToLogin()
      throw new AdminApiError('unauthenticated', '登入已逾期，請重新登入。', { status: 401 })
    }
    throw classifyByStatus(body401, 401)
  }

  if (!response.ok) {
    if (response.status === 413) {
      throw new AdminApiError('validation', '圖片檔案太大（上限 10 MB），請換一張或先壓縮。', { status: 413 })
    }
    const errorBody = await readErrorBody(response)
    throw classifyByStatus(errorBody, response.status)
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export function apiUploadRequest<T>(path: string, formData: FormData, options: UploadRequestOptions = {}): Promise<T> {
  return performUploadRequest<T>(path, formData, options, false)
}
