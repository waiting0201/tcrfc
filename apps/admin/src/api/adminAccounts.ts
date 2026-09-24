/**
 * J1 帳號管理 ＋ J4「掛在帳號底下」的俱樂部／球隊授權，對照
 * `apps/api/README.md`「S1-3 續作：J1／J2／J4 端點」與 `Features/AdminAccounts/AdminAccountDtos.cs`。
 * 全域端點（不含 `{club}` 路由段），一律需要系統管理員。
 */
import { apiRequest } from './http'

export interface AdminAccountListItemDto {
  id: string
  username: string
  displayName: string
  email?: string | null
  status: 'active' | 'disabled'
  isSuperAdmin: boolean
  mustChangePassword: boolean
  twoFactorEnabled: boolean
  primaryClubId?: string | null
  lastLoginAt?: string | null
  roleCodes: string[]
  updatedAt: string
}

export interface AdminAccountClubGrantDto {
  clubId: string
  clubCode: string
  grantedOn: string
  expiresOn?: string | null
  isActive: boolean
  isCurrentlyEffective: boolean
}

export interface AdminAccountTeamGrantDto {
  teamId: string
  teamCode: string
  clubCode: string
  expiresOn?: string | null
  isActive: boolean
  isCurrentlyEffective: boolean
}

export interface AdminAccountDetailDto {
  id: string
  username: string
  displayName: string
  email?: string | null
  status: 'active' | 'disabled'
  isSuperAdmin: boolean
  mustChangePassword: boolean
  twoFactorEnabled: boolean
  primaryClubId?: string | null
  locale?: string | null
  lastLoginAt?: string | null
  roleCodes: string[]
  clubGrants: AdminAccountClubGrantDto[]
  teamGrants: AdminAccountTeamGrantDto[]
  createdAt: string
  updatedAt: string
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ListAdminAccountsParams {
  status?: 'active' | 'disabled' | ''
  keyword?: string
  page?: number
  pageSize?: number
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === '') continue
    search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export function listAdminAccounts(params: ListAdminAccountsParams = {}): Promise<PagedResult<AdminAccountListItemDto>> {
  const query = buildQuery({
    status: params.status,
    keyword: params.keyword,
    page: params.page,
    pageSize: params.pageSize,
  })
  return apiRequest<PagedResult<AdminAccountListItemDto>>(`/api/v1/admin/accounts${query}`)
}

export function getAdminAccount(id: string): Promise<AdminAccountDetailDto> {
  return apiRequest<AdminAccountDetailDto>(`/api/v1/admin/accounts/${id}`)
}

export interface CreateAdminAccountPayload {
  username: string
  displayName: string
  email?: string | null
  primaryClubId?: string | null
  initialPassword: string
  isSuperAdmin: boolean
  roleCodes: string[]
  locale?: string | null
}

export function createAdminAccount(payload: CreateAdminAccountPayload): Promise<AdminAccountDetailDto> {
  return apiRequest<AdminAccountDetailDto>('/api/v1/admin/accounts', { method: 'POST', body: payload })
}

export interface UpdateAdminAccountPayload {
  displayName: string
  email?: string | null
  primaryClubId?: string | null
  isSuperAdmin: boolean
  roleCodes: string[]
  locale?: string | null
}

export function updateAdminAccount(id: string, payload: UpdateAdminAccountPayload): Promise<AdminAccountDetailDto> {
  return apiRequest<AdminAccountDetailDto>(`/api/v1/admin/accounts/${id}`, { method: 'PUT', body: payload })
}

export function setAdminAccountStatus(id: string, status: 'active' | 'disabled'): Promise<AdminAccountDetailDto> {
  return apiRequest<AdminAccountDetailDto>(`/api/v1/admin/accounts/${id}/status`, { method: 'POST', body: { status } })
}

export function resetAdminAccountPassword(id: string, newPassword: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/accounts/${id}/reset-password`, { method: 'POST', body: { newPassword } })
}

export function resetAdminAccountTwoFactor(id: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/accounts/${id}/reset-totp`, { method: 'POST' })
}

export function listAccountClubGrants(id: string): Promise<AdminAccountClubGrantDto[]> {
  return apiRequest<AdminAccountClubGrantDto[]>(`/api/v1/admin/accounts/${id}/club-grants`)
}

export interface CreateClubGrantPayload {
  clubId: string
  grantedOn?: string | null
  expiresOn?: string | null
}

export function upsertAccountClubGrant(id: string, payload: CreateClubGrantPayload): Promise<AdminAccountClubGrantDto> {
  return apiRequest<AdminAccountClubGrantDto>(`/api/v1/admin/accounts/${id}/club-grants`, { method: 'POST', body: payload })
}

export function revokeAccountClubGrant(id: string, clubId: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/accounts/${id}/club-grants/${clubId}`, { method: 'DELETE' })
}

export function listAccountTeamGrants(id: string): Promise<AdminAccountTeamGrantDto[]> {
  return apiRequest<AdminAccountTeamGrantDto[]>(`/api/v1/admin/accounts/${id}/team-grants`)
}

export interface CreateTeamGrantPayload {
  teamId: string
  expiresOn?: string | null
}

export function upsertAccountTeamGrant(id: string, payload: CreateTeamGrantPayload): Promise<AdminAccountTeamGrantDto> {
  return apiRequest<AdminAccountTeamGrantDto>(`/api/v1/admin/accounts/${id}/team-grants`, { method: 'POST', body: payload })
}

export function revokeAccountTeamGrant(id: string, teamId: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/accounts/${id}/team-grants/${teamId}`, { method: 'DELETE' })
}
