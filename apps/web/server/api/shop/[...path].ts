// server/api/shop/[...path].ts — 站內商店 8.3（S3-5）的同源 BFF 代理。
//
// 瀏覽器（與 SSR）一律呼叫 `/api/shop/{endpoint}`，由這裡組成 `GET|POST … /api/v1/{club}/shop/{endpoint}`
// （`{club}` 取自容器的 `NUXT_PUBLIC_CLUB`，**瀏覽器不能指定俱樂部**——購物車不得跨俱樂部混買，
// 從入口就沒有「換一個 club 參數」這條路）。
//
// ── 白名單，不是萬用轉發 ─────────────────────────────────────────────────────────────
// 每一條都明列「方法＋路徑形狀＋身分模式」。沒命中一律 404（路徑形狀不認得）或 405（方法不對）。
// 查詢字串只放行各路由列出的鍵，並檢查形狀；其他丟掉。
//
// ── 身分與權杖（為什麼不用瀏覽器 JS 帶）──────────────────────────────────────────────
//   - 會員：瀏覽器帶 `Authorization: Bearer`（15 分鐘存取權杖，見 `useMemberSession`），形狀檢查後原樣轉發；
//   - 訪客購物車權杖 `X-Cart-Token`：只存在 HttpOnly Cookie（`server/utils/shop-session.ts`），由這裡讀出並帶標頭；
//     後端第一次發權杖那一次的 `cartToken` 在這裡寫入 Cookie、**從回應本文剝掉**；
//   - 訂單權杖 `X-Order-Token`：同理。結帳成立（非會員）的 `accessToken` 與「信件連結權杖查單」的 `token`
//     都記進 Cookie（以訂單編號為鍵），之後付款／確認／取消／查詢由這裡依訂單編號帶標頭。
//   - 轉發的請求標頭只有：Authorization、Idempotency-Key、X-Cart-Token、X-Order-Token、訪客真實 IP。**不轉發瀏覽器的 Cookie。**
//
// ── 快取 ────────────────────────────────────────────────────────────────────────────
// 🔴 docs/14：庫存、金流冪等、訂單與購物車狀態不得讀快取。本路由所有回應（含錯誤）`Cache-Control: no-store`，
// 本檔沒有任何記憶體快取；`nuxt.config.ts` routeRules 對 `/api/shop/**` 與商店頁面同樣加 no-store。
//
// ── 刻意的行為 ──────────────────────────────────────────────────────────────────────
//   - `pay` 回應的 `paymentUrl` 必須是 https:// 才會交給瀏覽器（其餘一律視為沒有網址，不導向任意協定）。
//   - `cart/merge` 在沒有訪客購物車 Cookie 時不呼叫後端合併，改回會員購物車（等價於「沒東西可併」），前端不必先猜有沒有 Cookie。
//   - 回應本文若出現 `accessToken`／`cartToken`（只有結帳／購物車首次回應才有）一律被剝除。
import type { H3Event } from 'h3'

type ShopMethod = 'GET' | 'POST' | 'PUT' | 'DELETE'
type AuthMode = 'none' | 'optional' | 'required'
type Kind = 'catalog' | 'cart' | 'cart-merge' | 'checkout' | 'order' | 'order-pay' | 'order-lookup' | 'my-orders'

interface ShopRoute {
  method: ShopMethod
  pattern: RegExp
  auth: AuthMode
  kind: Kind
  /** 允許的查詢字串鍵 → 形狀檢查 */
  query?: Record<string, RegExp>
}

const GUID = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
const ORDER_NO = '[A-Za-z0-9_-]{4,64}'
const SLUG = '[a-z0-9][a-z0-9-]{0,127}'
const re = (body: string) => new RegExp(`^${body}$`, 'i')

const LANG = /^(?:zh|en)$/
const INT = /^\d{1,9}$/
// 篩選文字（尺寸／顏色／標籤）：只放行文字、數字、空白與常見標點（白名單，不含控制字元與 HTML 符號）
const FILTER_TEXT = /^[\p{L}\p{N} _\-./()（）+]{1,60}$/u
const LIST_QUERY: Record<string, RegExp> = {
  collection: re(SLUG),
  tag: FILTER_TEXT,
  minPrice: INT,
  maxPrice: INT,
  size: FILTER_TEXT,
  colour: FILTER_TEXT,
  isNew: /^true$/,
  sort: /^(?:newest|price_asc|price_desc)$/,
  page: /^\d{1,4}$/,
  pageSize: /^\d{1,2}$/,
  lang: LANG,
}

const ROUTES: readonly ShopRoute[] = [
  // 目錄（公開，不帶任何身分）
  { method: 'GET', pattern: re('info'), auth: 'none', kind: 'catalog', query: { lang: LANG } },
  { method: 'GET', pattern: re('collections'), auth: 'none', kind: 'catalog', query: { lang: LANG } },
  { method: 'GET', pattern: re('products'), auth: 'none', kind: 'catalog', query: LIST_QUERY },
  { method: 'GET', pattern: re(`products/${SLUG}`), auth: 'none', kind: 'catalog', query: { lang: LANG } },
  // 購物車
  { method: 'GET', pattern: re('cart'), auth: 'optional', kind: 'cart', query: { lang: LANG } },
  { method: 'POST', pattern: re('cart/items'), auth: 'optional', kind: 'cart', query: { lang: LANG } },
  { method: 'PUT', pattern: re(`cart/items/${GUID}`), auth: 'optional', kind: 'cart', query: { lang: LANG } },
  { method: 'DELETE', pattern: re(`cart/items/${GUID}`), auth: 'optional', kind: 'cart', query: { lang: LANG } },
  { method: 'DELETE', pattern: re('cart'), auth: 'optional', kind: 'cart', query: { lang: LANG } },
  { method: 'POST', pattern: re('cart/merge'), auth: 'required', kind: 'cart-merge', query: { lang: LANG } },
  // 結帳與訂單
  { method: 'POST', pattern: re('checkout'), auth: 'optional', kind: 'checkout', query: { lang: LANG } },
  { method: 'POST', pattern: re('orders/lookup'), auth: 'none', kind: 'order-lookup', query: { lang: LANG } },
  { method: 'GET', pattern: re('my-orders'), auth: 'required', kind: 'my-orders', query: { lang: LANG } },
  { method: 'GET', pattern: re(`orders/${ORDER_NO}`), auth: 'optional', kind: 'order', query: { lang: LANG } },
  { method: 'POST', pattern: re(`orders/${ORDER_NO}/pay`), auth: 'optional', kind: 'order-pay', query: { lang: LANG } },
  { method: 'POST', pattern: re(`orders/${ORDER_NO}/confirm`), auth: 'optional', kind: 'order', query: { lang: LANG } },
  { method: 'POST', pattern: re(`orders/${ORDER_NO}/cancel`), auth: 'optional', kind: 'order', query: { lang: LANG } },
]

const IDEMPOTENCY_KEY = /^[A-Za-z0-9_\-:.]{8,64}$/

function pickQuery(event: H3Event, allowed: Record<string, RegExp> | undefined): Record<string, string> | undefined {
  if (!allowed) return undefined
  const raw = getQuery(event)
  const out: Record<string, string> = {}
  for (const [key, shape] of Object.entries(allowed)) {
    const value = raw[key]
    if (typeof value === 'string' && shape.test(value)) out[key] = value
  }
  return Object.keys(out).length ? out : undefined
}

/** 路徑裡的訂單編號（`orders/{no}`、`orders/{no}/pay` …）；`orders/lookup` 不是訂單編號。 */
function orderNoOf(path: string): string | null {
  const m = /^orders\/([^/]+)/i.exec(path)
  return m && m[1]!.toLowerCase() !== 'lookup' ? m[1]! : null
}

function respond(event: H3Event, status: number, body: unknown): string | null {
  setNoStore(event)
  setResponseStatus(event, status)
  if (body === undefined || body === null) return null
  setResponseHeader(event, 'content-type', 'application/json')
  return JSON.stringify(body)
}

export default defineEventHandler(async (event) => {
  setNoStore(event)
  const path = (event.context.params?.path ?? '').replace(/\/+$/, '')
  const method = event.method as ShopMethod

  const byPath = ROUTES.filter(r => r.pattern.test(path))
  if (byPath.length === 0) throw createError({ statusCode: 404, statusMessage: 'Not Found' })
  const route = byPath.find(r => r.method === method)
  if (!route) throw createError({ statusCode: 405, statusMessage: 'Method Not Allowed' })

  // 目錄是公開唯讀，不涉及 Cookie；其餘（購物車、結帳、訂單）會讀寫 Cookie 與個人資料，必須是本站頁面發出的請求。
  if (route.kind !== 'catalog') assertSameOrigin(event)

  const club = useRuntimeConfig(event).public.club === 'bw' ? 'bw' : 'tcrfc'
  const headers: Record<string, string> = {}

  // ── 身分 ──
  const bearer = route.auth === 'none' ? null : readBearer(event)
  if (route.auth === 'required' && !bearer) {
    return memberProblem(event, 401, 'login_required', '請先登入。')
  }
  if (bearer) headers.authorization = bearer

  const usesCart = route.kind === 'cart' || route.kind === 'cart-merge' || route.kind === 'checkout'
  const cartToken = usesCart ? readCartToken(event) : null
  if (cartToken) headers['x-cart-token'] = cartToken

  const orderNo = orderNoOf(path)
  if (orderNo) {
    const orderToken = readOrderToken(event, orderNo)
    if (orderToken) headers['x-order-token'] = orderToken
  }

  if (route.kind === 'checkout') {
    const key = getRequestHeader(event, 'idempotency-key')?.trim()
    if (!key || !IDEMPOTENCY_KEY.test(key)) {
      return memberProblem(event, 400, 'idempotency_key_required', '缺少有效的 Idempotency-Key。')
    }
    headers['idempotency-key'] = key
  }

  // ── 本文 ──
  let body: Record<string, unknown> | undefined
  if (method !== 'GET') {
    const raw = await readBody(event).catch(() => undefined)
    if (raw !== undefined && (raw === null || typeof raw !== 'object' || Array.isArray(raw))) {
      return memberProblem(event, 400, 'invalid_body', '送出的資料格式不正確。')
    }
    body = raw as Record<string, unknown> | undefined
  }

  const query = pickQuery(event, route.query)

  // 沒有訪客購物車 Cookie 的合併＝沒東西可併，直接回會員購物車
  let upstreamMethod: ShopMethod = method
  let upstreamPath = path
  if (route.kind === 'cart-merge' && !cartToken) {
    upstreamMethod = 'GET'
    upstreamPath = 'cart'
    body = undefined
  }

  // 訂單查詢（信件連結權杖）：權杖在本文，後端不會在回應裡回傳它，需要自己記下來
  const lookupToken = route.kind === 'order-lookup' && typeof body?.token === 'string' ? body.token.trim() : null

  const res = await callUpstream(event, upstreamMethod, `${club}/shop/${upstreamPath}`, { body, headers, query })

  if (res.status < 200 || res.status >= 300 || !res.json || typeof res.json !== 'object' || Array.isArray(res.json)) {
    return sendUpstream(event, res)
  }

  const json = res.json as Record<string, unknown>

  // ── 回應後處理 ──
  if (route.kind === 'cart' || route.kind === 'cart-merge') {
    if (typeof json.cartToken === 'string' && json.cartToken) writeCartToken(event, json.cartToken)
    if (route.kind === 'cart-merge' && cartToken) clearCartToken(event) // 訪客購物車已併入會員購物車並刪除
    if (typeof json.itemCount === 'number') writeCartCount(event, json.itemCount)
    delete json.cartToken
  }
  else if (route.kind === 'checkout') {
    if (typeof json.accessToken === 'string' && typeof json.orderNo === 'string') {
      rememberOrderToken(event, json.orderNo, json.accessToken)
    }
    writeCartCount(event, 0) // 結帳成立（含冪等重送）購物車已在同一個交易內清空
    delete json.accessToken
  }
  else if (route.kind === 'order-lookup') {
    if (lookupToken && typeof json.orderNo === 'string') rememberOrderToken(event, json.orderNo, lookupToken)
    delete json.accessToken
  }
  else if (route.kind === 'order-pay') {
    delete json.accessToken
    if (typeof json.paymentUrl === 'string' && !/^https:\/\//i.test(json.paymentUrl)) json.paymentUrl = null
  }
  else if (route.kind === 'order') {
    delete json.accessToken
  }

  return respond(event, res.status, json)
})
