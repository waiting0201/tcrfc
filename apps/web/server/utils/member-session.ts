import type { H3Event } from 'h3'

// server/utils/member-session.ts — 會員更新權杖的 Cookie 持有者（S2-11）。
//
// ── 架構：Nuxt 伺服器是會員工作階段的 BFF ─────────────────────────────────────────────
// 後端的更新權杖有兩種遞送方式（apps/api README E 批「權杖遞送方式」）：
//   `cookie`：後端自己 `Set-Cookie`——但瀏覽器**只和 Nuxt 同源網域**說話，後端的 Cookie 屬性
//             （`__Host-` 前綴、`SameSite=None`）是為「瀏覽器直連 API 網域」設計的，經過代理轉出會
//             變成「Nuxt 網域上的 Cookie」，要靠代理轉發 Set-Cookie 與 Cookie 兩個方向，任何一段
//             設定錯就是靜默登入失敗；
//   `body`  ：更新權杖放回應本文，呼叫端自己存（App、**Nuxt 伺服器端代理**）。
// 所以這裡全部用 `tokenDelivery: "body"` 呼叫後端，更新權杖**只存在這一層設定的 HttpOnly Cookie**：
// JS 讀不到、不會出現在任何回應本文（`stripRefreshToken` 把它從轉給瀏覽器的 JSON 拿掉）。
// 瀏覽器端的 JS 只持有 15 分鐘的存取權杖（記憶體，不落地）。效果等同後端的 Cookie 模式，且
// 一般代理（`[...path].ts`）完全看不到 Cookie，沒有「不該轉發的 Cookie」被轉出去的路徑。
//
// ── Cookie 屬性 ──────────────────────────────────────────────────────────────────────
// HttpOnly；`SameSite=Strict`（只有本站頁面發出的請求會帶，加上 `assertSameOrigin` 雙重防 CSRF）；
// `Path=/`；不設 Domain；HTTPS 時加 `Secure` 並使用 `__Host-` 前綴（瀏覽器強制 Secure＋Path=/＋無 Domain）。
// 名稱帶俱樂部代碼：正式環境兩站網域不同本來互不相干，但本機兩個容器同為 `127.0.0.1`
// （Cookie 不分連接埠）會互踢，帶 club 就不會（會員帳號本來就跨俱樂部共用，各站各自登入一次）。
// 值前綴 `p.`（持久，「記住我」30 天）／`s.`（工作階段 Cookie），refresh 輪替時照原樣續用同一種。

const COOKIE_BASE = 'tcrfc-member-rt'

function club(event: H3Event): string {
  return useRuntimeConfig(event).public.club === 'bw' ? 'bw' : 'tcrfc'
}

function isSecure(event: H3Event): boolean {
  return getRequestProtocol(event, { xForwardedProto: true }) === 'https'
}

function cookieNames(event: H3Event): { secure: string, plain: string } {
  const c = club(event)
  return { secure: `__Host-${COOKIE_BASE}-${c}`, plain: `${COOKIE_BASE}-${c}` }
}

export interface StoredRefresh {
  token: string
  persistent: boolean
}

export function readRefreshCookie(event: H3Event): StoredRefresh | null {
  const names = cookieNames(event)
  const raw = getCookie(event, names.secure) || getCookie(event, names.plain) // 用 || 不用 ??：已清除的 Cookie 可能以空字串送來，需退回另一個命名（E-105）
  if (!raw || raw.length < 4) return null
  const persistent = raw.startsWith('p.')
  if (!persistent && !raw.startsWith('s.')) return null
  const token = raw.slice(2)
  return /^[A-Za-z0-9_~.+/=-]{16,512}$/.test(token) ? { token, persistent } : null
}

export function writeRefreshCookie(event: H3Event, token: string, persistent: boolean, expiresAt: string | null | undefined): void {
  const names = cookieNames(event)
  const secure = isSecure(event)
  const expires = expiresAt ? new Date(expiresAt) : null
  setCookie(event, secure ? names.secure : names.plain, `${persistent ? 'p' : 's'}.${token}`, {
    httpOnly: true,
    secure,
    sameSite: 'strict',
    path: '/',
    // 持久 Cookie 以後端告知的到期時間為準；工作階段 Cookie 不設（關瀏覽器即失效，後端另有 24 小時上限）
    ...(persistent && expires && !Number.isNaN(expires.getTime()) ? { expires } : {}),
  })
  // 若曾以另一種命名（例如 http→https 切換）留下舊 Cookie，清掉避免兩份並存
  deleteCookie(event, secure ? names.plain : names.secure, { path: '/' })
}

export function clearRefreshCookie(event: H3Event): void {
  const names = cookieNames(event)
  deleteCookie(event, names.secure, { path: '/', httpOnly: true, secure: true, sameSite: 'strict' })
  deleteCookie(event, names.plain, { path: '/', httpOnly: true, sameSite: 'strict' })
}

interface BackendSession {
  accessToken?: string
  accessTokenExpiresAt?: string
  refreshToken?: string
  refreshTokenExpiresAt?: string
  member?: unknown
}

/** 把後端 `MemberSession` 轉成瀏覽器可拿的形狀：寫入更新權杖 Cookie，並把更新權杖從本文拿掉。 */
export function adoptSession(event: H3Event, raw: unknown, persistent: boolean): { accessToken: string, accessTokenExpiresAt: string, member: unknown } | null {
  const s = raw as BackendSession | undefined
  if (!s || typeof s.accessToken !== 'string' || typeof s.refreshToken !== 'string') return null
  writeRefreshCookie(event, s.refreshToken, persistent, s.refreshTokenExpiresAt)
  return {
    accessToken: s.accessToken,
    accessTokenExpiresAt: s.accessTokenExpiresAt ?? '',
    member: s.member,
  }
}
