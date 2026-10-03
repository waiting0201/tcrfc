/**
 * 帳號管理：`/api/v1/donation-platform/admin/accounts`。形狀比照主站 `apps/admin/src/api/adminAccounts.ts`，
 * 但拿掉慈善庫沒有的維度（俱樂部授權、球隊授權、預設俱樂部）。全部端點只有系統管理員能呼叫。
 */
import { ADMIN_ROOT, apiRequest, buildQuery } from './http'
import type { PagedResult } from './types'

const ROOT = `${ADMIN_ROOT}/accounts`

export interface AdminAccountListItem {
  id: string
  username: string
  displayName: string
  email?: string | null
  status: 'active' | 'disabled'
  isSuperAdmin: boolean
  mustChangePassword: boolean
  twoFactorEnabled: boolean
  lastLoginAt?: string | null
  /** 角色識別名稱（不是顯示名稱）；畫面要用角色清單換成中文名稱，不直接顯示。 */
  roleCodes: string[]
  updatedAt: string
}

export interface AdminAccountDetail extends AdminAccountListItem {
  locale?: string | null
  createdAt: string
}

export interface ListAdminAccountsParams {
  status?: 'active' | 'disabled' | ''
  keyword?: string
  page?: number
  pageSize?: number
}

export const listAdminAccounts = (params: ListAdminAccountsParams = {}) =>
  apiRequest<PagedResult<AdminAccountListItem>>(`${ROOT}${buildQuery({ ...params })}`)

export const getAdminAccount = (id: string) => apiRequest<AdminAccountDetail>(`${ROOT}/${id}`)

export interface CreateAdminAccountPayload {
  username: string
  displayName: string
  email?: string | null
  initialPassword: string
  isSuperAdmin: boolean
  roleCodes: string[]
  locale?: string | null
}

export const createAdminAccount = (payload: CreateAdminAccountPayload) =>
  apiRequest<AdminAccountDetail>(ROOT, { method: 'POST', body: payload })

export interface UpdateAdminAccountPayload {
  displayName: string
  email?: string | null
  isSuperAdmin: boolean
  roleCodes: string[]
  locale?: string | null
}

export const updateAdminAccount = (id: string, payload: UpdateAdminAccountPayload) =>
  apiRequest<AdminAccountDetail>(`${ROOT}/${id}`, { method: 'PUT', body: payload })

export const setAdminAccountStatus = (id: string, status: 'active' | 'disabled') =>
  apiRequest<AdminAccountDetail>(`${ROOT}/${id}/status`, { method: 'POST', body: { status } })

export const resetAdminAccountPassword = (id: string, newPassword: string) =>
  apiRequest<void>(`${ROOT}/${id}/reset-password`, { method: 'POST', body: { newPassword } })

export const resetAdminAccountTwoFactor = (id: string) =>
  apiRequest<void>(`${ROOT}/${id}/reset-totp`, { method: 'POST' })
