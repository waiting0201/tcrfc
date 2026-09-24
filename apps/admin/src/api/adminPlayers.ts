/**
 * `apps/api` C2「球員」後台端點（`Features/AdminPlayers`），對照 apps/api/README.md「S1-7」「S1-7a」。
 */
import { apiRequest, apiUploadRequest } from './http'

export interface AdminPlayerLocaleContentDto {
  name?: string | null
  bio?: string | null
}

export interface AdminPlayerContentInputDto {
  zh: AdminPlayerLocaleContentDto
  en?: AdminPlayerLocaleContentDto | null
}

export interface AdminPlayerListItemDto {
  id: string
  teamId: string
  teamCode: string
  shirtNo?: number | null
  position?: string | null
  birthOn?: string | null
  status?: string | null
  photoKey?: string | null
  /** 肖像同意狀態（S1-7a），必填三態字串，見 apps/admin/src/types/team.ts。 */
  portraitConsentStatus: string
  nameZh?: string | null
  nameEn?: string | null
  updatedAt: string
}

export interface AdminPlayerDetailDto {
  id: string
  teamId: string
  teamCode: string
  shirtNo?: number | null
  position?: string | null
  birthOn?: string | null
  heightCm?: number | null
  weightKg?: number | null
  nationality?: string | null
  preferredFoot?: string | null
  joinedOn?: string | null
  status?: string | null
  photoKey?: string | null
  portraitConsentStatus: string
  zh: AdminPlayerLocaleContentDto
  en?: AdminPlayerLocaleContentDto | null
  createdAt: string
  updatedAt: string
}

export interface ListAdminPlayersParams {
  teamId?: string
  status?: string
}

/**
 * 🔴 `portraitConsentStatus` 一律明確帶出目前畫面上的值——後端的「省略」語意是「回退到最安全的
 * `not_consented`」（fail-closed，不是「維持不變」，見 `apps/api` `UpdateAdminPlayerRequest` 檔頭
 * 說明），跟新聞標籤／關聯那種「省略＝維持不變」完全不同，這裡絕對不能為了偷懶而省略這個欄位，
 * 否則會把使用者原本已經填好的同意狀態悄悄重置。
 */
export interface SavePlayerPayload {
  teamId: string
  shirtNo?: number | null
  position?: string | null
  birthOn?: string | null
  heightCm?: number | null
  weightKg?: number | null
  nationality?: string | null
  preferredFoot?: string | null
  joinedOn?: string | null
  status?: string | null
  portraitConsentStatus: string
  content: AdminPlayerContentInputDto
}

export interface UpdatePlayerPayload extends SavePlayerPayload {
  removePhoto: boolean
}

export function listAdminPlayers(club: string, params: ListAdminPlayersParams = {}): Promise<AdminPlayerListItemDto[]> {
  const search = new URLSearchParams()
  if (params.teamId) search.set('teamId', params.teamId)
  if (params.status) search.set('status', params.status)
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminPlayerListItemDto[]>(`/api/v1/admin/${club}/players${query}`)
}

export function getAdminPlayer(club: string, id: string): Promise<AdminPlayerDetailDto> {
  return apiRequest<AdminPlayerDetailDto>(`/api/v1/admin/${club}/players/${id}`)
}

function buildPlayerFormData(payload: SavePlayerPayload | UpdatePlayerPayload, file: File | null): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  if (file) form.append('file', file)
  return form
}

export function createAdminPlayer(club: string, payload: SavePlayerPayload, photoFile: File | null): Promise<AdminPlayerDetailDto> {
  return apiUploadRequest<AdminPlayerDetailDto>(`/api/v1/admin/${club}/players`, buildPlayerFormData(payload, photoFile), {
    method: 'POST',
  })
}

export function updateAdminPlayer(
  club: string,
  id: string,
  payload: UpdatePlayerPayload,
  photoFile: File | null,
): Promise<AdminPlayerDetailDto> {
  return apiUploadRequest<AdminPlayerDetailDto>(`/api/v1/admin/${club}/players/${id}`, buildPlayerFormData(payload, photoFile), {
    method: 'PUT',
  })
}
