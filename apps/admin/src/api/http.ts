/**
 * apps/api（.NET 後台寫入端點）的底層 fetch 封裝。⛔ 只給「後台新聞」這個垂直切片用
 * （這次任務的範圍限定，見 CLAUDE.md 任務指示），其餘 13 個模組之後要接時再依樣擴充，
 * 不預先做成大而全的框架。
 *
 * 三件事集中在這裡做，讓每支呼叫端點的函式不必重複處理：
 * 1. 組出 `X-Dev-Operator-Id` 標頭（見 devOperator.ts，⛔ 這不是身分驗證）。
 * 2. 把後端的 ProblemDetails（{ status, title, detail }）與「完全連不上」兩種情況，
 *    統一轉成帶有明確種類（kind）的 AdminApiError，畫面才能依種類分流顯示。
 * 3. 判斷「寫入端點開發模式開關關閉」——這個情況下路由完全不存在，回應是**空 body 的 404**
 *   （不是 ProblemDetails），必須跟「這筆資料真的找不到」的 404 分開處理，見下方
 *   classifyNotFound() 的說明與 gate.ts。
 */
import { devOperatorId } from './devOperator'

export const API_BASE_URL = (import.meta.env.VITE_ADMIN_API_BASE_URL as string | undefined)?.replace(/\/$/, '')
  || 'http://127.0.0.1:5299'

export type AdminApiErrorKind =
  | 'validation' // 400
  | 'slug-conflict' // 409，網址名稱重複
  | 'concurrency-conflict' // 409，資料已被其他人變更
  | 'featured-limit' // 409，置頂精選已達上限
  | 'status-conflict' // 409，狀態轉換不允許
  | 'forbidden' // 403，共用內容唯讀
  | 'not-found' // 404，且已確定寫入端點是開著的（真的找不到這筆資料／俱樂部）
  | 'gate-closed-or-unreachable' // 404 空 body（開發模式開關關閉，或路徑打錯）
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

interface ProblemDetailsBody {
  status?: number
  title?: string
  detail?: string
}

function classifyByTitle(title: string | undefined, status: number, detail: string): AdminApiError {
  if (status === 400) return new AdminApiError('validation', detail || '輸入內容有誤', { status, detail })
  if (status === 403) return new AdminApiError('forbidden', detail || '這是兩隊共用的內容，僅系統管理員可以編輯。', { status, detail })
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

/**
 * 對一個已經失敗（!ok）的 Response 做分類。
 * 🔴 空 body 的 404 有兩種完全不同的成因（見檔頭），呼叫端要用 `ambiguousNotFoundIsRoute`
 * 告訴這支函式：以目前的呼叫情境，一個查無 JSON 內容的 404 該怎麼解讀——
 * - 列表端點（不帶資料 id）：一定是路由層級（開發模式開關關閉，或俱樂部代碼打錯但
 *   ClubNotFoundException 其實有 JSON body，所以真正命中這裡的只剩「路由不存在」）。
 * - 帶資料 id 的端點：呼叫端如果已經先用 gate.ts 確認過開關是開的，就傳 false，
 *   讓空 body 404 被視為「這筆資料真的找不到」而不是開關問題。
 */
export async function classifyErrorResponse(response: Response, ambiguousNotFoundIsRoute: boolean): Promise<AdminApiError> {
  const rawText = await response.text().catch(() => '')

  if (response.status === 404 && rawText.trim().length === 0) {
    return ambiguousNotFoundIsRoute
      ? new AdminApiError('gate-closed-or-unreachable', '目前無法使用後台寫入功能', { status: 404 })
      : new AdminApiError('not-found', '找不到這筆資料，可能已被刪除，或您的帳號沒有權限查看', { status: 404 })
  }

  let body: ProblemDetailsBody | null = null
  try {
    body = rawText ? (JSON.parse(rawText) as ProblemDetailsBody) : null
  } catch {
    body = null
  }

  if (!body) {
    return new AdminApiError('server', '伺服器發生未預期的錯誤，請稍後再試。', { status: response.status })
  }

  return classifyByTitle(body.title, response.status, body.detail ?? '')
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  /** 見 classifyErrorResponse 的說明。預設 false（大多數呼叫在此之前都已經先過 gate 檢查）。 */
  ambiguousNotFoundIsRoute?: boolean
  signal?: AbortSignal
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, ambiguousNotFoundIsRoute = false, signal } = options

  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  // 🔴 不是身分驗證，見 devOperator.ts。只有寫入方法才需要帶，GET 不必。
  if (method !== 'GET') headers['X-Dev-Operator-Id'] = devOperatorId()

  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal,
    })
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') throw cause
    throw new AdminApiError('network', '無法連線到後台服務，請確認 apps/api 是否已啟動、網路是否正常。', {
      detail: cause instanceof Error ? cause.message : String(cause),
    })
  }

  if (!response.ok) {
    throw await classifyErrorResponse(response, ambiguousNotFoundIsRoute)
  }

  if (response.status === 204) return undefined as T
  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}

/**
 * 帶檔案的建立／更新請求專用（S0-8 修正，2026-09-22：`apps/api/README.md`「圖片上傳共用元件」
 * 整節改寫後的單一請求契約）：`multipart/form-data`，固定兩個欄位 `payload`（JSON 文字）與選填的
 * `file`，不能走 `apiRequest`（那支固定送 JSON body）。⛔ **不是獨立的「上傳端點」呼叫**——舊版
 * 兩段式設計（選檔後立刻呼叫獨立上傳端點拿 key）已經違反規劃書「選檔不上傳、儲存才上傳」被整支
 * 移除，這支函式現在是各模組自己的建立／更新 API（例如 `adminNews.ts` 的 `createAdminNews`／
 * `updateAdminNews`）在按下「儲存」那一刻才呼叫的唯一請求，圖片與其餘欄位一起送出。
 *
 * 錯誤分類邏輯與 `apiRequest` 共用同一套 `classifyErrorResponse`，唯一的差異是 413——**這個狀態碼
 * 永遠是平台層級的空白回應，不是本服務的 ProblemDetails 格式**（見 apps/api/README.md「Kestrel
 * 請求主體上限」），`classifyErrorResponse` 對非 404 空 body 會誤判成「伺服器發生未預期的錯誤」，
 * 這裡先攔下來給正確的中文訊息。正常情況下不會真的打到這裡——前端在呼叫這支函式之前已經先擋過
 * 10MB 上限，只有繞過前端檢查（例如直接呼叫 API）才會遇到。
 */
export interface UploadRequestOptions {
  method?: 'POST' | 'PUT'
  /** 見 classifyErrorResponse 的說明。預設 false——建立／更新端點的 404 大多是「這筆資料真的
   * 找不到」（例如更新時 id 不存在），不是路由層級問題；呼叫端如果情境不同可自行覆寫。 */
  ambiguousNotFoundIsRoute?: boolean
  signal?: AbortSignal
}

export async function apiUploadRequest<T>(path: string, formData: FormData, options: UploadRequestOptions = {}): Promise<T> {
  const { method = 'POST', ambiguousNotFoundIsRoute = false, signal } = options

  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      // 🔴 不手動設定 Content-Type：交給瀏覽器依 FormData 內容自動加上 multipart 邊界字串，
      // 手動設定反而會漏掉 boundary 導致伺服器端解析失敗。
      headers: { 'X-Dev-Operator-Id': devOperatorId() },
      body: formData,
      signal,
    })
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') throw cause
    throw new AdminApiError('network', '無法連線到後台服務，請確認 apps/api 是否已啟動、網路是否正常。', {
      detail: cause instanceof Error ? cause.message : String(cause),
    })
  }

  if (!response.ok) {
    if (response.status === 413) {
      throw new AdminApiError('validation', '圖片檔案太大（上限 10 MB），請換一張或先壓縮。', { status: 413 })
    }
    throw await classifyErrorResponse(response, ambiguousNotFoundIsRoute)
  }

  const text = await response.text()
  return (text ? JSON.parse(text) : undefined) as T
}
