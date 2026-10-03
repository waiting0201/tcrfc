/**
 * 角色與權限：`/api/v1/donation-platform/admin/roles`。形狀比照主站 `apps/admin/src/api/adminRoles.ts`，
 * 但拿掉慈善庫沒有的資料範圍（全部／僅授權的俱樂部）。全部端點只有系統管理員能呼叫。
 */
import { ADMIN_ROOT, apiRequest } from './http'

const ROOT = `${ADMIN_ROOT}/roles`

export interface AdminPermission {
  id: string
  code: string
  moduleCode: string
  submoduleCode?: string | null
  domain?: string | null
  action?: string | null
  isRestricted: boolean
  sysadminOnly: boolean
  nameZh: string
  nameEn?: string | null
}

export type RoleScopeType = 'all' | 'masked' | 'translate_only'

export interface AdminRolePermissionAssignment {
  permissionCode: string
  nameZh: string
  scopeType: RoleScopeType
}

export interface AdminRoleListItem {
  id: string
  code: string
  nameZh: string
  nameEn?: string | null
  isSystem: boolean
  sortOrder: number
  assignedAccountCount: number
}

export interface AdminRoleDetail extends AdminRoleListItem {
  permissions: AdminRolePermissionAssignment[]
}

export const listPermissionDictionary = () => apiRequest<AdminPermission[]>(`${ROOT}/permissions`)
export const listAdminRoles = () => apiRequest<AdminRoleListItem[]>(ROOT)
export const getAdminRole = (id: string) => apiRequest<AdminRoleDetail>(`${ROOT}/${id}`)

export interface CreateAdminRolePayload {
  code: string
  nameZh: string
  nameEn?: string | null
}

export const createAdminRole = (payload: CreateAdminRolePayload) =>
  apiRequest<AdminRoleDetail>(ROOT, { method: 'POST', body: payload })

export interface UpdateAdminRolePayload {
  nameZh: string
  nameEn?: string | null
}

export const updateAdminRole = (id: string, payload: UpdateAdminRolePayload) =>
  apiRequest<AdminRoleDetail>(`${ROOT}/${id}`, { method: 'PUT', body: payload })

export const deleteAdminRole = (id: string) => apiRequest<void>(`${ROOT}/${id}`, { method: 'DELETE' })

export interface RolePermissionInput {
  permissionCode: string
  scopeType: RoleScopeType
}

export const replaceRolePermissions = (id: string, permissions: RolePermissionInput[]) =>
  apiRequest<AdminRoleDetail>(`${ROOT}/${id}/permissions`, { method: 'PUT', body: { permissions } })
