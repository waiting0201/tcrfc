/**
 * `apps/api` P1「課程／營隊項目」後台端點（`Features/AdminPrograms`），對照
 * apps/api/README.md「S1-9」。上傳形狀比照 `adminPlayers.ts`／`adminStaff.ts`：
 * `multipart/form-data`，固定欄位 `payload`（JSON 文字）＋選填的 `file`（封面圖）。
 */
import { apiRequest, apiUploadRequest } from './http'

export interface AdminProgramLocaleContentDto {
  name?: string | null
  intro?: string | null
  /** `programs_i18n.content`（區塊編輯器輸出的 JSON 文字）。後端只驗證語法合法性，不驗證區塊
   * 結構——規劃書沒有像 B1 頁面那樣明訂區塊型別清單，見 `apps/api` `AdminProgramLocaleContent` 檔頭。 */
  content?: string | null
}

export interface AdminProgramContentInputDto {
  zh: AdminProgramLocaleContentDto
  en?: AdminProgramLocaleContentDto | null
}

export interface AdminProgramListItemDto {
  id: string
  slug: string
  programType?: string | null
  audience?: string | null
  ageMin?: number | null
  ageMax?: number | null
  status: string
  coverKey?: string | null
  nameZh?: string | null
  nameEn?: string | null
  sessionCount: number
  updatedAt: string
}

export interface AdminProgramStaffDto {
  staffId: string
  nameZh?: string | null
}

export interface AdminProgramPartnerDto {
  partnerId: string
  slug: string
}

export interface AdminProgramDetailDto {
  id: string
  slug: string
  programType?: string | null
  audience?: string | null
  ageMin?: number | null
  ageMax?: number | null
  status: string
  coverKey?: string | null
  zh: AdminProgramLocaleContentDto
  en?: AdminProgramLocaleContentDto | null
  staff: AdminProgramStaffDto[]
  partners: AdminProgramPartnerDto[]
  createdAt: string
  updatedAt: string
}

export interface ListAdminProgramsParams {
  programType?: string
}

export interface SaveProgramPayload {
  slug: string
  programType?: string | null
  audience?: string | null
  ageMin?: number | null
  ageMax?: number | null
  status?: string | null
  content: AdminProgramContentInputDto
  /** 省略＝維持不變、空陣列＝清空（比照 `UpdateAdminStaffRequest.Teams` 既有語意）。 */
  staffIds?: string[]
}

export interface UpdateProgramPayload extends SaveProgramPayload {
  removeCover: boolean
}

export function listAdminPrograms(club: string, params: ListAdminProgramsParams = {}): Promise<AdminProgramListItemDto[]> {
  const search = new URLSearchParams()
  if (params.programType) search.set('programType', params.programType)
  const query = search.toString() ? `?${search.toString()}` : ''
  return apiRequest<AdminProgramListItemDto[]>(`/api/v1/admin/${club}/programs${query}`)
}

export function getAdminProgram(club: string, id: string): Promise<AdminProgramDetailDto> {
  return apiRequest<AdminProgramDetailDto>(`/api/v1/admin/${club}/programs/${id}`)
}

function buildProgramFormData(payload: SaveProgramPayload | UpdateProgramPayload, file: File | null): FormData {
  const form = new FormData()
  form.append('payload', JSON.stringify(payload))
  if (file) form.append('file', file)
  return form
}

export function createAdminProgram(club: string, payload: SaveProgramPayload, coverFile: File | null): Promise<AdminProgramDetailDto> {
  return apiUploadRequest<AdminProgramDetailDto>(`/api/v1/admin/${club}/programs`, buildProgramFormData(payload, coverFile), {
    method: 'POST',
  })
}

export function updateAdminProgram(
  club: string,
  id: string,
  payload: UpdateProgramPayload,
  coverFile: File | null,
): Promise<AdminProgramDetailDto> {
  return apiUploadRequest<AdminProgramDetailDto>(`/api/v1/admin/${club}/programs/${id}`, buildProgramFormData(payload, coverFile), {
    method: 'PUT',
  })
}
