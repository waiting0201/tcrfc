import { isIP } from 'node:net'
import type { H3Event } from 'h3'

// server/utils/member-proxy.ts — 會員端點（S2-11）與文化互動端點（S3-2）的同源代理共用邏輯。
//
// ── 為什麼會員端點不走 `[...path].ts` 既有的 `$fetch` 分支 ────────────────────────────
// 既有分支（唯讀 GET、表單／FAQ／提案的 POST）把後端錯誤交給 ofetch 例外、再由 Nitro 包成
// `{ statusCode, message, data }`——表單頁只需要一句話，這樣夠用。會員流程要依後端
// ProblemDetails 的 **`code`**（`email_not_verified`／`account_locked`／`season_not_available`…）
// 決定畫面，還有 `lockedUntil`，所以這裡一律**原樣轉回後端的狀態碼與本文**（`ignoreResponseError`），
// 前端 `err.data` 就是 ProblemDetails。
//
// ── 白名單，不是萬用轉發 ─────────────────────────────────────────────────────────────
// 每一條都明列「方法＋路徑形狀＋是否要會員權杖」。刻意**不在白名單**的：
//   - `member/membership-orders/{no}/pay`、`…/confirm`：主站 §3.14 明文「網頁會籍站內結帳尚未拍板，
//     未拍板前不得逕行實作」，網頁端只送出 `created` 升級申請。前端不呼叫、代理也不放行，
//     兩層都擋，之後拍板要開通只需要在這裡加兩行。
//   - `member/auth/login|refresh|logout|logout-all|change-password|line/callback|line/complete`：
//     這些會發／撤銷更新權杖，必須走 `server/api/member-auth/*`（由那一層持有 HttpOnly Cookie）。
//   - `api/membership/activate`（伺服器內部開通端點）：永遠不對瀏覽器開放。
//
// ── 轉發哪些請求標頭 ─────────────────────────────────────────────────────────────────
// 只有三個：`Authorization`（形狀檢查過的 Bearer）、`Idempotency-Key`（形狀檢查過）、訪客真實 IP
// （限流用，來源是 Caddy 設的 `X-Real-IP`，理由見 `[...path].ts` 的 E-71 長註解）。
// **不轉發 Cookie**：會員的更新權杖 Cookie 只在 `server/api/member-auth/*` 讀取，不會被一般代理看到，
// 也就不可能被轉給後端的其他端點。

type MemberMethod = 'GET' | 'POST' | 'PUT' | 'DELETE'
type AuthMode = 'required' | 'optional' | 'none'

interface MemberRoute {
  method: MemberMethod
  pattern: RegExp
  auth: AuthMode
  /** 此端點需要 `Idempotency-Key`（會籍升級申請）。 */
  idempotency?: boolean
}

const CLUB = '[a-z][a-z0-9-]*'
const GUID = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
const SLUG = '[a-z0-9][a-z0-9-]*'
const ORDER_NO = '[A-Za-z0-9_-]{4,64}'
const CARD_TOKEN = '[A-Za-z0-9_-]{16,200}'

const re = (body: string) => new RegExp(`^${body}$`, 'i')

const MEMBER_ROUTES: readonly MemberRoute[] = [
  // 帳號（不發權杖的那幾條）
  { method: 'POST', pattern: re('member/auth/(?:register|verify-email|resend-verification|forgot-password|reset-password)'), auth: 'none' },
  { method: 'POST', pattern: re('member/auth/line/authorize'), auth: 'optional' }, // bind 模式需要權杖
  { method: 'GET', pattern: re('member/me'), auth: 'required' },
  { method: 'PUT', pattern: re('member/me'), auth: 'required' },
  { method: 'DELETE', pattern: re('member/me'), auth: 'required' },
  { method: 'DELETE', pattern: re('member/me/line'), auth: 'required' },
  // 會員中心
  { method: 'GET', pattern: re('member/memberships'), auth: 'required' },
  { method: 'POST', pattern: re(`${CLUB}/member/memberships/join`), auth: 'required' },
  { method: 'GET', pattern: re('member/cards'), auth: 'required' },
  { method: 'POST', pattern: re(`member/cards/${GUID}/regenerate`), auth: 'required' },
  { method: 'GET', pattern: re('member/jerseys'), auth: 'required' },
  { method: 'POST', pattern: re(`${CLUB}/member/jerseys`), auth: 'required' },
  { method: 'PUT', pattern: re(`${CLUB}/member/jerseys/${GUID}`), auth: 'required' },
  // 升級申請：只建立與讀取、取消；沒有 pay／confirm
  { method: 'GET', pattern: re('member/membership-orders'), auth: 'required' },
  { method: 'GET', pattern: re(`member/membership-orders/${ORDER_NO}`), auth: 'required' },
  { method: 'POST', pattern: re(`${CLUB}/member/membership-orders`), auth: 'required', idempotency: true },
  { method: 'POST', pattern: re(`member/membership-orders/${ORDER_NO}/cancel`), auth: 'required' },
  // 電子會員卡公開驗證頁（不需登入）
  { method: 'GET', pattern: re(`m/${CARD_TOKEN}`), auth: 'none' },
  // 8.2 球迷會活動：詳情帶權杖時多回 myRegistration；報名會員／非會員皆可（限付費活動後端自己判斷）
  { method: 'GET', pattern: re(`${CLUB}/fan-events/${SLUG}`), auth: 'optional' },
  { method: 'POST', pattern: re(`${CLUB}/fan-events/${SLUG}/registrations`), auth: 'optional' },
  { method: 'DELETE', pattern: re(`${CLUB}/fan-events/${SLUG}/registrations/me`), auth: 'required' },
  // 8.1 漫畫閱讀數（固定 204）。藍鯨由後端回 403
  { method: 'POST', pattern: re(`${CLUB}/comic/episodes/\\d{1,6}/views`), auth: 'none' },
]

export function matchMemberRoute(method: string, path: string): MemberRoute | undefined {
  return MEMBER_ROUTES.find(r => r.method === method && r.pattern.test(path))
}

const BEARER = /^Bearer [A-Za-z0-9._~+/=-]{20,4096}$/
const IDEMPOTENCY_KEY = /^[A-Za-z0-9_\-:.]{8,64}$/

/** 訪客真實 IP（供後端限流）；只信 Caddy 設定的 `X-Real-IP`（E-71），本機開發沒有就不送。 */
export function visitorIpHeaders(event: H3Event): Record<string, string> {
  const realIp = getRequestHeader(event, 'x-real-ip')?.trim()
  return realIp && isIP(realIp) ? { 'x-forwarded-for': realIp } : {}
}

export interface UpstreamResult {
  status: number
  contentType: string | null
  retryAfter: string | null
  text: string
  /** 回應本文是 JSON 時的解析結果（解析失敗為 undefined）。 */
  json?: unknown
}

/** 呼叫 apps/api；HTTP 錯誤不丟例外（原樣回傳），連線層失敗（API 沒起來）回 502 的 ProblemDetails 形狀。 */
export async function callUpstream(
  event: H3Event,
  method: MemberMethod,
  apiPath: string,
  options: { body?: unknown, headers?: Record<string, string>, query?: Record<string, unknown> } = {},
): Promise<UpstreamResult> {
  try {
    const res = await $fetch.raw<string>(`/api/v1/${apiPath}`, {
      baseURL: backendApiBase(),
      method,
      body: options.body as Record<string, unknown> | undefined,
      query: options.query,
      headers: { ...visitorIpHeaders(event), ...options.headers },
      responseType: 'text',
      ignoreResponseError: true,
      redirect: 'manual',
    })
    const text = typeof res._data === 'string' ? res._data : ''
    let json: unknown
    if (text) {
      try { json = JSON.parse(text) }
      catch { json = undefined }
    }
    return {
      status: res.status,
      contentType: res.headers.get('content-type'),
      retryAfter: res.headers.get('retry-after'),
      text,
      json,
    }
  }
  catch {
    const body = { status: 502, title: 'Bad Gateway', code: 'upstream_unavailable', detail: '服務暫時無法使用，請稍後再試。' }
    return { status: 502, contentType: 'application/problem+json', retryAfter: null, text: JSON.stringify(body), json: body }
  }
}

/** 會員相關回應一律不得被 SSR／CDN／瀏覽器快取（docs/14 不變量）。 */
export function setNoStore(event: H3Event): void {
  setResponseHeader(event, 'cache-control', 'no-store')
  setResponseHeader(event, 'pragma', 'no-cache')
}

/** 把後端回應原樣（狀態碼＋本文）轉給瀏覽器。 */
export function sendUpstream(event: H3Event, res: UpstreamResult): string | null {
  setNoStore(event)
  setResponseStatus(event, res.status)
  if (res.retryAfter) event.node.res.setHeader('retry-after', res.retryAfter)
  if (res.status === 204 || res.text === '') {
    if (res.status !== 204) setResponseHeader(event, 'content-type', 'application/json')
    return null
  }
  setResponseHeader(event, 'content-type', res.contentType?.includes('json') ? res.contentType : 'application/json')
  return res.text
}

/** 驗證請求來自本站頁面（CSRF 防護；Cookie 只在 `member-auth` 路由讀取，那裡一定要呼叫這個）。 */
export function assertSameOrigin(event: H3Event): void {
  const site = getRequestHeader(event, 'sec-fetch-site')
  if (site && site !== 'same-origin' && site !== 'none') {
    throw createError({ statusCode: 403, statusMessage: 'Forbidden' })
  }
  const origin = getRequestHeader(event, 'origin')
  if (origin) {
    let originHost = ''
    try { originHost = new URL(origin).host }
    catch { /* 非法 Origin 一律拒絕 */ }
    const host = getRequestHost(event, { xForwardedHost: true })
    if (!originHost || originHost !== host) {
      throw createError({ statusCode: 403, statusMessage: 'Forbidden' })
    }
  }
}

function problem(event: H3Event, status: number, code: string, detail: string): string {
  setNoStore(event)
  setResponseStatus(event, status)
  setResponseHeader(event, 'content-type', 'application/problem+json')
  return JSON.stringify({ status, title: code, code, detail })
}

/** 取得本次請求要轉給後端的 Authorization；形狀不對視同沒帶。 */
export function readBearer(event: H3Event): string | null {
  const value = getRequestHeader(event, 'authorization')?.trim()
  return value && BEARER.test(value) ? value : null
}

/** `[...path].ts` 的入口：命中白名單就處理並回傳 `{ handled: true, body }`，沒命中回 `{ handled: false }` 交還給既有邏輯。 */
export async function handleMemberProxy(event: H3Event, method: string, path: string): Promise<{ handled: false } | { handled: true, body: unknown }> {
  const route = matchMemberRoute(method, path)
  if (!route) return { handled: false }

  const headers: Record<string, string> = {}
  const bearer = readBearer(event)
  if (route.auth === 'required' && !bearer) {
    return { handled: true, body: problem(event, 401, 'login_required', '請先登入。') }
  }
  if (bearer && route.auth !== 'none') headers.authorization = bearer

  if (route.idempotency) {
    const key = getRequestHeader(event, 'idempotency-key')?.trim()
    if (!key || !IDEMPOTENCY_KEY.test(key)) {
      return { handled: true, body: problem(event, 400, 'idempotency_key_required', '缺少有效的 Idempotency-Key。') }
    }
    headers['idempotency-key'] = key
  }

  let body: unknown
  if (method === 'POST' || method === 'PUT' || method === 'DELETE') {
    body = await readBody(event).catch(() => undefined)
    if (body !== undefined && (body === null || typeof body !== 'object' || Array.isArray(body))) {
      return { handled: true, body: problem(event, 400, 'invalid_body', '送出的資料格式不正確。') }
    }
  }

  // 查詢字串只放行 lang（語系）；其他一律丟掉，不把任意參數帶進後端。
  const lang = String(getQuery(event).lang ?? '')
  const query = lang === 'zh' || lang === 'en' ? { lang } : undefined

  const res = await callUpstream(event, method as MemberMethod, path, { body, headers, query })
  return { handled: true, body: sendUpstream(event, res) }
}

export { problem as memberProblem }
