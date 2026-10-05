// app/composables/useMemberSession.ts — 會員工作階段（瀏覽器端，S2-11）
//
// 工作階段的兩個半邊：
//   - **更新權杖**：只存在 Nuxt 伺服器設定的 HttpOnly Cookie（server/utils/member-session.ts 檔頭說明），
//     JS 讀不到、也不會出現在任何回應本文。
//   - **存取權杖**（15 分鐘）：只放在這支檔案的模組變數（記憶體）。**不寫 localStorage／sessionStorage**
//     （XSS 一旦成立就能整份帶走）；重新整理頁面後由 Cookie 經 `/api/member-auth/refresh` 換回來。
//
// 🔴 refresh 的 single-flight（後端「舊更新權杖被重放＝全部撤銷」，同一份 Cookie 併發兩次 refresh 就會被踢出）：
//   1. **分頁內**：`store.inflight` 讓同一時間所有呼叫共用同一個 Promise。
//   2. **跨分頁**：`navigator.locks`（Web Locks）讓整個瀏覽器同一時間只有一個分頁在 refresh；取得鎖之後
//      先看記憶體裡的權杖是不是已被別的分頁換新（BroadcastChannel 會廣播新的存取權杖），是就不再打 refresh。
//      即使還是需要 refresh，因為 Cookie 在「取得鎖之後才讀」，第二個分頁送出的是第一個分頁剛輪替出來的新權杖，
//      不是舊的，所以序列化後不會被判成重放。
//   3. 不支援 Web Locks 的環境退回只有 1＋伺服器端「進行中請求合併」（server/api/member-auth/refresh.post.ts）。
// 登出會廣播給其他分頁，讓它們立即清掉記憶體裡的存取權杖（存取權杖本身無法撤銷，只能靠這個＋15 分鐘壽命）。
import type { MemberBrief, MemberBrowserSession } from '#shared/utils/member'

interface SessionStore {
  token: string
  /** 存取權杖到期時間（epoch ms） */
  expiresAt: number
  inflight: Promise<boolean> | null
  channel: BroadcastChannel | null
  wired: boolean
}

// 只在瀏覽器端函式裡讀寫；伺服器端永遠不會呼叫那些函式（會員頁一律 onMounted 才碰）。
const store: SessionStore = { token: '', expiresAt: 0, inflight: null, channel: null, wired: false }

const SKEW_MS = 30_000
const CHANNEL_NAME = 'tcrfc-member-session'
const LOCK_NAME = 'tcrfc-member-refresh'

type ChannelMessage =
  | { type: 'session', accessToken: string, expiresAt: number, member: MemberBrief }
  | { type: 'logout' }

export function useMemberSession() {
  const member = useState<MemberBrief | null>('member-brief', () => null)
  /** 是否已經嘗試過從 Cookie 還原（頁面據此區分「還在確認」與「確定沒登入」，避免閃一下登入表單）。 */
  const restored = useState<boolean>('member-restored', () => false)
  const isLoggedIn = computed(() => member.value !== null)

  function broadcast(message: ChannelMessage) {
    try { store.channel?.postMessage(message) }
    catch { /* 頻道已關閉：忽略 */ }
  }

  function apply(session: MemberBrowserSession, share = true) {
    store.token = session.accessToken
    const exp = Date.parse(session.accessTokenExpiresAt)
    store.expiresAt = Number.isNaN(exp) ? Date.now() + 14 * 60_000 : exp
    member.value = session.member
    restored.value = true
    if (share) broadcast({ type: 'session', accessToken: store.token, expiresAt: store.expiresAt, member: session.member })
  }

  function clear(share = true) {
    store.token = ''
    store.expiresAt = 0
    member.value = null
    restored.value = true
    if (share) broadcast({ type: 'logout' })
  }

  function wire() {
    if (!import.meta.client || store.wired) return
    store.wired = true
    if (typeof BroadcastChannel === 'undefined') return
    store.channel = new BroadcastChannel(CHANNEL_NAME)
    store.channel.onmessage = (ev: MessageEvent<ChannelMessage>) => {
      const msg = ev.data
      if (msg.type === 'session') {
        store.token = msg.accessToken
        store.expiresAt = msg.expiresAt
        member.value = msg.member
        restored.value = true
      }
      else if (msg.type === 'logout') {
        store.token = ''
        store.expiresAt = 0
        member.value = null
        restored.value = true
      }
    }
  }

  function runExclusive<T>(fn: () => Promise<T>): Promise<T> {
    const locks = (navigator as Navigator & { locks?: LockManager }).locks
    return locks ? (locks.request(LOCK_NAME, fn) as unknown as Promise<T>) : fn()
  }

  /** 以 Cookie 換新的存取權杖。`rejected`：剛被後端 401 的那顆權杖（記憶體裡若已是別的，代表別的分頁換過了）。 */
  function refresh(rejected?: string): Promise<boolean> {
    wire()
    if (store.inflight) return store.inflight
    store.inflight = runExclusive(async () => {
      if (store.token && store.token !== rejected && store.expiresAt - Date.now() > SKEW_MS) return true
      try {
        const session = await $fetch<MemberBrowserSession | null | undefined>('/api/member-auth/refresh', { method: 'POST' })
        // 204（沒有更新權杖 Cookie＝匿名訪客）：確定沒登入，不是錯誤（F5）
        if (!session) {
          clear()
          return false
        }
        apply(session)
        return true
      }
      catch (err) {
        // 401（沒有 Cookie／已過期／已被撤銷）：確定沒登入。其他錯誤（429／502）保留現狀，讓使用者重試。
        if (toMemberApiError(err).status === 401) clear()
        return false
      }
    }).finally(() => { store.inflight = null })
    return store.inflight
  }

  /** 還原登入狀態：記憶體有有效權杖就直接用，否則走 Cookie refresh。回傳「現在是否已登入」。 */
  async function restore(): Promise<boolean> {
    wire()
    if (store.token && store.expiresAt - Date.now() > SKEW_MS) {
      restored.value = true
      return true
    }
    const ok = await refresh()
    restored.value = true
    return ok
  }

  async function login(email: string, password: string, rememberMe: boolean) {
    wire()
    const session = await $fetch<MemberBrowserSession>('/api/member-auth/login', {
      method: 'POST',
      body: { email, password, rememberMe },
    })
    apply(session)
    return session
  }

  /** 把 LINE／變更密碼等流程拿到的新工作階段套用進來。 */
  function adopt(session: MemberBrowserSession) {
    wire()
    apply(session)
  }

  async function logout() {
    wire()
    // 先清本機狀態：就算後端撤銷失敗，這個分頁也要立刻是登出狀態（Cookie 由伺服器端一律清掉）
    clear()
    await $fetch('/api/member-auth/logout', { method: 'POST' }).catch(() => {})
  }

  async function logoutAll() {
    await authedFetch('/api/member-auth/logout-all', { method: 'POST' })
    clear()
  }

  /**
   * 帶存取權杖呼叫會員端點。權杖將到期先 refresh；遇 401 refresh 一次再重試（只重試一次）。
   * 仍失敗就視為登出並丟出 `login_required` 形狀的例外，頁面據此回登入畫面。
   */
  async function authedFetch<T = unknown>(url: string, options: Parameters<typeof $fetch>[1] = {}): Promise<T> {
    wire()
    if (!store.token || store.expiresAt - Date.now() <= SKEW_MS) {
      if (!(await refresh())) throw loginRequiredError()
    }
    // Nitro 的 $fetch 型別會把回傳值包成 TypedInternalResponse；這裡的 url 是執行期組出的字串，
    // 回傳型別由呼叫端以泛型 T 宣告，所以收斂成單純的 Promise<T>。
    const rawFetch = $fetch as unknown as (u: string, o: Record<string, unknown>) => Promise<T>
    const call = (token: string) => rawFetch(url, {
      ...(options as Record<string, unknown>),
      headers: { ...(options.headers as Record<string, string> | undefined), authorization: `Bearer ${token}` },
    })
    const used = store.token
    try {
      return await call(used)
    }
    catch (err) {
      if (toMemberApiError(err).status !== 401) throw err
      if (!(await refresh(used))) throw loginRequiredError()
      return await call(store.token)
    }
  }

  return { member, restored, isLoggedIn, restore, refresh, login, adopt, logout, logoutAll, authedFetch, setBrief: (b: MemberBrief) => { member.value = b } }
}

function loginRequiredError() {
  return Object.assign(new Error('login_required'), {
    status: 401,
    data: { code: 'login_required', detail: '登入已過期，請重新登入。', messageEn: 'Your session has expired. Please sign in again.' },
  })
}
