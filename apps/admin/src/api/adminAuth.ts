/**
 * `apps/api` 的 `/api/v1/admin/auth/*` 端點（登入、更新權杖、登出、改密、2FA），
 * 對照 apps/api/README.md「後台登入權杖設計」與 `Features/AdminAuth/AdminAuthDtos.cs`。
 *
 * `login`／`refreshAccessToken`／`logout` 刻意不透過 `apiRequest`（那支會自動帶 `Authorization`
 * 標頭並在 401 時呼叫 `refreshAccessToken` 本身——登入前沒有權杖可帶，refresh 端點本身也不該
 * 觸發自己的 401 重試迴圈），改用最小的一份直接 `fetch` 邏輯，跟 `http.ts` 各自獨立。
 * `changePassword`／2FA 三支需要「已登入」才能呼叫，走 `apiRequest` 就好。
 */
import { apiRequest, API_BASE_URL } from './http'
import { clearSession, setSession, type SessionPayload } from '@/auth/session'

export type LoginOutcome =
  | { kind: 'success' }
  | { kind: 'totp-required'; message: string }
  | { kind: 'locked'; message: string }
  | { kind: 'disabled'; message: string }
  | { kind: 'invalid-credentials'; message: string }
  | { kind: 'network'; message: string }

interface RawErrorBody {
  status?: string
  message?: string
}

async function parseJsonSafely(response: Response): Promise<Record<string, unknown> & RawErrorBody> {
  const text = await response.text().catch(() => '')
  if (!text) return {}
  try {
    return JSON.parse(text) as Record<string, unknown>
  } catch {
    return {}
  }
}

/** `POST /auth/login`：帳號密碼＋（若該帳號已啟用 2FA）選填的驗證碼。
 * 密碼已通過、但尚未提供驗證碼時後端回 200 `{status:"totp_required"}`（不是失敗嘗試，
 * 不計入鎖定門檻），前端據此再問一次驗證碼、帶著同一組帳密重新呼叫這支函式。 */
export async function login(username: string, password: string, totpCode?: string): Promise<LoginOutcome> {
  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}/api/v1/admin/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ username, password, totpCode: totpCode || null }),
    })
  } catch (cause) {
    return { kind: 'network', message: cause instanceof Error ? cause.message : '無法連線到後台服務。' }
  }

  const body = await parseJsonSafely(response)

  if (response.ok) {
    if (typeof body.accessToken === 'string') {
      setSession(body as unknown as SessionPayload)
      return { kind: 'success' }
    }
    if (body.status === 'totp_required') {
      return { kind: 'totp-required', message: body.message ?? '請輸入兩階段驗證碼。' }
    }
    return { kind: 'invalid-credentials', message: '登入回應格式不正確，請稍後再試。' }
  }

  if (response.status === 423) {
    return { kind: 'locked', message: body.message ?? '帳號已被鎖定，請稍後再試。' }
  }
  if (response.status === 401 && body.status === 'disabled') {
    return { kind: 'disabled', message: body.message ?? '帳號已停用，請聯繫系統管理員。' }
  }
  return { kind: 'invalid-credentials', message: body.message ?? '帳號或密碼錯誤。' }
}

let refreshPromise: Promise<boolean> | null = null

/** 用 `__Host-tcrfc-admin-rt` Cookie（瀏覽器自動附上，JS 讀不到）換一把新的存取權杖。
 * 多個請求同時遇到 401 時共用同一個進行中的 refresh，不會各自打好幾次 `/auth/refresh`。 */
export function refreshAccessToken(): Promise<boolean> {
  if (!refreshPromise) {
    refreshPromise = doRefresh().finally(() => {
      refreshPromise = null
    })
  }
  return refreshPromise
}

async function doRefresh(): Promise<boolean> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/v1/admin/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
    })
    if (!response.ok) {
      clearSession()
      return false
    }
    const body = await parseJsonSafely(response)
    if (typeof body.accessToken !== 'string') {
      clearSession()
      return false
    }
    setSession(body as unknown as SessionPayload)
    return true
  } catch {
    clearSession()
    return false
  }
}

export async function logout(): Promise<void> {
  try {
    await fetch(`${API_BASE_URL}/api/v1/admin/auth/logout`, { method: 'POST', credentials: 'include' })
  } catch {
    // 登出即使連不上伺服器也要讓使用者在前端這裡確實登出，不因網路問題卡住。
  }
  clearSession()
}

export function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  return apiRequest<void>('/api/v1/admin/auth/change-password', {
    method: 'POST',
    body: { currentPassword, newPassword },
  })
}

export interface TwoFactorSetup {
  secret: string
  otpAuthUrl: string
}

export function beginTwoFactorSetup(): Promise<TwoFactorSetup> {
  return apiRequest<TwoFactorSetup>('/api/v1/admin/auth/2fa/setup', { method: 'POST' })
}

export function confirmTwoFactorSetup(code: string): Promise<void> {
  return apiRequest<void>('/api/v1/admin/auth/2fa/confirm', { method: 'POST', body: { code } })
}

export function disableTwoFactor(password: string): Promise<void> {
  return apiRequest<void>('/api/v1/admin/auth/2fa/disable', { method: 'POST', body: { password } })
}

/**
 * `GET /auth/me`（S1-4 續作補上的端點，見 apps/api/README.md「前端回報缺口①」）——回答
 * 「這個登入的人可以切到哪些俱樂部、叫什麼名字、有哪些角色」，對照
 * `Features/AdminAuth/AdminAuthDtos.cs` 的 `MeResponse`／`MeClubGrantDto`／`MeRoleDto`（camelCase）。
 */
export interface AdminMeClubGrantDto {
  clubCode: string
  clubNameZh?: string | null
  clubNameEn?: string | null
  /** 是否為 `AdminUser.primaryClubId`——站台切換器的預設選取值。 */
  isPrimary: boolean
  /** 系統管理員固定為 `null`（不受俱樂部授權到期日限制）。 */
  expiresOn?: string | null
}

export interface AdminMeRoleDto {
  code: string
  nameZh: string
  nameEn?: string | null
}

export interface AdminMeResponse {
  adminUserId: string
  username: string
  displayName: string
  isSuperAdmin: boolean
  primaryClubCode?: string | null
  /** 已過濾到期與停用；系統管理員固定回「全部啟用中的俱樂部」（見後端註解）。 */
  clubGrants: AdminMeClubGrantDto[]
  roles: AdminMeRoleDto[]
}

export function getMe(): Promise<AdminMeResponse> {
  return apiRequest<AdminMeResponse>('/api/v1/admin/auth/me')
}
