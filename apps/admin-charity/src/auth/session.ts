import { computed, reactive } from 'vue'
import { parseUtcMs } from '@/utils/format'

/**
 * 登入工作階段（慈善獨立後台，與主站後台完全分開：不同 API 路徑、不同 Cookie 名稱
 * `__Host-tcrfc-charity-admin-rt`、不同權杖簽發者，兩邊的登入互不相通）。
 *
 * 存放策略：存取權杖只放記憶體（不落地 localStorage／sessionStorage，避免 XSS 直接偷走）；
 * 更新權杖是後端設的 HttpOnly Cookie，前端讀不到，重新整理頁面時由 `ensureSession()` 靜默換一次。
 * 權限碼只用來決定「顯示或隱藏按鈕」，介面不顯示它（規劃書 §4.0）；真正的授權一律由後端判斷。
 */
export interface AuthUser {
  username: string
  displayName: string
  isSuperAdmin: boolean
  roleNames: string[]
}

interface State {
  accessToken: string | null
  expiresAt: number | null
  user: AuthUser | null
  permissions: string[]
  bootstrapped: boolean
}

const state = reactive<State>({ accessToken: null, expiresAt: null, user: null, permissions: [], bootstrapped: false })

export const authUser = computed(() => state.user)
export const isAuthenticated = computed(() => state.accessToken !== null)
const permissionSet = computed(() => new Set(state.permissions))

export function hasPermission(code: string): boolean {
  return permissionSet.value.has(code)
}

export function setToken(accessToken: string, expiresAtUtc: string, username: string): void {
  state.accessToken = accessToken
  state.expiresAt = parseUtcMs(expiresAtUtc)
  state.user = {
    username,
    displayName: state.user?.username === username ? state.user.displayName : username,
    isSuperAdmin: state.user?.isSuperAdmin ?? false,
    roleNames: state.user?.roleNames ?? [],
  }
}

export function setProfile(profile: { username: string; displayName: string; isSuperAdmin: boolean; roleNames: string[]; permissions: string[] }): void {
  if (!state.user) return
  state.user.username = profile.username
  state.user.displayName = profile.displayName
  state.user.isSuperAdmin = profile.isSuperAdmin
  state.user.roleNames = profile.roleNames
  state.permissions = profile.permissions
}

export function clearSession(): void {
  state.accessToken = null
  state.expiresAt = null
  state.user = null
  state.permissions = []
}

export function getAccessToken(): string | null {
  return state.accessToken
}

export function isBootstrapped(): boolean {
  return state.bootstrapped
}

export function markBootstrapped(): void {
  state.bootstrapped = true
}
