// app/composables/useMemberLine.ts — LINE 一鍵登入／綁定的起點（S2-11）
//
// 流程：`POST member/auth/line/authorize` 取得 `authorizeUrl` 與 `state` → 把 `state` 存 sessionStorage →
// 導去 LINE 授權 → LINE 導回 `/zh/member/line-callback/?code=…&state=…`（pages/zh/member/line-callback.vue）
// → 該頁比對 `state`（與 sessionStorage 不符一律丟棄，防登入 CSRF）→ 呼叫 `/api/member-auth/line-callback`。
//
// 後端憑證未設定時回 503 `line_not_configured`：前端顯示「暫不提供」，不是錯誤頁。
//
// ⚠️ **導回網址必須登記在後端 `LINE_LOGIN_REDIRECT_URIS` 白名單，也要登記在 LINE Developers 的 Callback URL。**
// 兩個語系共用同一個導回網址（`/zh/member/line-callback/`），回來後再依 sessionStorage 裡記的語系導回。
export const LINE_PENDING_KEY = 'tcrfc.member.line'
export const LINE_CALLBACK_PATH = '/zh/member/line-callback/'

export interface LinePending {
  state: string
  mode: 'login' | 'bind'
  locale: 'zh' | 'en'
  next: string | null
  at: number
}

export function readLinePending(): LinePending | null {
  try {
    const raw = sessionStorage.getItem(LINE_PENDING_KEY)
    if (!raw) return null
    const p = JSON.parse(raw) as LinePending
    // 超過 15 分鐘的授權流程視為作廢（LINE 的授權碼本來就很短命）
    if (!p || typeof p.state !== 'string' || Date.now() - p.at > 15 * 60_000) return null
    return p
  }
  catch { return null }
}

export function clearLinePending() {
  try { sessionStorage.removeItem(LINE_PENDING_KEY) }
  catch { /* 無痕模式等：忽略 */ }
}

export function useMemberLine() {
  const config = useRuntimeConfig()
  const { locale, isEn, tx } = useLocale()
  const lineUnavailable = () => tx('LINE 登入目前暫不提供，請改用 Email 登入。', 'LINE login is currently unavailable. Please sign in with your email instead.')
  const { authedFetch } = useMemberSession()
  const route = useRoute()

  /** 回傳 null 代表已導向 LINE；回傳字串是要顯示給使用者的訊息（含「暫不提供」）。 */
  async function start(mode: 'login' | 'bind'): Promise<string | null> {
    const body = { club: config.public.club, mode, redirectUri: `${window.location.origin}${LINE_CALLBACK_PATH}` }
    try {
      const out = mode === 'bind'
        ? await authedFetch<{ authorizeUrl: string, state: string }>('/api/backend/member/auth/line/authorize', { method: 'POST', body })
        : await $fetch<{ authorizeUrl: string, state: string }>('/api/backend/member/auth/line/authorize', { method: 'POST', body })

      // 只接受導向 LINE 官方網域，避免後端設定錯誤或被竄改時把使用者導去別處
      const url = new URL(out.authorizeUrl)
      if (url.protocol !== 'https:' || !/(^|\.)line\.me$/.test(url.hostname)) {
        return lineUnavailable()
      }
      const pending: LinePending = {
        state: out.state,
        mode,
        locale: locale.value,
        next: safeNextPath(route.query.next),
        at: Date.now(),
      }
      sessionStorage.setItem(LINE_PENDING_KEY, JSON.stringify(pending))
      window.location.assign(out.authorizeUrl)
      return null
    }
    catch (err) {
      const e = toMemberApiError(err, undefined, isEn.value)
      if (e.status === 503 || e.code === 'line_not_configured') return lineUnavailable()
      return e.detail
    }
  }

  return { start }
}
