import type { H3Event } from 'h3'

// server/utils/shop-session.ts — 站內商店（S3-5）訪客身分的 Cookie 持有者。
//
// ── 為什麼購物車權杖與訂單權杖要放在 Nuxt 伺服器設定的 HttpOnly Cookie ────────────────
// 後端（apps/api README「F 批」）的兩種訪客權杖都是「持有即擁有」的 bearer 憑證：
//   - `X-Cart-Token`：訪客購物車的唯一憑證，後端只存雜湊，遺失無法補發；
//   - `X-Order-Token`：非會員結帳成立後操作該張訂單（付款／確認／取消／查詢完整收件資料）的憑證，30 天有效。
// 放在 localStorage 或一般 Cookie 都會讓 XSS 一次拿走。所以這兩個值只存在本檔寫入的 HttpOnly Cookie，
// 由 `server/api/shop/[...path].ts` 代為在呼叫後端時帶標頭，**回應本文一律剝掉**（瀏覽器的 JS 看不到）。
// 與會員更新權杖（`member-session.ts`）同一套設計原則，Cookie 名稱同樣帶俱樂部代碼，避免本機兩個容器
// 同為 127.0.0.1 時互踢。
//
// ── Cookie 屬性 ──────────────────────────────────────────────────────────────────────
// HttpOnly；`SameSite=Lax`（不用 Strict：從 LINE Pay 付款頁導回本站的第一個頂層導覽也要帶得到，
// 雖然導回頁面是在瀏覽器端才呼叫確認，Lax 仍是較不易出事的選擇；CSRF 另由 `assertSameOrigin` 擋住）；
// `Path=/`；不設 Domain；HTTPS 時 `Secure` ＋ `__Host-` 前綴。購物車權杖保留 30 天（後端訪客購物車閒置 30 天清除），
// 訂單權杖 30 天（與後端有效期一致）。
//
// ── 購物車件數 Cookie（非 HttpOnly）─────────────────────────────────────────────────
// 頁首購物車圖示要顯示件數，但不應該為了一個數字每頁多打一次 API。所以每次購物車回應經過代理時，
// 順手把 `itemCount`（一個整數，不含任何憑證或商品資料）寫進非 HttpOnly 的 Cookie，頁首在瀏覽器端讀取。
// 它只是顯示用提示：購物車的真相永遠是後端，會員跨裝置的購物車在第一次載入購物車之前不會反映在這個數字上。

const CART_COOKIE = 'tcrfc-shop-ct'
const ORDER_COOKIE = 'tcrfc-shop-ot'
const COUNT_COOKIE = 'tcrfc-shop-n'
const THIRTY_DAYS = 30 * 24 * 60 * 60
const MAX_ORDER_TOKENS = 8
const TOKEN_SHAPE = /^[A-Za-z0-9_~.+/=-]{16,256}$/
const ORDER_NO_SHAPE = /^[A-Za-z0-9_-]{4,64}$/

function club(event: H3Event): string {
  return useRuntimeConfig(event).public.club === 'bw' ? 'bw' : 'tcrfc'
}

function isSecure(event: H3Event): boolean {
  return getRequestProtocol(event, { xForwardedProto: true }) === 'https'
}

function names(event: H3Event, base: string): { secure: string, plain: string } {
  const c = club(event)
  return { secure: `__Host-${base}-${c}`, plain: `${base}-${c}` }
}

function readCookie(event: H3Event, base: string): string | undefined {
  const n = names(event, base)
  // `||` 不是 `??`：空字串（已被清除但仍被送來的 Cookie）也要退回另一個命名
  return getCookie(event, n.secure) || getCookie(event, n.plain)
}

function writeHttpOnly(event: H3Event, base: string, value: string): void {
  const n = names(event, base)
  const secure = isSecure(event)
  setCookie(event, secure ? n.secure : n.plain, value, {
    httpOnly: true,
    secure,
    sameSite: 'lax',
    path: '/',
    maxAge: THIRTY_DAYS,
  })
  // 協定切換（http→https）可能留下另一種命名的舊 Cookie，清掉避免兩份並存
  deleteCookie(event, secure ? n.plain : n.secure, { path: '/' })
}

function removeHttpOnly(event: H3Event, base: string): void {
  const n = names(event, base)
  deleteCookie(event, n.secure, { path: '/', httpOnly: true, secure: true, sameSite: 'lax' })
  deleteCookie(event, n.plain, { path: '/', httpOnly: true, sameSite: 'lax' })
}

// ── 購物車權杖 ───────────────────────────────────────────────────────────────────────
export function readCartToken(event: H3Event): string | null {
  const raw = readCookie(event, CART_COOKIE)
  return raw && TOKEN_SHAPE.test(raw) ? raw : null
}

export function writeCartToken(event: H3Event, token: string): void {
  if (TOKEN_SHAPE.test(token)) writeHttpOnly(event, CART_COOKIE, token)
}

export function clearCartToken(event: H3Event): void {
  removeHttpOnly(event, CART_COOKIE)
}

// ── 訂單權杖（訂單編號 → 權杖，最多保留最近 8 張）──────────────────────────────────────
function readOrderMap(event: H3Event): Record<string, string> {
  const raw = readCookie(event, ORDER_COOKIE)
  if (!raw) return {}
  try {
    const parsed: unknown = JSON.parse(raw)
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return {}
    const out: Record<string, string> = {}
    for (const [orderNo, token] of Object.entries(parsed as Record<string, unknown>)) {
      if (ORDER_NO_SHAPE.test(orderNo) && typeof token === 'string' && TOKEN_SHAPE.test(token)) out[orderNo] = token
    }
    return out
  }
  catch {
    return {}
  }
}

export function readOrderToken(event: H3Event, orderNo: string): string | null {
  return readOrderMap(event)[orderNo.toUpperCase()] ?? null
}

export function rememberOrderToken(event: H3Event, orderNo: string, token: string): void {
  const key = orderNo.toUpperCase()
  if (!ORDER_NO_SHAPE.test(key) || !TOKEN_SHAPE.test(token)) return
  // 以 Map 重新插入到最後，代表「最近使用」；只保留最近 8 張
  const map = new Map(Object.entries(readOrderMap(event)))
  map.delete(key)
  map.set(key, token)
  const entries = [...map.entries()].slice(-MAX_ORDER_TOKENS)
  writeHttpOnly(event, ORDER_COOKIE, JSON.stringify(Object.fromEntries(entries)))
}

// ── 件數提示 ─────────────────────────────────────────────────────────────────────────
export function writeCartCount(event: H3Event, count: number): void {
  const n = names(event, COUNT_COOKIE)
  const secure = isSecure(event)
  const safe = Number.isFinite(count) && count > 0 ? Math.min(Math.trunc(count), 9999) : 0
  // 件數是非機密的顯示提示，刻意不設 HttpOnly（頁首在瀏覽器端讀它）。名稱不用 `__Host-` 前綴以便前端用固定名稱讀取。
  setCookie(event, n.plain, String(safe), { httpOnly: false, secure, sameSite: 'lax', path: '/', maxAge: THIRTY_DAYS })
}

export const SHOP_COUNT_COOKIE_PREFIX = COUNT_COOKIE
