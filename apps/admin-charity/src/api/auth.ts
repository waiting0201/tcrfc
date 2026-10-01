/** `/api/v1/donation-platform/admin/auth/*`（登入、換權杖、登出、個人檔案、改密碼）。 */
import { API_BASE_URL, ADMIN_ROOT, apiRequest } from './http'
import { authUser, clearSession, setProfile, setToken } from '@/auth/session'

const AUTH = `${ADMIN_ROOT}/auth`

export type LoginOutcome =
  | { kind: 'success' }
  | { kind: 'totp-required'; message: string }
  | { kind: 'locked'; message: string }
  | { kind: 'failed'; message: string }

interface LoginBody {
  accessToken?: string
  accessTokenExpiresAtUtc?: string
  username?: string
  status?: string
  message?: string
}

async function readJson(response: Response): Promise<LoginBody> {
  const text = await response.text().catch(() => '')
  try { return text ? JSON.parse(text) as LoginBody : {} } catch { return {} }
}

export async function login(username: string, password: string, totpCode?: string): Promise<LoginOutcome> {
  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${AUTH}/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ username, password, totpCode: totpCode || null }),
    })
  } catch {
    return { kind: 'failed', message: '無法連線到後台服務，請確認網路是否正常。' }
  }
  const body = await readJson(response)
  if (response.ok) {
    if (body.accessToken && body.accessTokenExpiresAtUtc && body.username) {
      setToken(body.accessToken, body.accessTokenExpiresAtUtc, body.username)
      return { kind: 'success' }
    }
    // 已啟用兩階段驗證、尚未提供驗證碼：不是失敗，也不計入鎖定次數。
    if (body.status === 'totp_required') return { kind: 'totp-required', message: body.message ?? '請輸入兩階段驗證碼。' }
    return { kind: 'failed', message: '登入回應格式不正確，請稍後再試。' }
  }
  if (response.status === 423) return { kind: 'locked', message: body.message ?? '帳號已被鎖定，請稍後再試。' }
  if (response.status === 429) return { kind: 'failed', message: '嘗試次數過多，請稍候幾分鐘再試。' }
  return { kind: 'failed', message: body.message ?? '帳號或密碼錯誤。' }
}

let refreshPromise: Promise<boolean> | null = null

/** 用更新權杖 Cookie（HttpOnly，前端讀不到）換新的存取權杖；多個請求同時過期時共用同一次呼叫。 */
export function refreshAccessToken(): Promise<boolean> {
  if (!refreshPromise) {
    refreshPromise = doRefresh().finally(() => { refreshPromise = null })
  }
  return refreshPromise
}

async function doRefresh(): Promise<boolean> {
  try {
    const response = await fetch(`${API_BASE_URL}${AUTH}/refresh`, { method: 'POST', credentials: 'include' })
    if (!response.ok) { clearSession(); return false }
    const body = await readJson(response)
    if (!body.accessToken || !body.accessTokenExpiresAtUtc) { clearSession(); return false }
    // refresh 回應不含帳號名稱：沿用既有的，沒有（重新整理頁面後）就等 /me 補上。
    setToken(body.accessToken, body.accessTokenExpiresAtUtc, authUser.value?.username ?? '')
    return true
  } catch {
    clearSession()
    return false
  }
}

export async function logout(): Promise<void> {
  try {
    await fetch(`${API_BASE_URL}${AUTH}/logout`, { method: 'POST', credentials: 'include' })
  } catch {
    // 連不上伺服器也要讓使用者在前端確實登出。
  }
  clearSession()
}

interface MeResponse {
  username: string
  displayName: string
  isSuperAdmin: boolean
  roles: { code: string; nameZh: string }[]
  permissions: string[]
}

/** 載入個人檔案（姓名、角色、權限碼）。權限碼只用來決定顯示或隱藏按鈕。 */
export async function loadProfile(): Promise<void> {
  const me = await apiRequest<MeResponse>(`${AUTH}/me`)
  setProfile({
    username: me.username,
    displayName: me.displayName,
    isSuperAdmin: me.isSuperAdmin,
    roleNames: me.roles.map((r) => r.nameZh),
    permissions: me.permissions,
  })
}

export function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  return apiRequest<void>(`${AUTH}/change-password`, { method: 'POST', body: { currentPassword, newPassword } })
}
