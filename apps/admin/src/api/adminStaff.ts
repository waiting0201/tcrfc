/**
 * `apps/api` C3「教練與團隊成員」後台端點（`Features/AdminStaff`），對照
 * apps/api/README.md「S1-7」「S1-7a」。
 */
import { apiRequest, apiUploadRequest } from './http'

export interface AdminStaffLocaleContentDto {
  name?: string | null
  title?: string | null
  bio?: string | null
}

export interface AdminStaffContentInputDto {
  zh: AdminStaffLocaleContentDto
  en?: AdminStaffLocaleContentDto | null
}

export interface AdminStaffTeamAssignmentInputDto {
  teamId: string
  roleCode?: string | null
}

export interface AdminStaffTeamAssignmentDto {
  teamId: string
  teamCode: string
  roleCode?: string | null
}

export interface AdminStaffListItemDto {
  id: string
  /** true＝兩隊共同資料（`club_id IS NULL`）——這個俱樂部範圍清單仍會列出，但不可編輯。 */
  isShared: boolean
  staffGroup?: string | null
  licence?: string | null
  photoKey?: string | null
  portraitConsentStatus: string
  nameZh?: string | null
  nameEn?: string | null
  teamCodes: string[]
  updatedAt: string
}

export interface AdminStaffDetailDto {
  id: string
  isShared: boolean
  staffGroup?: string | null
  licence?: string | null
  photoKey?: string | null
  portraitConsentStatus: string
  zh: AdminStaffLocaleContentDto
  en?: AdminStaffLocaleContentDto | null
  teams: AdminStaffTeamAssignmentDto[]
  createdAt: string
  updatedAt: string
}

export interface ListAdminStaffParams {
  teamId?: string
}

/** 🔴 `portraitConsentStatus` 一律明確帶出目前畫面上的值——理由同 `adminPlayers.ts` 的
 * `SavePlayerPayload` 檔頭說明，後端省略語意是回退 `not_consented`，不是維持不變。 */
export interface SaveStaffPayload {
  staffGroup?: string | null
  licence?: string | null
  portraitConsentStatus: string
  content: AdminStaffContentInputDto
  /** 省略＝維持不變、空陣列＝清空（跟標籤／關聯同一種語意，不同於 `portraitConsentStatus`）。 */
  teams?: AdminStaffTeamAssignmentInputDto[]
}

export interface UpdateStaffPayload extends SaveStaffPayload {
  removePhoto: boolean
}

export function listAdminStaff(club: string, params: ListAdminStaffParams = {}): Promise<AdminStaffListItemDto[]> {
  const search = new URLSearchParams()
  if (params.teamId) search.set('teamId', params.teamId)
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminStaffListItemDto[]>(`/api/v1/admin/${club}/staff${query}`)
}

export function getAdminStaff(club: string, id: string): Promise<AdminStaffDetailDto> {
  return apiRequest<AdminStaffDetailDto>(`/api/v1/admin/${club}/staff/${id}`)
}

function buildStaffFormData(payload: SaveStaffPayload | UpdateStaffPayload, file: File | null): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  if (file) form.append('file', file)
  return form
}

export function createAdminStaff(club: string, payload: SaveStaffPayload, photoFile: File | null): Promise<AdminStaffDetailDto> {
  return apiUploadRequest<AdminStaffDetailDto>(`/api/v1/admin/${club}/staff`, buildStaffFormData(payload, photoFile), {
    method: 'POST',
  })
}

export function updateAdminStaff(
  club: string,
  id: string,
  payload: UpdateStaffPayload,
  photoFile: File | null,
): Promise<AdminStaffDetailDto> {
  return apiUploadRequest<AdminStaffDetailDto>(`/api/v1/admin/${club}/staff/${id}`, buildStaffFormData(payload, photoFile), {
    method: 'PUT',
  })
}
