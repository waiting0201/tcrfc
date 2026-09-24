import { computed, reactive } from 'vue'

/**
 * 登入工作階段狀態（單一真實來源）。跟專案既有慣例一致（見 `data/newsStore.ts`／`data/activeClub.ts`
 * 的說明）：不是要引入 Pinia，這裡只有一份跨元件共用的狀態，模組層級 `reactive()` 單例就夠用。
 *
 * 🔴 存放策略（任務指示要求說明取捨）：
 * - **存取權杖只放在這個模組的記憶體變數**，不落地 `localStorage`／`sessionStorage`——那兩個都是
 *   任何一段注入的第三方或被入侵的前端程式碼可以直接讀走的地方（XSS 一旦發生就等於偷走權杖）。
 *   代價是重新整理頁面會遺失記憶體狀態，但這個代價由「開機時用更新權杖 Cookie 靜默換一次」
 *   （見 `ensureSessionReady()`）吸收，使用者感受不到差異。
 * - **更新權杖完全不進入這層——它一路只存在 `apps/api` 設定的 `__Host-tcrfc-admin-rt`
 *   HttpOnly Cookie 裡**，前端 JavaScript 從語言層級就讀不到它（`HttpOnly`），這是後端已經做好的
 *   設計（見 apps/api/README.md「後台登入權杖設計」），前端唯一要做對的事只有兩件：
 *   ① 呼叫 `/auth/refresh`／`/auth/login`／`/auth/logout` 時帶 `credentials: 'include'`
 *      讓瀏覽器自動附上這顆 Cookie；② 不要自己另外用任何方式複製或暴露它。
 */
export interface AuthUser {
  username: string
  /** 姓名（`GET /auth/me` 回傳，見 `@/auth/clubAccess` 的 `ensureClubsLoaded`）。登入完成的當下
   * 還沒有這筆資料，先以帳號字串頂著，`/me` 查回來後由 `setDisplayName()` 補上。 */
  displayName: string
  isSuperAdmin: boolean
  mustChangePassword: boolean
  twoFactorEnabled: boolean
}

interface SessionState {
  accessToken: string | null
  accessTokenExpiresAt: number | null
  user: AuthUser | null
  /** 開機時的靜默換權杖是否已經跑過一次（不論成功或失敗）——避免路由守衛重複觸發。 */
  bootstrapped: boolean
}

const state = reactive<SessionState>({
  accessToken: null,
  accessTokenExpiresAt: null,
  user: null,
  bootstrapped: false,
})

export const authUser = computed(() => state.user)
export const isAuthenticated = computed(() => state.accessToken !== null && state.user !== null)
/** 強制流程尚未走完：首次登入改密，或兩階段驗證尚未啟用（兩者皆為 `AdminAccountGate` 在
 * 伺服器端強制擋下的前提，前端這裡只是提前導引使用者去把它做完，不是真正的安全邊界）。 */
export const needsForcedOnboarding = computed(
  () => state.user !== null && (state.user.mustChangePassword || !state.user.twoFactorEnabled),
)

export interface SessionPayload {
  accessToken: string
  accessTokenExpiresAtUtc: string
  username: string
  isSuperAdmin: boolean
  mustChangePassword: boolean
  twoFactorEnabled: boolean
}

export function setSession(payload: SessionPayload): void {
  state.accessToken = payload.accessToken
  state.accessTokenExpiresAt = Date.parse(payload.accessTokenExpiresAtUtc)
  state.user = {
    // 登入／換權杖回應本身不含姓名（見 `Features/AdminAuth/AdminAuthDtos.cs` 的 `LoginResponse`／
    // `RefreshResponse`），先用帳號字串頂著，避免畫面在 `/me` 查回來之前完全沒有名字可顯示；
    // 換權杖（refresh）時如果已經有姓名，不要被這裡重置回帳號字串。
    displayName: state.user?.username === payload.username && state.user.displayName ? state.user.displayName : payload.username,
    username: payload.username,
    isSuperAdmin: payload.isSuperAdmin,
    mustChangePassword: payload.mustChangePassword,
    twoFactorEnabled: payload.twoFactorEnabled,
  }
}

/** `GET /auth/me` 查回姓名後補上（見 `@/auth/clubAccess`）。 */
export function setDisplayName(displayName: string): void {
  if (!state.user || !displayName) return
  state.user.displayName = displayName
}

/** 改密／2FA 設定完成後，不必重新整理頁面就能反映最新狀態。 */
export function patchUserFlags(patch: Partial<Pick<AuthUser, 'mustChangePassword' | 'twoFactorEnabled'>>): void {
  if (!state.user) return
  Object.assign(state.user, patch)
}

export function clearSession(): void {
  state.accessToken = null
  state.accessTokenExpiresAt = null
  state.user = null
}

export function getAccessToken(): string | null {
  return state.accessToken
}

export function markBootstrapped(): void {
  state.bootstrapped = true
}

export function isBootstrapped(): boolean {
  return state.bootstrapped
}
