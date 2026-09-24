/**
 * J2 角色與權限，對照 `Features/AdminRoles/AdminRoleDtos.cs`。全域端點，需要系統管理員。
 */
import { apiRequest } from './http'

export interface AdminPermissionDto {
  id: string
  code: string
  moduleCode: string
  submoduleCode?: string | null
  domain?: string | null
  action?: string | null
  isClubScoped: boolean
  isRestricted: boolean
  sysadminOnly: boolean
  nameZh: string
  nameEn?: string | null
}

export type RoleScopeType = 'all' | 'own_teams' | 'academy_only' | 'masked' | 'translate_only' | 'own_clubs'

export interface AdminRolePermissionAssignmentDto {
  permissionCode: string
  nameZh: string
  scopeType: RoleScopeType
}

export interface AdminRoleListItemDto {
  id: string
  code: string
  nameZh: string
  nameEn?: string | null
  scopeMode: 'all_clubs' | 'own_clubs'
  isSystem: boolean
  sortOrder: number
  assignedAccountCount: number
}

export interface AdminRoleDetailDto {
  id: string
  code: string
  nameZh: string
  nameEn?: string | null
  scopeMode: 'all_clubs' | 'own_clubs'
  isSystem: boolean
  sortOrder: number
  assignedAccountCount: number
  permissions: AdminRolePermissionAssignmentDto[]
}

export function listPermissionDictionary(): Promise<AdminPermissionDto[]> {
  return apiRequest<AdminPermissionDto[]>('/api/v1/admin/roles/permissions')
}

export function listAdminRoles(): Promise<AdminRoleListItemDto[]> {
  return apiRequest<AdminRoleListItemDto[]>('/api/v1/admin/roles')
}

export function getAdminRole(id: string): Promise<AdminRoleDetailDto> {
  return apiRequest<AdminRoleDetailDto>(`/api/v1/admin/roles/${id}`)
}

export interface CreateAdminRolePayload {
  code: string
  nameZh: string
  nameEn?: string | null
  scopeMode: 'all_clubs' | 'own_clubs'
}

export function createAdminRole(payload: CreateAdminRolePayload): Promise<AdminRoleDetailDto> {
  return apiRequest<AdminRoleDetailDto>('/api/v1/admin/roles', { method: 'POST', body: payload })
}

export interface UpdateAdminRolePayload {
  nameZh: string
  nameEn?: string | null
  scopeMode: 'all_clubs' | 'own_clubs'
}

export function updateAdminRole(id: string, payload: UpdateAdminRolePayload): Promise<AdminRoleDetailDto> {
  return apiRequest<AdminRoleDetailDto>(`/api/v1/admin/roles/${id}`, { method: 'PUT', body: payload })
}

export function deleteAdminRole(id: string): Promise<void> {
  return apiRequest<void>(`/api/v1/admin/roles/${id}`, { method: 'DELETE' })
}

export interface RolePermissionInput {
  permissionCode: string
  scopeType: RoleScopeType
}

export function replaceRolePermissions(id: string, permissions: RolePermissionInput[]): Promise<AdminRoleDetailDto> {
  return apiRequest<AdminRoleDetailDto>(`/api/v1/admin/roles/${id}/permissions`, {
    method: 'PUT',
    body: { permissions },
  })
}
